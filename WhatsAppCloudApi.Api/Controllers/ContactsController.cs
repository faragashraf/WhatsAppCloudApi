using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/contacts")]
[Authorize]
public sealed class ContactsController : ApiControllerBase
{
    private readonly IContactService _contactService;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ApplicationDbContext _db;

    public ContactsController(IContactService contactService, ITenantContextAccessor tenantContext, ApplicationDbContext db)
    {
        _contactService = contactService;
        _tenantContext = tenantContext;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetContacts([FromQuery] ContactQueryParams query, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ContactsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _contactService.GetContactsAsync(ctx.CompanyId, query, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetContact(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ContactsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _contactService.GetContactByIdAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> CreateContact([FromBody] ContactUpsertRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ContactsCreate)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _contactService.CreateContactAsync(ctx.CompanyId, request, ct));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateContact(long id, [FromBody] ContactUpsertRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ContactsEdit)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _contactService.UpdateContactAsync(ctx.CompanyId, id, request, ct));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteContact(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ContactsDelete)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _contactService.DeleteContactAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost("import")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<IActionResult> ImportContacts([FromBody] ContactImportRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ContactsImport)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _contactService.ImportContactsAsync(ctx.CompanyId, request, ct));
    }

    private async Task<Domain.Models.UserPermissions> GetPermissions(int companyId, int userId, string role, CancellationToken ct)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Domain.Models.UserPermissions.FullAccess();
        var user = await _db.CompanyUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, ct);
        return user?.EffectivePermissions ?? Domain.Models.UserPermissions.MemberDefault();
    }
}
