using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhatsAppCloudApi.Api.Services;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Shared.Constants;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/webhook")]
public sealed class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    private readonly ITenantWhatsAppConfigService _tenantWhatsAppConfigService;
    private readonly IWebhookStore _store;
    private readonly IWebhookInboxQueue _webhookInboxQueue;

    public WebhookController(
        ILogger<WebhookController> logger,
        ITenantWhatsAppConfigService tenantWhatsAppConfigService,
        IWebhookStore store,
        IWebhookInboxQueue webhookInboxQueue)
    {
        _logger = logger;
        _tenantWhatsAppConfigService = tenantWhatsAppConfigService;
        _store = store;
        _webhookInboxQueue = webhookInboxQueue;
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
    [AllowAnonymous]
    [EnableRateLimiting("webhook")]
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
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Logs(
        [FromQuery] string format = "json",
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        var companyIdClaim = User.FindFirstValue("CompanyId");
        if (!int.TryParse(companyIdClaim, out var companyId) || companyId <= 0)
        {
            return Unauthorized();
        }

        var entries = await _store.GetAllAsync(companyId, Math.Clamp(take, 1, 1000), cancellationToken);

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
    [AllowAnonymous]
    [EnableRateLimiting("webhook")]
    [RequestTimeout("webhook-timeout")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var payloadForStore = payload.Length > 50_000 ? payload[..50_000] : payload;

        _logger.LogInformation("Webhook payload received ({Length} bytes)", payload.Length);

        var phoneNumberId = TryExtractPhoneNumberId(payload);
        var tenantConfig = phoneNumberId is null
            ? null
            : await _tenantWhatsAppConfigService.GetConfigByPhoneNumberIdAsync(phoneNumberId, cancellationToken);

        if (tenantConfig is null)
        {
            _logger.LogWarning("Webhook rejected for unknown phone number id {PhoneNumberId}.", phoneNumberId);
            return Unauthorized();
        }

        var hasAppSecret = !string.IsNullOrWhiteSpace(tenantConfig.AppSecret);
        bool? signatureValid = null;
        string? summary = null;

        if (hasAppSecret)
        {
            signatureValid = IsValidSignature(
                payload,
                Request.Headers[HeaderNames.Signature256].ToString(),
                tenantConfig.AppSecret);
        }
        else
        {
            summary = "Signature validation skipped (missing app secret)";
            _logger.LogWarning(
                "Webhook accepted without signature validation for phone {PhoneNumberId}: app secret is missing.",
                phoneNumberId);
        }

        if (hasAppSecret && signatureValid is false)
        {
            _logger.LogWarning("Webhook rejected for phone {PhoneNumberId}: invalid signature.", phoneNumberId);
            await _store.AddAsync(new WebhookLogEntry
            {
                CompanyId = tenantConfig.CompanyId,
                PhoneNumberId = phoneNumberId ?? "unknown",
                Timestamp = DateTimeOffset.UtcNow,
                Payload = payloadForStore,
                Summary = "Invalid signature",
                SignatureValid = false,
                CorrelationId = HttpContext.TraceIdentifier
            }, cancellationToken);
            return Unauthorized();
        }

        await _store.AddAsync(new WebhookLogEntry
        {
            CompanyId = tenantConfig.CompanyId,
            PhoneNumberId = phoneNumberId ?? "unknown",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = payloadForStore,
            Summary = summary,
            SignatureValid = signatureValid,
            CorrelationId = HttpContext.TraceIdentifier
        }, cancellationToken);

        var queueId = await _webhookInboxQueue.EnqueueAsync(new WebhookInboxQueueItem
        {
            CompanyId = tenantConfig.CompanyId,
            WhatsAppPhoneNumberId = tenantConfig.WhatsAppPhoneNumberId,
            PhoneNumberId = phoneNumberId ?? "unknown",
            PayloadJson = payload
        }, cancellationToken);

        _logger.LogInformation(
            "Webhook queued successfully. QueueId={QueueId} CompanyId={CompanyId} PhoneNumberId={PhoneNumberId}",
            queueId,
            tenantConfig.CompanyId,
            phoneNumberId);

        // Return OK so Meta does not retry
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
