using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ApiControllerBase
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin",
        "Member",
        "Agent"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IRoutingService _routingService;

    public UsersController(ApplicationDbContext dbContext, ITenantContextAccessor tenantContextAccessor, IRoutingService routingService)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
        _routingService = routingService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var users = await _dbContext.CompanyUsers
            .AsNoTracking()
            .Where(x => x.CompanyId == tenant.CompanyId)
            .ToListAsync(cancellationToken);

        return ToActionResult(ApiResponse<List<CompanyUser>>.Ok(users));
    }

    [HttpGet("agents")]
    public async Task<IActionResult> GetAgents(CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var agents = (await _routingService.GetEligibleUsersAsync(tenant.CompanyId, forAutoAssignment: false, cancellationToken))
            .Select(x => new { x.CompanyUserId, x.FullName, x.Email, x.Role, x.PermissionsJson })
            .ToList();

        return ToActionResult(ApiResponse<object>.Ok(agents));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        var user = await _dbContext.CompanyUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CompanyUserId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (user is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("User not found.", System.Net.HttpStatusCode.NotFound));
        }

        return ToActionResult(ApiResponse<CompanyUser>.Ok(user));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CompanyUserUpsertRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ToActionResult(ApiResponse<object>.Fail("Password is required.", System.Net.HttpStatusCode.BadRequest));
        }

        if (!IsStrongPassword(request.Password))
        {
            return ToActionResult(ApiResponse<object>.Fail("Password must include upper, lower, number, special character and be at least 10 chars.", System.Net.HttpStatusCode.BadRequest));
        }

        if (!AllowedRoles.Contains(request.Role))
        {
            return ToActionResult(ApiResponse<object>.Fail("Invalid role.", System.Net.HttpStatusCode.BadRequest));
        }

        var tenant = _tenantContextAccessor.GetRequiredContext();
        var email = request.Email.Trim().ToLowerInvariant();
        var exists = await _dbContext.CompanyUsers
            .AnyAsync(x => x.CompanyId == tenant.CompanyId && x.Email == email, cancellationToken);
        if (exists)
        {
            return ToActionResult(ApiResponse<object>.Fail("User email already exists.", System.Net.HttpStatusCode.Conflict));
        }

        var user = new CompanyUser
        {
            CompanyId = tenant.CompanyId,
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role.Trim(),
            IsActive = request.IsActive,
            PermissionsJson = request.Permissions is not null
                ? JsonSerializer.Serialize(request.Permissions, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                : null,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.CompanyUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.CompanyUserRoutingSettings.Add(new CompanyUserRoutingSettings
        {
            CompanyId = tenant.CompanyId,
            CompanyUserId = user.CompanyUserId,
            CanReceiveManualAssignments = true,
            CanReceiveAutoAssignments = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ToActionResult(ApiResponse<CompanyUser>.Ok(user, "User created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] CompanyUserUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedRoles.Contains(request.Role))
        {
            return ToActionResult(ApiResponse<object>.Fail("Invalid role.", System.Net.HttpStatusCode.BadRequest));
        }

        var tenant = _tenantContextAccessor.GetRequiredContext();
        var user = await _dbContext.CompanyUsers
            .FirstOrDefaultAsync(x => x.CompanyUserId == id && x.CompanyId == tenant.CompanyId, cancellationToken);

        if (user is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("User not found.", System.Net.HttpStatusCode.NotFound));
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var emailConflict = await _dbContext.CompanyUsers
            .AnyAsync(x => x.CompanyUserId != id && x.CompanyId == tenant.CompanyId && x.Email == email, cancellationToken);
        if (emailConflict)
        {
            return ToActionResult(ApiResponse<object>.Fail("User email already exists.", System.Net.HttpStatusCode.Conflict));
        }

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.Role = request.Role.Trim();
        user.IsActive = request.IsActive;
        user.PermissionsJson = request.Permissions is not null
            ? JsonSerializer.Serialize(request.Permissions, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            : user.PermissionsJson;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (!IsStrongPassword(request.Password))
            {
                return ToActionResult(ApiResponse<object>.Fail("Password must include upper, lower, number, special character and be at least 10 chars.", System.Net.HttpStatusCode.BadRequest));
            }
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<CompanyUser>.Ok(user, "User updated."));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken)
    {
        var tenant = _tenantContextAccessor.GetRequiredContext();
        if (id == tenant.UserId)
        {
            return ToActionResult(ApiResponse<object>.Fail("You cannot delete your own user.", System.Net.HttpStatusCode.BadRequest));
        }

        var user = await _dbContext.CompanyUsers
            .FirstOrDefaultAsync(x => x.CompanyUserId == id && x.CompanyId == tenant.CompanyId, cancellationToken);
        if (user is null)
        {
            return ToActionResult(ApiResponse<object>.Fail("User not found.", System.Net.HttpStatusCode.NotFound));
        }

        var utcNow = DateTime.UtcNow;
        var createdEmailAccounts = await _dbContext.EmailAccounts
            .Where(x => x.CreatedByUserId == user.CompanyUserId)
            .ToListAsync(cancellationToken);
        foreach (var account in createdEmailAccounts)
        {
            account.CreatedByUserId = null;
            account.UpdatedAtUtc = utcNow;
        }

        var createdNotificationRules = await _dbContext.EmailNotificationRules
            .Where(x => x.CreatedByUserId == user.CompanyUserId)
            .ToListAsync(cancellationToken);
        foreach (var rule in createdNotificationRules)
        {
            rule.CreatedByUserId = null;
            rule.UpdatedAtUtc = utcNow;
        }

        var createdQueueItems = await _dbContext.EmailQueue
            .Where(x => x.CreatedByUserId == user.CompanyUserId)
            .ToListAsync(cancellationToken);
        foreach (var queueItem in createdQueueItems)
        {
            queueItem.CreatedByUserId = null;
            queueItem.UpdatedAtUtc = utcNow;
        }

        var routingMemberships = await _dbContext.RoutingTeamMembers
            .Where(x => x.CompanyId == tenant.CompanyId && x.CompanyUserId == user.CompanyUserId)
            .ToListAsync(cancellationToken);
        if (routingMemberships.Count > 0)
        {
            _dbContext.RoutingTeamMembers.RemoveRange(routingMemberships);
        }

        _dbContext.CompanyUsers.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<object>.Ok(new { id }, "User deleted."));
    }

    private static bool IsStrongPassword(string password)
    {
        if (password.Length < 10)
        {
            return false;
        }

        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(ch => !char.IsLetterOrDigit(ch));
    }
}
