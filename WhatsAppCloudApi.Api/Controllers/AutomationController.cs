using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/automation")]
[Authorize]
public sealed class AutomationController : ApiControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly ITenantContextAccessor _tenantContext;

    public AutomationController(IAutomationService automationService, ITenantContextAccessor tenantContext)
    {
        _automationService = automationService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetRules(CancellationToken ct)
        => ToActionResult(await _automationService.GetRulesAsync(_tenantContext.GetRequiredContext().CompanyId, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetRule(long id, CancellationToken ct)
        => ToActionResult(await _automationService.GetRuleByIdAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] AutomationRuleUpsertRequest request, CancellationToken ct)
        => ToActionResult(await _automationService.CreateRuleAsync(_tenantContext.GetRequiredContext().CompanyId, request, ct));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateRule(long id, [FromBody] AutomationRuleUpsertRequest request, CancellationToken ct)
        => ToActionResult(await _automationService.UpdateRuleAsync(_tenantContext.GetRequiredContext().CompanyId, id, request, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteRule(long id, CancellationToken ct)
        => ToActionResult(await _automationService.DeleteRuleAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpPost("{id:long}/toggle")]
    public async Task<IActionResult> ToggleRule(long id, CancellationToken ct)
        => ToActionResult(await _automationService.ToggleRuleAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));
}
