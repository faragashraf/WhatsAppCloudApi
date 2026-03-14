using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/leads")]
[Authorize]
public sealed class LeadsController : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ITenantContextAccessor _tenantContext;

    public LeadsController(ApplicationDbContext db, ITenantContextAccessor tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeads([FromQuery] LeadQueryParams query, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationView)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var q = _db.LeadRecords
            .AsNoTracking()
            .Include(x => x.Contact)
            .Include(x => x.LeadDepartment)
            .Where(x => x.CompanyId == ctx.CompanyId);

        if (query.ContactId.HasValue)
        {
            q = q.Where(x => x.ContactId == query.ContactId.Value);
        }

        if (query.ConversationFlowId.HasValue)
        {
            q = q.Where(x => x.ConversationFlowId == query.ConversationFlowId.Value);
        }

        if (query.LeadDepartmentId.HasValue)
        {
            q = q.Where(x => x.LeadDepartmentId == query.LeadDepartmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.DepartmentKey))
        {
            var key = query.DepartmentKey.Trim().ToLowerInvariant();
            q = q.Where(x => x.LeadDepartment != null && x.LeadDepartment.DepartmentKey.ToLower() == key);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            var source = query.Source.Trim().ToLowerInvariant();
            q = q.Where(x => x.Source.ToLower() == source);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            q = q.Where(x => x.Status.ToUpper() == status);
        }

        if (query.FromUtc.HasValue)
        {
            q = q.Where(x => x.CreatedAtUtc >= query.FromUtc.Value);
        }

        if (query.ToUtc.HasValue)
        {
            q = q.Where(x => x.CreatedAtUtc <= query.ToUtc.Value);
        }

        var totalCount = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows.Select(ToLeadDto).ToList();
        return ToActionResult(ApiResponse<PagedResult<LeadRecordDto>>.Ok(new PagedResult<LeadRecordDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        }));
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationView)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var baseQuery = _db.LeadRecords
            .AsNoTracking()
            .Where(x => x.CompanyId == ctx.CompanyId);

        var totalLeads = await baseQuery.CountAsync(ct);
        var newLeads = await baseQuery.CountAsync(x => x.Status.ToUpper() == "NEW", ct);
        var contactLinkedLeads = await baseQuery.CountAsync(x => x.ContactId.HasValue, ct);
        var metaFlowLeads = await baseQuery.CountAsync(x => x.Source.ToLower() == "meta_flow", ct);
        var lastLeadAtUtc = await baseQuery.MaxAsync(x => (DateTime?)x.CreatedAtUtc, ct);

        var bySource = await baseQuery
            .GroupBy(x => x.Source.ToLower())
            .Select(g => new LeadSourceSummaryDto
            {
                Source = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var byDepartment = await _db.LeadRecords
            .AsNoTracking()
            .Where(x => x.CompanyId == ctx.CompanyId)
            .GroupBy(x => new
            {
                x.LeadDepartmentId,
                DepartmentKey = x.LeadDepartment != null ? x.LeadDepartment.DepartmentKey : "unassigned",
                NameAr = x.LeadDepartment != null ? x.LeadDepartment.NameAr : "\u063A\u064A\u0631 \u0645\u062D\u062F\u062F",
                NameEn = x.LeadDepartment != null ? x.LeadDepartment.NameEn : "Unassigned"
            })
            .Select(g => new LeadDepartmentSummaryDto
            {
                LeadDepartmentId = g.Key.LeadDepartmentId,
                DepartmentKey = g.Key.DepartmentKey,
                NameAr = g.Key.NameAr,
                NameEn = g.Key.NameEn,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var summary = new LeadDashboardSummaryDto
        {
            TotalLeads = totalLeads,
            NewLeads = newLeads,
            ContactLinkedLeads = contactLinkedLeads,
            MetaFlowLeads = metaFlowLeads,
            LastLeadAtUtc = lastLeadAtUtc,
            ByDepartment = byDepartment,
            BySource = bySource
        };

        return ToActionResult(ApiResponse<LeadDashboardSummaryDto>.Ok(summary));
    }

    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments(CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationView)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var items = await _db.LeadDepartments
            .AsNoTracking()
            .Include(x => x.RoutingTeam)
            .Where(x => x.CompanyId == ctx.CompanyId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NameEn)
            .Select(x => new LeadDepartmentDto
            {
                LeadDepartmentId = x.LeadDepartmentId,
                CompanyId = x.CompanyId,
                DepartmentKey = x.DepartmentKey,
                NameAr = x.NameAr,
                NameEn = x.NameEn,
                RoutingTeamId = x.RoutingTeamId,
                RoutingTeamName = x.RoutingTeam != null ? x.RoutingTeam.Name : null,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder,
                LeadCount = x.Leads.Count,
                CreatedAtUtc = x.CreatedAtUtc,
                UpdatedAtUtc = x.UpdatedAtUtc
            })
            .ToListAsync(ct);

        return ToActionResult(ApiResponse<List<LeadDepartmentDto>>.Ok(items));
    }

    [HttpPost("departments")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDepartment([FromBody] UpsertLeadDepartmentRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationEdit)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var key = request.DepartmentKey.Trim().ToLowerInvariant();
        var keyExists = await _db.LeadDepartments
            .AnyAsync(x => x.CompanyId == ctx.CompanyId && x.DepartmentKey.ToLower() == key, ct);
        if (keyExists)
        {
            return ToActionResult(ApiResponse<object>.Fail("Department key already exists.", System.Net.HttpStatusCode.Conflict));
        }

        var routingTeamError = await ValidateRoutingTeamAsync(ctx.CompanyId, request.RoutingTeamId, ct);
        if (routingTeamError is not null)
        {
            return routingTeamError;
        }

        var row = new LeadDepartment
        {
            CompanyId = ctx.CompanyId,
            DepartmentKey = key,
            NameAr = request.NameAr.Trim(),
            NameEn = request.NameEn.Trim(),
            RoutingTeamId = request.RoutingTeamId,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.LeadDepartments.Add(row);
        await _db.SaveChangesAsync(ct);

        return ToActionResult(ApiResponse<LeadDepartmentDto>.Ok(await ToDepartmentDtoAsync(row, ct)));
    }

    [HttpPut("departments/{departmentId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDepartment([FromRoute] int departmentId, [FromBody] UpsertLeadDepartmentRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationEdit)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var row = await _db.LeadDepartments
            .FirstOrDefaultAsync(x => x.CompanyId == ctx.CompanyId && x.LeadDepartmentId == departmentId, ct);
        if (row is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("Department not found.", System.Net.HttpStatusCode.NotFound));
        }

        var key = request.DepartmentKey.Trim().ToLowerInvariant();
        var keyConflict = await _db.LeadDepartments
            .AnyAsync(x => x.CompanyId == ctx.CompanyId && x.LeadDepartmentId != departmentId && x.DepartmentKey.ToLower() == key, ct);
        if (keyConflict)
        {
            return ToActionResult(ApiResponse<object>.Fail("Department key already exists.", System.Net.HttpStatusCode.Conflict));
        }

        var routingTeamError = await ValidateRoutingTeamAsync(ctx.CompanyId, request.RoutingTeamId, ct);
        if (routingTeamError is not null)
        {
            return routingTeamError;
        }

        row.DepartmentKey = key;
        row.NameAr = request.NameAr.Trim();
        row.NameEn = request.NameEn.Trim();
        row.RoutingTeamId = request.RoutingTeamId;
        row.IsActive = request.IsActive;
        row.SortOrder = request.SortOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToActionResult(ApiResponse<LeadDepartmentDto>.Ok(await ToDepartmentDtoAsync(row, ct)));
    }

    [HttpPost("departments/seed-defaults")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SeedDefaultDepartments(CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationEdit)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var seeds = new (string Key, string Ar, string En, int Sort)[]
        {
            ("sales", "\u0627\u0644\u0645\u0628\u064A\u0639\u0627\u062A", "Sales", 10),
            ("marketing", "\u0627\u0644\u062A\u0633\u0648\u064A\u0642", "Marketing", 20),
            ("customer_service", "\u062E\u062F\u0645\u0629 \u0627\u0644\u0639\u0645\u0644\u0627\u0621", "Customer Service", 30)
        };

        var existingKeys = await _db.LeadDepartments
            .Where(x => x.CompanyId == ctx.CompanyId)
            .Select(x => x.DepartmentKey.ToLower())
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        foreach (var seed in seeds)
        {
            if (existingKeys.Contains(seed.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            _db.LeadDepartments.Add(new LeadDepartment
            {
                CompanyId = ctx.CompanyId,
                DepartmentKey = seed.Key,
                NameAr = seed.Ar,
                NameEn = seed.En,
                IsActive = true,
                SortOrder = seed.Sort,
                CreatedAtUtc = now
            });
        }

        await _db.SaveChangesAsync(ct);
        return ToActionResult(ApiResponse<bool>.Ok(true));
    }

    private async Task<IActionResult?> ValidateRoutingTeamAsync(int companyId, int? routingTeamId, CancellationToken ct)
    {
        if (!routingTeamId.HasValue)
        {
            return null;
        }

        var exists = await _db.RoutingTeams
            .AsNoTracking()
            .AnyAsync(x => x.CompanyId == companyId && x.RoutingTeamId == routingTeamId.Value, ct);
        if (exists)
        {
            return null;
        }

        return ToActionResult(ApiResponse<object>.Fail("Selected routing team was not found.", System.Net.HttpStatusCode.BadRequest));
    }

    private async Task<LeadDepartmentDto> ToDepartmentDtoAsync(LeadDepartment row, CancellationToken ct)
    {
        var routingTeamName = row.RoutingTeamId.HasValue
            ? await _db.RoutingTeams
                .Where(x => x.CompanyId == row.CompanyId && x.RoutingTeamId == row.RoutingTeamId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct)
            : null;

        return new LeadDepartmentDto
        {
            LeadDepartmentId = row.LeadDepartmentId,
            CompanyId = row.CompanyId,
            DepartmentKey = row.DepartmentKey,
            NameAr = row.NameAr,
            NameEn = row.NameEn,
            RoutingTeamId = row.RoutingTeamId,
            RoutingTeamName = routingTeamName,
            IsActive = row.IsActive,
            SortOrder = row.SortOrder,
            LeadCount = await _db.LeadRecords.CountAsync(x => x.CompanyId == row.CompanyId && x.LeadDepartmentId == row.LeadDepartmentId, ct),
            CreatedAtUtc = row.CreatedAtUtc,
            UpdatedAtUtc = row.UpdatedAtUtc
        };
    }

    private static LeadRecordDto ToLeadDto(LeadRecord row)
    {
        return new LeadRecordDto
        {
            LeadRecordId = row.LeadRecordId,
            CompanyId = row.CompanyId,
            ConversationFlowFormSubmissionId = row.ConversationFlowFormSubmissionId,
            ConversationFlowId = row.ConversationFlowId,
            ConversationFlowSessionId = row.ConversationFlowSessionId,
            ConversationId = row.ConversationId,
            ContactId = row.ContactId,
            ContactName = row.Contact?.Name,
            ContactPhoneNumber = row.Contact?.PhoneNumber,
            LeadDepartmentId = row.LeadDepartmentId,
            DepartmentKey = row.LeadDepartment?.DepartmentKey,
            DepartmentNameAr = row.LeadDepartment?.NameAr,
            DepartmentNameEn = row.LeadDepartment?.NameEn,
            Source = row.Source,
            Status = row.Status,
            ExtractedValues = ParseExtractedValues(row.ExtractedValuesJson),
            Notes = row.Notes,
            CreatedAtUtc = row.CreatedAtUtc,
            UpdatedAtUtc = row.UpdatedAtUtc
        };
    }

    private static Dictionary<string, string> ParseExtractedValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOpts)
                   ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task<Domain.Models.UserPermissions> GetPermissions(int companyId, int userId, string role, CancellationToken ct)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.Models.UserPermissions.FullAccess();
        }

        var user = await _db.CompanyUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, ct);
        return user?.EffectivePermissions ?? Domain.Models.UserPermissions.MemberDefault();
    }
}
