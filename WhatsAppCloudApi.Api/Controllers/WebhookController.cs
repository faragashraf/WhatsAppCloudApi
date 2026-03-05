using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Constants;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/webhook")]
public sealed class WebhookController : ControllerBase
{
    private readonly ILogger<WebhookController> _logger;
    private readonly WhatsAppOptions _options;

    public WebhookController(ILogger<WebhookController> logger, IOptions<WhatsAppOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Verify WhatsApp webhook endpoint.
    /// </summary>
    [HttpGet]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string mode, [FromQuery(Name = "hub.verify_token")] string verifyToken, [FromQuery(Name = "hub.challenge")] string challenge)
    {
        if (mode == "subscribe" && verifyToken == _options.VerifyToken)
        {
            return Ok(challenge);
        }

        return Unauthorized();
    }

    /// <summary>
    /// Receive webhook events from WhatsApp Cloud API.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);

        if (!IsValidSignature(payload, Request.Headers[HeaderNames.Signature256].ToString()))
        {
            _logger.LogWarning("Invalid webhook signature.");
            return Unauthorized();
        }

        _logger.LogInformation("Webhook payload: {Payload}", payload);

        var webhook = JsonSerializer.Deserialize<WebhookPayload>(payload);
        if (webhook is not null)
        {
            foreach (var entry in webhook.Entry)
            {
                foreach (var change in entry.Changes)
                {
                    if (change.Value?.Messages is { Count: > 0 })
                    {
                        _logger.LogInformation("Incoming messages received: {Count}", change.Value.Messages.Count);
                    }

                    if (change.Value?.Statuses is { Count: > 0 })
                    {
                        _logger.LogInformation("Status updates received: {Count}", change.Value.Statuses.Count);
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
