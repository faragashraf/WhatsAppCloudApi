using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/company")]
[Authorize]
public sealed class CompanyConnectionController : ApiControllerBase
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IMetaVerificationService _metaVerificationService;

    public CompanyConnectionController(
        ITenantContextAccessor tenantContextAccessor,
        IMetaVerificationService metaVerificationService)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _metaVerificationService = metaVerificationService;
    }

    /// <summary>
    /// Connect a WhatsApp Business Account by verifying credentials with Meta,
    /// importing phone numbers, and configuring webhooks.
    /// </summary>
    [HttpPost("connect-meta")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConnectMeta([FromBody] ConnectMetaRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var webhookBaseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

        var result = await _metaVerificationService.ConnectBusinessAccountAsync(
            tenant.CompanyId, request, webhookBaseUrl, cancellationToken);

        if (result.Status != "Connected")
        {
            return ToActionResult(ApiResponse<ConnectMetaResponse>.Fail(
                result.ErrorMessage ?? "Connection failed.",
                result.Status switch
                {
                    "InvalidToken" => System.Net.HttpStatusCode.Unauthorized,
                    "AccountNotFound" => System.Net.HttpStatusCode.NotFound,
                    "PermissionDenied" => System.Net.HttpStatusCode.Forbidden,
                    _ => System.Net.HttpStatusCode.BadRequest,
                }));
        }

        return ToActionResult(ApiResponse<ConnectMetaResponse>.Ok(result, "WhatsApp Business Account connected successfully."));
    }

    /// <summary>
    /// Verify current WhatsApp connection status, refresh phone numbers, and check token validity.
    /// Called when company admin opens the profile/settings page.
    /// </summary>
    [HttpGet("connection-status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetConnectionStatus(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var status = await _metaVerificationService.VerifyConnectionAsync(tenant.CompanyId, cancellationToken);
        return ToActionResult(ApiResponse<WhatsAppConnectionStatus>.Ok(status));
    }
}
