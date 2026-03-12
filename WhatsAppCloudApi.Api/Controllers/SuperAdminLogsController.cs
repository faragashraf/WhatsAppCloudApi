using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/super-admin/logs")]
[Authorize]
public sealed class SuperAdminLogsController : ApiControllerBase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 300;
    private const int PreviewLength = 220;

    private readonly ApplicationDbContext _db;

    public SuperAdminLogsController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet("api")]
    public async Task<IActionResult> GetApiLogs([FromQuery] ApiLogQuery query, CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct))
        {
            return Forbid();
        }

        var take = Math.Clamp(query.Take <= 0 ? DefaultTake : query.Take, 1, MaxTake);
        var logsQuery = _db.ApiLogs.AsNoTracking().AsQueryable();

        if (query.CompanyId is > 0)
        {
            logsQuery = logsQuery.Where(x => x.CompanyId == query.CompanyId);
        }

        if (query.CompanyUserId is > 0)
        {
            logsQuery = logsQuery.Where(x => x.CompanyUserId == query.CompanyUserId);
        }

        if (query.AfterId is > 0)
        {
            logsQuery = logsQuery.Where(x => x.ApiLogId > query.AfterId.Value);
        }

        var items = await logsQuery
            .OrderByDescending(x => x.ApiLogId)
            .Take(take)
            .Select(x => new ApiLogListItemDto
            {
                ApiLogId = x.ApiLogId,
                CompanyId = x.CompanyId,
                CompanyName = x.Company != null ? x.Company.CompanyName : null,
                CompanyUserId = x.CompanyUserId,
                CompanyUserName = x.CompanyUser != null ? x.CompanyUser.FullName : null,
                CompanyUserEmail = x.CompanyUser != null ? x.CompanyUser.Email : null,
                Endpoint = x.Endpoint,
                HttpMethod = x.HttpMethod,
                StatusCode = x.StatusCode,
                IpAddress = x.IpAddress,
                CreatedAtUtc = x.CreatedAtUtc,
                RequestPreview = Truncate(x.RequestBody, PreviewLength),
                ResponsePreview = Truncate(x.ResponseBody, PreviewLength)
            })
            .ToListAsync(ct);

        var latestId = items.Count == 0
            ? query.AfterId.GetValueOrDefault()
            : items.Max(x => x.ApiLogId);

        return ToActionResult(ApiResponse<ApiLogFeedDto>.Ok(new ApiLogFeedDto
        {
            Items = items,
            LatestId = latestId
        }));
    }

    [HttpGet("api/{apiLogId:long}")]
    public async Task<IActionResult> GetApiLog(long apiLogId, CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct))
        {
            return Forbid();
        }

        var log = await _db.ApiLogs
            .AsNoTracking()
            .Where(x => x.ApiLogId == apiLogId)
            .Select(x => new ApiLogDetailsDto
            {
                ApiLogId = x.ApiLogId,
                CompanyId = x.CompanyId,
                CompanyName = x.Company != null ? x.Company.CompanyName : null,
                CompanyUserId = x.CompanyUserId,
                CompanyUserName = x.CompanyUser != null ? x.CompanyUser.FullName : null,
                CompanyUserEmail = x.CompanyUser != null ? x.CompanyUser.Email : null,
                Endpoint = x.Endpoint,
                HttpMethod = x.HttpMethod,
                StatusCode = x.StatusCode,
                IpAddress = x.IpAddress,
                CreatedAtUtc = x.CreatedAtUtc,
                RequestBody = x.RequestBody,
                ResponseBody = x.ResponseBody
            })
            .FirstOrDefaultAsync(ct);

        if (log is null)
        {
            return ToActionResult(ApiResponse<ApiLogDetailsDto>.Fail("Log was not found.", System.Net.HttpStatusCode.NotFound));
        }

        return ToActionResult(ApiResponse<ApiLogDetailsDto>.Ok(log));
    }

    [HttpGet("company-users")]
    public async Task<IActionResult> GetCompanyUsers([FromQuery] int companyId, CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct))
        {
            return Forbid();
        }

        if (companyId <= 0)
        {
            return ToActionResult(ApiResponse<IReadOnlyList<CompanyUserOptionDto>>.Ok([]));
        }

        var users = await _db.CompanyUsers
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .OrderBy(x => x.FullName)
            .Select(x => new CompanyUserOptionDto
            {
                CompanyUserId = x.CompanyUserId,
                FullName = x.FullName,
                Email = x.Email,
                IsActive = x.IsActive
            })
            .ToListAsync(ct);

        return ToActionResult(ApiResponse<IReadOnlyList<CompanyUserOptionDto>>.Ok(users));
    }

    private async Task<bool> IsSuperAdminAsync(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue("UserId");
        if (!int.TryParse(userIdClaim, out var userId) || userId <= 0)
        {
            return false;
        }

        return await _db.CompanyUsers.AnyAsync(
            u => u.CompanyUserId == userId && u.IsActive && u.IsSuperAdmin,
            ct);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    public sealed class ApiLogQuery
    {
        public int Take { get; set; } = DefaultTake;
        public long? AfterId { get; set; }
        public int? CompanyId { get; set; }
        public int? CompanyUserId { get; set; }
    }

    public sealed class ApiLogFeedDto
    {
        public long LatestId { get; set; }
        public IReadOnlyList<ApiLogListItemDto> Items { get; set; } = [];
    }

    public sealed class ApiLogListItemDto
    {
        public long ApiLogId { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public int? CompanyUserId { get; set; }
        public string? CompanyUserName { get; set; }
        public string? CompanyUserEmail { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? RequestPreview { get; set; }
        public string? ResponsePreview { get; set; }
    }

    public sealed class ApiLogDetailsDto
    {
        public long ApiLogId { get; set; }
        public int? CompanyId { get; set; }
        public string? CompanyName { get; set; }
        public int? CompanyUserId { get; set; }
        public string? CompanyUserName { get; set; }
        public string? CompanyUserEmail { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? RequestBody { get; set; }
        public string? ResponseBody { get; set; }
    }

    public sealed class CompanyUserOptionDto
    {
        public int CompanyUserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
