using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Security.Claims;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Logging;

namespace WhatsAppCloudApi.Api.Middleware;

public sealed class ApiLoggingMiddleware
{
    private const int MaxPersistedBodyLength = 8000;
    private const long MaxReadableBodyBytes = 131072; // 128 KB

    private static readonly Regex SensitiveJsonRegex = new(
        "\"(password|newPassword|accessToken|refreshToken|verifyToken|appSecret|otp|code)\"\\s*:\\s*\".*?\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex EmailJsonRegex = new(
        "\"email\"\\s*:\\s*\"(?<email>.*?)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private readonly RequestDelegate _next;

    public ApiLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<ApiLoggingMiddleware> logger)
    {
        if (ShouldSkipLogging(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
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
            stopwatch.Stop();

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
                    RequestBody = Truncate(Sanitize(requestBody, context.Request.Path), MaxPersistedBodyLength),
                    ResponseBody = Truncate(Sanitize(responseBodyText, context.Request.Path), MaxPersistedBodyLength),
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

            logger.LogInformation(
                "API request logged. Method={Method} Endpoint={Endpoint} Status={Status} ElapsedMs={ElapsedMs}",
                context.Request.Method,
                endpoint,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
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
        if (request.ContentLength is null or <= 0
            || request.Body.CanRead == false
            || request.ContentLength > MaxReadableBodyBytes
            || !IsTextBasedContentType(request.ContentType))
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
        if (body.Length > MaxReadableBodyBytes)
        {
            return body[..(int)MaxReadableBodyBytes];
        }

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

    private static bool IsTextBasedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("application/xml", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("text/", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Sanitize(string? value, PathString requestPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var masked = LogSanitizer.MaskSensitive(value);
        masked = SensitiveJsonRegex.Replace(masked, m =>
        {
            var split = m.Value.Split(':', 2);
            return split.Length == 2 ? $"{split[0]}: \"***\"" : "\"***\"";
        });

        if (ShouldMaskAuthResetEmail(requestPath))
        {
            masked = EmailJsonRegex.Replace(masked, match =>
            {
                var email = match.Groups["email"].Value;
                var split = match.Value.Split(':', 2);
                if (split.Length != 2)
                {
                    return "\"email\": \"***\"";
                }

                return $"{split[0]}: \"{MaskEmail(email)}\"";
            });
        }

        return masked;
    }

    private static bool ShouldMaskAuthResetEmail(PathString path)
    {
        return path.StartsWithSegments("/api/auth/forgot-password", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/verify-otp", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/reset-password", StringComparison.OrdinalIgnoreCase);
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "***";
        }

        var normalized = email.Trim();
        var atIndex = normalized.IndexOf('@');
        if (atIndex <= 0 || atIndex >= normalized.Length - 1)
        {
            return "***";
        }

        var local = normalized[..atIndex];
        var domain = normalized[(atIndex + 1)..];
        var visibleLocal = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visibleLocal}***@{domain}";
    }

    private static bool ShouldSkipLogging(PathString path)
    {
        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/webhook", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/super-admin/logs", StringComparison.OrdinalIgnoreCase);
    }
}
