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
    /// Update the access token for the currently active WhatsApp connection.
    /// </summary>
    [HttpPost("access-token")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAccessToken([FromBody] UpdateAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var webhookBaseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

        var result = await _metaVerificationService.UpdateAccessTokenAsync(
            tenant.CompanyId, request, webhookBaseUrl, cancellationToken);

        if (result.Status != "Connected")
        {
            return ToActionResult(ApiResponse<ConnectMetaResponse>.Fail(
                result.ErrorMessage ?? "Could not update access token.",
                result.Status switch
                {
                    "InvalidToken" => System.Net.HttpStatusCode.Unauthorized,
                    "AccountNotFound" => System.Net.HttpStatusCode.NotFound,
                    "PermissionDenied" => System.Net.HttpStatusCode.Forbidden,
                    _ => System.Net.HttpStatusCode.BadRequest,
                }));
        }

        return ToActionResult(ApiResponse<ConnectMetaResponse>.Ok(result, "Access token updated successfully."));
    }

    /// <summary>
    /// Rotate webhook verify token for the currently active WhatsApp connection.
    /// </summary>
    [HttpPost("verify-token/rotate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RotateVerifyToken(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var result = await _metaVerificationService.RotateVerifyTokenAsync(tenant.CompanyId, cancellationToken);
        if (result is null)
        {
            return ToActionResult(ApiResponse<object>.Fail(
                "No active WhatsApp account found for this company.",
                System.Net.HttpStatusCode.NotFound));
        }

        return ToActionResult(ApiResponse<RotateVerifyTokenResponse>.Ok(result, "Verify token rotated successfully."));
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
        var webhookBaseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        status.WebhookUrl = webhookBaseUrl.TrimEnd('/') + "/api/webhook";
        return ToActionResult(ApiResponse<WhatsAppConnectionStatus>.Ok(status));
    }
}
