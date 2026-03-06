using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
[Authorize]
public sealed class WhatsAppController : ApiControllerBase
{
    private readonly IWhatsAppService _whatsAppService;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ITenantWhatsAppConfigService _tenantWhatsAppConfigService;

    public WhatsAppController(
        IWhatsAppService whatsAppService,
        ITenantContextAccessor tenantContextAccessor,
        ITenantWhatsAppConfigService tenantWhatsAppConfigService)
    {
        _whatsAppService = whatsAppService;
        _tenantContextAccessor = tenantContextAccessor;
        _tenantWhatsAppConfigService = tenantWhatsAppConfigService;
    }

    /// <summary>
    /// Send a text message.
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// {
    ///   "to": "201234567890",
    ///   "body": "Hello from API",
    ///   "previewUrl": false
    /// }
    /// </remarks>
    [HttpPost("messages/text")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse<GenericGraphResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GenericGraphResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<GenericGraphResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendText([FromBody] SendTextMessageRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.SendTextMessageAsync(request, cancellationToken));

    /// <summary>
    /// Send a template message.
    /// </summary>
    [HttpPost("messages/template")]
    [SwaggerOperation(Tags = ["Messages"])]
    public async Task<IActionResult> SendTemplate([FromBody] SendTemplateMessageRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.SendTemplateMessageAsync(request, cancellationToken));

    /// <summary>
    /// Send a media message.
    /// </summary>
    [HttpPost("messages/media")]
    [SwaggerOperation(Tags = ["Messages"])]
    public async Task<IActionResult> SendMedia([FromBody] SendMediaMessageRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.SendMediaMessageAsync(request, cancellationToken));

    /// <summary>
    /// Upload media file.
    /// </summary>
    [HttpPost("media/upload")]
    [SwaggerOperation(Tags = ["Media"])]
    public async Task<IActionResult> UploadMedia([FromBody] UploadMediaRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.UploadMediaAsync(request, cancellationToken));

    /// <summary>
    /// Get media URL by media ID.
    /// </summary>
    [HttpGet("media/{mediaId}")]
    [SwaggerOperation(Tags = ["Media"])]
    public async Task<IActionResult> GetMediaUrl([FromRoute] string mediaId, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.GetMediaUrlAsync(mediaId, cancellationToken));

    /// <summary>
    /// Delete media by media ID.
    /// </summary>
    [HttpDelete("media/{mediaId}")]
    [SwaggerOperation(Tags = ["Media"])]
    public async Task<IActionResult> DeleteMedia([FromRoute] string mediaId, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.DeleteMediaAsync(mediaId, cancellationToken));

    /// <summary>
    /// Mark message as read.
    /// </summary>
    [HttpPost("messages/read")]
    [SwaggerOperation(Tags = ["Messages"])]
    public async Task<IActionResult> MarkRead([FromBody] MarkAsReadRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.MarkMessageAsReadAsync(request, cancellationToken));

    /// <summary>
    /// Get phone number details.
    /// </summary>
    [HttpGet("phone-number")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> GetPhoneNumberDetails(CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.GetPhoneNumberDetailsAsync(cancellationToken));

    /// <summary>
    /// Register phone number.
    /// </summary>
    [HttpPost("phone-number/register")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> RegisterPhoneNumber([FromBody] RegisterPhoneNumberRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.RegisterPhoneNumberAsync(request, cancellationToken));

    /// <summary>
    /// Deregister phone number.
    /// </summary>
    [HttpPost("phone-number/deregister")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> DeregisterPhoneNumber(CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.DeregisterPhoneNumberAsync(cancellationToken));

    /// <summary>
    /// Request phone number verification code.
    /// </summary>
    [HttpPost("phone-number/request-code")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> RequestVerificationCode([FromBody] RequestVerificationCodeRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.RequestVerificationCodeAsync(request, cancellationToken));

    /// <summary>
    /// Verify phone number code.
    /// </summary>
    [HttpPost("phone-number/verify-code")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyCodeRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.VerifyCodeAsync(request, cancellationToken));

    /// <summary>
    /// Get message templates.
    /// </summary>
    [HttpGet("templates")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.GetMessageTemplatesAsync(cancellationToken));

    /// <summary>
    /// Create message template.
    /// </summary>
    [HttpPost("templates")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.CreateMessageTemplateAsync(request, cancellationToken));

    /// <summary>
    /// Delete message template.
    /// </summary>
    [HttpDelete("templates/{templateId}")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> DeleteTemplate([FromRoute] string templateId, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.DeleteMessageTemplateAsync(templateId, cancellationToken));

    /// <summary>
    /// Get business profile.
    /// </summary>
    [HttpGet("business-profile")]
    [SwaggerOperation(Tags = ["Business Profile"])]
    public async Task<IActionResult> GetBusinessProfile(CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.GetBusinessProfileAsync(cancellationToken));

    /// <summary>
    /// Update business profile.
    /// </summary>
    [HttpPost("business-profile")]
    [SwaggerOperation(Tags = ["Business Profile"])]
    public async Task<IActionResult> UpdateBusinessProfile([FromBody] UpdateBusinessProfileRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.UpdateBusinessProfileAsync(request, cancellationToken));

    /// <summary>
    /// Send a raw request to Graph API for endpoints not yet mapped.
    /// </summary>
    [HttpPost("graph")]
    [SwaggerOperation(Tags = ["Graph"])]
    public async Task<IActionResult> SendGraphRequest([FromBody] GraphApiRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.SendGraphRequestAsync(request, cancellationToken));

    [HttpGet("graph/{*path}")]
    [SwaggerOperation(Tags = ["Graph"])]
    public async Task<IActionResult> GraphGet([FromRoute] string path, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Get.Method, path, BuildQueryFromRequest(), null, cancellationToken);

    [HttpPost("graph/{*path}")]
    [SwaggerOperation(Tags = ["Graph"])]
    public async Task<IActionResult> GraphPost([FromRoute] string path, [FromBody] JsonElement? body, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Post.Method, path, BuildQueryFromRequest(), body, cancellationToken);

    [HttpPut("graph/{*path}")]
    [SwaggerOperation(Tags = ["Graph"])]
    public async Task<IActionResult> GraphPut([FromRoute] string path, [FromBody] JsonElement? body, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Put.Method, path, BuildQueryFromRequest(), body, cancellationToken);

    [HttpDelete("graph/{*path}")]
    [SwaggerOperation(Tags = ["Graph"])]
    public async Task<IActionResult> GraphDelete([FromRoute] string path, [FromBody] JsonElement? body, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Delete.Method, path, BuildQueryFromRequest(), body, cancellationToken);

    [HttpGet("waba")]
    [SwaggerOperation(Tags = ["WABA"])]
    public async Task<IActionResult> GetWaba(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, config.BusinessAccountId, null, null, cancellationToken);
    }

    [HttpGet("waba/owned")]
    [SwaggerOperation(Tags = ["WABA"])]
    public async Task<IActionResult> GetOwnedWabas(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.BusinessAccountId}/owned_whatsapp_business_accounts", null, null, cancellationToken);
    }

    [HttpGet("waba/shared")]
    [SwaggerOperation(Tags = ["WABA"])]
    public async Task<IActionResult> GetSharedWabas(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.BusinessAccountId}/client_whatsapp_business_accounts", null, null, cancellationToken);
    }

    [HttpPost("waba/subscriptions")]
    [SwaggerOperation(Tags = ["Webhook Subscriptions"])]
    public async Task<IActionResult> SubscribeWaba([FromBody] JsonElement? body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.BusinessAccountId}/subscribed_apps", null, body, cancellationToken);
    }

    [HttpGet("waba/subscriptions")]
    [SwaggerOperation(Tags = ["Webhook Subscriptions"])]
    public async Task<IActionResult> GetWabaSubscriptions(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.BusinessAccountId}/subscribed_apps", null, null, cancellationToken);
    }

    [HttpDelete("waba/subscriptions")]
    [SwaggerOperation(Tags = ["Webhook Subscriptions"])]
    public async Task<IActionResult> UnsubscribeWaba(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Delete.Method, $"{config.BusinessAccountId}/subscribed_apps", null, null, cancellationToken);
    }

