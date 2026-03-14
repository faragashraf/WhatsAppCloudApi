using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController : ApiControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ApplicationDbContext _db;

    public ConversationsController(
        IConversationService conversationService,
        ITenantContextAccessor tenantContext,
        ApplicationDbContext db)
    {
        _conversationService = conversationService;
        _tenantContext = tenantContext;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations([FromQuery] ConversationQueryParams query, CancellationToken ct = default)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.GetConversationsAsync(ctx.CompanyId, query, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetConversation(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.GetConversationByIdAsync(ctx.CompanyId, id, ct));
    }

    [HttpGet("{id:long}/messages")]
    public async Task<IActionResult> GetMessages(long id, [FromQuery] ConversationMessageQueryParams query, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.GetMessagesAsync(ctx.CompanyId, id, query, ct));
    }

    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> SendMessage(long id, [FromBody] SendConversationMessageRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsSend)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        return ToActionResult(await _conversationService.SendMessageAsync(ctx.CompanyId, id, request, ctx.UserId, ctx.Role, ct));
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.MarkAsReadAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost("{id:long}/typing-indicator")]
    public async Task<IActionResult> SendTypingIndicator(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsSend)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.SendTypingIndicatorAsync(ctx.CompanyId, id, ctx.UserId, ctx.Role, ct));
    }

    /// <summary>Admin-only: assign a conversation to a specific user.</summary>
    [HttpPut("{id:long}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignConversation(long id, [FromBody] AssignConversationRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsAssign)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.AssignConversationAsync(
            ctx.CompanyId,
            id,
            request.UserId,
            changedByUserId: ctx.UserId,
            updateContactOwner: request.UpdateContactOwner,
            reason: request.Reason,
            ct: ct));
    }

    /// <summary>Admin-only: assign a conversation to a routing team, with optional immediate user distribution.</summary>
    [HttpPut("{id:long}/assign-team")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignConversationToTeam(long id, [FromBody] AssignConversationToTeamRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsAssign)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.AssignConversationToTeamAsync(
            ctx.CompanyId,
            id,
            request.TeamId,
            userId: request.UserId,
            autoDistributeToTeamMember: request.AutoDistributeToTeamMember,
            changedByUserId: ctx.UserId,
            updateContactOwner: request.UpdateContactOwner,
            reason: request.Reason,
            ct: ct));
    }

    /// <summary>Admin-only: unassign a conversation.</summary>
    [HttpDelete("{id:long}/assign")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UnassignConversation(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsAssign)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.UnassignConversationAsync(ctx.CompanyId, id, ctx.UserId, ct));
    }

    /// <summary>Any user: pick an unassigned conversation (self-assign).</summary>
    [HttpPost("{id:long}/pick")]
    public async Task<IActionResult> PickConversation(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsAssign)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.PickConversationAsync(ctx.CompanyId, id, ctx.UserId, reason: "SELF_PICK", ct: ct));
    }

    [HttpGet("{id:long}/assignment-history")]
    public async Task<IActionResult> GetAssignmentHistory(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsView)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        var exists = await _db.Conversations
            .AsNoTracking()
            .AnyAsync(x => x.CompanyId == ctx.CompanyId && x.ConversationId == id, ct);
        if (!exists)
            return ToActionResult(ApiResponse<object>.Fail("Conversation not found.", System.Net.HttpStatusCode.NotFound));

        var items = await _db.ConversationAssignmentHistory
            .AsNoTracking()
            .Where(x => x.CompanyId == ctx.CompanyId && x.ConversationId == id)
            .OrderByDescending(x => x.ChangedAtUtc)
            .Take(100)
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

        return ToActionResult(ApiResponse<List<ConversationAssignmentHistoryDto>>.Ok(items));
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
