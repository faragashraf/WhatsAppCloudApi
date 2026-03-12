using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhatsAppCloudApi.Api.Services;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Shared.Constants;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/webhook/custom")]
public sealed class CustomWebhookController : ApiControllerBase
{
    private readonly ICustomWebhookDispatcher _dispatcher;

    public CustomWebhookController(ICustomWebhookDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Public custom webhook that renders variables into a preconfigured message and queues it for delivery.
    /// </summary>
    [HttpPost("dispatch")]
    [AllowAnonymous]
    [EnableRateLimiting("webhook")]
    [RequestTimeout("webhook-timeout")]
    [RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Dispatch(
        [FromBody] CustomWebhookDispatchRequest request,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        var headerToken = Request.Headers[HeaderNames.WebhookToken].ToString();
        var webhookToken = string.IsNullOrWhiteSpace(headerToken) ? token : headerToken;

        var response = await _dispatcher.DispatchAsync(
            request,
            webhookToken,
            HttpContext.TraceIdentifier,
            cancellationToken);

        return ToActionResult(response);
    }
}
