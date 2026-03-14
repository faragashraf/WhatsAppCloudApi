using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
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
    private readonly IRoutingService _routingService;

    public ContactsController(IContactService contactService, ITenantContextAccessor tenantContext, ApplicationDbContext db, IRoutingService routingService)
    {
        _contactService = contactService;
        _tenantContext = tenantContext;
        _db = db;
        _routingService = routingService;
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

    [HttpGet("{id:long}/profile")]
    public async Task<IActionResult> GetContactProfile(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ContactsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        var contact = await _db.Contacts
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .FirstOrDefaultAsync(x => x.CompanyId == ctx.CompanyId && x.ContactId == id && x.IsActive, ct);
        if (contact is null)
            return ToActionResult(ApiResponse<object>.Fail("Contact not found.", System.Net.HttpStatusCode.NotFound));

        var conversationCount = await _db.Conversations
            .AsNoTracking()
            .CountAsync(x => x.CompanyId == ctx.CompanyId && x.ContactId == id, ct);

        var recentConversations = await _db.Conversations
            .AsNoTracking()
            .Where(x => x.CompanyId == ctx.CompanyId && x.ContactId == id)
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .Take(10)
            .Select(x => new ContactConversationSummaryDto
            {
                ConversationId = x.ConversationId,
                WhatsAppPhoneNumberId = x.WhatsAppPhoneNumberId,
                ContactNumber = x.ContactNumber,
                ContactName = x.ContactName,
                Status = x.Status,
                UnreadCount = x.UnreadCount,
                AssignedTeamId = x.AssignedTeamId,
                AssignedTeamName = x.AssignedTeam != null ? x.AssignedTeam.Name : null,
                AssignedUserId = x.AssignedUserId,
                AssignedUserName = x.AssignedUser != null ? x.AssignedUser.FullName : null,
                LastMessageAtUtc = x.LastMessageAtUtc,
                LastInboundMessageAtUtc = x.LastInboundMessageAtUtc,
                LastMessageContent = x.LastMessageContent,
                LastMessageType = x.LastMessageType
            })
            .ToListAsync(ct);

        var assignmentHistory = await _db.ConversationAssignmentHistory
            .AsNoTracking()
            .Where(x => x.CompanyId == ctx.CompanyId && x.ContactId == id)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Take(20)
            .Select(x => new ConversationAssignmentHistoryDto
            {
                ConversationAssignmentHistoryId = x.ConversationAssignmentHistoryId,
                ConversationId = x.ConversationId,
                ContactId = x.ContactId,
                PreviousAssignedTeamId = x.PreviousAssignedTeamId,
                PreviousAssignedTeamName = x.PreviousAssignedTeam != null ? x.PreviousAssignedTeam.Name : null,
                NewAssignedTeamId = x.NewAssignedTeamId,
                NewAssignedTeamName = x.NewAssignedTeam != null ? x.NewAssignedTeam.Name : null,
                PreviousAssignedUserId = x.PreviousAssignedUserId,
                PreviousAssignedUserName = x.PreviousAssignedUser != null ? x.PreviousAssignedUser.FullName : null,
                NewAssignedUserId = x.NewAssignedUserId,
                NewAssignedUserName = x.NewAssignedUser != null ? x.NewAssignedUser.FullName : null,
                PreviousOwnerUserId = x.PreviousOwnerUserId,
                PreviousOwnerUserName = x.PreviousOwnerUser != null ? x.PreviousOwnerUser.FullName : null,
                NewOwnerUserId = x.NewOwnerUserId,
                NewOwnerUserName = x.NewOwnerUser != null ? x.NewOwnerUser.FullName : null,
                ChangedByUserId = x.ChangedByUserId,
                ChangedByUserName = x.ChangedByUser != null ? x.ChangedByUser.FullName : null,
                AssignmentMode = x.AssignmentMode,
                Reason = x.Reason,
                Notes = x.Notes,
                ChangedAtUtc = x.ChangedAtUtc
            })
            .ToListAsync(ct);

        var profileHistory = await _db.ContactProfileHistory
            .AsNoTracking()
            .Where(x => x.CompanyId == ctx.CompanyId && x.ContactId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(50)
            .Select(x => new ContactProfileHistoryDto
            {
                ContactProfileHistoryId = x.ContactProfileHistoryId,
                ChangeType = x.ChangeType,
                FieldName = x.FieldName,
                PreviousValue = x.PreviousValue,
                NewValue = x.NewValue,
                Source = x.Source,
                Notes = x.Notes,
                ChangedByUserId = x.ChangedByUserId,
                ChangedByUserName = x.ChangedByUser != null ? x.ChangedByUser.FullName : null,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToListAsync(ct);

        var messageCount = await _db.Messages
            .AsNoTracking()
            .CountAsync(x => x.CompanyId == ctx.CompanyId && x.ContactId == id, ct);

        var profile = new ContactProfileDto
        {
            ContactId = contact.ContactId,
            CompanyId = contact.CompanyId,
            Name = contact.Name,
            PhoneNumber = contact.PhoneNumber,
            Email = contact.Email,
            Tags = contact.Tags,
            Source = contact.Source,
            Notes = contact.Notes,
            IsActive = contact.IsActive,
            FirstSeenAtUtc = contact.FirstSeenAtUtc,
            LastSeenAtUtc = contact.LastSeenAtUtc,
            LastInboundMessageAtUtc = contact.LastInboundMessageAtUtc,
            LastOutboundMessageAtUtc = contact.LastOutboundMessageAtUtc,
            CreatedAtUtc = contact.CreatedAtUtc,
            UpdatedAtUtc = contact.UpdatedAtUtc,
            Owner = contact.OwnerUser is null
                ? null
                : new ContactOwnerSummaryDto
                {
                    CompanyUserId = contact.OwnerUser.CompanyUserId,
                    FullName = contact.OwnerUser.FullName,
                    Email = contact.OwnerUser.Email,
                    IsActive = contact.OwnerUser.IsActive
                },
            ConversationCount = conversationCount,
            MessageCount = messageCount,
            RecentConversations = recentConversations,
            AssignmentHistory = assignmentHistory,
            ProfileHistory = profileHistory
        };

        return ToActionResult(ApiResponse<ContactProfileDto>.Ok(profile));
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

    [HttpPut("{id:long}/owner")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateContactOwner(long id, [FromBody] UpdateContactOwnerRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ContactsEdit)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        var contact = await _db.Contacts
            .FirstOrDefaultAsync(x => x.CompanyId == ctx.CompanyId && x.ContactId == id && x.IsActive, ct);
        if (contact is null)
            return ToActionResult(ApiResponse<object>.Fail("Contact not found.", System.Net.HttpStatusCode.NotFound));

        if (request.UserId.HasValue)
        {
            var eligibleUserIds = (await _routingService.GetEligibleUsersAsync(ctx.CompanyId, forAutoAssignment: false, ct))
                .Select(x => x.CompanyUserId)
                .ToHashSet();

            if (!eligibleUserIds.Contains(request.UserId.Value))
            {
                return ToActionResult(ApiResponse<object>.Fail("Selected user is not eligible for ownership.", System.Net.HttpStatusCode.BadRequest));
            }
        }

        var previousOwnerUserId = contact.OwnerUserId;

        contact.OwnerUserId = request.UserId;
        contact.OwnerAssignedAtUtc = request.UserId.HasValue ? DateTime.UtcNow : null;
        contact.UpdatedAtUtc = DateTime.UtcNow;

        if (previousOwnerUserId != request.UserId)
        {
            _db.ContactProfileHistory.Add(new ContactProfileHistory
            {
                CompanyId = ctx.CompanyId,
                ContactId = contact.ContactId,
                ChangedByUserId = ctx.UserId,
                ChangeType = "OWNER_UPDATED",
                FieldName = "owner_user_id",
                PreviousValue = previousOwnerUserId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                NewValue = request.UserId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Source = "contacts_owner_api",
                Notes = "Contact owner updated from profile.",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);

        return ToActionResult(ApiResponse<Contact>.Ok(contact, "Contact owner updated."));
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
