using System.Security.Claims;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Api.Middleware;

public sealed class ApiLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public ApiLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<ApiLoggingMiddleware> logger)
    {
        var endpoint = $"{context.Request.Path}{context.Request.QueryString}";
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        var originalBodyStream = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            var responseBodyText = await ReadResponseBodyAsync(context.Response);
            await responseBody.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;

            try
            {
                var companyId = TryParseInt(context.User.FindFirstValue("CompanyId"));
                var userId = TryParseInt(context.User.FindFirstValue("UserId"));

                var log = new ApiLog
                {
                    CompanyId = companyId,
                    CompanyUserId = userId,
                    Endpoint = endpoint,
                    HttpMethod = context.Request.Method,
                    RequestBody = Truncate(requestBody, 8000),
                    ResponseBody = Truncate(responseBodyText, 8000),
                    StatusCode = context.Response.StatusCode,
                    IpAddress = ipAddress,
                    CreatedAtUtc = DateTime.UtcNow
                };

                await PersistLogAsync(context, log, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to persist API log entry.");
            }
        }
    }

    private static async Task PersistLogAsync(HttpContext context, ApiLog log, ILogger logger)
    {
        try
        {
            await using var scope = context.RequestServices.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            dbContext.ApiLogs.Add(log);
            await dbContext.SaveChangesAsync(context.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist API log entry.");
        }
    }

    private static async Task<string?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or <= 0 || request.Body.CanRead == false)
        {
            return null;
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }

    private static async Task<string?> ReadResponseBodyAsync(HttpResponse response)
    {
        if (!response.Body.CanRead)
        {
            return null;
        }

        response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        response.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    private static int? TryParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
