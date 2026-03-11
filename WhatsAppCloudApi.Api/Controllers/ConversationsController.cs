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

        return ToActionResult(await _conversationService.AssignConversationAsync(ctx.CompanyId, id, request.UserId, ct));
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

        return ToActionResult(await _conversationService.UnassignConversationAsync(ctx.CompanyId, id, ct));
    }

    /// <summary>Any user: pick an unassigned conversation (self-assign).</summary>
    [HttpPost("{id:long}/pick")]
    public async Task<IActionResult> PickConversation(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.ConversationsAssign)
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));

        return ToActionResult(await _conversationService.PickConversationAsync(ctx.CompanyId, id, ctx.UserId, ct));
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
