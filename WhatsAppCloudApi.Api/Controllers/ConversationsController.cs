using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController : ApiControllerBase
{
    private readonly IConversationService _conversationService;
    private readonly ITenantContextAccessor _tenantContext;

    public ConversationsController(IConversationService conversationService, ITenantContextAccessor tenantContext)
    {
        _conversationService = conversationService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations([FromQuery] ConversationQueryParams query, CancellationToken ct)
        => ToActionResult(await _conversationService.GetConversationsAsync(_tenantContext.GetRequiredContext().CompanyId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetConversation(long id, CancellationToken ct)
        => ToActionResult(await _conversationService.GetConversationByIdAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpGet("{id:long}/messages")]
    public async Task<IActionResult> GetMessages(long id, [FromQuery] ConversationMessageQueryParams query, CancellationToken ct)
        => ToActionResult(await _conversationService.GetMessagesAsync(_tenantContext.GetRequiredContext().CompanyId, id, query, ct));

    [HttpPost("{id:long}/messages")]
    public async Task<IActionResult> SendMessage(long id, [FromBody] SendConversationMessageRequest request, CancellationToken ct)
        => ToActionResult(await _conversationService.SendMessageAsync(_tenantContext.GetRequiredContext().CompanyId, id, request, ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken ct)
        => ToActionResult(await _conversationService.MarkAsReadAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpPut("{id:long}/assign")]
    public async Task<IActionResult> AssignConversation(long id, [FromBody] AssignConversationRequest request, CancellationToken ct)
        => ToActionResult(await _conversationService.AssignConversationAsync(_tenantContext.GetRequiredContext().CompanyId, id, request.UserId, ct));

    [HttpDelete("{id:long}/assign")]
    public async Task<IActionResult> UnassignConversation(long id, CancellationToken ct)
        => ToActionResult(await _conversationService.UnassignConversationAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));
}
