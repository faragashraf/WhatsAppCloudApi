using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ITenantContextAccessor _tenantContext;

    public NotificationsController(INotificationService notificationService, ITenantContextAccessor tenantContext)
    {
        _notificationService = notificationService;
        _tenantContext = tenantContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] NotificationQueryParams query, CancellationToken ct)
        => ToActionResult(await _notificationService.GetNotificationsAsync(_tenantContext.GetRequiredContext().CompanyId, query, ct));

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
        => ToActionResult(await _notificationService.GetUnreadCountAsync(_tenantContext.GetRequiredContext().CompanyId, ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken ct)
        => ToActionResult(await _notificationService.MarkAsReadAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct)
        => ToActionResult(await _notificationService.MarkAllAsReadAsync(_tenantContext.GetRequiredContext().CompanyId, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteNotification(long id, CancellationToken ct)
        => ToActionResult(await _notificationService.DeleteNotificationAsync(_tenantContext.GetRequiredContext().CompanyId, id, ct));
}
