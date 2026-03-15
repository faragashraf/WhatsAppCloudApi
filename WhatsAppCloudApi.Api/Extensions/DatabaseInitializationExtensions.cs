using System.Data;
using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Api.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDatabaseAsync(
        this WebApplication app,
        DatabaseInitializationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DatabaseInitializationOptions();

        var logger = app.Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");

        Exception? lastException = null;
        for (var attempt = 1; attempt <= options.MaxRetryCount; attempt++)
        {
            try
            {
                using var scope = app.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await InitializeDatabaseCoreAsync(dbContext, logger, options, cancellationToken);
                logger.LogInformation("Database initialization completed successfully on attempt {Attempt}.", attempt);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt >= options.MaxRetryCount)
                {
                    break;
                }

                logger.LogWarning(
                    ex,
                    "Database initialization attempt {Attempt}/{MaxRetryCount} failed. Retrying in {RetryDelaySeconds} seconds.",
                    attempt,
                    options.MaxRetryCount,
                    options.RetryDelaySeconds);

                await Task.Delay(TimeSpan.FromSeconds(options.RetryDelaySeconds), cancellationToken);
            }
        }

        throw new InvalidOperationException(
            $"Database initialization failed after {options.MaxRetryCount} attempt(s).",
            lastException);
    }

    private static async Task InitializeDatabaseCoreAsync(
        ApplicationDbContext dbContext,
        ILogger logger,
        DatabaseInitializationOptions options,
        CancellationToken cancellationToken)
    {
        await EnsureLegacyBaselineMigrationAsync(dbContext, logger, cancellationToken);

        var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (options.LogPendingMigrations)
        {
            if (pendingMigrations.Count == 0)
            {
                logger.LogInformation("No pending database migrations were found.");
            }
            else
            {
                logger.LogInformation(
                    "Applying {Count} pending database migration(s): {MigrationIds}",
                    pendingMigrations.Count,
                    string.Join(", ", pendingMigrations));
            }
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
        await BackfillLegacyConversationContactsAsync(dbContext, logger, cancellationToken);

        await SeedBasicPlanAsync(dbContext, logger, cancellationToken);
        await SeedNotificationsAsync(dbContext, logger, cancellationToken);
    }

    private static async Task EnsureLegacyBaselineMigrationAsync(
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var migrations = dbContext.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
        {
            return;
        }

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        var existingAppTables = await QueryScalarAsync(
            dbContext,
            "SELECT COUNT(*) FROM sys.tables WHERE name IN ('Companies', 'CompanyUsers', 'SubscriptionPlans')",
            cancellationToken);
        if (existingAppTables == 0)
        {
            return;
        }

        var historyTableExists = await QueryScalarAsync(
            dbContext,
            "SELECT COUNT(*) FROM sys.tables WHERE name = '__EFMigrationsHistory'",
            cancellationToken);

        var historyRows = historyTableExists > 0
            ? await QueryScalarAsync(dbContext, "SELECT COUNT(*) FROM [__EFMigrationsHistory]", cancellationToken)
            : 0;

        if (historyRows > 0)
        {
            return;
        }

        var baselineMigrationId = migrations[0];
        var productVersion = typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+', 2)[0]
            ?? "8.0.15";

        await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
IF OBJECT_ID(N'__EFMigrationsHistory', N'U') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = {baselineMigrationId})
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ({baselineMigrationId}, {productVersion});
END;", cancellationToken);

        logger.LogInformation(
            "Marked baseline migration {MigrationId} as applied for an existing legacy database.",
            baselineMigrationId);
    }

    private static async Task SeedBasicPlanAsync(
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var existingBasicPlan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(x => x.Code == "BASIC", cancellationToken);

        if (existingBasicPlan is not null)
        {
            if (existingBasicPlan.MaxPhoneNumbers <= 0)
            {
                existingBasicPlan.MaxPhoneNumbers = 1;
                existingBasicPlan.UpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Updated BASIC subscription plan phone-number limit to 1.");
            }

            return;
        }

        dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Name = "Basic",
            Code = "BASIC",
            TrialDays = 14,
            MaxMessagesPerMonth = 1000,
            MaxWhatsAppAccounts = 1,
            MaxPhoneNumbers = 1,
            MonthlyPrice = 0m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded BASIC subscription plan.");
    }

    private static async Task SeedNotificationsAsync(
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Notifications.AnyAsync(cancellationToken))
        {
            return;
        }

        var unreadConversations = await dbContext.Conversations
            .AsNoTracking()
            .Where(x => x.UnreadCount > 0)
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .Take(200)
            .Select(x => new
            {
                x.CompanyId,
                x.ContactName,
                x.ContactNumber,
                x.LastMessageContent,
                x.LastMessageType,
                x.LastMessageAtUtc,
                x.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var seededNotifications = unreadConversations
            .Select(x =>
            {
                var title = (x.ContactName ?? x.ContactNumber) ?? "New message";
                var body = string.IsNullOrWhiteSpace(x.LastMessageContent)
                    ? $"[{x.LastMessageType ?? "message"}]"
                    : x.LastMessageContent!;

                return new Notification
                {
                    CompanyId = x.CompanyId,
                    Type = "info",
                    Title = title.Length > 300 ? title[..300] : title,
                    Body = body.Length > 4000 ? body[..4000] : body,
                    Category = "inbox",
                    CreatedAtUtc = x.LastMessageAtUtc ?? x.CreatedAtUtc
                };
            })
            .ToList();

        if (seededNotifications.Count == 0)
        {
            return;
        }

        dbContext.Notifications.AddRange(seededNotifications);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} inbox notifications from unread conversations.", seededNotifications.Count);
    }

    private static async Task BackfillLegacyConversationContactsAsync(
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var inserted = await dbContext.Database.ExecuteSqlRawAsync("""
;WITH MissingContacts AS (
    SELECT
        c.[CompanyId],
        c.[ContactNumber],
        MAX(NULLIF(LTRIM(RTRIM(c.[ContactName])), N'')) AS [ContactName],
        MIN(ISNULL(c.[CreatedAtUtc], GETUTCDATE())) AS [FirstSeenAtUtc],
        MAX(COALESCE(c.[LastMessageAtUtc], c.[CreatedAtUtc], GETUTCDATE())) AS [LastSeenAtUtc],
        MAX(c.[LastInboundMessageAtUtc]) AS [LastInboundMessageAtUtc]
    FROM [Conversations] c
    LEFT JOIN [Contacts] ct
        ON ct.[CompanyId] = c.[CompanyId]
       AND ct.[PhoneNumber] = c.[ContactNumber]
    WHERE c.[ContactId] IS NULL
      AND c.[ContactNumber] IS NOT NULL
      AND ct.[ContactId] IS NULL
    GROUP BY c.[CompanyId], c.[ContactNumber]
)
INSERT INTO [Contacts] (
    [CompanyId],
    [Name],
    [PhoneNumber],
    [Source],
    [IsActive],
    [FirstSeenAtUtc],
    [LastSeenAtUtc],
    [LastInboundMessageAtUtc],
    [CreatedAtUtc],
    [UpdatedAtUtc]
)
SELECT
    mc.[CompanyId],
    COALESCE(mc.[ContactName], mc.[ContactNumber]),
    mc.[ContactNumber],
    N'legacy_conversation_backfill',
    1,
    mc.[FirstSeenAtUtc],
    mc.[LastSeenAtUtc],
    mc.[LastInboundMessageAtUtc],
    mc.[FirstSeenAtUtc],
    GETUTCDATE()
FROM MissingContacts mc;
""", cancellationToken);

            var updated = await dbContext.Database.ExecuteSqlRawAsync("""
UPDATE c
SET
    c.[ContactId] = ct.[ContactId],
    c.[ContactName] = COALESCE(NULLIF(LTRIM(RTRIM(c.[ContactName])), N''), ct.[Name])
FROM [Conversations] c
INNER JOIN [Contacts] ct
    ON ct.[CompanyId] = c.[CompanyId]
   AND ct.[PhoneNumber] = c.[ContactNumber]
WHERE c.[ContactId] IS NULL
  AND c.[ContactNumber] IS NOT NULL;
""", cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            if (inserted > 0 || updated > 0)
            {
                logger.LogInformation(
                    "Backfilled legacy conversation contacts. Inserted {InsertedContacts}, updated {UpdatedConversations} conversations.",
                    inserted,
                    updated);
            }
        });
    }

    private static async Task<int> QueryScalarAsync(
        ApplicationDbContext dbContext,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                int intValue => intValue,
                long longValue => (int)longValue,
                decimal decimalValue => (int)decimalValue,
                _ => Convert.ToInt32(result ?? 0, CultureInfo.InvariantCulture)
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
