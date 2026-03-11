using System.Text.RegularExpressions;

namespace WhatsAppCloudApi.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    private static readonly Regex AllowedCharactersRegex =
        new("^[a-zA-Z0-9\\-_.]{8,128}$", RegexOptions.Compiled);

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsValid(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        await _next(context);
    }

    private static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && AllowedCharactersRegex.IsMatch(value);
}
