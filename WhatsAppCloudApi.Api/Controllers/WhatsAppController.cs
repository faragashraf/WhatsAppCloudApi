using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
[Authorize]
public sealed class WhatsAppController : ApiControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly Regex FormFieldReferenceRegex = new(@"\$\{form\.([A-Za-z0-9_]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> NonFieldFormComponentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "footer",
        "textheading",
        "textsubheading",
        "textbody",
        "textcaption",
        "richtext",
        "image",
        "heading",
        "subheading"
    };
    private static readonly HashSet<string> UserFacingFlowStringKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "label",
        "title",
        "text",
        "subtitle",
        "subheading",
        "heading",
        "description",
        "helper-text",
        "helper_text",
        "placeholder",
        "hint"
    };
    private readonly IWhatsAppService _whatsAppService;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ITenantWhatsAppConfigService _tenantWhatsAppConfigService;
    private readonly IHttpClientFactory _httpClientFactory;

    public WhatsAppController(
        IWhatsAppService whatsAppService,
        ITenantContextAccessor tenantContextAccessor,
        ITenantWhatsAppConfigService tenantWhatsAppConfigService,
        IHttpClientFactory httpClientFactory)
    {
        _whatsAppService = whatsAppService;
        _tenantContextAccessor = tenantContextAccessor;
        _tenantWhatsAppConfigService = tenantWhatsAppConfigService;
        _httpClientFactory = httpClientFactory;
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
    /// Send media message directly from uploaded file (single call: upload + send).
    /// </summary>
    [HttpPost("messages/media/direct")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(Tags = ["Messages"])]
    [EnableRateLimiting("upload")]
    [RequestTimeout("upload-timeout")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> SendMediaDirect(
        [FromForm] string to,
        [FromForm] string? mediaType,
        [FromForm] string? caption,
        [FromForm] string? fileName,
        [FromForm] string? phoneNumberId,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail("File is required.", System.Net.HttpStatusCode.BadRequest));
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail("File size must not exceed 10MB.", System.Net.HttpStatusCode.BadRequest));
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var request = new SendDirectMediaFileMessageRequest
        {
            To = to,
            MediaType = mediaType,
            Caption = caption,
            PhoneNumberId = phoneNumberId,
            FileName = string.IsNullOrWhiteSpace(fileName) ? file.FileName : fileName,
            ContentType = file.ContentType,
            FileData = buffer.ToArray()
        };

        return ToActionResult(await _whatsAppService.SendDirectMediaFileMessageAsync(request, cancellationToken));
    }

    /// <summary>
    /// Upload media file.
    /// </summary>
    [HttpPost("media/upload")]
    [SwaggerOperation(Tags = ["Media"])]
    [EnableRateLimiting("upload")]
    [RequestTimeout("upload-timeout")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadMedia([FromBody] UploadMediaRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.UploadMediaAsync(request, cancellationToken));

    /// <summary>
    /// Upload media file directly from multipart/form-data.
    /// This endpoint accepts a local file and internally transforms it to Meta upload shape.
    /// </summary>
    [HttpPost("media/upload/file")]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(Tags = ["Media"])]
    [EnableRateLimiting("upload")]
    [RequestTimeout("upload-timeout")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadMediaFile(
        [FromForm] IFormFile? file,
        [FromForm] string? fileName,
        [FromForm] string? contentType,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length <= 0)
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail("File is required.", System.Net.HttpStatusCode.BadRequest));
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail("File size must not exceed 10MB.", System.Net.HttpStatusCode.BadRequest));
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);

        var request = new UploadMediaFileRequest
        {
            FileName = string.IsNullOrWhiteSpace(fileName) ? file.FileName : fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? file.ContentType : contentType,
            FileData = buffer.ToArray()
        };

        return ToActionResult(await _whatsAppService.UploadMediaFileAsync(request, cancellationToken));
    }

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
    /// Download media content by media ID (authenticated proxy).
    /// Useful for inbox previews/download when media URL requires bearer token.
    /// </summary>
    [HttpGet("media/file/{mediaId}")]
    [SwaggerOperation(Tags = ["Media"])]
    public async Task<IActionResult> DownloadMediaFile([FromRoute] string mediaId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return ToActionResult(ApiResponse<object>.Fail("Media ID is required.", System.Net.HttpStatusCode.BadRequest));
        }

        var config = await GetTenantConfigAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("meta-graph");

        // Step 1: resolve media metadata to obtain a temporary download URL.
        using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, mediaId.Trim());
        metadataRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
        using var metadataResponse = await client.SendAsync(metadataRequest, cancellationToken);
        var metadataBody = await metadataResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!metadataResponse.IsSuccessStatusCode)
        {
            return ToActionResult(ApiResponse<object>.Fail(
                "Unable to resolve media metadata.",
                metadataResponse.StatusCode,
                details: metadataBody.Length > 500 ? metadataBody[..500] : metadataBody));
        }

        string? downloadUrl = null;
        string? mimeTypeFromMetadata = null;
        try
        {
            using var doc = JsonDocument.Parse(metadataBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("url", out var urlNode))
            {
                downloadUrl = urlNode.GetString();
            }

            if (root.TryGetProperty("mime_type", out var mimeNode))
            {
                mimeTypeFromMetadata = mimeNode.GetString();
            }
        }
        catch (JsonException)
        {
            return ToActionResult(ApiResponse<object>.Fail(
                "Unexpected media metadata response from Meta.",
                System.Net.HttpStatusCode.BadGateway));
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return ToActionResult(ApiResponse<object>.Fail(
                "Media download URL was not returned by Meta.",
                System.Net.HttpStatusCode.BadGateway));
        }

        // Step 2: download media bytes from Meta using the same bearer token.
        using var fileRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        fileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
        using var fileResponse = await client.SendAsync(fileRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (!fileResponse.IsSuccessStatusCode)
        {
            var fileError = await fileResponse.Content.ReadAsStringAsync(cancellationToken);
            return ToActionResult(ApiResponse<object>.Fail(
                "Unable to download media content.",
                fileResponse.StatusCode,
                details: fileError.Length > 500 ? fileError[..500] : fileError));
        }

        await using var sourceStream = await fileResponse.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new MemoryStream();
        await sourceStream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var contentType = fileResponse.Content.Headers.ContentType?.MediaType
            ?? mimeTypeFromMetadata
            ?? "application/octet-stream";

        Response.Headers.CacheControl = "no-store";
        return File(buffer, contentType);
    }

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

    [HttpPost("flows/create")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> CreateFlowMultipart([FromBody] MetaFlowCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail("Flow name is required.", System.Net.HttpStatusCode.BadRequest));
        }

        var config = await GetTenantConfigAsync(cancellationToken);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(request.Name.Trim()), "name");

        foreach (var category in NormalizeFlowCategories(request.Categories))
        {
            form.Add(new StringContent(category), "categories");
        }

        if (!string.IsNullOrWhiteSpace(request.CloneFlowId))
        {
            form.Add(new StringContent(request.CloneFlowId.Trim()), "clone_flow_id");
        }

        if (!string.IsNullOrWhiteSpace(request.EndpointUri))
        {
            form.Add(new StringContent(request.EndpointUri.Trim()), "endpoint_uri");
        }

        return ToActionResult(await SendGraphMultipartAsync($"{config.BusinessAccountId}/flows", form, cancellationToken));
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

    [HttpPost("flows/{flowId}/metadata")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> UpdateFlowMetadataMultipart(
        [FromRoute] string flowId,
        [FromBody] MetaFlowUpdateMetadataRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail("Flow id is required.", System.Net.HttpStatusCode.BadRequest));
        }

        if (string.IsNullOrWhiteSpace(request.Name)
            && string.IsNullOrWhiteSpace(request.EndpointUri)
            && (request.Categories is null || request.Categories.Count == 0))
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail(
                "Provide at least one metadata field (name, categories, endpointUri).",
                System.Net.HttpStatusCode.BadRequest));
        }

        using var form = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            form.Add(new StringContent(request.Name.Trim()), "name");
        }

        if (request.Categories is { Count: > 0 })
        {
            foreach (var category in NormalizeFlowCategories(request.Categories))
            {
                form.Add(new StringContent(category), "categories");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.EndpointUri))
        {
            form.Add(new StringContent(request.EndpointUri.Trim()), "endpoint_uri");
        }

        return ToActionResult(await SendGraphMultipartAsync(Uri.EscapeDataString(flowId), form, cancellationToken));
    }

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

    [HttpGet("flows/{flowId}/assets/flow-json")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> GetFlowJsonAsset([FromRoute] string flowId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return ToActionResult(ApiResponse<MetaFlowJsonAssetResponse>.Fail(
                "Flow id is required.",
                System.Net.HttpStatusCode.BadRequest));
        }

        var config = await GetTenantConfigAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("meta-graph");

        using var assetsRequest = new HttpRequestMessage(HttpMethod.Get, $"{Uri.EscapeDataString(flowId)}/assets");
        assetsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
        using var assetsResponse = await client.SendAsync(assetsRequest, cancellationToken);
        var assetsContent = await assetsResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!assetsResponse.IsSuccessStatusCode)
        {
            var details = assetsContent.Length > 3000 ? assetsContent[..3000] : assetsContent;
            return ToActionResult(ApiResponse<MetaFlowJsonAssetResponse>.Fail(
                "Unable to list flow assets from Meta.",
                assetsResponse.StatusCode,
                details: details));
        }

        if (!TryResolveFlowJsonAsset(assetsContent, out var assetId, out var assetName, out var downloadUrl, out var inlineFlowJson))
        {
            return ToActionResult(ApiResponse<MetaFlowJsonAssetResponse>.Ok(new MetaFlowJsonAssetResponse
            {
                FlowId = flowId.Trim(),
                FlowJson = null
            }));
        }

        if (!string.IsNullOrWhiteSpace(inlineFlowJson))
        {
            return ToActionResult(ApiResponse<MetaFlowJsonAssetResponse>.Ok(new MetaFlowJsonAssetResponse
            {
                FlowId = flowId.Trim(),
                AssetId = assetId,
                AssetName = assetName,
                FlowJson = NormalizeJsonText(inlineFlowJson)
            }));
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return ToActionResult(ApiResponse<MetaFlowJsonAssetResponse>.Ok(new MetaFlowJsonAssetResponse
            {
                FlowId = flowId.Trim(),
                AssetId = assetId,
                AssetName = assetName,
                FlowJson = null
            }));
        }

        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        downloadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);
        using var downloadResponse = await client.SendAsync(downloadRequest, cancellationToken);
        var fileContent = await downloadResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!downloadResponse.IsSuccessStatusCode)
        {
            var details = fileContent.Length > 3000 ? fileContent[..3000] : fileContent;
            return ToActionResult(ApiResponse<MetaFlowJsonAssetResponse>.Fail(
                "Unable to download flow.json asset from Meta.",
                downloadResponse.StatusCode,
                details: details));
        }

        return ToActionResult(ApiResponse<MetaFlowJsonAssetResponse>.Ok(new MetaFlowJsonAssetResponse
        {
            FlowId = flowId.Trim(),
            AssetId = assetId,
            AssetName = assetName,
            FlowJson = NormalizeJsonText(fileContent)
        }));
    }

    [HttpPost("flows/{flowId}/assets/flow-json")]
    [SwaggerOperation(Tags = ["Flows"])]
    public async Task<IActionResult> UploadFlowJsonAsset(
        [FromRoute] string flowId,
        [FromBody] MetaFlowJsonUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(flowId))
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail("Flow id is required.", System.Net.HttpStatusCode.BadRequest));
        }

        if (string.IsNullOrWhiteSpace(request.FlowJson))
        {
            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail("flowJson is required.", System.Net.HttpStatusCode.BadRequest));
        }

        if (!TryValidateFlowJsonForUpload(request.FlowJson, out var normalizedFlowJson, out var validationErrors))
        {
            var summary = validationErrors.Count > 0
                ? $"flow.json validation failed: {validationErrors[0]}"
                : "flow.json validation failed.";
            var details = string.Join(Environment.NewLine, validationErrors.Take(20));

            return ToActionResult(ApiResponse<GenericGraphResponse>.Fail(
                summary,
                System.Net.HttpStatusCode.BadRequest,
                details: string.IsNullOrWhiteSpace(details) ? null : details));
        }

        using var form = new MultipartFormDataContent();
        var bytes = Encoding.UTF8.GetBytes(normalizedFlowJson);
        using var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(fileContent, "file", "flow.json");
        form.Add(new StringContent("flow.json"), "name");
        form.Add(new StringContent("FLOW_JSON"), "asset_type");

        return ToActionResult(await SendGraphMultipartAsync($"{Uri.EscapeDataString(flowId)}/assets", form, cancellationToken));
    }

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

    private static bool TryResolveFlowJsonAsset(
        string rawAssetsJson,
        out string? assetId,
        out string? assetName,
        out string? downloadUrl,
        out string? inlineFlowJson)
    {
        assetId = null;
        assetName = null;
        downloadUrl = null;
        inlineFlowJson = null;

        if (string.IsNullOrWhiteSpace(rawAssetsJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawAssetsJson);
            if (!doc.RootElement.TryGetProperty("data", out var dataNode) || dataNode.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            JsonElement? selected = null;
            DateTimeOffset selectedStamp = DateTimeOffset.MinValue;
            foreach (var candidate in dataNode.EnumerateArray())
            {
                if (candidate.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var candidateAssetType = ReadJsonString(candidate, "asset_type");
                var candidateName = ReadJsonString(candidate, "name");
                var isFlowJsonAsset = string.Equals(candidateAssetType, "FLOW_JSON", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidateName, "flow.json", StringComparison.OrdinalIgnoreCase);
                if (!isFlowJsonAsset)
                {
                    continue;
                }

                var updatedAtRaw = ReadJsonString(candidate, "updated_at")
                    ?? ReadJsonString(candidate, "updatedAt")
                    ?? ReadJsonString(candidate, "created_time")
                    ?? ReadJsonString(candidate, "createdAt");

                var hasDate = DateTimeOffset.TryParse(updatedAtRaw, out var candidateStamp);
                if (!selected.HasValue || (hasDate && candidateStamp > selectedStamp))
                {
                    selected = candidate;
                    if (hasDate)
                    {
                        selectedStamp = candidateStamp;
                    }
                }
            }

            if (!selected.HasValue)
            {
                return false;
            }

            var chosen = selected.Value;
            assetId = ReadJsonString(chosen, "id");
            assetName = ReadJsonString(chosen, "name");
            downloadUrl = ReadJsonString(chosen, "download_url")
                ?? ReadJsonString(chosen, "downloadUrl")
                ?? ReadJsonString(chosen, "url");
            inlineFlowJson = ReadJsonString(chosen, "flow_json")
                ?? ReadJsonString(chosen, "flowJson")
                ?? ReadJsonString(chosen, "content")
                ?? ReadJsonString(chosen, "file_content");
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadJsonString(JsonElement source, string propertyName)
    {
        if (source.ValueKind != JsonValueKind.Object || !source.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Object => value.GetRawText(),
            JsonValueKind.Array => value.GetRawText(),
            _ => null
        };
    }

    private static bool TryValidateFlowJsonForUpload(
        string rawFlowJson,
        out string normalizedFlowJson,
        out List<string> validationErrors)
    {
        normalizedFlowJson = rawFlowJson?.Trim() ?? string.Empty;
        validationErrors = [];

        if (string.IsNullOrWhiteSpace(normalizedFlowJson))
        {
            validationErrors.Add("flow.json content is required.");
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(normalizedFlowJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                validationErrors.Add("flow.json root must be a JSON object.");
                return false;
            }

            ValidateFlowJsonShape(doc.RootElement, validationErrors);
            ValidateFlowJsonEncoding(doc.RootElement, validationErrors);
            if (validationErrors.Count > 0)
            {
                return false;
            }

            normalizedFlowJson = JsonSerializer.Serialize(doc.RootElement);
            return true;
        }
        catch (JsonException)
        {
            validationErrors.Add("flow.json must be valid JSON.");
            return false;
        }
    }

    private static void ValidateFlowJsonShape(JsonElement root, List<string> errors)
    {
        var version = ReadJsonStringIgnoreCase(root, "version");
        if (string.IsNullOrWhiteSpace(version))
        {
            errors.Add("Missing required property 'version'.");
        }

        if (!TryGetPropertyIgnoreCase(root, "screens", out var screens) || screens.ValueKind != JsonValueKind.Array || screens.GetArrayLength() == 0)
        {
            errors.Add("Missing required property 'screens' with at least one screen.");
            return;
        }

        var foundFormComponent = false;
        var screenIndex = 0;
        foreach (var screen in screens.EnumerateArray())
        {
            screenIndex++;
            if (screen.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Screen #{screenIndex} must be a JSON object.");
                continue;
            }

            var screenId = ReadJsonStringIgnoreCase(screen, "id");
            var screenLabel = string.IsNullOrWhiteSpace(screenId) ? $"#{screenIndex}" : screenId.Trim();

            var formNodes = new List<JsonElement>();
            CollectFormNodes(screen, formNodes);
            if (formNodes.Count == 0)
            {
                continue;
            }

            foundFormComponent = true;
            for (var formIndex = 0; formIndex < formNodes.Count; formIndex++)
            {
                ValidateFormNode(formNodes[formIndex], screenLabel, formIndex + 1, errors);
            }
        }

        if (!foundFormComponent)
        {
            errors.Add("flow.json must contain at least one Form component.");
        }
    }

    private static void CollectFormNodes(JsonElement node, List<JsonElement> forms)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var type = ReadJsonStringIgnoreCase(node, "type");
                if (string.Equals(type, "Form", StringComparison.OrdinalIgnoreCase))
                {
                    forms.Add(node);
                }

                foreach (var property in node.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        CollectFormNodes(property.Value, forms);
                    }
                }

                break;
            }
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    CollectFormNodes(item, forms);
                }

                break;
        }
    }

    private static void ValidateFormNode(JsonElement formNode, string screenLabel, int formOrdinal, List<string> errors)
    {
        var formName = ReadJsonStringIgnoreCase(formNode, "name");
        var formLabel = string.IsNullOrWhiteSpace(formName)
            ? $"screen '{screenLabel}' form #{formOrdinal}"
            : $"form '{formName.Trim()}'";

        if (!TryGetPropertyIgnoreCase(formNode, "children", out var children) || children.ValueKind != JsonValueKind.Array || children.GetArrayLength() == 0)
        {
            errors.Add($"{formLabel} must include non-empty 'children'.");
            return;
        }

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        JsonElement? footerNode = null;

        foreach (var child in children.EnumerateArray())
        {
            if (child.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var childType = (ReadJsonStringIgnoreCase(child, "type") ?? string.Empty).Trim();
            if (string.Equals(childType, "Footer", StringComparison.OrdinalIgnoreCase))
            {
                footerNode ??= child;
                continue;
            }

            if (!IsFormFieldNode(child, childType))
            {
                continue;
            }

            var fieldName = ReadJsonStringIgnoreCase(child, "name");
            if (!string.IsNullOrWhiteSpace(fieldName))
            {
                fieldNames.Add(fieldName.Trim());
            }
        }

        if (fieldNames.Count == 0)
        {
            errors.Add($"{formLabel} must include at least one named input field.");
        }

        if (!footerNode.HasValue)
        {
            errors.Add($"{formLabel} must include a Footer component.");
            return;
        }

        ValidateFooterPayload(footerNode.Value, formLabel, fieldNames, errors);
    }

    private static bool IsFormFieldNode(JsonElement node, string childType)
    {
        var fieldName = ReadJsonStringIgnoreCase(node, "name");
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        var normalizedType = childType.Trim();
        if (string.IsNullOrWhiteSpace(normalizedType))
        {
            return true;
        }

        return !NonFieldFormComponentTypes.Contains(normalizedType);
    }

    private static void ValidateFooterPayload(
        JsonElement footerNode,
        string formLabel,
        HashSet<string> fieldNames,
        List<string> errors)
    {
        if (!TryGetPropertyIgnoreCase(footerNode, "on-click-action", out var actionNode)
            && !TryGetPropertyIgnoreCase(footerNode, "onClickAction", out actionNode))
        {
            errors.Add($"{formLabel} Footer must include 'on-click-action'.");
            return;
        }

        if (actionNode.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{formLabel} Footer 'on-click-action' must be a JSON object.");
            return;
        }

        var actionName = ReadJsonStringIgnoreCase(actionNode, "name");
        if (!string.Equals(actionName, "complete", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{formLabel} Footer action must be 'complete'.");
        }

        if (!TryGetPropertyIgnoreCase(actionNode, "payload", out var payloadNode) || payloadNode.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{formLabel} Footer complete action must include a JSON object payload.");
            return;
        }

        if (!payloadNode.EnumerateObject().Any())
        {
            errors.Add($"{formLabel} Footer payload cannot be empty.");
            return;
        }

        var formReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectFormReferences(payloadNode, formReferences);
        if (formReferences.Count == 0)
        {
            errors.Add($"{formLabel} Footer payload must contain references like ${{form.field_name}}.");
            return;
        }

        foreach (var fieldName in fieldNames)
        {
            if (!formReferences.Contains(fieldName))
            {
                errors.Add($"{formLabel} Footer payload is missing field reference '{fieldName}'.");
            }
        }
    }

    private static void CollectFormReferences(JsonElement node, HashSet<string> references)
    {
        switch (node.ValueKind)
        {
            case JsonValueKind.String:
            {
                var raw = node.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return;
                }

                var matches = FormFieldReferenceRegex.Matches(raw);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count < 2)
                    {
                        continue;
                    }

                    var fieldName = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(fieldName))
                    {
                        references.Add(fieldName);
                    }
                }

                return;
            }
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                {
                    CollectFormReferences(property.Value, references);
                }

                return;
            case JsonValueKind.Array:
                foreach (var item in node.EnumerateArray())
                {
                    CollectFormReferences(item, references);
                }

                return;
        }
    }

    private static void ValidateFlowJsonEncoding(JsonElement node, List<string> errors)
    {
        var suspiciousPaths = new List<string>();
        CollectMojibakePaths(node, "$", suspiciousPaths, maxCount: 5);
        foreach (var path in suspiciousPaths)
        {
            errors.Add($"Potential text encoding issue at '{path}'. Save flow.json as UTF-8 and re-upload.");
        }
    }

    private static void CollectMojibakePaths(JsonElement node, string path, List<string> result, int maxCount)
    {
        if (result.Count >= maxCount)
        {
            return;
        }

        switch (node.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in node.EnumerateObject())
                {
                    if (result.Count >= maxCount)
                    {
                        return;
                    }

                    var propertyPath = $"{path}.{property.Name}";
                    if (property.Value.ValueKind == JsonValueKind.String
                        && UserFacingFlowStringKeys.Contains(property.Name)
                        && LooksLikeMojibake(property.Value.GetString()))
                    {
                        result.Add(propertyPath);
                        continue;
                    }

                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    {
                        CollectMojibakePaths(property.Value, propertyPath, result, maxCount);
                    }
                }

                break;
            case JsonValueKind.Array:
            {
                var index = 0;
                foreach (var item in node.EnumerateArray())
                {
                    if (result.Count >= maxCount)
                    {
                        return;
                    }

                    CollectMojibakePaths(item, $"{path}[{index}]", result, maxCount);
                    index++;
                }

                break;
            }
        }
    }

    private static bool LooksLikeMojibake(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (value.IndexOf('\uFFFD') >= 0)
        {
            return true;
        }

        if (ContainsArabicCharacters(value))
        {
            return false;
        }

        var suspiciousPairCount = 0;
        for (var index = 0; index < value.Length - 1; index++)
        {
            var current = value[index];
            var next = value[index + 1];
            if ((current == 'Ø' || current == 'Ù') && next >= '\u0080' && next <= '\u00BF')
            {
                suspiciousPairCount++;
            }
        }

        if (suspiciousPairCount >= 2)
        {
            return true;
        }

        if ((value.Contains('Ã') || value.Contains('Â')) && value.Any(ch => ch >= '\u0080' && ch <= '\u00BF'))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsArabicCharacters(string value)
    {
        foreach (var ch in value)
        {
            if ((ch >= '\u0600' && ch <= '\u06FF')
                || (ch >= '\u0750' && ch <= '\u077F')
                || (ch >= '\u08A0' && ch <= '\u08FF'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement source, string propertyName, out JsonElement value)
    {
        if (source.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        foreach (var property in source.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }

    private static string? ReadJsonStringIgnoreCase(JsonElement source, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(source, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Object => value.GetRawText(),
            JsonValueKind.Array => value.GetRawText(),
            _ => null
        };
    }

    private static string NormalizeJsonText(string raw)
    {
        var trimmed = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "{}";
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            });
        }
        catch (JsonException)
        {
            return trimmed;
        }
    }

    private async Task<TenantWhatsAppConfig> GetTenantConfigAsync(CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.GetRequiredContext();
        return await _tenantWhatsAppConfigService.GetRequiredConfigAsync(context.CompanyId, cancellationToken);
    }

    private static IEnumerable<string> NormalizeFlowCategories(IEnumerable<string>? categories)
    {
        var normalized = (categories ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        return normalized.Count == 0 ? ["OTHER"] : normalized;
    }

    private async Task<ApiResponse<GenericGraphResponse>> SendGraphMultipartAsync(
        string path,
        MultipartFormDataContent content,
        CancellationToken cancellationToken)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        var client = _httpClientFactory.CreateClient("meta-graph");

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        GenericGraphResponse parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GenericGraphResponse>(responseContent, JsonOpts) ?? new GenericGraphResponse();
        }
        catch
        {
            parsed = new GenericGraphResponse();
        }

        parsed.RawContent = responseContent;

        if (!response.IsSuccessStatusCode)
        {
            var details = responseContent.Length > 3000 ? responseContent[..3000] : responseContent;
            return ApiResponse<GenericGraphResponse>.Fail(
                "Meta Graph API request failed.",
                response.StatusCode,
                details: details);
        }

        return ApiResponse<GenericGraphResponse>.Ok(parsed);
    }
}
