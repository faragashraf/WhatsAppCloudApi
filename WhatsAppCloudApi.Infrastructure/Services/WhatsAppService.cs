using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class WhatsAppService : IWhatsAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image", "video", "audio", "document", "sticker"
    };
    private static readonly HashSet<string> AllowedUploadContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Common images
        "image/jpeg", "image/jpg", "image/png", "image/webp",
        // Common videos
        "video/mp4", "video/3gpp",
        // Common audios
        "audio/mpeg", "audio/mp3", "audio/ogg", "audio/mp4", "audio/aac", "audio/amr",
        // Documents
        "application/pdf", "text/plain", "text/csv",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/zip", "application/x-zip-compressed", "application/octet-stream"
    };
    private const int MaxUploadBytes = 10 * 1024 * 1024; // 10 MB

    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ITenantWhatsAppConfigService _tenantWhatsAppConfigService;
    private readonly ISubscriptionValidationService _subscriptionValidationService;
    private readonly IMessageDispatchService _messageDispatchService;
    private readonly IWhatsAppGraphClient _graphClient;

    public WhatsAppService(
        ITenantContextAccessor tenantContextAccessor,
        ITenantWhatsAppConfigService tenantWhatsAppConfigService,
        ISubscriptionValidationService subscriptionValidationService,
        IMessageDispatchService messageDispatchService,
        IWhatsAppGraphClient graphClient)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _tenantWhatsAppConfigService = tenantWhatsAppConfigService;
        _subscriptionValidationService = subscriptionValidationService;
        _messageDispatchService = messageDispatchService;
        _graphClient = graphClient;
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendTextMessageAsync(SendTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsValidRecipient(request.To))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid recipient phone number.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Message body is required.", HttpStatusCode.BadRequest);
        }

        var context = _tenantContextAccessor.GetRequiredContext();
        await _subscriptionValidationService.ValidateCanSendMessageAsync(context.CompanyId, cancellationToken);
        var config = await ResolveConfigAsync(context.CompanyId, request.PhoneNumberId, cancellationToken);

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "text",
            text = new { preview_url = request.PreviewUrl, body = request.Body }
        };

        var payloadBody = JsonSerializer.Serialize(payload, JsonOptions);
        var message = await _messageDispatchService.QueueMessageAsync(
            context.CompanyId,
            config.WhatsAppPhoneNumberId,
            request.To,
            "TEXT",
            payloadBody,
            new MessageQueuePayload
            {
                Method = HttpMethod.Post.Method,
                Path = $"{config.PhoneNumberId}/messages",
                Body = payloadBody
            },
            cancellationToken);

        return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse
        {
            Id = message.MessageId.ToString(),
            Success = true
        }, "Message queued");
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsValidRecipient(request.To))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid recipient phone number.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Template name is required.", HttpStatusCode.BadRequest);
        }

        var context = _tenantContextAccessor.GetRequiredContext();
        await _subscriptionValidationService.ValidateCanSendMessageAsync(context.CompanyId, cancellationToken);
        var config = await ResolveConfigAsync(context.CompanyId, request.PhoneNumberId, cancellationToken);

        var payload = new
        {
            messaging_product = "whatsapp",
            to = request.To,
            type = "template",
            template = new
            {
                name = request.TemplateName,
                language = new { code = request.LanguageCode },
                components = request.Components
            }
        };

        var payloadBody = JsonSerializer.Serialize(payload, JsonOptions);
        var message = await _messageDispatchService.QueueMessageAsync(
            context.CompanyId,
            config.WhatsAppPhoneNumberId,
            request.To,
            "TEMPLATE",
            payloadBody,
            new MessageQueuePayload
            {
                Method = HttpMethod.Post.Method,
                Path = $"{config.PhoneNumberId}/messages",
                Body = payloadBody
            },
            cancellationToken);

        return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse
        {
            Id = message.MessageId.ToString(),
            Success = true
        }, "Message queued");
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendMediaMessageAsync(SendMediaMessageRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsValidRecipient(request.To))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid recipient phone number.", HttpStatusCode.BadRequest);
        }

        if (!AllowedMediaTypes.Contains(request.MediaType))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Unsupported media type.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.MediaId) && string.IsNullOrWhiteSpace(request.Link))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Either media ID or link must be provided.", HttpStatusCode.BadRequest);
        }

        var context = _tenantContextAccessor.GetRequiredContext();
        await _subscriptionValidationService.ValidateCanSendMessageAsync(context.CompanyId, cancellationToken);
        var config = await ResolveConfigAsync(context.CompanyId, request.PhoneNumberId, cancellationToken);

        var mediaPayload = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(request.MediaId))
        {
            mediaPayload["id"] = request.MediaId;
        }

        if (!string.IsNullOrWhiteSpace(request.Link))
        {
            mediaPayload["link"] = request.Link;
        }

        if (!string.IsNullOrWhiteSpace(request.Caption))
        {
            mediaPayload["caption"] = request.Caption;
        }

        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            mediaPayload["filename"] = request.FileName;
        }

        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = request.To,
            ["type"] = request.MediaType.ToLowerInvariant(),
            [request.MediaType.ToLowerInvariant()] = mediaPayload
        };

        var payloadBody = JsonSerializer.Serialize(payload, JsonOptions);
        var message = await _messageDispatchService.QueueMessageAsync(
            context.CompanyId,
            config.WhatsAppPhoneNumberId,
            request.To,
            request.MediaType.ToUpperInvariant(),
            payloadBody,
            new MessageQueuePayload
            {
                Method = HttpMethod.Post.Method,
                Path = $"{config.PhoneNumberId}/messages",
                Body = payloadBody
            },
            cancellationToken);

        return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse
        {
            Id = message.MessageId.ToString(),
            Success = true
        }, "Message queued");
    }

    public async Task<ApiResponse<GenericGraphResponse>> UploadMediaAsync(UploadMediaRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return ApiResponse<GenericGraphResponse>.Fail("File name is required.", HttpStatusCode.BadRequest);
        }

        if (!AllowedUploadContentTypes.Contains(request.ContentType))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Unsupported content type.", HttpStatusCode.BadRequest);
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.Base64Data);
        }
        catch (FormatException)
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid Base64 payload.", HttpStatusCode.BadRequest);
        }

        if (bytes.Length == 0 || bytes.Length > MaxUploadBytes)
        {
            return ApiResponse<GenericGraphResponse>.Fail($"File size must be between 1 byte and {MaxUploadBytes / (1024 * 1024)}MB.", HttpStatusCode.BadRequest);
        }

        var config = await GetTenantConfigAsync(cancellationToken);
        var safeFileName = SanitizeFileName(request.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid file name.", HttpStatusCode.BadRequest);
        }

        using var formData = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);

        formData.Add(new StringContent("whatsapp"), "messaging_product");
        formData.Add(fileContent, "file", safeFileName);

        return await _graphClient.SendAsync(config, HttpMethod.Post, $"{config.PhoneNumberId}/media", formData, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> GetMediaUrlAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await _graphClient.SendAsync(config, HttpMethod.Get, mediaId, null, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> DeleteMediaAsync(string mediaId, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await _graphClient.SendAsync(config, HttpMethod.Delete, mediaId, null, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> MarkMessageAsReadAsync(MarkAsReadRequest request, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);

        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = request.MessageId
        };

        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        return await _graphClient.SendAsync(config, HttpMethod.Put, $"{config.PhoneNumberId}/messages", content, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> GetPhoneNumberDetailsAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await _graphClient.SendAsync(
            config,
            HttpMethod.Get,
            $"{config.PhoneNumberId}?fields=verified_name,display_phone_number,quality_rating,code_verification_status",
            null,
            cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> RegisterPhoneNumberAsync(RegisterPhoneNumberRequest request, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);

        var payload = new
        {
            messaging_product = "whatsapp",
            pin = request.Pin
        };

        return await PostGraphAsync(config, $"{config.PhoneNumberId}/register", payload, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> DeregisterPhoneNumberAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await _graphClient.SendAsync(config, HttpMethod.Post, $"{config.PhoneNumberId}/deregister", null, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> RequestVerificationCodeAsync(RequestVerificationCodeRequest request, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);

        var payload = new
        {
            code_method = request.CodeMethod,
            locale = request.Locale
        };

        return await PostGraphAsync(config, $"{config.PhoneNumberId}/request_code", payload, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> VerifyCodeAsync(VerifyCodeRequest request, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);

        var payload = new
        {
            code = request.Code
        };

        return await PostGraphAsync(config, $"{config.PhoneNumberId}/verify_code", payload, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> GetMessageTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await _graphClient.SendAsync(config, HttpMethod.Get, $"{config.BusinessAccountId}/message_templates", null, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> CreateMessageTemplateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await PostGraphAsync(config, $"{config.BusinessAccountId}/message_templates", request, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> DeleteMessageTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await _graphClient.SendAsync(
            config,
            HttpMethod.Delete,
            $"{config.BusinessAccountId}/message_templates?name={Uri.EscapeDataString(templateId)}",
            null,
            cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> GetBusinessProfileAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);
        return await _graphClient.SendAsync(
            config,
            HttpMethod.Get,
            $"{config.PhoneNumberId}/whatsapp_business_profile?fields=about,address,description,email,profile_picture_url,websites,vertical",
            null,
            cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> UpdateBusinessProfileAsync(UpdateBusinessProfileRequest request, CancellationToken cancellationToken = default)
    {
        var config = await GetTenantConfigAsync(cancellationToken);

        var payload = new
        {
            messaging_product = "whatsapp",
            about = request.About,
            address = request.Address,
            description = request.Description,
            email = request.Email,
            profile_picture_handle = request.ProfilePictureHandle,
            websites = request.Websites,
            vertical = request.Vertical
        };

        return await PostGraphAsync(config, $"{config.PhoneNumberId}/whatsapp_business_profile", payload, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendGraphRequestAsync(GraphApiRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Method) || string.IsNullOrWhiteSpace(request.Path))
        {
            return ApiResponse<GenericGraphResponse>.Fail(
                "Method and path are required.",
                HttpStatusCode.BadRequest);
        }

        if (!IsAllowedGraphMethod(request.Method))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Unsupported HTTP method.", HttpStatusCode.BadRequest);
        }

        if (!TryNormalizeGraphPath(request.Path, out var normalizedPath))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid graph path.", HttpStatusCode.BadRequest);
        }

        var config = await GetTenantConfigAsync(cancellationToken);
        var method = new HttpMethod(request.Method.ToUpperInvariant());
        var path = BuildPath(normalizedPath, request.Query);
        if (path.Length > 2048)
        {
            return ApiResponse<GenericGraphResponse>.Fail("Graph path is too long.", HttpStatusCode.BadRequest);
        }

        var context = _tenantContextAccessor.GetRequiredContext();

        HttpContent? content = null;
        if (request.Body is not null && method != HttpMethod.Get && method != HttpMethod.Head)
        {
            var body = JsonSerializer.Serialize(request.Body, JsonOptions);

            if (method == HttpMethod.Post && IsMessagesPath(path))
            {
                await _subscriptionValidationService.ValidateCanSendMessageAsync(context.CompanyId, cancellationToken);
                var (toNumber, messageType) = ExtractMessageMetadata(body);
                var message = await _messageDispatchService.QueueMessageAsync(
                    context.CompanyId,
                    config.WhatsAppPhoneNumberId,
                    toNumber,
                    messageType,
                    body,
                    new MessageQueuePayload
                    {
                        Method = method.Method,
                        Path = path,
                        Body = body
                    },
                    cancellationToken);

                return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse
                {
                    Id = message.MessageId.ToString(),
                    Success = true
                }, "Message queued");
            }

            content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return await _graphClient.SendAsync(config, method, path, content, cancellationToken);
    }

    private static string BuildPath(string path, Dictionary<string, string?> query)
    {
        if (query.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&", query
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value ?? string.Empty)}"));

        if (string.IsNullOrEmpty(queryString))
        {
            return path;
        }

        return path.Contains('?', StringComparison.Ordinal) ? $"{path}&{queryString}" : $"{path}?{queryString}";
    }

    private Task<ApiResponse<GenericGraphResponse>> PostGraphAsync(TenantWhatsAppConfig config, string path, object payload, CancellationToken cancellationToken)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        return _graphClient.SendAsync(config, HttpMethod.Post, path, content, cancellationToken);
    }

    private static (string ToNumber, string MessageType) ExtractMessageMetadata(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var to = root.TryGetProperty("to", out var toNode) ? toNode.GetString() : null;
            var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
            return (to ?? "UNKNOWN", string.IsNullOrWhiteSpace(type) ? "UNKNOWN" : type.ToUpperInvariant());
        }
        catch
        {
            return ("UNKNOWN", "UNKNOWN");
        }
    }

    private static bool IsAllowedGraphMethod(string method)
    {
        return method.Equals(HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase)
            || method.Equals(HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase)
            || method.Equals(HttpMethod.Put.Method, StringComparison.OrdinalIgnoreCase)
            || method.Equals(HttpMethod.Delete.Method, StringComparison.OrdinalIgnoreCase)
            || method.Equals(HttpMethod.Patch.Method, StringComparison.OrdinalIgnoreCase)
            || method.Equals(HttpMethod.Head.Method, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeGraphPath(string path, out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            return false;
        }

        trimmed = trimmed.TrimStart('/');
        if (trimmed.Contains("..", StringComparison.Ordinal) || trimmed.Contains('\\'))
        {
            return false;
        }

        normalizedPath = trimmed;
        return true;
    }

    private static bool IsMessagesPath(string path)
    {
        var pathOnly = path.Split('?', 2)[0];
        return pathOnly.EndsWith("/messages", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsValidRecipient(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is < 6 or > 30)
        {
            return false;
        }

        return trimmed.All(ch => char.IsDigit(ch) || ch == '+');
    }

    private static string SanitizeFileName(string fileName)
    {
        var candidate = Path.GetFileName(fileName)?.Trim() ?? string.Empty;
        if (candidate.Length == 0 || candidate.Length > 255)
        {
            return string.Empty;
        }

        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            candidate = candidate.Replace(invalidChar.ToString(), string.Empty, StringComparison.Ordinal);
        }

        return candidate;
    }

    private async Task<TenantWhatsAppConfig> GetTenantConfigAsync(CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.GetRequiredContext();
        return await _tenantWhatsAppConfigService.GetRequiredConfigAsync(context.CompanyId, cancellationToken);
    }

    /// <summary>
    /// Resolves the WhatsApp config. When <paramref name="phoneNumberId"/> is supplied,
    /// the specific phone number is used instead of the company default.
    /// Ensures the phone number belongs to the same company (security guard).
    /// </summary>
    private async Task<TenantWhatsAppConfig> ResolveConfigAsync(int companyId, string? phoneNumberId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId))
            return await _tenantWhatsAppConfigService.GetRequiredConfigAsync(companyId, cancellationToken);

        var config = await _tenantWhatsAppConfigService.GetConfigByPhoneNumberIdAsync(phoneNumberId, cancellationToken);
        if (config is null || config.CompanyId != companyId)
            throw new InvalidOperationException("The specified phone number is not available for this company.");

        return config;
    }
}
