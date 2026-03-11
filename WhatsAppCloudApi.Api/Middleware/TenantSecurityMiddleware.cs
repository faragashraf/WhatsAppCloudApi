using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Middleware;

public sealed class TenantSecurityMiddleware
{
    private readonly RequestDelegate _next;

    public TenantSecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext, ILogger<TenantSecurityMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated != true || IsExcludedPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst("UserId")?.Value;
        var companyIdClaim = context.User.FindFirst("CompanyId")?.Value;

        if (!int.TryParse(userIdClaim, out var userId) || !int.TryParse(companyIdClaim, out var companyId))
        {
            await WriteUnauthorizedAsync(context, "Invalid tenant context.");
            return;
        }

        var userIsActive = await dbContext.CompanyUsers
            .AsNoTracking()
            .AnyAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, context.RequestAborted);

        if (!userIsActive)
        {
            logger.LogWarning("Rejected request for inactive or mismatched user {UserId} in company {CompanyId}.", userId, companyId);
            await WriteUnauthorizedAsync(context, "User is inactive.");
            return;
        }

        var companyState = await dbContext.Companies
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .Select(c => new { c.IsDeleted, Status = (c.Status ?? "ACTIVE").ToUpper() })
            .FirstOrDefaultAsync(context.RequestAborted);

        if (companyState is null || companyState.IsDeleted || companyState.Status is "SUSPENDED" or "DELETED" or "INACTIVE")
        {
            logger.LogWarning("Rejected request for blocked company {CompanyId}.", companyId);
            await WriteForbiddenAsync(context, "Company access is blocked.");
            return;
        }

        await _next(context);
    }

    private static bool IsExcludedPath(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        return path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/api/webhook", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/api/webhook/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail(message, System.Net.HttpStatusCode.Unauthorized, correlationId: context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(response);
    }

    private static async Task WriteForbiddenAsync(HttpContext context, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail(message, System.Net.HttpStatusCode.Forbidden, correlationId: context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(response);
    }
}
