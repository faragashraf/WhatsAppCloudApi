using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/campaigns")]
[Authorize]
public sealed class CampaignsController : ApiControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly ITenantContextAccessor _tenantContext;

    public CampaignsController(ICampaignService campaignService, ITenantContextAccessor tenantContext)
    {
        _campaignService = campaignService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetCampaigns([FromQuery] CampaignQueryParams query, CancellationToken ct)
        => ToActionResult(await _campaignService.GetCampaignsAsync(_tenantContext.GetRequiredContext().CompanyId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetCampaign(long id, CancellationToken ct)
        => ToActionResult(await _campaignService.GetCampaignByIdAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpPost]
    public async Task<IActionResult> CreateCampaign([FromBody] CampaignCreateRequest request, CancellationToken ct)
        => ToActionResult(await _campaignService.CreateCampaignAsync(_tenantContext.GetRequiredContext().CompanyId, request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateCampaign(long id, [FromBody] CampaignUpdateRequest request, CancellationToken ct)
        => ToActionResult(await _campaignService.UpdateCampaignAsync(_tenantContext.GetRequiredContext().CompanyId, id, request, ct));

    [HttpPost("{id:long}/launch")]
    public async Task<IActionResult> LaunchCampaign(long id, CancellationToken ct)
        => ToActionResult(await _campaignService.LaunchCampaignAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> CancelCampaign(long id, CancellationToken ct)
        => ToActionResult(await _campaignService.CancelCampaignAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpGet("{id:long}/contacts")]
    public async Task<IActionResult> GetCampaignContacts(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => ToActionResult(await _campaignService.GetCampaignContactsAsync(_tenantContext.GetRequiredContext().CompanyId, id, page, pageSize, ct));
}
