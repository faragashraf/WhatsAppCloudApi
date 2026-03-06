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

    public WebhookController(
        ILogger<WebhookController> logger,
        ITenantWhatsAppConfigService tenantWhatsAppConfigService,
        IWebhookStore store)
    {
        _logger = logger;
        _tenantWhatsAppConfigService = tenantWhatsAppConfigService;
        _store = store;
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

        _logger.LogInformation("Webhook payload: {Payload}", payload);
        _store.Add(new WebhookLogEntry { Timestamp = DateTimeOffset.UtcNow, Payload = payload });

        var phoneNumberId = TryExtractPhoneNumberId(payload);
        var tenantConfig = phoneNumberId is null
            ? null
            : await _tenantWhatsAppConfigService.GetConfigByPhoneNumberIdAsync(phoneNumberId, cancellationToken);

        if (!IsValidSignature(payload, Request.Headers[HeaderNames.Signature256].ToString(), tenantConfig?.AppSecret))
        {
            _logger.LogWarning("Invalid webhook signature.");
            _store.Add(new WebhookLogEntry { Timestamp = DateTimeOffset.UtcNow, Payload = payload, Summary = "Invalid signature" });
            return Unauthorized();
        }

        var webhook = JsonSerializer.Deserialize<WebhookPayload>(payload);
        if (webhook is not null)
        {
            foreach (var entry in webhook.Entry)
            {
                foreach (var change in entry.Changes)
                {
                    var value = change.Value;
                    if (value?.Messages is { Count: > 0 })
                    {
                        foreach (var msg in value.Messages)
                        {
                            var from = msg.From;
                            var id = msg.Id;
                            var type = msg.Type;
                            string? text = null;
                            string? mediaId = null;

                            if (type == "text" && msg.Text is not null)
                            {
                                text = msg.Text.Body;
                            }

                            if ((type == "image" || type == "video" || type == "audio" || type == "document") && msg.Image is not null)
                            {
                                mediaId = msg.Image.Id;
                            }

                            _logger.LogInformation("Msg from {From} id={Id} type={Type} text={Text} mediaId={MediaId}", from, id, type, text, mediaId);
                        }
                    }

                    if (value?.Statuses is { Count: > 0 })
                    {
                        foreach (var status in value.Statuses)
                        {
                            _logger.LogInformation("Status update for {Id}: {Status}", status.Id, status.Status);
                        }
                    }
                }
            }
        }

        return Ok();
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
