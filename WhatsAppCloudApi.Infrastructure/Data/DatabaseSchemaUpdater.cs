using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WhatsAppCloudApi.Infrastructure.Data;

public static class DatabaseSchemaUpdater
{
    public static async Task EnsureCompatibilityAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            const string sql = """
DECLARE @ApiLogsSchema SYSNAME;

SELECT TOP (1) @ApiLogsSchema = [s].[name]
FROM [sys].[tables] AS [t]
INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [t].[schema_id]
WHERE [t].[name] = N'ApiLogs'
ORDER BY CASE WHEN [s].[name] = N'dbo' THEN 0 ELSE 1 END;

IF @ApiLogsSchema IS NOT NULL
BEGIN
    DECLARE @ApiLogsObject NVARCHAR(300) = QUOTENAME(@ApiLogsSchema) + N'.[ApiLogs]';
    DECLARE @ApiLogsObjectName NVARCHAR(300) = @ApiLogsSchema + N'.ApiLogs';
    DECLARE @IdentityColumnName SYSNAME;
    DECLARE @HasIdentityColumn BIT = CASE WHEN EXISTS (
        SELECT 1
        FROM [sys].[identity_columns] AS [ic]
        INNER JOIN [sys].[tables] AS [t] ON [t].[object_id] = [ic].[object_id]
        INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [t].[schema_id]
        WHERE [t].[name] = N'ApiLogs' AND [s].[name] = @ApiLogsSchema
    ) THEN 1 ELSE 0 END;

    SELECT TOP (1) @IdentityColumnName = [ic].[name]
    FROM [sys].[identity_columns] AS [ic]
    INNER JOIN [sys].[tables] AS [t] ON [t].[object_id] = [ic].[object_id]
    INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [t].[schema_id]
    WHERE [t].[name] = N'ApiLogs' AND [s].[name] = @ApiLogsSchema;

    IF COL_LENGTH(@ApiLogsObjectName, 'ApiLogId') IS NULL AND COL_LENGTH(@ApiLogsObjectName, 'Id') IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @ApiLogsObject + N'.[Id]'', N''ApiLogId'', ''COLUMN'';');
    END

    IF COL_LENGTH(@ApiLogsObjectName, 'CompanyUserId') IS NULL AND COL_LENGTH(@ApiLogsObjectName, 'UserId') IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @ApiLogsObject + N'.[UserId]'', N''CompanyUserId'', ''COLUMN'';');
    END

    IF COL_LENGTH(@ApiLogsObjectName, 'CreatedAtUtc') IS NULL AND COL_LENGTH(@ApiLogsObjectName, 'CreatedAt') IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @ApiLogsObject + N'.[CreatedAt]'', N''CreatedAtUtc'', ''COLUMN'';');
    END

    IF COL_LENGTH(@ApiLogsObjectName, 'ApiLogId') IS NULL AND @HasIdentityColumn = 0
        EXEC(N'ALTER TABLE ' + @ApiLogsObject + N' ADD [ApiLogId] BIGINT IDENTITY(1,1) NOT NULL;');

    IF COL_LENGTH(@ApiLogsObjectName, 'ApiLogId') IS NULL AND @HasIdentityColumn = 1 AND @IdentityColumnName IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @ApiLogsObject + N'.' + QUOTENAME(@IdentityColumnName) + N''', N''ApiLogId'', ''COLUMN'';');
    END

    IF COL_LENGTH(@ApiLogsObjectName, 'CompanyUserId') IS NULL
        EXEC(N'ALTER TABLE ' + @ApiLogsObject + N' ADD [CompanyUserId] INT NULL;');

    IF COL_LENGTH(@ApiLogsObjectName, 'HttpMethod') IS NULL
        EXEC(N'ALTER TABLE ' + @ApiLogsObject + N' ADD [HttpMethod] NVARCHAR(10) NULL;');

    IF COL_LENGTH(@ApiLogsObjectName, 'CreatedAtUtc') IS NULL
        EXEC(N'ALTER TABLE ' + @ApiLogsObject + N' ADD [CreatedAtUtc] DATETIME NOT NULL CONSTRAINT [DF_ApiLogs_CreatedAtUtc] DEFAULT (GETUTCDATE());');
END

DECLARE @MessageQueueSchema SYSNAME;

SELECT TOP (1) @MessageQueueSchema = [s].[name]
FROM [sys].[tables] AS [t]
INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [t].[schema_id]
WHERE [t].[name] = N'MessageQueue'
ORDER BY CASE WHEN [s].[name] = N'dbo' THEN 0 ELSE 1 END;

IF @MessageQueueSchema IS NOT NULL
BEGIN
    DECLARE @MessageQueueObject NVARCHAR(300) = QUOTENAME(@MessageQueueSchema) + N'.[MessageQueue]';
    DECLARE @MessageQueueObjectName NVARCHAR(300) = @MessageQueueSchema + N'.MessageQueue';
    DECLARE @MessageQueueIdentityColumnName SYSNAME;
    DECLARE @MessageQueueHasIdentityColumn BIT = CASE WHEN EXISTS (
        SELECT 1
        FROM [sys].[identity_columns] AS [ic]
        INNER JOIN [sys].[tables] AS [t] ON [t].[object_id] = [ic].[object_id]
        INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [t].[schema_id]
        WHERE [t].[name] = N'MessageQueue' AND [s].[name] = @MessageQueueSchema
    ) THEN 1 ELSE 0 END;

    SELECT TOP (1) @MessageQueueIdentityColumnName = [ic].[name]
    FROM [sys].[identity_columns] AS [ic]
    INNER JOIN [sys].[tables] AS [t] ON [t].[object_id] = [ic].[object_id]
    INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [t].[schema_id]
    WHERE [t].[name] = N'MessageQueue' AND [s].[name] = @MessageQueueSchema;

    IF COL_LENGTH(@MessageQueueObjectName, 'MessageQueueId') IS NULL AND COL_LENGTH(@MessageQueueObjectName, 'Id') IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @MessageQueueObject + N'.[Id]'', N''MessageQueueId'', ''COLUMN'';');
    END

    IF COL_LENGTH(@MessageQueueObjectName, 'CreatedAtUtc') IS NULL AND COL_LENGTH(@MessageQueueObjectName, 'CreatedAt') IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @MessageQueueObject + N'.[CreatedAt]'', N''CreatedAtUtc'', ''COLUMN'';');
    END

    IF COL_LENGTH(@MessageQueueObjectName, 'UpdatedAtUtc') IS NULL AND COL_LENGTH(@MessageQueueObjectName, 'UpdatedAt') IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @MessageQueueObject + N'.[UpdatedAt]'', N''UpdatedAtUtc'', ''COLUMN'';');
    END

    IF COL_LENGTH(@MessageQueueObjectName, 'LastAttemptAtUtc') IS NULL AND COL_LENGTH(@MessageQueueObjectName, 'LastAttemptAt') IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @MessageQueueObject + N'.[LastAttemptAt]'', N''LastAttemptAtUtc'', ''COLUMN'';');
    END

    IF COL_LENGTH(@MessageQueueObjectName, 'PayloadJson') IS NULL AND COL_LENGTH(@MessageQueueObjectName, 'Payload') IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @MessageQueueObject + N'.[Payload]'', N''PayloadJson'', ''COLUMN'';');
    END

    IF COL_LENGTH(@MessageQueueObjectName, 'MessageQueueId') IS NULL AND @MessageQueueHasIdentityColumn = 0
        EXEC(N'ALTER TABLE ' + @MessageQueueObject + N' ADD [MessageQueueId] BIGINT IDENTITY(1,1) NOT NULL;');

    IF COL_LENGTH(@MessageQueueObjectName, 'MessageQueueId') IS NULL AND @MessageQueueHasIdentityColumn = 1 AND @MessageQueueIdentityColumnName IS NOT NULL
    BEGIN
        EXEC(N'EXEC sp_rename N''' + @MessageQueueObject + N'.' + QUOTENAME(@MessageQueueIdentityColumnName) + N''', N''MessageQueueId'', ''COLUMN'';');
    END

    IF COL_LENGTH(@MessageQueueObjectName, 'PayloadJson') IS NULL
        EXEC(N'ALTER TABLE ' + @MessageQueueObject + N' ADD [PayloadJson] NVARCHAR(MAX) NULL;');

    IF COL_LENGTH(@MessageQueueObjectName, 'LastError') IS NULL
        EXEC(N'ALTER TABLE ' + @MessageQueueObject + N' ADD [LastError] NVARCHAR(MAX) NULL;');

    IF COL_LENGTH(@MessageQueueObjectName, 'CreatedAtUtc') IS NULL
        EXEC(N'ALTER TABLE ' + @MessageQueueObject + N' ADD [CreatedAtUtc] DATETIME NOT NULL CONSTRAINT [DF_MessageQueue_CreatedAtUtc] DEFAULT (GETUTCDATE());');

    IF COL_LENGTH(@MessageQueueObjectName, 'LastAttemptAtUtc') IS NULL
        EXEC(N'ALTER TABLE ' + @MessageQueueObject + N' ADD [LastAttemptAtUtc] DATETIME NULL;');

    IF COL_LENGTH(@MessageQueueObjectName, 'UpdatedAtUtc') IS NULL
        EXEC(N'ALTER TABLE ' + @MessageQueueObject + N' ADD [UpdatedAtUtc] DATETIME NULL;');
END

IF OBJECT_ID(N'[dbo].[Messages]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.Messages', 'WhatsAppPhoneNumberId') IS NULL
        ALTER TABLE [dbo].[Messages] ADD [WhatsAppPhoneNumberId] INT NULL;

    IF COL_LENGTH('dbo.Messages', 'CreatedAtUtc') IS NULL
        ALTER TABLE [dbo].[Messages] ADD [CreatedAtUtc] DATETIME NOT NULL CONSTRAINT [DF_Messages_CreatedAtUtc] DEFAULT (GETUTCDATE());

    IF COL_LENGTH('dbo.Messages', 'UpdatedAtUtc') IS NULL
        ALTER TABLE [dbo].[Messages] ADD [UpdatedAtUtc] DATETIME NULL;

    IF COL_LENGTH('dbo.Messages', 'ExternalMessageId') IS NULL
        ALTER TABLE [dbo].[Messages] ADD [ExternalMessageId] NVARCHAR(120) NULL;

    IF COL_LENGTH('dbo.Messages', 'FailureReason') IS NULL
        ALTER TABLE [dbo].[Messages] ADD [FailureReason] NVARCHAR(MAX) NULL;
END
""";

            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database compatibility patch could not be applied. Continuing startup.");
        }
    }
}
