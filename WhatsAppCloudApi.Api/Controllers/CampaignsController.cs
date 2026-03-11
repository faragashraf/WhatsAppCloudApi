using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize]
public sealed class CampaignsController : ApiControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ApplicationDbContext _db;

    public CampaignsController(ICampaignService campaignService, ITenantContextAccessor tenantContext, ApplicationDbContext db)
    {
        _campaignService = campaignService;
        _tenantContext = tenantContext;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCampaigns([FromQuery] CampaignQueryParams query, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.CampaignsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _campaignService.GetCampaignsAsync(ctx.CompanyId, query, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetCampaign(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.CampaignsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _campaignService.GetCampaignByIdAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCampaign([FromBody] CampaignCreateRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.CampaignsCreate)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _campaignService.CreateCampaignAsync(ctx.CompanyId, request, ct));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateCampaign(long id, [FromBody] CampaignUpdateRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.CampaignsEdit)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _campaignService.UpdateCampaignAsync(ctx.CompanyId, id, request, ct));
    }

    [HttpPost("{id:long}/launch")]
    public async Task<IActionResult> LaunchCampaign(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.CampaignsLaunch)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _campaignService.LaunchCampaignAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> CancelCampaign(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.CampaignsLaunch)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _campaignService.CancelCampaignAsync(ctx.CompanyId, id, ct));
    }

    [HttpGet("{id:long}/contacts")]
    public async Task<IActionResult> GetCampaignContacts(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.CampaignsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _campaignService.GetCampaignContactsAsync(ctx.CompanyId, id, page, pageSize, ct));
    }

    private async Task<Domain.Models.UserPermissions> GetPermissions(int companyId, int userId, string role, CancellationToken ct)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Domain.Models.UserPermissions.FullAccess();
        var user = await _db.CompanyUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, ct);
        return user?.EffectivePermissions ?? Domain.Models.UserPermissions.MemberDefault();
    }
}
