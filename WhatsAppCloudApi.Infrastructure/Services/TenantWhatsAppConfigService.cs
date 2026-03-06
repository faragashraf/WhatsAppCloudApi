using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class TenantWhatsAppConfigService : ITenantWhatsAppConfigService
{
    private readonly ApplicationDbContext _dbContext;

    public TenantWhatsAppConfigService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantWhatsAppConfig> GetRequiredConfigAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigSafeAsync(companyId, null, null, cancellationToken);

        if (config is null)
        {
            throw new InvalidOperationException("No active WhatsApp configuration found for the company.");
        }

        return config;
    }

    public async Task<TenantWhatsAppConfig?> GetConfigByVerifyTokenAsync(string verifyToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(verifyToken))
        {
            return null;
        }

        return await GetConfigSafeAsync(null, verifyToken, null, cancellationToken);
    }

    public async Task<TenantWhatsAppConfig?> GetConfigByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return null;
        }

        return await GetConfigSafeAsync(null, null, phoneNumberId, cancellationToken);
    }

    private async Task<TenantWhatsAppConfig?> GetConfigSafeAsync(
        int? companyId,
        string? verifyToken,
        string? phoneNumberId,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = QueryConfig();

            if (companyId.HasValue)
            {
                query = query.Where(x => x.CompanyId == companyId.Value);
            }

            if (!string.IsNullOrWhiteSpace(verifyToken))
            {
                query = query.Where(x => x.VerifyToken == verifyToken);
            }

            if (!string.IsNullOrWhiteSpace(phoneNumberId))
            {
                query = query.Where(x => x.PhoneNumberId == phoneNumberId);
            }

            var config = await query
                .OrderByDescending(x => x.IsDefaultPhoneNumber)
                .ThenByDescending(x => x.IsDefaultAccount)
                .FirstOrDefaultAsync(cancellationToken);

            return config is null ? null : ToTenantConfig(config);
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            return await QueryConfigFallbackAsync(companyId, verifyToken, phoneNumberId, cancellationToken);
        }
    }

    private async Task<TenantWhatsAppConfig?> QueryConfigFallbackAsync(
        int? companyId,
        string? verifyToken,
        string? phoneNumberId,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var currentTransaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var accountColumns = await GetTableColumnsAsync(connection, "WhatsAppAccounts", cancellationToken, currentTransaction);
        var phoneColumns = await GetTableColumnsAsync(connection, "WhatsAppPhoneNumbers", cancellationToken, currentTransaction);
        if (accountColumns.Count == 0 || phoneColumns.Count == 0)
        {
            return null;
        }

        var accountIdColumn = PickColumn(accountColumns, "WhatsAppAccountId", "WabaAccountId", "AccountId", "Id");
        var phoneAccountIdColumn = PickColumn(phoneColumns, "WhatsAppAccountId", "WabaAccountId", "AccountId");
        var accountCompanyColumn = PickColumn(accountColumns, "CompanyId", "TenantId", "CompanyID");
        var phoneCompanyColumn = PickColumn(phoneColumns, "CompanyId", "TenantId", "CompanyID");
        var phoneRowIdColumn = PickColumn(phoneColumns, "WhatsAppPhoneNumberId", "PhoneNumberRecordId", "PhoneNumberRowId", "Id", "PhoneId");
        var accessTokenColumn = PickColumn(accountColumns, "AccessToken", "Token");
        var businessAccountIdColumn = PickColumn(accountColumns, "BusinessAccountId", "WabaId", "WhatsAppBusinessAccountId");
        var phoneNumberIdColumn = PickColumn(phoneColumns, "PhoneNumberId", "MetaPhoneNumberId", "NumberId");
        var verifyTokenColumn = PickColumn(accountColumns, "VerifyToken", "WebhookVerifyToken");
        var appSecretColumn = PickColumn(accountColumns, "AppSecret", "ApplicationSecret");
        var accountIsDefaultColumn = PickColumn(accountColumns, "IsDefault", "DefaultFlag");
        var phoneIsDefaultColumn = PickColumn(phoneColumns, "IsDefault", "DefaultFlag");
        var accountIsActiveColumn = PickColumn(accountColumns, "IsActive", "Active", "Status");
        var phoneIsActiveColumn = PickColumn(phoneColumns, "IsActive", "Active", "Status");

        if (accountIdColumn is null || phoneAccountIdColumn is null || accessTokenColumn is null || businessAccountIdColumn is null || phoneNumberIdColumn is null)
        {
            return null;
        }

        var selectCompanyExpression = accountCompanyColumn is not null
            ? $"a.[{accountCompanyColumn}]"
            : phoneCompanyColumn is not null
                ? $"p.[{phoneCompanyColumn}]"
                : "NULL";

        var selectPhoneRowIdExpression = phoneRowIdColumn is not null
            ? $"p.[{phoneRowIdColumn}]"
            : "NULL";

        var selectVerifyTokenExpression = verifyTokenColumn is not null
            ? $"a.[{verifyTokenColumn}]"
            : "NULL";

        var selectAppSecretExpression = appSecretColumn is not null
            ? $"a.[{appSecretColumn}]"
            : "NULL";

        var whereParts = new List<string>();
        await using var command = connection.CreateCommand();
        command.Transaction = currentTransaction;

        if (companyId.HasValue)
        {
            if (accountCompanyColumn is null && phoneCompanyColumn is null)
            {
                return null;
            }

            var companyPredicates = new List<string>();
            if (accountCompanyColumn is not null)
            {
                companyPredicates.Add($"a.[{accountCompanyColumn}] = @companyId");
            }

            if (phoneCompanyColumn is not null)
            {
                companyPredicates.Add($"p.[{phoneCompanyColumn}] = @companyId");
            }

            whereParts.Add($"({string.Join(" OR ", companyPredicates)})");

            var companyParameter = command.CreateParameter();
            companyParameter.ParameterName = "@companyId";
            companyParameter.Value = companyId.Value;
            command.Parameters.Add(companyParameter);
        }

        if (!string.IsNullOrWhiteSpace(verifyToken))
        {
            if (verifyTokenColumn is null)
            {
                return null;
            }

            whereParts.Add($"a.[{verifyTokenColumn}] = @verifyToken");
            var verifyTokenParameter = command.CreateParameter();
            verifyTokenParameter.ParameterName = "@verifyToken";
            verifyTokenParameter.Value = verifyToken;
            command.Parameters.Add(verifyTokenParameter);
        }

        if (!string.IsNullOrWhiteSpace(phoneNumberId))
        {
            whereParts.Add($"p.[{phoneNumberIdColumn}] = @phoneNumberId");
            var phoneNumberIdParameter = command.CreateParameter();
            phoneNumberIdParameter.ParameterName = "@phoneNumberId";
            phoneNumberIdParameter.Value = phoneNumberId;
            command.Parameters.Add(phoneNumberIdParameter);
        }

        if (accountIsActiveColumn is not null)
        {
            whereParts.Add(GetTruthyPredicate("a", accountIsActiveColumn, accountColumns[accountIsActiveColumn]));
        }

        if (phoneIsActiveColumn is not null)
        {
            whereParts.Add(GetTruthyPredicate("p", phoneIsActiveColumn, phoneColumns[phoneIsActiveColumn]));
        }

        var whereClause = whereParts.Count > 0 ? string.Join(" AND ", whereParts) : "1 = 1";

        var orderByParts = new List<string>();
        if (phoneIsDefaultColumn is not null)
        {
            orderByParts.Add($"{GetSortableBooleanExpression("p", phoneIsDefaultColumn, phoneColumns[phoneIsDefaultColumn])} DESC");
        }

        if (accountIsDefaultColumn is not null)
        {
            orderByParts.Add($"{GetSortableBooleanExpression("a", accountIsDefaultColumn, accountColumns[accountIsDefaultColumn])} DESC");
        }

        orderByParts.Add($"a.[{accountIdColumn}] DESC");
        var orderByClause = string.Join(", ", orderByParts);

        command.CommandText = $"""
            SELECT TOP 1
                {selectCompanyExpression} AS CompanyId,
                a.[{accountIdColumn}] AS WhatsAppAccountId,
                {selectPhoneRowIdExpression} AS WhatsAppPhoneNumberId,
                a.[{accessTokenColumn}] AS AccessToken,
                a.[{businessAccountIdColumn}] AS BusinessAccountId,
                p.[{phoneNumberIdColumn}] AS PhoneNumberId,
                {selectVerifyTokenExpression} AS VerifyToken,
                {selectAppSecretExpression} AS AppSecret
            FROM [WhatsAppAccounts] a
            INNER JOIN [WhatsAppPhoneNumbers] p
                ON a.[{accountIdColumn}] = p.[{phoneAccountIdColumn}]
            WHERE {whereClause}
            ORDER BY {orderByClause}
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var resolvedCompanyId = TryGetInt(reader, "CompanyId") ?? companyId;
        if (!resolvedCompanyId.HasValue)
        {
            return null;
        }

        var resolvedWhatsAppAccountId = TryGetString(reader, "WhatsAppAccountId");
        if (string.IsNullOrWhiteSpace(resolvedWhatsAppAccountId))
        {
            return null;
        }

        var accessToken = TryGetString(reader, "AccessToken");
        var businessAccountId = TryGetString(reader, "BusinessAccountId");
        var resolvedPhoneNumberId = TryGetString(reader, "PhoneNumberId");

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(businessAccountId) || string.IsNullOrWhiteSpace(resolvedPhoneNumberId))
        {
            return null;
        }

        return new TenantWhatsAppConfig
        {
            CompanyId = resolvedCompanyId.Value,
            WhatsAppAccountId = resolvedWhatsAppAccountId,
            WhatsAppPhoneNumberId = TryGetInt(reader, "WhatsAppPhoneNumberId") ?? 0,
            AccessToken = accessToken,
            BusinessAccountId = businessAccountId,
            PhoneNumberId = resolvedPhoneNumberId,
            VerifyToken = TryGetString(reader, "VerifyToken") ?? string.Empty,
            AppSecret = TryGetString(reader, "AppSecret")
        };
    }

    private IQueryable<TenantConfigProjection> QueryConfig()
    {
        return from account in _dbContext.WhatsAppAccounts.AsNoTracking()
               join phone in _dbContext.WhatsAppPhoneNumbers.AsNoTracking()
                   on account.WhatsAppAccountId equals phone.WhatsAppAccountId
               where account.IsActive && phone.IsActive
               select new TenantConfigProjection
               {
                   CompanyId = account.CompanyId,
                   WhatsAppAccountId = account.WhatsAppAccountId,
                   WhatsAppPhoneNumberId = phone.WhatsAppPhoneNumberId,
                   AccessToken = account.AccessToken,
                   BusinessAccountId = account.BusinessAccountId,
                   PhoneNumberId = phone.PhoneNumberId,
                   VerifyToken = account.VerifyToken,
                   AppSecret = account.AppSecret,
                   IsDefaultAccount = account.IsDefault,
                   IsDefaultPhoneNumber = phone.IsDefault
               };
    }

    private static TenantWhatsAppConfig ToTenantConfig(TenantConfigProjection projection)
    {
        return new TenantWhatsAppConfig
        {
            CompanyId = projection.CompanyId,
            WhatsAppAccountId = projection.WhatsAppAccountId,
            WhatsAppPhoneNumberId = projection.WhatsAppPhoneNumberId,
            AccessToken = projection.AccessToken,
            BusinessAccountId = projection.BusinessAccountId,
            PhoneNumberId = projection.PhoneNumberId,
            VerifyToken = projection.VerifyToken,
            AppSecret = projection.AppSecret
        };
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

    private static string GetTruthyPredicate(string alias, string columnName, string dataType)
    {
        var scopedColumn = $"{alias}.[{columnName}]";
        if (IsNumericBooleanType(dataType))
        {
            return $"{scopedColumn} = 1";
        }

        return $"UPPER(CAST({scopedColumn} AS NVARCHAR(100))) IN ('ACTIVE','1','TRUE','YES')";
    }

    private static string GetSortableBooleanExpression(string alias, string columnName, string dataType)
    {
        var scopedColumn = $"{alias}.[{columnName}]";
        if (IsNumericBooleanType(dataType))
        {
            return $"CASE WHEN {scopedColumn} = 1 THEN 1 ELSE 0 END";
        }

        return $"CASE WHEN UPPER(CAST({scopedColumn} AS NVARCHAR(100))) IN ('ACTIVE','TRUE','1','YES','DEFAULT') THEN 1 ELSE 0 END";
    }

    private static bool IsNumericBooleanType(string dataType)
    {
        return dataType.Equals("bit", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("tinyint", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("smallint", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("int", StringComparison.OrdinalIgnoreCase)
            || dataType.Equals("bigint", StringComparison.OrdinalIgnoreCase);
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
            || message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase);
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

    private static int? TryGetInt(System.Data.Common.DbDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static string? TryGetString(System.Data.Common.DbDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal)?.ToString();
    }

    private static int TryGetOrdinal(System.Data.Common.DbDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class TenantConfigProjection
    {
        public int CompanyId { get; set; }
        public string WhatsAppAccountId { get; set; } = string.Empty;
        public int WhatsAppPhoneNumberId { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string BusinessAccountId { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public string VerifyToken { get; set; } = string.Empty;
        public string? AppSecret { get; set; }
        public bool IsDefaultAccount { get; set; }
        public bool IsDefaultPhoneNumber { get; set; }
    }
}