    [HttpPost("messages")]
    [SwaggerOperation(Tags = ["Messages"])]
    public async Task<IActionResult> SendMessageRaw([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.PhoneNumberId}/messages", null, body, cancellationToken);
    }

    [HttpGet("phone-numbers")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> GetPhoneNumbers(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.BusinessAccountId}/phone_numbers", null, null, cancellationToken);
    }

    [HttpGet("phone-number/{phoneNumberId}")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> GetPhoneNumberById([FromRoute] string phoneNumberId, [FromQuery] string? fields, CancellationToken cancellationToken)
    {
        Dictionary<string, string?>? query = null;
        if (!string.IsNullOrWhiteSpace(fields))
        {
            query = new Dictionary<string, string?> { ["fields"] = fields };
        }

        return await SendGraph(HttpMethod.Get.Method, phoneNumberId, query, null, cancellationToken);
    }

    [HttpPost("phone-number/two-step")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> SetTwoStepVerification([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, config.PhoneNumberId, null, body, cancellationToken);
    }

    [HttpGet("commerce-settings")]
    [SwaggerOperation(Tags = ["Commerce"])]
    public async Task<IActionResult> GetCommerceSettings(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.PhoneNumberId}/whatsapp_commerce_settings", null, null, cancellationToken);
    }

    [HttpPost("commerce-settings")]
    [SwaggerOperation(Tags = ["Commerce"])]
    public async Task<IActionResult> UpdateCommerceSettings([FromQuery] bool isCartEnabled, [FromQuery] bool isCatalogVisible, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(
                HttpMethod.Post.Method,
                $"{config.PhoneNumberId}/whatsapp_commerce_settings",
                new Dictionary<string, string?>
                {
                    ["is_cart_enabled"] = isCartEnabled.ToString().ToLowerInvariant(),
                    ["is_catalog_visible"] = isCatalogVisible.ToString().ToLowerInvariant()
                },
                null,
                cancellationToken);
    }

    [HttpGet("block-users")]
    [SwaggerOperation(Tags = ["Block Users"])]
    public async Task<IActionResult> GetBlockedUsers(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.PhoneNumberId}/block_users", null, null, cancellationToken);
    }

