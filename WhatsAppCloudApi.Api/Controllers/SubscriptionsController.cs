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
[Route("api/subscriptions")]
[Authorize]
public sealed class SubscriptionsController : ApiControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public SubscriptionsController(ApplicationDbContext dbContext, ITenantContextAccessor tenantContextAccessor)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken cancellationToken)
    {
        try
        {
            var plans = await _dbContext.SubscriptionPlans
                .AsNoTracking()
                .Where(x => x.IsActive)
                .ToListAsync(cancellationToken);

            return ToActionResult(ApiResponse<List<SubscriptionPlan>>.Ok(plans));
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            var plans = await QueryPlansFallbackAsync(cancellationToken);
            return ToActionResult(ApiResponse<List<SubscriptionPlan>>.Ok(plans));
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetCompanySubscriptions(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        try
        {
            var subscriptions = await _dbContext.CompanySubscriptions
                .AsNoTracking()
                .Where(x => x.CompanyId == tenant.CompanyId)
                .OrderByDescending(x => x.CreatedAtUtc)
                .ToListAsync(cancellationToken);

            return ToActionResult(ApiResponse<List<CompanySubscription>>.Ok(subscriptions));
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            var subscriptions = await QueryCompanySubscriptionsFallbackAsync(tenant.CompanyId, null, cancellationToken);
            return ToActionResult(ApiResponse<List<CompanySubscription>>.Ok(subscriptions));
        }
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var now = DateTime.UtcNow;

        CompanySubscription? current;
        try
        {
            current = await _dbContext.CompanySubscriptions
                .AsNoTracking()
                .Where(x => x.CompanyId == tenant.CompanyId && x.IsActive)
                .Where(x =>
                    (x.Status == "ACTIVE" && (x.EndDate == null || x.EndDate >= now)) ||
                    (x.Status == "TRIAL" && x.TrialEndDate != null && x.TrialEndDate >= now))
                .OrderByDescending(x => x.EndDate ?? x.TrialEndDate)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            var subscriptions = await QueryCompanySubscriptionsFallbackAsync(tenant.CompanyId, null, cancellationToken);
            current = subscriptions
                .Where(x => x.IsActive)
                .Where(x =>
                    (string.Equals(x.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) && (x.EndDate is null || x.EndDate >= now)) ||
                    (string.Equals(x.Status, "TRIAL", StringComparison.OrdinalIgnoreCase) && x.TrialEndDate is not null && x.TrialEndDate >= now))
                .OrderByDescending(x => x.EndDate ?? x.TrialEndDate)
                .FirstOrDefault();
        }

        if (current is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Subscription expired.", System.Net.HttpStatusCode.BadRequest));
        }

        return ToActionResult(ApiResponse<CompanySubscription>.Ok(current));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CompanySubscriptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        bool planExists;
        try
        {
            planExists = await _dbContext.SubscriptionPlans
                .AnyAsync(x => x.SubscriptionPlanId == request.SubscriptionPlanId && x.IsActive, cancellationToken);
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            planExists = await PlanExistsFallbackAsync(request.SubscriptionPlanId, cancellationToken);
        }

        if (!planExists)
        {
            return ToActionResult(ApiResponse<object>.Fail("Subscription plan not found.", System.Net.HttpStatusCode.BadRequest));
        }

        try
        {
            var entity = new CompanySubscription
            {
                CompanyId = tenant.CompanyId,
                SubscriptionPlanId = request.SubscriptionPlanId,
                Status = request.Status,
                TrialStartDate = request.TrialStartDate,
                TrialEndDate = request.TrialEndDate,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.CompanySubscriptions.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToActionResult(ApiResponse<CompanySubscription>.Ok(entity, "Subscription created."));
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            var created = await InsertCompanySubscriptionFallbackAsync(tenant.CompanyId, request, cancellationToken);
            return ToActionResult(ApiResponse<CompanySubscription>.Ok(created, "Subscription created."));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] CompanySubscriptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();

        bool planExists;
        try
        {
            planExists = await _dbContext.SubscriptionPlans
                .AnyAsync(x => x.SubscriptionPlanId == request.SubscriptionPlanId && x.IsActive, cancellationToken);
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            planExists = await PlanExistsFallbackAsync(request.SubscriptionPlanId, cancellationToken);
        }

        if (!planExists)
        {
            return ToActionResult(ApiResponse<object>.Fail("Subscription plan not found.", System.Net.HttpStatusCode.BadRequest));
        }

        try
        {
            var entity = await _dbContext.CompanySubscriptions
                .FirstOrDefaultAsync(x => x.CompanySubscriptionId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

            if (entity is null)
            {
                return ToActionResult(ApiResponse<object>.Fail("Subscription not found.", System.Net.HttpStatusCode.NotFound));
            }

            entity.SubscriptionPlanId = request.SubscriptionPlanId;
            entity.Status = request.Status;
            entity.TrialStartDate = request.TrialStartDate;
            entity.TrialEndDate = request.TrialEndDate;
            entity.StartDate = request.StartDate;
            entity.EndDate = request.EndDate;
            entity.IsActive = request.IsActive;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToActionResult(ApiResponse<CompanySubscription>.Ok(entity, "Subscription updated."));
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            var updated = await UpdateCompanySubscriptionFallbackAsync(tenant.CompanyId, id, request, cancellationToken);
            if (updated is null)
            {
                return ToActionResult(ApiResponse<object>.Fail("Subscription not found.", System.Net.HttpStatusCode.NotFound));
            }

            return ToActionResult(ApiResponse<CompanySubscription>.Ok(updated, "Subscription updated."));
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        try
        {
            var entity = await _dbContext.CompanySubscriptions
                .FirstOrDefaultAsync(x => x.CompanySubscriptionId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

            if (entity is null)
            {
                return ToActionResult(ApiResponse<object>.Fail("Subscription not found.", System.Net.HttpStatusCode.NotFound));
            }

            _dbContext.CompanySubscriptions.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToActionResult(ApiResponse<object>.Ok(new { id }, "Subscription deleted."));
        }
        catch (Exception ex) when (IsSchemaMismatch(ex))
        {
            var deleted = await DeleteCompanySubscriptionFallbackAsync(tenant.CompanyId, id, cancellationToken);
            if (!deleted)
            {
                return ToActionResult(ApiResponse<object>.Fail("Subscription not found.", System.Net.HttpStatusCode.NotFound));
            }

            return ToActionResult(ApiResponse<object>.Ok(new { id }, "Subscription deleted."));
        }
    }

    private async Task<List<SubscriptionPlan>> QueryPlansFallbackAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetColumnsWithTypesAsync(connection, "SubscriptionPlans", cancellationToken);
        if (columns.Count == 0)
        {
            return [];
        }

        var planIdColumn = PickColumn(columns, "SubscriptionPlanId", "PlanId", "Id");
        if (planIdColumn is null)
        {
            return [];
        }

        var nameColumn = PickColumn(columns, "Name", "PlanName");
        var codeColumn = PickColumn(columns, "Code", "PlanCode");
        var trialDaysColumn = PickColumn(columns, "TrialDays");
        var maxMessagesColumn = PickColumn(columns, "MaxMessagesPerMonth");
        var maxAccountsColumn = PickColumn(columns, "MaxWhatsAppAccounts");
        var monthlyPriceColumn = PickColumn(columns, "MonthlyPrice", "PricePerMonth", "PricePerYear");
        var isActiveColumn = PickColumn(columns, "IsActive", "Active", "Status");
        var createdAtColumn = PickColumn(columns, "CreatedAtUtc", "CreatedAt");
        var updatedAtColumn = PickColumn(columns, "UpdatedAtUtc", "UpdatedAt");

        var selectParts = new List<string> { $"[{planIdColumn}] AS SubscriptionPlanId" };
        if (nameColumn is not null) selectParts.Add($"[{nameColumn}] AS Name");
        if (codeColumn is not null) selectParts.Add($"[{codeColumn}] AS Code");
        if (trialDaysColumn is not null) selectParts.Add($"[{trialDaysColumn}] AS TrialDays");
        if (maxMessagesColumn is not null) selectParts.Add($"[{maxMessagesColumn}] AS MaxMessagesPerMonth");
        if (maxAccountsColumn is not null) selectParts.Add($"[{maxAccountsColumn}] AS MaxWhatsAppAccounts");
        if (monthlyPriceColumn is not null) selectParts.Add($"[{monthlyPriceColumn}] AS MonthlyPrice");
        if (isActiveColumn is not null) selectParts.Add($"[{isActiveColumn}] AS IsActive");
        if (createdAtColumn is not null) selectParts.Add($"[{createdAtColumn}] AS CreatedAtUtc");
        if (updatedAtColumn is not null) selectParts.Add($"[{updatedAtColumn}] AS UpdatedAtUtc");

        var sql = $"SELECT {string.Join(", ", selectParts)} FROM [SubscriptionPlans]";
        if (isActiveColumn is not null)
        {
            sql += $" WHERE {BuildTruthyPredicate(isActiveColumn, columns[isActiveColumn])}";
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        var items = new List<SubscriptionPlan>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new SubscriptionPlan
            {
                SubscriptionPlanId = TryGetInt(reader, "SubscriptionPlanId") ?? 0,
                Name = TryGetString(reader, "Name") ?? string.Empty,
                Code = TryGetString(reader, "Code") ?? string.Empty,
                TrialDays = TryGetInt(reader, "TrialDays") ?? 0,
                MaxMessagesPerMonth = TryGetInt(reader, "MaxMessagesPerMonth") ?? 0,
                MaxWhatsAppAccounts = TryGetInt(reader, "MaxWhatsAppAccounts") ?? 0,
                MonthlyPrice = TryGetDecimal(reader, "MonthlyPrice"),
                IsActive = TryGetBool(reader, "IsActive") ?? true,
                CreatedAtUtc = TryGetDateTime(reader, "CreatedAtUtc") ?? DateTime.UtcNow,
                UpdatedAtUtc = TryGetDateTime(reader, "UpdatedAtUtc")
            });
        }

        return items;
    }

    private async Task<List<CompanySubscription>> QueryCompanySubscriptionsFallbackAsync(int companyId, int? id, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetColumnsWithTypesAsync(connection, "CompanySubscriptions", cancellationToken);
        if (columns.Count == 0)
        {
            return [];
        }

        var idColumn = PickColumn(columns, "CompanySubscriptionId", "SubscriptionId", "Id");
        var companyColumn = PickColumn(columns, "CompanyId", "TenantId", "CompanyID");
        if (idColumn is null || companyColumn is null)
        {
            return [];
        }

        var planIdColumn = PickColumn(columns, "SubscriptionPlanId", "PlanId", "SubscriptionId");
        var statusColumn = PickColumn(columns, "Status", "PaymentStatus");
        var isTrialColumn = PickColumn(columns, "IsTrial");
        var trialStartColumn = PickColumn(columns, "TrialStartDate");
        var trialEndColumn = PickColumn(columns, "TrialEndDate");
        var startDateColumn = PickColumn(columns, "StartDate");
        var endDateColumn = PickColumn(columns, "EndDate");
        var isActiveColumn = PickColumn(columns, "IsActive", "Active");
        var createdAtColumn = PickColumn(columns, "CreatedAtUtc", "CreatedAt", "StartDate");
        var updatedAtColumn = PickColumn(columns, "UpdatedAtUtc", "UpdatedAt");

        var selectParts = new List<string>
        {
            $"[{idColumn}] AS CompanySubscriptionId",
            $"[{companyColumn}] AS CompanyId"
        };
        if (planIdColumn is not null) selectParts.Add($"[{planIdColumn}] AS SubscriptionPlanId");
        if (statusColumn is not null) selectParts.Add($"[{statusColumn}] AS Status");
        if (isTrialColumn is not null) selectParts.Add($"[{isTrialColumn}] AS IsTrial");
        if (trialStartColumn is not null) selectParts.Add($"[{trialStartColumn}] AS TrialStartDate");
        if (trialEndColumn is not null) selectParts.Add($"[{trialEndColumn}] AS TrialEndDate");
        if (startDateColumn is not null) selectParts.Add($"[{startDateColumn}] AS StartDate");
        if (endDateColumn is not null) selectParts.Add($"[{endDateColumn}] AS EndDate");
        if (isActiveColumn is not null) selectParts.Add($"[{isActiveColumn}] AS IsActive");
        if (createdAtColumn is not null) selectParts.Add($"[{createdAtColumn}] AS CreatedAtUtc");
        if (updatedAtColumn is not null) selectParts.Add($"[{updatedAtColumn}] AS UpdatedAtUtc");

        var sql = $"SELECT {string.Join(", ", selectParts)} FROM [CompanySubscriptions] WHERE [{companyColumn}] = @companyId";
        if (id.HasValue)
        {
            sql += $" AND [{idColumn}] = @id";
        }

        if (createdAtColumn is not null)
        {
            sql += " ORDER BY [CreatedAtUtc] DESC";
        }
        else if (idColumn is not null)
        {
            sql += $" ORDER BY [{idColumn}] DESC";
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        var companyParameter = command.CreateParameter();
        companyParameter.ParameterName = "@companyId";
        companyParameter.Value = companyId;
        command.Parameters.Add(companyParameter);

        if (id.HasValue)
        {
            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "@id";
            idParameter.Value = id.Value;
            command.Parameters.Add(idParameter);
        }

        var items = new List<CompanySubscription>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var status = TryGetString(reader, "Status");
            var isTrial = TryGetBool(reader, "IsTrial") ?? false;
            if (string.IsNullOrWhiteSpace(status))
            {
                status = isTrial ? "TRIAL" : "ACTIVE";
            }

            var trialStart = TryGetDateTime(reader, "TrialStartDate");
            var trialEnd = TryGetDateTime(reader, "TrialEndDate");
            var startDate = TryGetDateTime(reader, "StartDate");
            var endDate = TryGetDateTime(reader, "EndDate");

            if (trialStart is null && isTrial)
            {
                trialStart = startDate;
            }

            if (trialEnd is null && isTrial)
            {
                trialEnd = endDate;
            }

            items.Add(new CompanySubscription
            {
                CompanySubscriptionId = TryGetInt(reader, "CompanySubscriptionId") ?? 0,
                CompanyId = TryGetInt(reader, "CompanyId") ?? companyId,
                SubscriptionPlanId = TryGetInt(reader, "SubscriptionPlanId") ?? 0,
                Status = status!,
                TrialStartDate = trialStart,
                TrialEndDate = trialEnd,
                StartDate = startDate,
                EndDate = endDate,
                IsActive = TryGetBool(reader, "IsActive") ?? true,
                CreatedAtUtc = TryGetDateTime(reader, "CreatedAtUtc") ?? DateTime.UtcNow,
                UpdatedAtUtc = TryGetDateTime(reader, "UpdatedAtUtc")
            });
        }

        return items;
    }

    private async Task<bool> PlanExistsFallbackAsync(int subscriptionPlanId, CancellationToken cancellationToken)
    {
        var plans = await QueryPlansFallbackAsync(cancellationToken);
        return plans.Any(x => x.SubscriptionPlanId == subscriptionPlanId && x.IsActive);
    }

    private async Task<CompanySubscription> InsertCompanySubscriptionFallbackAsync(int companyId, CompanySubscriptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetColumnsWithTypesAsync(connection, "CompanySubscriptions", cancellationToken);
        if (columns.Count == 0)
        {
            throw new InvalidOperationException("CompanySubscriptions table was not found.");
        }

        var mapped = new List<(string Column, object Value)>();
        var companyColumn = PickColumn(columns, "CompanyId", "TenantId", "CompanyID");
        if (companyColumn is null)
        {
            throw new InvalidOperationException("CompanySubscriptions must contain CompanyId-compatible column.");
        }

        mapped.Add((companyColumn, companyId));

        var planIdColumn = PickColumn(columns, "SubscriptionPlanId", "PlanId");
        if (planIdColumn is not null) mapped.Add((planIdColumn, request.SubscriptionPlanId));

        var statusColumn = PickColumn(columns, "Status", "PaymentStatus");
        if (statusColumn is not null) mapped.Add((statusColumn, request.Status));

        var isTrialColumn = PickColumn(columns, "IsTrial");
        if (isTrialColumn is not null)
        {
            mapped.Add((isTrialColumn, string.Equals(request.Status, "TRIAL", StringComparison.OrdinalIgnoreCase)));
        }

        var trialStartColumn = PickColumn(columns, "TrialStartDate");
        if (trialStartColumn is not null && request.TrialStartDate.HasValue) mapped.Add((trialStartColumn, request.TrialStartDate.Value));

        var trialEndColumn = PickColumn(columns, "TrialEndDate");
        if (trialEndColumn is not null && request.TrialEndDate.HasValue) mapped.Add((trialEndColumn, request.TrialEndDate.Value));

        var startDateColumn = PickColumn(columns, "StartDate");
        if (startDateColumn is not null && request.StartDate.HasValue) mapped.Add((startDateColumn, request.StartDate.Value));

        var endDateColumn = PickColumn(columns, "EndDate");
        if (endDateColumn is not null && request.EndDate.HasValue) mapped.Add((endDateColumn, request.EndDate.Value));

        var isActiveColumn = PickColumn(columns, "IsActive", "Active");
        if (isActiveColumn is not null) mapped.Add((isActiveColumn, request.IsActive));

        var createdAtColumn = PickColumn(columns, "CreatedAtUtc", "CreatedAt");
        if (createdAtColumn is not null) mapped.Add((createdAtColumn, DateTime.UtcNow));

        var updatedAtColumn = PickColumn(columns, "UpdatedAtUtc", "UpdatedAt");
        if (updatedAtColumn is not null) mapped.Add((updatedAtColumn, DateTime.UtcNow));

        await using var command = connection.CreateCommand();
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        var columnList = string.Join(", ", mapped.Select(x => $"[{x.Column}]"));
        var parameterList = new List<string>();
        for (var i = 0; i < mapped.Count; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{i}";
            parameter.Value = mapped[i].Value;
            command.Parameters.Add(parameter);
            parameterList.Add(parameter.ParameterName);
        }

        command.CommandText = $"INSERT INTO [CompanySubscriptions] ({columnList}) VALUES ({string.Join(", ", parameterList)})";
        await command.ExecuteNonQueryAsync(cancellationToken);

        var inserted = await QueryCompanySubscriptionsFallbackAsync(companyId, null, cancellationToken);
        return inserted.FirstOrDefault() ?? new CompanySubscription
        {
            CompanyId = companyId,
            SubscriptionPlanId = request.SubscriptionPlanId,
            Status = request.Status,
            TrialStartDate = request.TrialStartDate,
            TrialEndDate = request.TrialEndDate,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<CompanySubscription?> UpdateCompanySubscriptionFallbackAsync(int companyId, int id, CompanySubscriptionUpsertRequest request, CancellationToken cancellationToken)
    {
        var existing = await QueryCompanySubscriptionsFallbackAsync(companyId, id, cancellationToken);
        if (existing.Count == 0)
        {
            return null;
        }

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetColumnsWithTypesAsync(connection, "CompanySubscriptions", cancellationToken);
        var idColumn = PickColumn(columns, "CompanySubscriptionId", "SubscriptionId", "Id");
        var companyColumn = PickColumn(columns, "CompanyId", "TenantId", "CompanyID");
        if (idColumn is null || companyColumn is null)
        {
            return null;
        }

        var updates = new List<(string Column, object Value)>();
        var planIdColumn = PickColumn(columns, "SubscriptionPlanId", "PlanId");
        if (planIdColumn is not null) updates.Add((planIdColumn, request.SubscriptionPlanId));

        var statusColumn = PickColumn(columns, "Status", "PaymentStatus");
        if (statusColumn is not null) updates.Add((statusColumn, request.Status));

        var isTrialColumn = PickColumn(columns, "IsTrial");
        if (isTrialColumn is not null)
        {
            updates.Add((isTrialColumn, string.Equals(request.Status, "TRIAL", StringComparison.OrdinalIgnoreCase)));
        }

        var trialStartColumn = PickColumn(columns, "TrialStartDate");
        if (trialStartColumn is not null) updates.Add((trialStartColumn, (object?)request.TrialStartDate ?? DBNull.Value));

        var trialEndColumn = PickColumn(columns, "TrialEndDate");
        if (trialEndColumn is not null) updates.Add((trialEndColumn, (object?)request.TrialEndDate ?? DBNull.Value));

        var startDateColumn = PickColumn(columns, "StartDate");
        if (startDateColumn is not null) updates.Add((startDateColumn, (object?)request.StartDate ?? DBNull.Value));

        var endDateColumn = PickColumn(columns, "EndDate");
        if (endDateColumn is not null) updates.Add((endDateColumn, (object?)request.EndDate ?? DBNull.Value));

        var isActiveColumn = PickColumn(columns, "IsActive", "Active");
        if (isActiveColumn is not null) updates.Add((isActiveColumn, request.IsActive));

        var updatedAtColumn = PickColumn(columns, "UpdatedAtUtc", "UpdatedAt");
        if (updatedAtColumn is not null) updates.Add((updatedAtColumn, DateTime.UtcNow));

        await using var command = connection.CreateCommand();
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();

        var setParts = new List<string>();
        for (var i = 0; i < updates.Count; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{i}";
            parameter.Value = updates[i].Value;
            command.Parameters.Add(parameter);
            setParts.Add($"[{updates[i].Column}] = {parameter.ParameterName}");
        }

        var idParameter = command.CreateParameter();
        idParameter.ParameterName = "@id";
        idParameter.Value = id;
        command.Parameters.Add(idParameter);

        var companyParameter = command.CreateParameter();
        companyParameter.ParameterName = "@companyId";
        companyParameter.Value = companyId;
        command.Parameters.Add(companyParameter);

        command.CommandText = $"UPDATE [CompanySubscriptions] SET {string.Join(", ", setParts)} WHERE [{idColumn}] = @id AND [{companyColumn}] = @companyId";
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected <= 0)
        {
            return null;
        }

        return (await QueryCompanySubscriptionsFallbackAsync(companyId, id, cancellationToken)).FirstOrDefault();
    }

    private async Task<bool> DeleteCompanySubscriptionFallbackAsync(int companyId, int id, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var columns = await GetColumnsWithTypesAsync(connection, "CompanySubscriptions", cancellationToken);
        var idColumn = PickColumn(columns, "CompanySubscriptionId", "SubscriptionId", "Id");
        var companyColumn = PickColumn(columns, "CompanyId", "TenantId", "CompanyID");
        if (idColumn is null || companyColumn is null)
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"DELETE FROM [CompanySubscriptions] WHERE [{idColumn}] = @id AND [{companyColumn}] = @companyId";

        var idParameter = command.CreateParameter();
        idParameter.ParameterName = "@id";
        idParameter.Value = id;
        command.Parameters.Add(idParameter);

        var companyParameter = command.CreateParameter();
        companyParameter.ParameterName = "@companyId";
        companyParameter.Value = companyId;
        command.Parameters.Add(companyParameter);

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static async Task<Dictionary<string, string>> GetColumnsWithTypesAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COLUMN_NAME, DATA_TYPE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME = @tableName
            """;

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

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

    private static string BuildTruthyPredicate(string columnName, string dataType)
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

    private static int? TryGetInt(DbDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static decimal? TryGetDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? TryGetDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = TryGetOrdinal(reader, columnName);
        if (ordinal < 0 || reader.IsDBNull(ordinal))
        {
            return null;
        }

        return Convert.ToDateTime(reader.GetValue(ordinal));
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
}
