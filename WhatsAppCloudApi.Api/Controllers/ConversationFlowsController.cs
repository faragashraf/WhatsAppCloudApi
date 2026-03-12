using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/conversation-flows")]
[Authorize]
public sealed class ConversationFlowsController : ApiControllerBase
{
    private readonly IConversationFlowService _conversationFlowService;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ApplicationDbContext _db;
    private readonly ICustomerConversationResolver _resolver;

    public ConversationFlowsController(
        IConversationFlowService conversationFlowService,
        ITenantContextAccessor tenantContext,
        ApplicationDbContext db,
        ICustomerConversationResolver resolver)
    {
        _conversationFlowService = conversationFlowService;
        _tenantContext = tenantContext;
        _db = db;
        _resolver = resolver;
    }

    [HttpGet]
    public async Task<IActionResult> GetFlows(CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationView)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        return ToActionResult(await _conversationFlowService.GetFlowsAsync(ctx.CompanyId, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetFlow(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationView)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        return ToActionResult(await _conversationFlowService.GetFlowByIdAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost]
    public async Task<IActionResult> CreateFlow([FromBody] ConversationFlowUpsertRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationCreate)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        return ToActionResult(await _conversationFlowService.CreateFlowAsync(ctx.CompanyId, request, ct));
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> UpdateFlow(long id, [FromBody] ConversationFlowUpsertRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationEdit)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        return ToActionResult(await _conversationFlowService.UpdateFlowAsync(ctx.CompanyId, id, request, ct));
    }

    [HttpPost("{id:long}/publish")]
    public async Task<IActionResult> PublishFlow(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationEdit)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        return ToActionResult(await _conversationFlowService.PublishFlowAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost("{id:long}/toggle")]
    public async Task<IActionResult> ToggleFlow(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationEdit)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        return ToActionResult(await _conversationFlowService.ToggleFlowAsync(ctx.CompanyId, id, ct));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteFlow(long id, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationDelete)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        return ToActionResult(await _conversationFlowService.DeleteFlowAsync(ctx.CompanyId, id, ct));
    }

    [HttpPost("{id:long}/simulate")]
    public async Task<IActionResult> SimulateFlow(long id, [FromBody] ConversationFlowSimulationRequest request, CancellationToken ct)
    {
        var ctx = _tenantContext.GetRequiredContext();
        var perms = await GetPermissions(ctx.CompanyId, ctx.UserId, ctx.Role, ct);
        if (!perms.AutomationView)
        {
            return ToActionResult(ApiResponse<object>.Fail("Access denied.", System.Net.HttpStatusCode.Forbidden));
        }

        var exists = await _db.ConversationFlows
            .AsNoTracking()
            .AnyAsync(x => x.CompanyId == ctx.CompanyId && x.ConversationFlowId == id, ct);
        if (!exists)
        {
            return ToActionResult(ApiResponse<object>.Fail("Flow not found.", System.Net.HttpStatusCode.NotFound));
        }

        var resolved = await _resolver.ResolveAsync(
            ctx.CompanyId,
            request.ContactNumber,
            request.WhatsAppPhoneNumberId,
            request.ContactName,
            source: "flow_simulation",
            cancellationToken: ct);

        var runtime = await _conversationFlowService.SimulateFlowAsync(ctx.CompanyId, id, resolved.Conversation, resolved.Contact, new Domain.Models.FlowInboundMessage
        {
            MessageType = request.MessageType,
            Text = request.Content,
            SelectionId = request.SelectionId,
            SelectionTitle = request.SelectionTitle,
            IsDryRun = request.DryRun
        }, request.UsePublishedVersion, ct);

        return ToActionResult(ApiResponse<ConversationFlowSimulationResultDto>.Ok(new ConversationFlowSimulationResultDto
        {
            ConversationId = resolved.Conversation.ConversationId,
            ContactId = resolved.Contact.ContactId,
            Handled = runtime.Handled,
            StartedNewSession = runtime.StartedNewSession,
            SessionId = runtime.SessionId,
            SessionStatus = runtime.SessionStatus,
            CurrentNodeId = runtime.CurrentNodeId,
            Variables = runtime.Variables.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase),
            Actions = runtime.Actions.Select(action => new ConversationFlowSimulationActionDto
            {
                NodeId = action.NodeId,
                ActionType = action.ActionType,
                Preview = action.Preview,
                MetadataJson = action.MetadataJson
            }).ToList()
        }));
    }

    private async Task<Domain.Models.UserPermissions> GetPermissions(int companyId, int userId, string role, CancellationToken ct)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Domain.Models.UserPermissions.FullAccess();
        }

        var user = await _db.CompanyUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CompanyUserId == userId && u.CompanyId == companyId && u.IsActive, ct);
        return user?.EffectivePermissions ?? Domain.Models.UserPermissions.MemberDefault();
    }
}
