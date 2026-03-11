using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/routing")]
[Authorize(Roles = "Admin")]
public sealed class RoutingController : ApiControllerBase
{
    private readonly IRoutingService _routingService;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public RoutingController(IRoutingService routingService, ITenantContextAccessor tenantContextAccessor)
    {
        _routingService = routingService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        return ToActionResult(await _routingService.GetCompanySettingsAsync(tenant.CompanyId, cancellationToken));
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateCompanyRoutingSettingsRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        return ToActionResult(await _routingService.UpdateCompanySettingsAsync(tenant.CompanyId, request, cancellationToken));
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        return ToActionResult(await _routingService.GetCompanyUserRoutingSettingsAsync(tenant.CompanyId, cancellationToken));
    }

    [HttpPut("users/{userId:int}")]
    public async Task<IActionResult> UpdateUser([FromRoute] int userId, [FromBody] UpdateCompanyUserRoutingSettingsRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        return ToActionResult(await _routingService.UpdateCompanyUserRoutingSettingsAsync(tenant.CompanyId, userId, request, cancellationToken));
    }
}
