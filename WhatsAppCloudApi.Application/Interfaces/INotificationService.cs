using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface INotificationService
{
    Task<ApiResponse<PagedResult<Notification>>> GetNotificationsAsync(int companyId, NotificationQueryParams query, CancellationToken ct = default);
    Task<ApiResponse<int>> GetUnreadCountAsync(int companyId, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkAsReadAsync(int companyId, long notificationId, CancellationToken ct = default);
    Task<ApiResponse<bool>> MarkAllAsReadAsync(int companyId, CancellationToken ct = default);
    Task<ApiResponse<Notification>> CreateNotificationAsync(int companyId, CreateNotificationRequest request, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteNotificationAsync(int companyId, long notificationId, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteAllNotificationsAsync(int companyId, bool? isRead = null, CancellationToken ct = default);
}
