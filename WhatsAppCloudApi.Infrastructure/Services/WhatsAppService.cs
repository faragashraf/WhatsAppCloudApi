using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;
using WhatsAppCloudApi.Shared.Utilities;

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
    private readonly ICustomerConversationResolver _customerConversationResolver;
    private readonly ApplicationDbContext _dbContext;

    public WhatsAppService(
        ApplicationDbContext dbContext,
        ITenantContextAccessor tenantContextAccessor,
        ITenantWhatsAppConfigService tenantWhatsAppConfigService,
        ISubscriptionValidationService subscriptionValidationService,
        IMessageDispatchService messageDispatchService,
        IWhatsAppGraphClient graphClient,
        ICustomerConversationResolver customerConversationResolver)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
        _tenantWhatsAppConfigService = tenantWhatsAppConfigService;
        _subscriptionValidationService = subscriptionValidationService;
        _messageDispatchService = messageDispatchService;
        _graphClient = graphClient;
        _customerConversationResolver = customerConversationResolver;
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendTextMessageAsync(SendTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRecipient = PhoneNumberNormalizer.Normalize(request.To);
        if (normalizedRecipient is null)
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
            to = normalizedRecipient,
            type = "text",
            text = new { preview_url = request.PreviewUrl, body = request.Body }
        };

        var payloadBody = JsonSerializer.Serialize(payload, JsonOptions);
        var queued = await QueueOutboundMessageAsync(
            context.CompanyId,
            config,
            normalizedRecipient,
            "TEXT",
            payloadBody,
            "text",
            request.Body,
            "DIRECT",
            cancellationToken);

        return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse
        {
            Id = queued.Message.MessageId.ToString(),
            MessageId = queued.Message.MessageId,
            ContactId = queued.Message.ContactId,
            ConversationId = queued.Message.ConversationId,
            Success = true
        }, "Message queued");
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRecipient = PhoneNumberNormalizer.Normalize(request.To);
        if (normalizedRecipient is null)
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
            to = normalizedRecipient,
            type = "template",
            template = new
            {
                name = request.TemplateName,
                language = new { code = request.LanguageCode },
                components = request.Components
            }
        };

        var payloadBody = JsonSerializer.Serialize(payload, JsonOptions);
        var queued = await QueueOutboundMessageAsync(
            context.CompanyId,
            config,
            normalizedRecipient,
            "TEMPLATE",
            payloadBody,
            "template",
            $"Template: {request.TemplateName}",
            "DIRECT",
            cancellationToken);

        return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse
        {
            Id = queued.Message.MessageId.ToString(),
            MessageId = queued.Message.MessageId,
            ContactId = queued.Message.ContactId,
            ConversationId = queued.Message.ConversationId,
            Success = true
        }, "Message queued");
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendMediaMessageAsync(SendMediaMessageRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRecipient = PhoneNumberNormalizer.Normalize(request.To);
        if (normalizedRecipient is null)
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
            ["to"] = normalizedRecipient,
            ["type"] = request.MediaType.ToLowerInvariant(),
            [request.MediaType.ToLowerInvariant()] = mediaPayload
        };

        var payloadBody = JsonSerializer.Serialize(payload, JsonOptions);
        var queued = await QueueOutboundMessageAsync(
            context.CompanyId,
            config,
            normalizedRecipient,
            request.MediaType.ToUpperInvariant(),
            payloadBody,
            request.MediaType.ToLowerInvariant(),
            request.Caption ?? request.FileName ?? $"[{request.MediaType}]",
            "DIRECT",
            cancellationToken,
            mediaUrl: request.Link ?? request.MediaId,
            fileName: request.FileName);

        return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse
        {
            Id = queued.Message.MessageId.ToString(),
            MessageId = queued.Message.MessageId,
            ContactId = queued.Message.ContactId,
            ConversationId = queued.Message.ConversationId,
            Success = true
        }, "Message queued");
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendDirectMediaFileMessageAsync(SendDirectMediaFileMessageRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedRecipient = PhoneNumberNormalizer.Normalize(request.To);
        if (normalizedRecipient is null)
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid recipient phone number.", HttpStatusCode.BadRequest);
        }

        if (request.FileData is null || request.FileData.Length == 0)
        {
            return ApiResponse<GenericGraphResponse>.Fail("File is required.", HttpStatusCode.BadRequest);
        }

        var inferredMediaType = NormalizeDirectMediaType(request.MediaType, request.ContentType, request.FileName);
        if (!AllowedMediaTypes.Contains(inferredMediaType))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Unsupported media type.", HttpStatusCode.BadRequest);
        }

        var context = _tenantContextAccessor.GetRequiredContext();
        var config = await ResolveConfigAsync(context.CompanyId, request.PhoneNumberId, cancellationToken);

        var resolvedFileName = string.IsNullOrWhiteSpace(request.FileName)
            ? $"upload-{DateTime.UtcNow:yyyyMMddHHmmss}"
            : request.FileName!;

        var upload = await UploadMediaBytesAsync(
            resolvedFileName,
            request.ContentType,
            request.FileData,
            config,
            cancellationToken);
        if (!upload.Success)
        {
            return upload;
        }

        var mediaId = ExtractGraphId(upload.Data);
        if (string.IsNullOrWhiteSpace(mediaId))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Meta upload did not return a media ID.", HttpStatusCode.BadGateway);
        }

        var sendRequest = new SendMediaMessageRequest
        {
            To = normalizedRecipient,
            MediaType = inferredMediaType,
            MediaId = mediaId,
            Caption = request.Caption,
            FileName = resolvedFileName,
            PhoneNumberId = request.PhoneNumberId
        };

        return await SendMediaMessageAsync(sendRequest, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> UploadMediaAsync(UploadMediaRequest request, CancellationToken cancellationToken = default)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.Base64Data);
        }
        catch (FormatException)
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid Base64 payload.", HttpStatusCode.BadRequest);
        }

        return await UploadMediaFileAsync(new UploadMediaFileRequest
        {
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileData = bytes
        }, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> UploadMediaFileAsync(UploadMediaFileRequest request, CancellationToken cancellationToken = default)
        => await UploadMediaBytesAsync(request.FileName, request.ContentType, request.FileData, null, cancellationToken);

    private async Task<ApiResponse<GenericGraphResponse>> UploadMediaBytesAsync(
        string? fileName,
        string? contentType,
        byte[]? fileData,
        TenantWhatsAppConfig? config,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return ApiResponse<GenericGraphResponse>.Fail("File name is required.", HttpStatusCode.BadRequest);
        }

        var normalizedContentType = NormalizeUploadContentType(contentType);
        if (!AllowedUploadContentTypes.Contains(normalizedContentType))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Unsupported content type.", HttpStatusCode.BadRequest);
        }

        var bytes = fileData ?? [];
        if (bytes.Length == 0 || bytes.Length > MaxUploadBytes)
        {
            return ApiResponse<GenericGraphResponse>.Fail($"File size must be between 1 byte and {MaxUploadBytes / (1024 * 1024)}MB.", HttpStatusCode.BadRequest);
        }

        config ??= await GetTenantConfigAsync(cancellationToken);
        var safeFileName = SanitizeFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return ApiResponse<GenericGraphResponse>.Fail("Invalid file name.", HttpStatusCode.BadRequest);
        }

        using var formData = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(normalizedContentType);

        formData.Add(new StringContent("whatsapp"), "messaging_product");
        formData.Add(fileContent, "file", safeFileName);

        return await _graphClient.SendAsync(config, HttpMethod.Post, $"{config.PhoneNumberId}/media", formData, cancellationToken);
    }

    private static string NormalizeUploadContentType(string? contentType)
    {
        var trimmed = (contentType ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? "application/octet-stream" : trimmed;
    }

    private static string NormalizeDirectMediaType(string? mediaType, string? contentType, string? fileName)
    {
        var normalizedMediaType = (mediaType ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedMediaType))
        {
            return normalizedMediaType;
        }

        var normalizedContentType = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedContentType.StartsWith("image/"))
        {
            if (normalizedContentType is "image/webp" && !string.IsNullOrWhiteSpace(fileName) && fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
            {
                return "sticker";
            }

            return "image";
        }

        if (normalizedContentType.StartsWith("video/"))
        {
            return "video";
        }

        if (normalizedContentType.StartsWith("audio/"))
        {
            return "audio";
        }

        if (!string.IsNullOrWhiteSpace(fileName) && fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            return "sticker";
        }

        return "document";
    }

    private static string? ExtractGraphId(GenericGraphResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(response.Id))
        {
            return response.Id;
        }

        if (response.AdditionalData is null)
        {
            return null;
        }

        if (response.AdditionalData.TryGetValue("id", out var idNode))
        {
            return idNode.ValueKind == JsonValueKind.String ? idNode.GetString() : idNode.ToString();
        }

        return null;
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

        var config = await ResolveGraphConfigAsync(normalizedPath, cancellationToken);
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
                var metadata = ExtractMessageMetadata(body);
                if (metadata.ToNumber is null)
                {
                    return ApiResponse<GenericGraphResponse>.Fail("The request body does not contain a valid recipient number.", HttpStatusCode.BadRequest);
                }

                var queued = await QueueOutboundMessageAsync(
                    context.CompanyId,
                    config,
                    metadata.ToNumber,
                    metadata.MessageType,
                    body,
                    metadata.ConversationMessageType,
                    metadata.Preview,
                    "GRAPH",
                    cancellationToken,
                    mediaUrl: metadata.MediaUrl,
                    fileName: metadata.FileName);

                return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse
                {
                    Id = queued.Message.MessageId.ToString(),
                    MessageId = queued.Message.MessageId,
                    ContactId = queued.Message.ContactId,
                    ConversationId = queued.Message.ConversationId,
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

    private static (string? ToNumber, string MessageType, string ConversationMessageType, string Preview, string? MediaUrl, string? FileName) ExtractMessageMetadata(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var to = root.TryGetProperty("to", out var toNode) ? PhoneNumberNormalizer.Normalize(toNode.GetString()) : null;
            var type = root.TryGetProperty("type", out var typeNode) ? typeNode.GetString() : null;
            var normalizedType = string.IsNullOrWhiteSpace(type) ? "UNKNOWN" : type.Trim().ToUpperInvariant();
            var conversationType = normalizedType.ToLowerInvariant();
            var preview = $"[{conversationType}]";
            string? mediaUrl = null;
            string? fileName = null;

            if (normalizedType == "TEXT" && root.TryGetProperty("text", out var textNode))
            {
                preview = textNode.TryGetProperty("body", out var bodyNode)
                    ? bodyNode.GetString() ?? preview
                    : preview;
            }
            else if (normalizedType == "TEMPLATE" && root.TryGetProperty("template", out var templateNode))
            {
                preview = templateNode.TryGetProperty("name", out var nameNode)
                    ? $"Template: {nameNode.GetString()}"
                    : "Template";
            }
            else if (root.TryGetProperty(conversationType, out var mediaNode))
            {
                preview = mediaNode.TryGetProperty("caption", out var captionNode) && !string.IsNullOrWhiteSpace(captionNode.GetString())
                    ? captionNode.GetString()!
                    : preview;
                mediaUrl = mediaNode.TryGetProperty("link", out var linkNode)
                    ? linkNode.GetString()
                    : mediaNode.TryGetProperty("id", out var idNode)
                        ? idNode.GetString()
                        : null;
                fileName = mediaNode.TryGetProperty("filename", out var fileNameNode)
                    ? fileNameNode.GetString()
                    : null;
            }

            return (to, normalizedType, conversationType, preview, mediaUrl, fileName);
        }
        catch
        {
            return (null, "UNKNOWN", "unknown", "[unknown]", null, null);
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

    private async Task<QueuedLinkedMessageResult> QueueOutboundMessageAsync(
        int companyId,
        TenantWhatsAppConfig config,
        string normalizedRecipient,
        string messageType,
        string payloadBody,
        string conversationMessageType,
        string preview,
        string source,
        CancellationToken cancellationToken,
        string? mediaUrl = null,
        string? fileName = null)
    {
        var resolved = await _customerConversationResolver.ResolveAsync(
            companyId,
            normalizedRecipient,
            config.WhatsAppPhoneNumberId,
            source: source,
            cancellationToken: cancellationToken);

        var now = DateTime.UtcNow;
        var queued = await _messageDispatchService.QueueLinkedMessageAsync(new QueueLinkedMessageRequest
        {
            CompanyId = companyId,
            WhatsAppPhoneNumberId = config.WhatsAppPhoneNumberId,
            ContactId = resolved.Contact.ContactId,
            ConversationId = resolved.Conversation.ConversationId,
            ToNumber = normalizedRecipient,
            MessageType = messageType,
            MessageBody = payloadBody,
            Source = source,
            ConversationMessageType = conversationMessageType,
            ConversationContent = preview,
            MediaUrl = mediaUrl,
            FileName = fileName,
            ConversationMessageStatus = "sending",
            CreatedAtUtc = now,
            Payload = new MessageQueuePayload
            {
                Method = HttpMethod.Post.Method,
                Path = $"{config.PhoneNumberId}/messages",
                Body = payloadBody
            }
        }, cancellationToken);

        resolved.Contact.LastSeenAtUtc = now;
        resolved.Contact.LastOutboundMessageAtUtc = now;
        resolved.Contact.UpdatedAtUtc = now;

        resolved.Conversation.LastMessageContent = preview.Length > 1000 ? preview[..1000] : preview;
        resolved.Conversation.LastMessageType = conversationMessageType;
        resolved.Conversation.LastMessageAtUtc = now;
        resolved.Conversation.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return queued;
    }

    private async Task<TenantWhatsAppConfig> ResolveGraphConfigAsync(string path, CancellationToken cancellationToken)
    {
        var context = _tenantContextAccessor.GetRequiredContext();
        var firstSegment = path.Split('/', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(firstSegment))
        {
            var config = await _tenantWhatsAppConfigService.GetConfigByPhoneNumberIdAsync(firstSegment, cancellationToken);
            if (config is not null && config.CompanyId == context.CompanyId)
            {
                return config;
            }
        }

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
