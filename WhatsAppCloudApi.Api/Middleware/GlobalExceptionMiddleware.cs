using System.Net;
using System.Text.Json;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(
                "An unexpected error occurred.",
                HttpStatusCode.InternalServerError,
                correlationId: context.TraceIdentifier,
                details: ex.Message);

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
