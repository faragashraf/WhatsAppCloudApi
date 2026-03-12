using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Services;

public interface ICustomWebhookDispatcher
{
    Task<ApiResponse<CustomWebhookDispatchResultDto>> DispatchAsync(
        CustomWebhookDispatchRequest request,
        string? webhookToken,
        string correlationId,
        CancellationToken cancellationToken = default);
}

public sealed class CustomWebhookDispatcher : ICustomWebhookDispatcher
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private static readonly Regex PlaceholderPattern = new("{{\\s*([^{}]+?)\\s*}}", RegexOptions.Compiled);
    private static readonly TextInfo InvariantTextInfo = CultureInfo.InvariantCulture.TextInfo;

    private readonly ITenantWhatsAppConfigService _configService;
    private readonly ICustomerConversationResolver _resolver;
    private readonly IMessageDispatchService _messageDispatchService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IWebhookStore _webhookStore;
    private readonly ILogger<CustomWebhookDispatcher> _logger;

    private sealed class RenderResult
    {
        public string Message { get; init; } = string.Empty;
        public List<string> MissingVariables { get; init; } = [];
    }

    private enum OutsideWindowAction
    {
        Block = 0,
        AllowText = 1,
        Template = 2
    }

    private sealed class OutsideWindowTemplateRenderResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string TemplateName { get; init; } = string.Empty;
        public string LanguageCode { get; init; } = "en_US";
        public List<Dictionary<string, object?>> Components { get; init; } = [];
        public List<string> MissingVariables { get; init; } = [];
    }

    private sealed class AttachmentBuildResult
    {
        public bool Success { get; init; }
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.BadRequest;
        public string? ErrorMessage { get; init; }
        public string MessageType { get; init; } = string.Empty;
        public string ConversationMessageType { get; init; } = string.Empty;
        public string Preview { get; init; } = string.Empty;
        public string PayloadBody { get; init; } = string.Empty;
        public string? MediaReference { get; init; }
        public string? MediaMimeType { get; init; }
        public string? FileName { get; init; }
        public List<string> MissingVariables { get; init; } = [];
    }

    public CustomWebhookDispatcher(
        ITenantWhatsAppConfigService configService,
        ICustomerConversationResolver resolver,
        IMessageDispatchService messageDispatchService,
        ApplicationDbContext dbContext,
        IWebhookStore webhookStore,
        ILogger<CustomWebhookDispatcher> logger)
    {
        _configService = configService;
        _resolver = resolver;
        _messageDispatchService = messageDispatchService;
        _dbContext = dbContext;
        _webhookStore = webhookStore;
        _logger = logger;
    }

    public async Task<ApiResponse<CustomWebhookDispatchResultDto>> DispatchAsync(
        CustomWebhookDispatchRequest request,
        string? webhookToken,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                "Request body is required.",
                HttpStatusCode.BadRequest,
                correlationId);
        }

        if (string.IsNullOrWhiteSpace(webhookToken))
        {
            return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                "Webhook token is required.",
                HttpStatusCode.Unauthorized,
                correlationId);
        }

        var tenantConfig = await _configService.GetConfigByVerifyTokenAsync(webhookToken.Trim(), cancellationToken);
        if (tenantConfig is null)
        {
            return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                "Invalid webhook token.",
                HttpStatusCode.Unauthorized,
                correlationId);
        }

        var activeConfig = await ResolveTargetConfigAsync(tenantConfig, request, correlationId, cancellationToken);
        if (!activeConfig.Success || activeConfig.Config is null)
        {
            await TryLogAsync(
                tenantConfig.CompanyId,
                tenantConfig.PhoneNumberId,
                request,
                activeConfig.LogSummary ?? "Custom webhook rejected.",
                correlationId,
                cancellationToken);

            return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                activeConfig.ErrorMessage ?? "Invalid webhook request.",
                activeConfig.StatusCode ?? HttpStatusCode.BadRequest,
                correlationId);
        }

        ResolvedConversationContext resolved;
        try
        {
            resolved = await _resolver.ResolveAsync(
                tenantConfig.CompanyId,
                request.To,
                activeConfig.Config.WhatsAppPhoneNumberId,
                request.ContactName,
                source: "custom_webhook",
                cancellationToken: cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            await TryLogAsync(
                tenantConfig.CompanyId,
                activeConfig.Config.PhoneNumberId,
                request,
                $"Custom webhook rejected: {ex.Message}",
                correlationId,
                cancellationToken);

            return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                ex.Message,
                HttpStatusCode.BadRequest,
                correlationId);
        }

        var conversationWindowOpen = await IsConversationWindowOpenAsync(resolved.Conversation, cancellationToken);
        var outsideWindowAction = ResolveOutsideWindowAction(request);
        if (!conversationWindowOpen && outsideWindowAction == OutsideWindowAction.Block)
        {
            const string message = "24-hour customer support window has expired. Set outsideWindowAction to allow_text or template.";
            await TryLogAsync(
                tenantConfig.CompanyId,
                activeConfig.Config.PhoneNumberId,
                request,
                $"Custom webhook blocked: {message}",
                correlationId,
                cancellationToken);

            return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                message,
                HttpStatusCode.Forbidden,
                correlationId);
        }

        var variables = BuildVariables(request, tenantConfig.CompanyId, resolved, correlationId);
        var now = DateTime.UtcNow;
        QueueLinkedMessageRequest queueRequest;
        List<string> missingVariables;
        string dispatchMode;
        string renderedOutput;

        if (!conversationWindowOpen && outsideWindowAction == OutsideWindowAction.Template)
        {
            if (request.Attachment is not null)
            {
                const string attachmentTemplateError = "Attachments are not supported when outsideWindowAction is template.";
                await TryLogAsync(
                    tenantConfig.CompanyId,
                    activeConfig.Config.PhoneNumberId,
                    request,
                    $"Custom webhook rejected: {attachmentTemplateError}",
                    correlationId,
                    cancellationToken);

                return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                    attachmentTemplateError,
                    HttpStatusCode.BadRequest,
                    correlationId);
            }

            var templateRender = RenderOutsideWindowTemplate(
                request.OutsideWindowTemplate,
                variables,
                request.KeepUnresolvedPlaceholders);

            if (!templateRender.Success)
            {
                await TryLogAsync(
                    tenantConfig.CompanyId,
                    activeConfig.Config.PhoneNumberId,
                    request,
                    $"Custom webhook rejected: {templateRender.ErrorMessage}",
                    correlationId,
                    cancellationToken);

                return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                    templateRender.ErrorMessage ?? "Invalid outside window template payload.",
                    HttpStatusCode.BadRequest,
                    correlationId);
            }

            missingVariables = templateRender.MissingVariables;
            if (request.StrictVariables && missingVariables.Count > 0)
            {
                var missing = string.Join(", ", missingVariables);
                await TryLogAsync(
                    tenantConfig.CompanyId,
                    activeConfig.Config.PhoneNumberId,
                    request,
                    $"Custom webhook rejected: missing template variables ({missing}).",
                    correlationId,
                    cancellationToken);

                return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                    "Missing required variables.",
                    HttpStatusCode.BadRequest,
                    correlationId,
                    details: missing);
            }

            var payload = new Dictionary<string, object?>
            {
                ["messaging_product"] = "whatsapp",
                ["recipient_type"] = "individual",
                ["to"] = resolved.NormalizedPhoneNumber,
                ["type"] = "template",
                ["template"] = new Dictionary<string, object?>
                {
                    ["name"] = templateRender.TemplateName,
                    ["language"] = new { code = templateRender.LanguageCode },
                    ["components"] = templateRender.Components.Count > 0 ? templateRender.Components : null
                }
            };

            var payloadBody = JsonSerializer.Serialize(payload, JsonOpts);
            renderedOutput = $"Template: {templateRender.TemplateName}";
            dispatchMode = "template";
            queueRequest = new QueueLinkedMessageRequest
            {
                CompanyId = tenantConfig.CompanyId,
                WhatsAppPhoneNumberId = activeConfig.Config.WhatsAppPhoneNumberId,
                ContactId = resolved.Contact.ContactId,
                ConversationId = resolved.Conversation.ConversationId,
                ToNumber = resolved.NormalizedPhoneNumber,
                MessageType = "TEMPLATE",
                MessageBody = payloadBody,
                Source = "CUSTOM_WEBHOOK",
                ConversationMessageType = "template",
                ConversationContent = renderedOutput,
                ConversationMessageStatus = "sending",
                CreatedAtUtc = now,
                Payload = new MessageQueuePayload
                {
                    Method = HttpMethod.Post.Method,
                    Path = $"{activeConfig.Config.PhoneNumberId}/messages",
                    Body = payloadBody
                }
            };
        }
        else
        {
            var renderResult = RenderMessage(request.Message, variables, request.KeepUnresolvedPlaceholders);
            if (request.Attachment is not null)
            {
                if (!conversationWindowOpen)
                {
                    const string attachmentWindowError = "Attachments can only be sent within the 24-hour customer support window.";
                    await TryLogAsync(
                        tenantConfig.CompanyId,
                        activeConfig.Config.PhoneNumberId,
                        request,
                        $"Custom webhook blocked: {attachmentWindowError}",
                        correlationId,
                        cancellationToken);

                    return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                        attachmentWindowError,
                        HttpStatusCode.Forbidden,
                        correlationId);
                }

                var attachmentResult = BuildAttachmentPayload(
                    request,
                    request.Attachment,
                    resolved.NormalizedPhoneNumber,
                    variables,
                    renderResult.Message);

                if (!attachmentResult.Success)
                {
                    await TryLogAsync(
                        tenantConfig.CompanyId,
                        activeConfig.Config.PhoneNumberId,
                        request,
                        $"Custom webhook rejected: {attachmentResult.ErrorMessage}",
                        correlationId,
                        cancellationToken);

                    return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                        attachmentResult.ErrorMessage ?? "Invalid attachment payload.",
                        attachmentResult.StatusCode,
                        correlationId);
                }

                var combinedMissing = new HashSet<string>(renderResult.MissingVariables, StringComparer.OrdinalIgnoreCase);
                foreach (var missing in attachmentResult.MissingVariables)
                {
                    combinedMissing.Add(missing);
                }

                missingVariables = combinedMissing.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                if (request.StrictVariables && missingVariables.Count > 0)
                {
                    var missing = string.Join(", ", missingVariables);
                    await TryLogAsync(
                        tenantConfig.CompanyId,
                        activeConfig.Config.PhoneNumberId,
                        request,
                        $"Custom webhook rejected: missing variables ({missing}).",
                        correlationId,
                        cancellationToken);

                    return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                        "Missing required variables.",
                        HttpStatusCode.BadRequest,
                        correlationId,
                        details: missing);
                }

                renderedOutput = attachmentResult.Preview;
                dispatchMode = "attachment";
                queueRequest = new QueueLinkedMessageRequest
                {
                    CompanyId = tenantConfig.CompanyId,
                    WhatsAppPhoneNumberId = activeConfig.Config.WhatsAppPhoneNumberId,
                    ContactId = resolved.Contact.ContactId,
                    ConversationId = resolved.Conversation.ConversationId,
                    ToNumber = resolved.NormalizedPhoneNumber,
                    MessageType = attachmentResult.MessageType,
                    MessageBody = attachmentResult.PayloadBody,
                    Source = "CUSTOM_WEBHOOK",
                    ConversationMessageType = attachmentResult.ConversationMessageType,
                    ConversationContent = renderedOutput,
                    MediaUrl = attachmentResult.MediaReference,
                    MediaMimeType = attachmentResult.MediaMimeType,
                    FileName = attachmentResult.FileName,
                    ConversationMessageStatus = "sending",
                    CreatedAtUtc = now,
                    Payload = new MessageQueuePayload
                    {
                        Method = HttpMethod.Post.Method,
                        Path = $"{activeConfig.Config.PhoneNumberId}/messages",
                        Body = attachmentResult.PayloadBody
                    }
                };
            }
            else
            {
                missingVariables = renderResult.MissingVariables;
                if (request.StrictVariables && missingVariables.Count > 0)
                {
                    var missing = string.Join(", ", missingVariables);
                    await TryLogAsync(
                        tenantConfig.CompanyId,
                        activeConfig.Config.PhoneNumberId,
                        request,
                        $"Custom webhook rejected: missing variables ({missing}).",
                        correlationId,
                        cancellationToken);

                    return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                        "Missing required variables.",
                        HttpStatusCode.BadRequest,
                        correlationId,
                        details: missing);
                }

                if (string.IsNullOrWhiteSpace(renderResult.Message))
                {
                    return ApiResponse<CustomWebhookDispatchResultDto>.Fail(
                        "Rendered message is empty.",
                        HttpStatusCode.BadRequest,
                        correlationId);
                }

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = resolved.NormalizedPhoneNumber,
                    type = "text",
                    text = new { body = renderResult.Message }
                };

                var payloadBody = JsonSerializer.Serialize(payload, JsonOpts);
                renderedOutput = renderResult.Message;
                dispatchMode = conversationWindowOpen ? "text" : "allow_text";
                queueRequest = new QueueLinkedMessageRequest
                {
                    CompanyId = tenantConfig.CompanyId,
                    WhatsAppPhoneNumberId = activeConfig.Config.WhatsAppPhoneNumberId,
                    ContactId = resolved.Contact.ContactId,
                    ConversationId = resolved.Conversation.ConversationId,
                    ToNumber = resolved.NormalizedPhoneNumber,
                    MessageType = "TEXT",
                    MessageBody = payloadBody,
                    Source = "CUSTOM_WEBHOOK",
                    ConversationMessageType = "text",
                    ConversationContent = renderedOutput,
                    ConversationMessageStatus = "sending",
                    CreatedAtUtc = now,
                    Payload = new MessageQueuePayload
                    {
                        Method = HttpMethod.Post.Method,
                        Path = $"{activeConfig.Config.PhoneNumberId}/messages",
                        Body = payloadBody
                    }
                };
            }
        }

        var queued = await _messageDispatchService.QueueLinkedMessageAsync(queueRequest, cancellationToken);
        UpdateConversationState(
            resolved.Conversation,
            resolved.Contact,
            queueRequest.ConversationContent,
            queueRequest.ConversationMessageType,
            now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryLogAsync(
            tenantConfig.CompanyId,
            activeConfig.Config.PhoneNumberId,
            request,
            $"Custom webhook queued to {resolved.NormalizedPhoneNumber} using {dispatchMode}.",
            correlationId,
            cancellationToken);

        return ApiResponse<CustomWebhookDispatchResultDto>.Ok(new CustomWebhookDispatchResultDto
        {
            CompanyId = tenantConfig.CompanyId,
            WhatsAppPhoneNumberId = activeConfig.Config.WhatsAppPhoneNumberId,
            ContactId = resolved.Contact.ContactId,
            ConversationId = resolved.Conversation.ConversationId,
            MessageId = queued.Message.MessageId,
            ConversationMessageId = queued.ConversationMessage.ConversationMessageId,
            To = resolved.NormalizedPhoneNumber,
            RenderedMessage = renderedOutput,
            MissingVariables = missingVariables,
            DispatchMode = dispatchMode,
            ConversationWindowOpen = conversationWindowOpen
        }, "Message queued.", correlationId);
    }

    private async Task<(bool Success, TenantWhatsAppConfig? Config, HttpStatusCode? StatusCode, string? ErrorMessage, string? LogSummary)> ResolveTargetConfigAsync(
        TenantWhatsAppConfig tenantConfig,
        CustomWebhookDispatchRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        if (request.WhatsAppPhoneNumberId.HasValue)
        {
            var config = await _configService.GetConfigByWhatsAppPhoneNumberIdAsync(request.WhatsAppPhoneNumberId.Value, cancellationToken);
            if (config is null || config.CompanyId != tenantConfig.CompanyId)
            {
                _logger.LogWarning(
                    "Custom webhook {CorrelationId} rejected: invalid WhatsApp phone number id {WhatsAppPhoneNumberId}.",
                    correlationId,
                    request.WhatsAppPhoneNumberId.Value);

                return (false, null, HttpStatusCode.Forbidden, "The requested WhatsApp phone number is not allowed for this webhook token.", "Invalid target WhatsApp phone number id.");
            }

            return (true, config, null, null, null);
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumberId))
        {
            var config = await _configService.GetConfigByPhoneNumberIdAsync(request.PhoneNumberId.Trim(), cancellationToken);
            if (config is null || config.CompanyId != tenantConfig.CompanyId)
            {
                _logger.LogWarning(
                    "Custom webhook {CorrelationId} rejected: invalid phone_number_id {PhoneNumberId}.",
                    correlationId,
                    request.PhoneNumberId);

                return (false, null, HttpStatusCode.Forbidden, "The requested phone_number_id is not allowed for this webhook token.", "Invalid target phone_number_id.");
            }

            return (true, config, null, null, null);
        }

        return (true, tenantConfig, null, null, null);
    }

    private async Task<bool> IsConversationWindowOpenAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        var lastInboundAtUtc = conversation.LastInboundMessageAtUtc;
        if (!lastInboundAtUtc.HasValue)
        {
            lastInboundAtUtc = await _dbContext.ConversationMessages
                .AsNoTracking()
                .Where(x =>
                    x.ConversationId == conversation.ConversationId
                    && x.CompanyId == conversation.CompanyId
                    && x.Direction == "inbound")
                .OrderByDescending(x => x.TimestampUtc)
                .Select(x => (DateTime?)x.TimestampUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (!lastInboundAtUtc.HasValue)
        {
            return false;
        }

        var utc = lastInboundAtUtc.Value.Kind == DateTimeKind.Utc
            ? lastInboundAtUtc.Value
            : DateTime.SpecifyKind(lastInboundAtUtc.Value, DateTimeKind.Utc);

        return DateTime.UtcNow.Subtract(utc) < TimeSpan.FromHours(24);
    }

    private static Dictionary<string, string> BuildVariables(
        CustomWebhookDispatchRequest request,
        int companyId,
        ResolvedConversationContext resolved,
        string correlationId)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["customer_name"] = string.IsNullOrWhiteSpace(resolved.Contact.Name)
                ? (resolved.Conversation.ContactName ?? resolved.NormalizedPhoneNumber)
                : resolved.Contact.Name,
            ["contact_name"] = string.IsNullOrWhiteSpace(resolved.Contact.Name)
                ? (resolved.Conversation.ContactName ?? resolved.NormalizedPhoneNumber)
                : resolved.Contact.Name,
            ["contact_number"] = resolved.NormalizedPhoneNumber,
            ["contact_id"] = resolved.Contact.ContactId.ToString(CultureInfo.InvariantCulture),
            ["conversation_id"] = resolved.Conversation.ConversationId.ToString(CultureInfo.InvariantCulture),
            ["company_id"] = companyId.ToString(CultureInfo.InvariantCulture),
            ["now_utc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["correlation_id"] = correlationId
        };

        if (request.Data.HasValue)
        {
            FlattenJsonElement(request.Data.Value, null, variables);
            FlattenJsonElement(request.Data.Value, "payload", variables);
        }

        if (request.Variables is null)
        {
            return variables;
        }

        foreach (var entry in request.Variables)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            variables[entry.Key.Trim()] = ConvertJsonValueToString(entry.Value);
        }

        return variables;
    }

    private static void FlattenJsonElement(JsonElement element, string? prefix, IDictionary<string, string> variables)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                foreach (var property in element.EnumerateObject())
                {
                    var childPrefix = string.IsNullOrWhiteSpace(prefix)
                        ? property.Name
                        : $"{prefix}.{property.Name}";
                    FlattenJsonElement(property.Value, childPrefix, variables);
                }

                break;
            }
            case JsonValueKind.Array:
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var childPrefix = string.IsNullOrWhiteSpace(prefix)
                        ? $"[{index}]"
                        : $"{prefix}[{index}]";
                    FlattenJsonElement(item, childPrefix, variables);
                    index++;
                }

                break;
            }
            default:
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    return;
                }

                variables[prefix] = ConvertJsonValueToString(element);
                break;
            }
        }
    }

    private static string ConvertJsonValueToString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            _ => element.GetRawText()
        };
    }

    private static AttachmentBuildResult BuildAttachmentPayload(
        CustomWebhookDispatchRequest request,
        CustomWebhookAttachmentDto attachment,
        string toNumber,
        IReadOnlyDictionary<string, string> variables,
        string renderedMessage)
    {
        var messageType = NormalizeNullable(attachment.Type)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(messageType))
        {
            return new AttachmentBuildResult
            {
                Success = false,
                ErrorMessage = "attachment.type is required."
            };
        }

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image",
            "video",
            "audio",
            "document",
            "sticker"
        };
        if (!allowedTypes.Contains(messageType))
        {
            return new AttachmentBuildResult
            {
                Success = false,
                ErrorMessage = $"Unsupported attachment type '{messageType}'."
            };
        }

        var mediaId = NormalizeNullable(attachment.MediaId);
        var mediaUrl = NormalizeNullable(attachment.MediaUrl);
        if (string.IsNullOrWhiteSpace(mediaId) == string.IsNullOrWhiteSpace(mediaUrl))
        {
            return new AttachmentBuildResult
            {
                Success = false,
                ErrorMessage = "Provide exactly one attachment reference: mediaId or mediaUrl."
            };
        }

        if (!string.IsNullOrWhiteSpace(mediaUrl)
            && (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var parsedMediaUri)
                || (parsedMediaUri.Scheme != Uri.UriSchemeHttp && parsedMediaUri.Scheme != Uri.UriSchemeHttps)))
        {
            return new AttachmentBuildResult
            {
                Success = false,
                ErrorMessage = "attachment.mediaUrl must be an absolute HTTP/HTTPS URL."
            };
        }

        var captionTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image",
            "video",
            "document"
        };

        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? caption = null;
        if (captionTypes.Contains(messageType))
        {
            if (!string.IsNullOrWhiteSpace(attachment.Caption))
            {
                var captionRender = RenderMessage(attachment.Caption, variables, request.KeepUnresolvedPlaceholders);
                caption = captionRender.Message;
                foreach (var missingVariable in captionRender.MissingVariables)
                {
                    missing.Add(missingVariable);
                }
            }
            else if (!string.IsNullOrWhiteSpace(renderedMessage))
            {
                caption = renderedMessage;
            }

            if (!string.IsNullOrWhiteSpace(caption) && caption.Length > 1024)
            {
                caption = caption[..1024];
            }
        }

        var mediaObject = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(mediaId))
        {
            mediaObject["id"] = mediaId;
        }
        else
        {
            mediaObject["link"] = mediaUrl;
        }

        if (!string.IsNullOrWhiteSpace(caption) && captionTypes.Contains(messageType))
        {
            mediaObject["caption"] = caption;
        }

        var fileName = NormalizeNullable(attachment.FileName);
        if (string.Equals(messageType, "document", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(fileName))
        {
            mediaObject["filename"] = fileName;
        }

        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = toNumber,
            ["type"] = messageType,
            [messageType] = mediaObject
        };

        var preview = !string.IsNullOrWhiteSpace(caption)
            ? caption
            : !string.IsNullOrWhiteSpace(renderedMessage)
                ? renderedMessage
                : $"[{messageType}]";

        return new AttachmentBuildResult
        {
            Success = true,
            MessageType = messageType.ToUpperInvariant(),
            ConversationMessageType = messageType,
            Preview = preview,
            PayloadBody = JsonSerializer.Serialize(payload, JsonOpts),
            MediaReference = !string.IsNullOrWhiteSpace(mediaId) ? mediaId : mediaUrl,
            MediaMimeType = NormalizeNullable(attachment.MimeType),
            FileName = fileName,
            MissingVariables = missing.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static RenderResult RenderMessage(string template, IReadOnlyDictionary<string, string> variables, bool keepUnresolvedPlaceholders)
    {
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rendered = PlaceholderPattern.Replace(template, match =>
        {
            var expression = match.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(expression))
            {
                return keepUnresolvedPlaceholders ? match.Value : string.Empty;
            }

            var tokens = expression.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return keepUnresolvedPlaceholders ? match.Value : string.Empty;
            }

            var variableKey = tokens[0];
            var found = variables.TryGetValue(variableKey, out var rawValue);
            var value = found ? rawValue ?? string.Empty : string.Empty;

            if (!found)
            {
                missing.Add(variableKey);
            }

            for (var i = 1; i < tokens.Length; i++)
            {
                value = ApplyFilter(value, tokens[i]);
            }

            if (!found && string.IsNullOrWhiteSpace(value) && keepUnresolvedPlaceholders)
            {
                return match.Value;
            }

            return value;
        });

        return new RenderResult
        {
            Message = rendered.Trim(),
            MissingVariables = missing.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static string ApplyFilter(string value, string filterExpression)
    {
        var separatorIndex = filterExpression.IndexOf(':');
        var filterName = separatorIndex < 0
            ? filterExpression.Trim().ToLowerInvariant()
            : filterExpression[..separatorIndex].Trim().ToLowerInvariant();
        var argument = separatorIndex < 0 ? null : filterExpression[(separatorIndex + 1)..].Trim();

        return filterName switch
        {
            "default" => string.IsNullOrWhiteSpace(value) ? (argument ?? string.Empty) : value,
            "upper" => value.ToUpperInvariant(),
            "lower" => value.ToLowerInvariant(),
            "trim" => value.Trim(),
            "title" => InvariantTextInfo.ToTitleCase(value.ToLowerInvariant()),
            "truncate" => ApplyTruncate(value, argument),
            "date" => ApplyDateFormat(value, argument),
            _ => value
        };
    }

    private static string ApplyTruncate(string value, string? argument)
    {
        if (!int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out var length) || length < 0)
        {
            return value;
        }

        return value.Length <= length ? value : value[..length];
    }

    private static string ApplyDateFormat(string value, string? argument)
    {
        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return value;
        }

        var format = string.IsNullOrWhiteSpace(argument) ? "yyyy-MM-dd HH:mm:ss" : argument;
        return parsed.ToUniversalTime().ToString(format, CultureInfo.InvariantCulture);
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OutsideWindowAction ResolveOutsideWindowAction(CustomWebhookDispatchRequest request)
    {
        var raw = request.OutsideWindowAction?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return request.AllowOutside24HourWindow
                ? OutsideWindowAction.AllowText
                : OutsideWindowAction.Block;
        }

        return raw switch
        {
            "allow_text" => OutsideWindowAction.AllowText,
            "allowtext" => OutsideWindowAction.AllowText,
            "text" => OutsideWindowAction.AllowText,
            "template" => OutsideWindowAction.Template,
            "use_template" => OutsideWindowAction.Template,
            _ => request.AllowOutside24HourWindow ? OutsideWindowAction.AllowText : OutsideWindowAction.Block
        };
    }

    private static OutsideWindowTemplateRenderResult RenderOutsideWindowTemplate(
        CustomWebhookOutsideWindowTemplateDto? template,
        IReadOnlyDictionary<string, string> variables,
        bool keepUnresolvedPlaceholders)
    {
        if (template is null || string.IsNullOrWhiteSpace(template.TemplateName))
        {
            return new OutsideWindowTemplateRenderResult
            {
                Success = false,
                ErrorMessage = "outsideWindowTemplate.templateName is required for template mode."
            };
        }

        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = new List<Dictionary<string, object?>>();

        foreach (var component in template.Components ?? [])
        {
            if (string.IsNullOrWhiteSpace(component.Type))
            {
                continue;
            }

            var renderedParameters = new List<Dictionary<string, object?>>();
            foreach (var parameter in component.Parameters ?? [])
            {
                if (string.IsNullOrWhiteSpace(parameter.Type))
                {
                    continue;
                }

                var normalizedType = parameter.Type.Trim().ToLowerInvariant();
                if (normalizedType == "text")
                {
                    var renderedText = RenderMessage(parameter.Text ?? string.Empty, variables, keepUnresolvedPlaceholders);
                    foreach (var missingVariable in renderedText.MissingVariables)
                    {
                        missing.Add(missingVariable);
                    }

                    renderedParameters.Add(new Dictionary<string, object?>
                    {
                        ["type"] = "text",
                        ["text"] = renderedText.Message
                    });
                    continue;
                }

                var passthrough = new Dictionary<string, object?>
                {
                    ["type"] = normalizedType
                };
                if (!string.IsNullOrWhiteSpace(parameter.Text))
                {
                    passthrough["text"] = parameter.Text;
                }

                renderedParameters.Add(passthrough);
            }

            var renderedComponent = new Dictionary<string, object?>
            {
                ["type"] = component.Type.Trim().ToLowerInvariant()
            };
            if (renderedParameters.Count > 0)
            {
                renderedComponent["parameters"] = renderedParameters;
            }

            components.Add(renderedComponent);
        }

        return new OutsideWindowTemplateRenderResult
        {
            Success = true,
            TemplateName = template.TemplateName.Trim(),
            LanguageCode = string.IsNullOrWhiteSpace(template.LanguageCode)
                ? "en_US"
                : template.LanguageCode.Trim(),
            Components = components,
            MissingVariables = missing.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private void UpdateConversationState(
        Conversation conversation,
        Contact contact,
        string content,
        string messageType,
        DateTime now)
    {
        var preview = content.Length > 1000 ? content[..1000] : content;

        conversation.LastMessageContent = preview;
        conversation.LastMessageType = string.IsNullOrWhiteSpace(messageType) ? "text" : messageType.Trim().ToLowerInvariant();
        conversation.LastMessageAtUtc = now;
        conversation.UpdatedAtUtc = now;

        contact.LastSeenAtUtc = now;
        contact.LastOutboundMessageAtUtc = now;
        contact.UpdatedAtUtc = now;
    }

    private async Task TryLogAsync(
        int companyId,
        string phoneNumberId,
        CustomWebhookDispatchRequest request,
        string summary,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _webhookStore.AddAsync(new WebhookLogEntry
            {
                CompanyId = companyId,
                PhoneNumberId = string.IsNullOrWhiteSpace(phoneNumberId) ? "unknown" : phoneNumberId,
                Timestamp = DateTimeOffset.UtcNow,
                Payload = JsonSerializer.Serialize(request, JsonOpts),
                Summary = summary,
                CorrelationId = correlationId
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist custom webhook log entry.");
        }
    }
}
