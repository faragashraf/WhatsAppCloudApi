using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data.Common;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/phone-numbers")]
[Authorize]
public sealed class PhoneNumbersController : ApiControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public PhoneNumbersController(ApplicationDbContext dbContext, ITenantContextAccessor tenantContextAccessor)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var result = await QueryPhoneNumbersAsync(tenant.CompanyId, null, cancellationToken);
        if (!result.Success)
        {
            return ToActionResult(ApiResponse<object>.Fail(result.ErrorMessage!, System.Net.HttpStatusCode.BadRequest));
        }

        return ToActionResult(ApiResponse<List<PhoneNumberRowDto>>.Ok(result.Items));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var result = await QueryPhoneNumbersAsync(tenant.CompanyId, id, cancellationToken);
        if (!result.Success)
        {
            return ToActionResult(ApiResponse<object>.Fail(result.ErrorMessage!, System.Net.HttpStatusCode.BadRequest));
        }

        var record = result.Items.FirstOrDefault();

        if (record is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Phone number not found.", System.Net.HttpStatusCode.NotFound));
        }

        return ToActionResult(ApiResponse<PhoneNumberRowDto>.Ok(record));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] WhatsAppPhoneNumberUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var resolvedAccountId = await ResolveWhatsAppAccountIdAsync(tenant.CompanyId, request, cancellationToken);
        if (string.IsNullOrWhiteSpace(resolvedAccountId))
        {
            return ToActionResult(ApiResponse<object>.Fail(
                "WhatsApp account not found for this company. Use local WhatsAppAccountId or provide BusinessAccountId.",
                System.Net.HttpStatusCode.BadRequest));
        }

        if (request.IsDefault)
        {
            await ClearDefaultPhoneNumbersAsync(tenant.CompanyId, cancellationToken);
        }

        var entity = new WhatsAppPhoneNumber
        {
            CompanyId = tenant.CompanyId,
            WhatsAppAccountId = resolvedAccountId!,
            PhoneNumberId = request.PhoneNumberId.Trim(),
            DisplayPhoneNumber = request.DisplayPhoneNumber.Trim(),
            VerifiedName = request.VerifiedName,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.WhatsAppPhoneNumbers.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<WhatsAppPhoneNumber>.Ok(entity, "Phone number created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] WhatsAppPhoneNumberUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.WhatsAppPhoneNumbers
            .FirstOrDefaultAsync(x => x.WhatsAppPhoneNumberId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Phone number not found.", System.Net.HttpStatusCode.NotFound));
        }

        var resolvedAccountId = await ResolveWhatsAppAccountIdAsync(tenant.CompanyId, request, cancellationToken);
        if (string.IsNullOrWhiteSpace(resolvedAccountId))
        {
            return ToActionResult(ApiResponse<object>.Fail(
                "WhatsApp account not found for this company. Use local WhatsAppAccountId or provide BusinessAccountId.",
                System.Net.HttpStatusCode.BadRequest));
        }

        if (request.IsDefault)
        {
            await ClearDefaultPhoneNumbersAsync(tenant.CompanyId, cancellationToken);
        }

        entity.WhatsAppAccountId = resolvedAccountId!;
        entity.PhoneNumberId = request.PhoneNumberId.Trim();
        entity.DisplayPhoneNumber = request.DisplayPhoneNumber.Trim();
        entity.VerifiedName = request.VerifiedName;
        entity.IsDefault = request.IsDefault;
        entity.IsActive = request.IsActive;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<WhatsAppPhoneNumber>.Ok(entity, "Phone number updated."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var entity = await _dbContext.WhatsAppPhoneNumbers
            .FirstOrDefaultAsync(x => x.WhatsAppPhoneNumberId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (entity is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Phone number not found.", System.Net.HttpStatusCode.NotFound));
        }

        _dbContext.WhatsAppPhoneNumbers.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<object>.Ok(new { id }, "Phone number deleted."));
    }

    private async Task ClearDefaultPhoneNumbersAsync(int companyId, CancellationToken cancellationToken)
    {
        var defaults = await _dbContext.WhatsAppPhoneNumbers
            .Where(x => x.CompanyId == companyId && x.IsDefault)
            .ToListAsync(cancellationToken);
        foreach (var item in defaults)
        {
            item.IsDefault = false;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private async Task<string?> ResolveWhatsAppAccountIdAsync(int companyId, WhatsAppPhoneNumberUpsertRequest request, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetColumnsAsync(connection, "WhatsAppAccounts", cancellationToken);
        if (columns.Count == 0)
        {
            return null;
        }

        var idColumn = PickColumn(columns, "WhatsAppAccountId", "WabaAccountId", "AccountId", "Id");
        if (idColumn is null)
        {
            return null;
        }

        var companyColumn = PickColumn(columns, "CompanyId", "TenantId", "CompanyID");
        var businessAccountIdColumn = PickColumn(columns, "BusinessAccountId", "WabaBusinessId", "WhatsAppBusinessAccountId", "BusinessId");
        var candidateBusinessIds = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.WhatsAppAccountId))
        {
            candidateBusinessIds.Add(request.WhatsAppAccountId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.BusinessAccountId))
        {
            var trimmed = request.BusinessAccountId.Trim();
            if (!candidateBusinessIds.Contains(trimmed, StringComparer.Ordinal))
            {
                candidateBusinessIds.Add(trimmed);
            }
        }

        if (candidateBusinessIds.Count == 0)
        {
            return null;
        }

        foreach (var candidate in candidateBusinessIds)
        {
            var predicates = new List<string>
            {
                BuildCandidatePredicate(null, idColumn)
            };

            if (businessAccountIdColumn is not null && !string.Equals(businessAccountIdColumn, idColumn, StringComparison.OrdinalIgnoreCase))
            {
                predicates.Add(BuildCandidatePredicate(null, businessAccountIdColumn));
            }

            await using var command = connection.CreateCommand();
            command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = companyColumn is null
                ? $"SELECT TOP 1 CAST([{idColumn}] AS NVARCHAR(100)) FROM [WhatsAppAccounts] WHERE {string.Join(" OR ", predicates)}"
                : $"SELECT TOP 1 CAST([{idColumn}] AS NVARCHAR(100)) FROM [WhatsAppAccounts] WHERE [{companyColumn}] = @companyId AND ({string.Join(" OR ", predicates)})";

            var candidateParameter = command.CreateParameter();
            candidateParameter.ParameterName = "@candidate";
            candidateParameter.Value = candidate;
            command.Parameters.Add(candidateParameter);

            if (companyColumn is not null)
            {
                var companyIdParameter = command.CreateParameter();
                companyIdParameter.ParameterName = "@companyId";
                companyIdParameter.Value = companyId;
                command.Parameters.Add(companyIdParameter);
            }

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is not null && result is not DBNull)
            {
                return result.ToString();
            }
        }

        // Fallback: candidate may be Meta Business ID; resolve through MetaBusinessAccounts join.
        var accountMetaBusinessAccountIdColumn = PickColumn(columns, "MetaBusinessAccountId", "MetaBusinessId");
        if (accountMetaBusinessAccountIdColumn is not null)
        {
            var metaColumns = await GetColumnsAsync(connection, "MetaBusinessAccounts", cancellationToken);
            var metaIdColumn = PickColumn(metaColumns, "MetaBusinessAccountId", "Id");
            var metaBusinessIdColumn = PickColumn(metaColumns, "BusinessId", "WabaBusinessId", "WhatsAppBusinessAccountId");
            var metaCompanyColumn = PickColumn(metaColumns, "CompanyId", "TenantId", "CompanyID");

            if (metaColumns.Count > 0 && metaIdColumn is not null && metaBusinessIdColumn is not null)
            {
                foreach (var candidate in candidateBusinessIds)
                {
                    await using var command = connection.CreateCommand();
                    command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

                    var whereParts = new List<string>();
                    if (companyColumn is not null)
                    {
                        whereParts.Add($"a.[{companyColumn}] = @companyId");
                    }

                    if (metaCompanyColumn is not null)
                    {
                        whereParts.Add($"m.[{metaCompanyColumn}] = @companyId");
                    }

                    whereParts.Add(BuildCandidatePredicate("m", metaBusinessIdColumn));
                    var whereClause = string.Join(" AND ", whereParts);

                    command.CommandText = $"""
                        SELECT TOP 1 CAST(a.[{idColumn}] AS NVARCHAR(100))
                        FROM [WhatsAppAccounts] a
                        INNER JOIN [MetaBusinessAccounts] m
                            ON CAST(a.[{accountMetaBusinessAccountIdColumn}] AS NVARCHAR(100)) = CAST(m.[{metaIdColumn}] AS NVARCHAR(100))
                        WHERE {whereClause}
                        """;

                    var candidateParameter = command.CreateParameter();
                    candidateParameter.ParameterName = "@candidate";
                    candidateParameter.Value = candidate;
                    command.Parameters.Add(candidateParameter);

                    if (companyColumn is not null || metaCompanyColumn is not null)
                    {
                        var companyIdParameter = command.CreateParameter();
                        companyIdParameter.ParameterName = "@companyId";
                        companyIdParameter.Value = companyId;
                        command.Parameters.Add(companyIdParameter);
                    }

                    var result = await command.ExecuteScalarAsync(cancellationToken);
                    if (result is not null && result is not DBNull)
                    {
                        return result.ToString();
                    }
                }
            }
        }

        return null;
    }

    private static string BuildCandidatePredicate(string? alias, string columnName)
    {
        var scopedColumn = string.IsNullOrWhiteSpace(alias)
            ? $"[{columnName}]"
            : $"{alias}.[{columnName}]";

        return $"(CAST({scopedColumn} AS NVARCHAR(100)) = @candidate OR (TRY_CONVERT(BIGINT, {scopedColumn}) IS NOT NULL AND TRY_CONVERT(BIGINT, @candidate) IS NOT NULL AND TRY_CONVERT(BIGINT, {scopedColumn}) = TRY_CONVERT(BIGINT, @candidate)))";
    }

    private async Task<PhoneNumberQueryResult> QueryPhoneNumbersAsync(int companyId, int? id, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetColumnsAsync(connection, "WhatsAppPhoneNumbers", cancellationToken);
        if (columns.Count == 0)
        {
            return PhoneNumberQueryResult.Fail("WhatsAppPhoneNumbers table was not found.");
        }

        var companyColumn = PickColumn(columns, "CompanyId", "TenantId", "CompanyID");
        if (companyColumn is null)
        {
            return PhoneNumberQueryResult.Fail("Cannot enforce tenant isolation for WhatsAppPhoneNumbers because no CompanyId-compatible column exists.");
        }

        var idColumn = PickColumn(columns, "WhatsAppPhoneNumberId", "PhoneNumberRecordId", "PhoneNumberRowId", "Id", "PhoneId");
        if (id.HasValue && idColumn is null)
        {
            return PhoneNumberQueryResult.Fail("Cannot filter by id because no id column exists on WhatsAppPhoneNumbers.");
        }

        var accountIdColumn = PickColumn(columns, "WhatsAppAccountId", "WabaAccountId", "AccountId");
        var phoneNumberIdColumn = PickColumn(columns, "PhoneNumberId", "MetaPhoneNumberId", "NumberId");
        var displayColumn = PickColumn(columns, "DisplayPhoneNumber", "DisplayNumber", "PhoneNumber");
        var verifiedColumn = PickColumn(columns, "VerifiedName", "VerifiedDisplayName", "Name");
        var isDefaultColumn = PickColumn(columns, "IsDefault", "DefaultFlag");
        var isActiveColumn = PickColumn(columns, "IsActive", "Active", "Status");

        var selectColumns = new List<string> { $"[{companyColumn}] AS CompanyId" };
        if (idColumn is not null) selectColumns.Add($"[{idColumn}] AS Id");
        if (accountIdColumn is not null) selectColumns.Add($"[{accountIdColumn}] AS WhatsAppAccountId");
        if (phoneNumberIdColumn is not null) selectColumns.Add($"[{phoneNumberIdColumn}] AS PhoneNumberId");
        if (displayColumn is not null) selectColumns.Add($"[{displayColumn}] AS DisplayPhoneNumber");
        if (verifiedColumn is not null) selectColumns.Add($"[{verifiedColumn}] AS VerifiedName");
        if (isDefaultColumn is not null) selectColumns.Add($"[{isDefaultColumn}] AS IsDefault");
        if (isActiveColumn is not null) selectColumns.Add($"[{isActiveColumn}] AS IsActive");

        if (selectColumns.Count == 1)
        {
            return PhoneNumberQueryResult.Fail("No recognizable phone number columns found in WhatsAppPhoneNumbers.");
        }

        var sql = $"SELECT {string.Join(", ", selectColumns)} FROM [WhatsAppPhoneNumbers] WHERE [{companyColumn}] = @companyId";
        if (id.HasValue && idColumn is not null)
        {
            sql += $" AND [{idColumn}] = @id";
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        var companyParameter = command.CreateParameter();
        companyParameter.ParameterName = "@companyId";
        companyParameter.Value = companyId;
        command.Parameters.Add(companyParameter);

        if (id.HasValue && idColumn is not null)
        {
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "@id";
            idParameter.Value = id.Value;
            command.Parameters.Add(idParameter);
        }

        var items = new List<PhoneNumberRowDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new PhoneNumberRowDto
            {
                Id = TryGetInt(reader, "Id"),
                CompanyId = TryGetInt(reader, "CompanyId"),
                WhatsAppAccountId = TryGetString(reader, "WhatsAppAccountId"),
                PhoneNumberId = TryGetString(reader, "PhoneNumberId"),
                DisplayPhoneNumber = TryGetString(reader, "DisplayPhoneNumber"),
                VerifiedName = TryGetString(reader, "VerifiedName"),
                IsDefault = TryGetBool(reader, "IsDefault"),
                IsActive = TryGetBool(reader, "IsActive")
            });
        }

        return PhoneNumberQueryResult.Ok(items);
    }

    private static async Task<HashSet<string>> GetColumnsAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName
            """;
        command.Transaction = null;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static string? PickColumn(IReadOnlyCollection<string> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (columns.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static int? TryGetInt(DbDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static string? TryGetString(DbDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal)?.ToString();
    }

    private static bool? TryGetBool(DbDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        if (value is bool b) return b;
        if (value is byte by) return by != 0;
        if (value is short s) return s != 0;
        if (value is int i) return i != 0;

        if (value is string str)
        {
            var normalized = str.Trim().ToUpperInvariant();
            if (normalized is "TRUE" or "1" or "YES" or "ACTIVE") return true;
            if (normalized is "FALSE" or "0" or "NO" or "INACTIVE") return false;
        }

        return null;
    }

    private static int TryGetOrdinal(DbDataReader reader, string columnName)
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

    private sealed class PhoneNumberQueryResult
    {
        public bool Success { get; }
        public string? ErrorMessage { get; }
        public List<PhoneNumberRowDto> Items { get; }

        private PhoneNumberQueryResult(bool success, string? errorMessage, List<PhoneNumberRowDto>? items = null)
        {
            Success = success;
            ErrorMessage = errorMessage;
            Items = items ?? [];
        }

        public static PhoneNumberQueryResult Ok(List<PhoneNumberRowDto> items) => new(true, null, items);
        public static PhoneNumberQueryResult Fail(string errorMessage) => new(false, errorMessage);
    }

    public sealed class PhoneNumberRowDto
    {
        public int? Id { get; set; }
        public int? CompanyId { get; set; }
        public string? WhatsAppAccountId { get; set; }
        public string? PhoneNumberId { get; set; }
        public string? DisplayPhoneNumber { get; set; }
        public string? VerifiedName { get; set; }
        public bool? IsDefault { get; set; }
        public bool? IsActive { get; set; }
    }
}
