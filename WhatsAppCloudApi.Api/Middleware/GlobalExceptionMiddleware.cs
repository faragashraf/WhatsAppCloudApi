using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Exceptions;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", context.TraceIdentifier);

            var statusCode = ex switch
            {
                UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                System.Security.SecurityException => HttpStatusCode.Forbidden,
                KeyNotFoundException => HttpStatusCode.NotFound,
                InvalidOperationException => HttpStatusCode.BadRequest,
                ArgumentException => HttpStatusCode.BadRequest,
                FormatException => HttpStatusCode.BadRequest,
                JsonException => HttpStatusCode.BadRequest,
                EmailDeliveryException => HttpStatusCode.ServiceUnavailable,
                DbUpdateException => HttpStatusCode.Conflict,
                OperationCanceledException when context.RequestAborted.IsCancellationRequested => (HttpStatusCode)499,
                _ => HttpStatusCode.InternalServerError
            };

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var publicMessage = statusCode switch
            {
                HttpStatusCode.InternalServerError => "An unexpected error occurred.",
                HttpStatusCode.Unauthorized => "Unauthorized.",
                HttpStatusCode.Forbidden => "Forbidden.",
                HttpStatusCode.NotFound => "Not found.",
                HttpStatusCode.ServiceUnavailable => ex.Message,
                (HttpStatusCode)499 => "Request canceled.",
                _ => ex.Message
            };

            var details = _environment.IsDevelopment() && statusCode == HttpStatusCode.BadRequest
                ? ex.Message
                : null;

            var response = ApiResponse<object>.Fail(
                publicMessage,
                statusCode,
                correlationId: context.TraceIdentifier,
                details: details);

            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
