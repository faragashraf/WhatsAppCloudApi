using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public TenantContext GetRequiredContext()
    {
        if (!TryGetContext(out var context) || context is null)
        {
            throw new UnauthorizedAccessException("Tenant context is not available.");
        }

        return context;
    }

    public bool TryGetContext(out TenantContext? context)
    {
        context = null;

        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var companyIdClaim = user.FindFirstValue("CompanyId");
        var userIdClaim = user.FindFirstValue("UserId");
        var roleClaim = user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("Role") ?? "Member";

        if (!int.TryParse(companyIdClaim, out var companyId))
        {
            return false;
        }

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return false;
        }

        context = new TenantContext
        {
            CompanyId = companyId,
            UserId = userId,
            Role = roleClaim
        };
        return true;
    }
}
