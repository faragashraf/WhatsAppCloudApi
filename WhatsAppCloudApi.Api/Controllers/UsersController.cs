using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ApiControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public UsersController(ApplicationDbContext dbContext, ITenantContextAccessor tenantContextAccessor)
    {
        _dbContext = dbContext;
        _tenantContextAccessor = tenantContextAccessor;
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
            Role = request.Role,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.CompanyUsers.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<CompanyUser>.Ok(user, "User created."));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] CompanyUserUpsertRequest request, CancellationToken cancellationToken)
    {
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
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
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

        _dbContext.CompanyUsers.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToActionResult(ApiResponse<object>.Ok(new { id }, "User deleted."));
    }
}
