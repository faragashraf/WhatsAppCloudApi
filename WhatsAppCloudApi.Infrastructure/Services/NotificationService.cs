using System.Net;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db) => _db = db;

    public async Task<ApiResponse<PagedResult<Notification>>> GetNotificationsAsync(int companyId, NotificationQueryParams query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var q = _db.Notifications.Where(n => n.CompanyId == companyId);

        if (query.IsRead.HasValue)
            q = q.Where(n => n.IsRead == query.IsRead.Value);

        if (!string.IsNullOrWhiteSpace(query.Category))
            q = q.Where(n => n.Category == query.Category);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<Notification>>.Ok(new PagedResult<Notification>
        {
            Items = items, TotalCount = total, Page = page, PageSize = pageSize
        });
    }

    public async Task<ApiResponse<int>> GetUnreadCountAsync(int companyId, CancellationToken ct)
    {
        var count = await _db.Notifications.CountAsync(n => n.CompanyId == companyId && !n.IsRead, ct);
        return ApiResponse<int>.Ok(count);
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(int companyId, long notificationId, CancellationToken ct)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.CompanyId == companyId && n.NotificationId == notificationId, ct);
        if (notification is null)
            return ApiResponse<bool>.Fail("Notification not found", HttpStatusCode.NotFound);

        notification.IsRead = true;
        notification.ReadAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> MarkAllAsReadAsync(int companyId, CancellationToken ct)
    {
        await _db.Notifications
            .Where(n => n.CompanyId == companyId && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAtUtc, DateTime.UtcNow), ct);

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<Notification>> CreateNotificationAsync(int companyId, CreateNotificationRequest request, CancellationToken ct)
    {
        if (request.TargetUserId.HasValue)
        {
            var targetUserExists = await _db.CompanyUsers
                .AsNoTracking()
                .AnyAsync(u => u.CompanyUserId == request.TargetUserId.Value && u.CompanyId == companyId && u.IsActive, ct);

            if (!targetUserExists)
            {
                return ApiResponse<Notification>.Fail("Target user is invalid for this company.", HttpStatusCode.BadRequest);
            }
        }

        var notification = new Notification
        {
            CompanyId = companyId,
            CompanyUserId = request.TargetUserId,
            Type = request.Type,
            Title = request.Title,
            Body = request.Body,
            Category = request.Category,
            MetadataJson = request.MetadataJson,
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<Notification>.Ok(notification);
    }

    public async Task<ApiResponse<bool>> DeleteNotificationAsync(int companyId, long notificationId, CancellationToken ct)
    {
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.CompanyId == companyId && n.NotificationId == notificationId, ct);
        if (notification is null)
            return ApiResponse<bool>.Fail("Notification not found", HttpStatusCode.NotFound);

        _db.Notifications.Remove(notification);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true);
    }
}