    [HttpPost("block-users")]
    [SwaggerOperation(Tags = ["Block Users"])]
    public async Task<IActionResult> BlockUsers([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.PhoneNumberId}/block_users", null, body, cancellationToken);
    }

    [HttpDelete("block-users")]
    [SwaggerOperation(Tags = ["Block Users"])]
    public async Task<IActionResult> UnblockUsers([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Delete.Method, $"{config.PhoneNumberId}/block_users", null, body, cancellationToken);
    }

    [HttpGet("qr-codes")]
    [SwaggerOperation(Tags = ["QR Codes"])]
    public async Task<IActionResult> GetQrCodes([FromQuery] string? fields, [FromQuery] string? code, CancellationToken cancellationToken)
    {
        Dictionary<string, string?>? query = null;
        if (!string.IsNullOrWhiteSpace(fields) || !string.IsNullOrWhiteSpace(code))
        {
            query = new Dictionary<string, string?>();
            if (!string.IsNullOrWhiteSpace(fields)) query["fields"] = fields;
            if (!string.IsNullOrWhiteSpace(code)) query["code"] = code;
        }

        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.PhoneNumberId}/message_qrdls", query, null, cancellationToken);
    }

    [HttpGet("qr-codes/{qrCodeId}")]
    [SwaggerOperation(Tags = ["QR Codes"])]
    public async Task<IActionResult> GetQrCodeById([FromRoute] string qrCodeId, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.PhoneNumberId}/message_qrdls/{Uri.EscapeDataString(qrCodeId)}", null, null, cancellationToken);
    }

    [HttpPost("qr-codes")]
    [SwaggerOperation(Tags = ["QR Codes"])]
    public async Task<IActionResult> CreateOrUpdateQrCode([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.PhoneNumberId}/message_qrdls", null, body, cancellationToken);
    }

    [HttpDelete("qr-codes/{qrCodeId}")]
    [SwaggerOperation(Tags = ["QR Codes"])]
    public async Task<IActionResult> DeleteQrCode([FromRoute] string qrCodeId, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Delete.Method, $"{config.PhoneNumberId}/message_qrdls/{Uri.EscapeDataString(qrCodeId)}", null, null, cancellationToken);
    }

    [HttpGet("analytics")]
    [SwaggerOperation(Tags = ["Analytics"])]
    public async Task<IActionResult> GetAnalytics([FromQuery] string fields, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, config.BusinessAccountId, new Dictionary<string, string?> { ["fields"] = fields }, null, cancellationToken);
    }

    [HttpGet("templates/{templateId}")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> GetTemplateById([FromRoute] string templateId, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Get.Method, Uri.EscapeDataString(templateId), BuildQueryFromRequest(), null, cancellationToken);

    [HttpGet("templates/search")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> GetTemplatesByName([FromQuery] string name, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.BusinessAccountId}/message_templates", new Dictionary<string, string?> { ["name"] = name }, null, cancellationToken);
    }

    [HttpGet("templates/namespace")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> GetTemplateNamespace(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, config.BusinessAccountId, new Dictionary<string, string?> { ["fields"] = "message_template_namespace" }, null, cancellationToken);
    }

    [HttpPost("templates/{templateId}/edit")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> EditTemplate([FromRoute] string templateId, [FromBody] JsonElement body, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Post.Method, Uri.EscapeDataString(templateId), null, body, cancellationToken);

    [HttpDelete("templates")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> DeleteTemplateAdvanced([FromQuery] string? name, [FromQuery] string? hsmId, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(name)) query["name"] = name;
        if (!string.IsNullOrWhiteSpace(hsmId)) query["hsm_id"] = hsmId;

        return await SendGraph(HttpMethod.Delete.Method, $"{config.BusinessAccountId}/message_templates", query, null, cancellationToken);
    }

    [HttpGet("media/download/{*mediaPath}")]
    [SwaggerOperation(Tags = ["Media"])]
    public async Task<IActionResult> DownloadMediaRaw([FromRoute] string mediaPath, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Get.Method, mediaPath, BuildQueryFromRequest(), null, cancellationToken);

    [HttpPost("flows")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> CreateFlow([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.BusinessAccountId}/flows", null, body, cancellationToken);
    }

    [HttpGet("flows")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> ListFlows(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.BusinessAccountId}/flows", null, null, cancellationToken);
    }

    [HttpPost("flows/migrate")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> MigrateFlows([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.BusinessAccountId}/migrate_flows", null, body, cancellationToken);
    }

    [HttpGet("flows/{flowId}")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> GetFlow([FromRoute] string flowId, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Get.Method, Uri.EscapeDataString(flowId), BuildQueryFromRequest(), null, cancellationToken);

    [HttpPost("flows/{flowId}")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> UpdateFlowMetadata([FromRoute] string flowId, [FromBody] JsonElement body, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Post.Method, Uri.EscapeDataString(flowId), null, body, cancellationToken);

    [HttpDelete("flows/{flowId}")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> DeleteFlow([FromRoute] string flowId, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Delete.Method, Uri.EscapeDataString(flowId), null, null, cancellationToken);

    [HttpPost("flows/{flowId}/publish")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> PublishFlow([FromRoute] string flowId, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Post.Method, $"{Uri.EscapeDataString(flowId)}/publish", null, null, cancellationToken);

    [HttpPost("flows/{flowId}/deprecate")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> DeprecateFlow([FromRoute] string flowId, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Post.Method, $"{Uri.EscapeDataString(flowId)}/deprecate", null, null, cancellationToken);

    [HttpGet("flows/{flowId}/assets")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> ListFlowAssets([FromRoute] string flowId, CancellationToken cancellationToken)
        => await SendGraph(HttpMethod.Get.Method, $"{Uri.EscapeDataString(flowId)}/assets", null, null, cancellationToken);

    [HttpPost("phone-number/encryption")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> SetEncryptionPublicKey([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.PhoneNumberId}/whatsapp_business_encryption", null, body, cancellationToken);
    }

    [HttpGet("phone-number/encryption")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> GetEncryptionPublicKey(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.PhoneNumberId}/whatsapp_business_encryption", null, null, cancellationToken);
    }

    [HttpGet("business-portfolio")]
    [SwaggerOperation(Tags = ["Business Portfolio"])]
    public async Task<IActionResult> GetBusinessPortfolio(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, config.BusinessAccountId, BuildQueryFromRequest(), null, cancellationToken);
    }

    [HttpGet("billing/extendedcredits")]
    [SwaggerOperation(Tags = ["Billing"])]
    public async Task<IActionResult> GetExtendedCredits(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.BusinessAccountId}/extendedcredits", BuildQueryFromRequest(), null, cancellationToken);
    }

    [HttpPost("onprem/migrate")]
    [SwaggerOperation(Tags = ["OnPrem Migration"])]
    public async Task<IActionResult> MigrateOnPremAccount([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.PhoneNumberId}/register", null, body, cancellationToken);
    }

    [HttpGet("business-compliance")]
    [SwaggerOperation(Tags = ["Business Compliance"])]
    public async Task<IActionResult> GetBusinessCompliance(CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Get.Method, $"{config.PhoneNumberId}/business_compliance_info", null, null, cancellationToken);
    }

    [HttpPost("business-compliance")]
    [SwaggerOperation(Tags = ["Business Compliance"])]
    public async Task<IActionResult> UpsertBusinessCompliance([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.PhoneNumberId}/business_compliance_info", null, body, cancellationToken);
    }

    [HttpPost("typing-indicator")]
    [SwaggerOperation(Tags = ["Messages"])]
    public async Task<IActionResult> SendTypingIndicator([FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await SendGraph(HttpMethod.Post.Method, $"{config.PhoneNumberId}/messages", null, body, cancellationToken);
    }

    private async Task<IActionResult> SendGraph(string method, string path, Dictionary<string, string?>? query, JsonElement? body, CancellationToken cancellationToken)
    {
        var request = new GraphApiRequest
        {
            Method = method,
            Path = path,
            Query = query ?? [],
            Body = body
        };

        var result = await _whatsAppService.SendGraphRequestAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    private Dictionary<string, string?> BuildQueryFromRequest()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in Request.Query)
        {
            result[item.Key] = item.Value.ToString();
        }

        return result;
    }

    private async Task<TenantWhatsAppConfig> GetTenantConfigAsync(CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.GetRequiredContext();
        return await _tenantWhatsAppConfigService.GetRequiredConfigAsync(context.CompanyId, cancellationToken);
    }
}
