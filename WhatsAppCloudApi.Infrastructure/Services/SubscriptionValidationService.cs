using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class SubscriptionValidationService : ISubscriptionValidationService
{
    private readonly ApplicationDbContext _dbContext;

    public SubscriptionValidationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ValidateCanSendMessageAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var state = await GetSubscriptionStateSafeAsync(companyId, cancellationToken);
        if (!state.IsMessageSendingAllowed)
        {
            throw new InvalidOperationException("Subscription expired.");
        }

        if (state.MaxMessagesPerMonth > 0)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var sentCount = await GetSentMessagesCountSafeAsync(companyId, monthStart, monthEnd, cancellationToken);

            if (sentCount >= state.MaxMessagesPerMonth)
            {
                throw new InvalidOperationException("Monthly message limit exceeded for your subscription plan.");
            }
        }
    }

    public async Task ValidateWhatsAppAccountLimitAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var state = await GetSubscriptionStateSafeAsync(companyId, cancellationToken);
        if (!state.IsMessageSendingAllowed)
        {
            throw new InvalidOperationException("Subscription expired.");
        }

        if (state.MaxWhatsAppAccounts > 0)
        {
            var accountsCount = await GetActiveWhatsAppAccountsCountSafeAsync(companyId, cancellationToken);

            if (accountsCount >= state.MaxWhatsAppAccounts)
            {
                throw new InvalidOperationException("Maximum WhatsApp account limit exceeded for your subscription plan.");
            }
        }
    }

    private async Task<int> GetSentMessagesCountSafeAsync(
        int companyId,
        DateTime monthStart,
        DateTime monthEnd,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.Messages
                .Where(x => x.CompanyId == companyId && x.CreatedAtUtc >= monthStart && x.CreatedAtUtc < monthEnd)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            return await GetSentMessagesCountFallbackAsync(companyId, monthStart, monthEnd, cancellationToken);
        }
    }

    private async Task<int> GetActiveWhatsAppAccountsCountSafeAsync(int companyId, CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.WhatsAppAccounts
                .Where(x => x.CompanyId == companyId && x.IsActive)
                .CountAsync(cancellationToken);
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            return await GetActiveWhatsAppAccountsCountFallbackAsync(companyId, cancellationToken);
        }
    }

    private async Task<int> GetSentMessagesCountFallbackAsync(
        int companyId,
        DateTime monthStart,
        DateTime monthEnd,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var currentTransaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetTableColumnsAsync(connection, "Messages", cancellationToken, currentTransaction);
        if (columns.Count == 0)
        {
            return 0;
        }

        var companyColumn = PickColumn(columns, "CompanyId", "TenantId", "CompanyID");
        var createdAtColumn = PickColumn(columns, "CreatedAtUtc", "CreatedAt", "CreatedDate");
        if (companyColumn is null || createdAtColumn is null)
        {
            return 0;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = currentTransaction;
        command.CommandText = $"""
            SELECT COUNT(1)
            FROM [Messages]
            WHERE [{companyColumn}] = @companyId
              AND [{createdAtColumn}] >= @monthStart
              AND [{createdAtColumn}] < @monthEnd
            """;

        var companyParameter = command.CreateParameter();
        companyParameter.ParameterName = "@companyId";
        companyParameter.Value = companyId;
        command.Parameters.Add(companyParameter);

        var startParameter = command.CreateParameter();
        startParameter.ParameterName = "@monthStart";
        startParameter.Value = monthStart;
        command.Parameters.Add(startParameter);

        var endParameter = command.CreateParameter();
        endParameter.ParameterName = "@monthEnd";
        endParameter.Value = monthEnd;
        command.Parameters.Add(endParameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? 0 : Convert.ToInt32(result);
    }

    private async Task<int> GetActiveWhatsAppAccountsCountFallbackAsync(int companyId, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var currentTransaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetTableColumnsAsync(connection, "WhatsAppAccounts", cancellationToken, currentTransaction);
        if (columns.Count == 0)
        {
            return 0;
        }

        var companyColumn = PickColumn(columns, "CompanyId", "TenantId", "CompanyID");
        if (companyColumn is null)
        {
            return 0;
        }

        var whereParts = new List<string> { $"[{companyColumn}] = @companyId" };
        var isActiveColumn = PickColumn(columns, "IsActive", "Active", "Status");
        if (isActiveColumn is not null)
        {
            whereParts.Add(GetTruthyPredicate(isActiveColumn, columns[isActiveColumn]));
        }

        await using var command = connection.CreateCommand();
        command.Transaction = currentTransaction;
        command.CommandText = $"SELECT COUNT(1) FROM [WhatsAppAccounts] WHERE {string.Join(" AND ", whereParts)}";

        var companyParameter = command.CreateParameter();
        companyParameter.ParameterName = "@companyId";
        companyParameter.Value = companyId;
        command.Parameters.Add(companyParameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? 0 : Convert.ToInt32(result);
    }

    private async Task<SubscriptionState> GetSubscriptionStateSafeAsync(int companyId, CancellationToken cancellationToken)
    {
        try
        {
            return await GetSubscriptionStateAsync(companyId, cancellationToken);
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            return await GetFallbackSubscriptionStateFromCompanyAsync(companyId, cancellationToken);
        }
    }

    private async Task<SubscriptionState> GetSubscriptionStateAsync(int companyId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var subscriptions = await _dbContext.CompanySubscriptions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Join(
                _dbContext.SubscriptionPlans.AsNoTracking(),
                subscription => subscription.SubscriptionPlanId,
                plan => plan.SubscriptionPlanId,
                (subscription, plan) => new
                {
                    subscription.Status,
                    subscription.IsActive,
                    subscription.TrialEndDate,
                    subscription.StartDate,
                    subscription.EndDate,
                    plan.MaxMessagesPerMonth,
                    plan.MaxWhatsAppAccounts
                })
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return SubscriptionState.Expired();
        }

        var activePaid = subscriptions
            .Where(x => x.IsActive && string.Equals(x.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Where(x => (x.StartDate is null || x.StartDate <= now) && (x.EndDate is null || x.EndDate >= now))
            .OrderByDescending(x => x.EndDate ?? DateTime.MaxValue)
            .FirstOrDefault();

        if (activePaid is not null)
        {
            return SubscriptionState.Allowed(activePaid.MaxMessagesPerMonth, activePaid.MaxWhatsAppAccounts);
        }

        var activeTrial = subscriptions
            .Where(x => x.IsActive && string.Equals(x.Status, "TRIAL", StringComparison.OrdinalIgnoreCase))
            .Where(x => x.TrialEndDate is not null && x.TrialEndDate >= now)
            .OrderByDescending(x => x.TrialEndDate)
            .FirstOrDefault();

        if (activeTrial is not null)
        {
            return SubscriptionState.Allowed(activeTrial.MaxMessagesPerMonth, activeTrial.MaxWhatsAppAccounts);
        }

        return SubscriptionState.Expired();
    }

    private async Task<SubscriptionState> GetFallbackSubscriptionStateFromCompanyAsync(int companyId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var company = await _dbContext.Companies
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new
            {
                x.Status,
                x.TrialEndDate,
                x.SubscriptionEndDate
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return SubscriptionState.Expired();
        }

        var isInactive = string.Equals(company.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(company.Status, "DISABLED", StringComparison.OrdinalIgnoreCase);
        if (isInactive)
        {
            return SubscriptionState.Expired();
        }

        var trialActive = company.TrialEndDate is not null && company.TrialEndDate >= now;
        var paidActive = company.SubscriptionEndDate is not null && company.SubscriptionEndDate >= now;

        // If plans/subscriptions schema cannot be resolved, allow send based on company date windows
        // and treat limits as unlimited to avoid false blocking.
        return (trialActive || paidActive)
            ? SubscriptionState.Allowed(0, 0)
            : SubscriptionState.Expired();
    }

    private static bool IsSchemaMismatch(Exception ex)
    {
        var message = GetInnermostMessage(ex);
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("could not be bound", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, string>> GetTableColumnsAsync(
        System.Data.Common.DbConnection connection,
        string tableName,
        CancellationToken cancellationToken,
        System.Data.Common.DbTransaction? transaction = null)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName
            """;

        var tableNameParameter = command.CreateParameter();
        tableNameParameter.ParameterName = "@tableName";
        tableNameParameter.Value = tableName;
        command.Parameters.Add(tableNameParameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }

        return result;
    }

    private static string? PickColumn(IReadOnlyDictionary<string, string> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (columns.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string GetTruthyPredicate(string columnName, string dataType)
    {
        if (IsNumericBooleanType(dataType))
        {
            return $"[{columnName}] = 1";
        }

        return $"UPPER(CAST([{columnName}] AS NVARCHAR(100))) IN ('ACTIVE','1','TRUE','YES')";
    }

    private static bool IsNumericBooleanType(string dataType)
    {
        return dataType.Equals("bit", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("tinyint", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("smallint", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("int", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("bigint", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetInnermostMessage(Exception ex)
    {
        var current = ex;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current.Message;
    }

    private sealed record SubscriptionState(bool IsMessageSendingAllowed, int MaxMessagesPerMonth, int MaxWhatsAppAccounts)
    {
        public static SubscriptionState Expired() => new(false, 0, 0);
        public static SubscriptionState Allowed(int maxMessagesPerMonth, int maxWhatsAppAccounts)
            => new(true, maxMessagesPerMonth, maxWhatsAppAccounts);
    }
}
