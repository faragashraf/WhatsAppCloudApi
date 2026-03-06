using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Constants;
using WhatsAppCloudApi.Api.Services;
using System.Net;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/webhook")]
public sealed class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    private readonly WhatsAppOptions _options;
    private readonly IWebhookStore _store;

    public WebhookController(ILogger<WebhookController> logger, IOptions<WhatsAppOptions> options, IWebhookStore store)
    {
        _logger = logger;
        _options = options.Value;
        _store = store;
    }

    private static string EncodeToHtmlEntities(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        // If the input contains literal \uXXXX sequences, replace them with actual chars first
        try
        {
            input = System.Text.RegularExpressions.Regex.Unescape(input);
        }
        catch
        {
            // ignore
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

    /// <summary>
    /// Verify WhatsApp webhook endpoint.
    /// </summary>
    [HttpGet]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? verifyToken, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        // If no query parameters are provided, return a simple 200 OK to avoid forcing clients
        // to always include query params when calling the endpoint manually. This does NOT
        // perform webhook verification. To perform verification WhatsApp will call this
        // endpoint with the required query parameters (mode=subscribe, hub.verify_token, hub.challenge).
        if (string.IsNullOrWhiteSpace(mode) && string.IsNullOrWhiteSpace(verifyToken) && string.IsNullOrWhiteSpace(challenge))
        {
            return Ok();
        }

        if (mode == "subscribe" && verifyToken == _options.VerifyToken && !string.IsNullOrWhiteSpace(challenge))
        {
            return Ok(challenge);
        }

        return Unauthorized();
    }

    /// <summary>
    /// Return recent webhook log entries. Use `?format=html` to get a minimal HTML page.
    /// </summary>
    [HttpGet("logs")]
    public IActionResult Logs([FromQuery] string format = "json")
    {
        var entries = _store.GetAll().OrderByDescending(e => e.Timestamp).ToArray();

        if (string.Equals(format, "html", StringComparison.OrdinalIgnoreCase))
        {
            // Determine if Arabic rendering is requested either via ?lang=ar or Accept-Language header
            var queryLang = Request.Query["lang"].ToString();
            var acceptLang = Request.Headers["Accept-Language"].ToString();
            var isArabic = string.Equals(queryLang, "ar", StringComparison.OrdinalIgnoreCase)
                           || (!string.IsNullOrWhiteSpace(acceptLang) && acceptLang.IndexOf("ar", StringComparison.OrdinalIgnoreCase) >= 0);

            var htmlLang = isArabic ? "ar" : "en";
            var dir = isArabic ? "rtl" : "ltr";
            var headerText = isArabic ? "\u0633\u062c\u0644\u0627\u062a \u0627\u0644\u0648\u064a\u0628 \u0647\u0648\u0643" : "Webhook Logs";
            var invalidSignatureAr = "\u062a\u0648\u0642\u064a\u0639 \u063a\u064a\u0631 \u0635\u0627\u0644\u062d";

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
                    // If payload is JSON, parse and re-serialize with an encoder that
                    // does not escape non-ASCII characters so Arabic appears correctly.
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
                    // Not JSON — try unescaping common \uXXXX sequences
                    try
                    {
                        displayPayload = System.Text.RegularExpressions.Regex.Unescape(displayPayload);
                    }
                    catch
                    {
                        // ignore
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


    /// <summary>
    /// Receive webhook events from WhatsApp Cloud API.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        // Always persist the raw payload so /logs can be inspected during development or debugging.
        _logger.LogInformation("Webhook payload: {Payload}", payload);
        _store.Add(new WebhookLogEntry { Timestamp = DateTimeOffset.UtcNow, Payload = payload });

        // Validate signature; if invalid, record a note and return 401. Keeping the payload
        // in the store helps diagnose signature/header issues.
        if (!IsValidSignature(payload, Request.Headers[HeaderNames.Signature256].ToString()))
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
                    if (value?.Contacts is { Count: > 0 })
                    {
                        var waId = value.Contacts[0].WaId; // adapt to your model property names
                    }

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
                                text = msg.Text.Body;

                            if ((type == "image" || type == "video" || type == "audio" || type == "document") && msg.Image is not null)
                                mediaId = msg.Image.Id; // or msg.Video.Id etc.

                            // Example: push to background processing queue, persist to DB, or trigger business handler
                            _logger.LogInformation("Msg from {From} id={Id} type={Type} text={Text} mediaId={MediaId}", from, id, type, text, mediaId);

                            // if mediaId != null -> call GET /{mediaId} to retrieve temporary media URL, then download
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

    private bool IsValidSignature(string payload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(_options.AppSecret) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedSignature = signatureHeader[7..];
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.AppSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(providedSignature.ToLowerInvariant()));
    }
}
