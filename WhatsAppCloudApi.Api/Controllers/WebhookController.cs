using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Api.Services;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Constants;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/webhook")]
public sealed class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    private readonly ITenantWhatsAppConfigService _tenantWhatsAppConfigService;
    private readonly IWebhookStore _store;
    private readonly IConversationService _conversationService;

    public WebhookController(
        ILogger<WebhookController> logger,
        ITenantWhatsAppConfigService tenantWhatsAppConfigService,
        IWebhookStore store,
        IConversationService conversationService)
    {
        _logger = logger;
        _tenantWhatsAppConfigService = tenantWhatsAppConfigService;
        _store = store;
        _conversationService = conversationService;
    }

    private static string EncodeToHtmlEntities(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        try
        {
            input = System.Text.RegularExpressions.Regex.Unescape(input);
        }
        catch
        {
        }

        var sb = new StringBuilder();
        foreach (var ch in input)
        {
            if (ch > 127)
            {
                sb.Append("&#");
                sb.Append((int)ch);
                sb.Append(';');
            }
            else
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    [HttpGet]
    public async Task<IActionResult> Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(mode) && string.IsNullOrWhiteSpace(verifyToken) && string.IsNullOrWhiteSpace(challenge))
        {
            return Ok();
        }

        if (mode == "subscribe" && !string.IsNullOrWhiteSpace(verifyToken) && !string.IsNullOrWhiteSpace(challenge))
        {
            var config = await _tenantWhatsAppConfigService.GetConfigByVerifyTokenAsync(verifyToken, cancellationToken);
            if (config is not null)
            {
                return Ok(challenge);
            }
        }

        return Unauthorized();
    }

    [HttpGet("logs")]
    public IActionResult Logs([FromQuery] string format = "json")
    {
        var entries = _store.GetAll().OrderByDescending(e => e.Timestamp).ToArray();

        if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
        {
            var queryLang = Request.Query["lang"].ToString();
            var acceptLang = Request.Headers["Accept-Language"].ToString();
            var isArabic = string.Equals(queryLang, "ar", StringComparison.OrdinalIgnoreCase)
                           || (!string.IsNullOrWhiteSpace(acceptLang) && acceptLang.IndexOf("ar", StringComparison.OrdinalIgnoreCase) >= 0);

            var htmlLang = isArabic ? "ar" : "en";
            var dir = isArabic ? "rtl" : "ltr";
            var headerText = isArabic ? "سجلات الويب هوك" : "Webhook Logs";
            var invalidSignatureAr = "توقيع غير صالح";

            var sb = new StringBuilder();
            sb.Append($"<html lang=\"{htmlLang}\" dir=\"{dir}\"><head><meta charset=\"utf-8\"><title>{EncodeToHtmlEntities(headerText)}</title></head><body>");
            sb.Append($"<h1>{EncodeToHtmlEntities(headerText)}</h1>");
            sb.Append("<ul style=\"list-style:none;padding:0\">");
            foreach (var e in entries)
            {
                sb.Append("<li style=\"margin-bottom:1rem;border:1px solid #ddd;padding:8px\">");
                sb.Append($"<div><strong>{WebUtility.HtmlEncode(e.Timestamp.ToString("u"))}</strong></div>");
                if (!string.IsNullOrWhiteSpace(e.Summary))
                {
                    if (isArabic && string.Equals(e.Summary, "Invalid signature", StringComparison.OrdinalIgnoreCase))
                    {
                        sb.Append($"<div>{EncodeToHtmlEntities(invalidSignatureAr)}</div>");
                    }
                    else
                    {
                        sb.Append($"<div>{WebUtility.HtmlEncode(e.Summary)}</div>");
                    }
                }
                sb.Append("<pre style=\"white-space:pre-wrap;word-break:break-word;background:#f9f9f9;padding:8px\">");
                string displayPayload = e.Payload ?? string.Empty;
                try
                {
                    using var doc = JsonDocument.Parse(displayPayload);
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };
                    displayPayload = JsonSerializer.Serialize(doc.RootElement, options);
                }
                catch
                {
                    try
                    {
                        displayPayload = System.Text.RegularExpressions.Regex.Unescape(displayPayload);
                    }
                    catch
                    {
                    }
                }

                sb.Append(WebUtility.HtmlEncode(displayPayload));
                sb.Append("</pre>");
                sb.Append("</li>");
            }
            sb.Append("</ul></body></html>");

            return Content(sb.ToString(), "text/html; charset=utf-8");
        }

        return Ok(entries);
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        _logger.LogInformation("Webhook payload received ({Length} bytes)", payload.Length);

        var phoneNumberId = TryExtractPhoneNumberId(payload);
        var tenantConfig = phoneNumberId is null
            ? null
            : await _tenantWhatsAppConfigService.GetConfigByPhoneNumberIdAsync(phoneNumberId, cancellationToken);

        var signatureValid = IsValidSignature(payload, Request.Headers[HeaderNames.Signature256].ToString(), tenantConfig?.AppSecret);
        var summary = signatureValid ? null : "Invalid signature (processed)";
        _store.Add(new WebhookLogEntry { Timestamp = DateTimeOffset.UtcNow, Payload = payload, Summary = summary });

        if (!signatureValid)
        {
            _logger.LogWarning("Invalid webhook signature for phone {PhoneNumberId}. Payload will still be processed.", phoneNumberId);
        }

        // Always process the payload to persist inbound messages and status updates
        if (tenantConfig is not null)
        {
            await ProcessWebhookPayloadAsync(payload, tenantConfig, cancellationToken);
        }

        // Return OK so Meta does not retry
        return Ok();
    }

    private async Task ProcessWebhookPayloadAsync(string payload, TenantWhatsAppConfig tenantConfig, CancellationToken ct)
    {
        WebhookPayload? webhook;
        try
        {
            webhook = JsonSerializer.Deserialize<WebhookPayload>(payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize webhook payload");
            return;
        }

        if (webhook is null) return;

        foreach (var entry in webhook.Entry)
        {
            foreach (var change in entry.Changes)
            {
                var value = change.Value;
                if (value is null) continue;

                // ── Persist inbound messages ──
                if (value.Messages is { Count: > 0 })
                {
                    foreach (var msg in value.Messages)
                    {
                        string content = "";
                        string? mediaUrl = null;
                        string? mediaMimeType = null;
                        string msgType = msg.Type ?? "text";

                        if (msg.Type == "text")
                        {
                            content = msg.Text?.Body ?? "";
                        }
                        else
                        {
                            var media = msg.Image ?? msg.Video ?? msg.Audio ?? msg.Document ?? msg.Sticker;
                            if (media is not null)
                            {
                                mediaUrl = media.Id;
                                mediaMimeType = media.MimeType;
                                content = media.Caption ?? "";
                            }
                        }

                        var contactName = value.Contacts?
                            .FirstOrDefault(c => c.WaId == msg.From)?.Profile?.Name;

                        try
                        {
                            await _conversationService.ProcessInboundMessageAsync(
                                tenantConfig.CompanyId,
                                msg.From ?? "",
                                contactName,
                                tenantConfig.WhatsAppPhoneNumberId,
                                msg.Id ?? "",
                                msgType,
                                content,
                                mediaUrl,
                                mediaMimeType,
                                ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to persist inbound message {MetaId}", msg.Id);
                        }

                        _logger.LogInformation("Inbound msg from {From} id={Id} type={Type}", msg.From, msg.Id, msgType);
                    }
                }

                // ── Process status updates ──
                if (value.Statuses is { Count: > 0 })
                {
                    foreach (var status in value.Statuses)
                    {
                        try
                        {
                            await _conversationService.ProcessStatusUpdateAsync(
                                status.Id ?? "", status.Status ?? "", ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process status update for {MetaId}", status.Id);
                        }

                        _logger.LogInformation("Status update {Id}: {Status}", status.Id, status.Status);
                    }
                }
            }
        }
    }

    private static string? TryExtractPhoneNumberId(string payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("entry", out var entryNode) || entryNode.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var entry in entryNode.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changesNode) || changesNode.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var change in changesNode.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var valueNode))
                    {
                        continue;
                    }

                    if (!valueNode.TryGetProperty("metadata", out var metadataNode))
                    {
                        continue;
                    }

                    if (!metadataNode.TryGetProperty("phone_number_id", out var phoneIdNode))
                    {
                        continue;
                    }

                    var phoneId = phoneIdNode.GetString();
                    if (!string.IsNullOrWhiteSpace(phoneId))
                    {
                        return phoneId;
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool IsValidSignature(string payload, string signatureHeader, string? appSecret)
    {
        if (string.IsNullOrWhiteSpace(appSecret) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedSignature = signatureHeader[7..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(providedSignature.ToLowerInvariant()));
    }
}
