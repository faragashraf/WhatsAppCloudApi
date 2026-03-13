using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;
using ApiLogEntity = WhatsAppCloudApi.Domain.Entities.ApiLog;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/super-admin/logs")]
[Authorize]
public sealed class SuperAdminLogsController : ApiControllerBase
{
    private const int DefaultTake = 100;
    private const int MaxTake = 300;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;
    private const int CategorySampleSize = 5000;
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

        var logsQuery = _db.ApiLogs.AsNoTracking().AsQueryable();
        logsQuery = ApplyCompanyUserFilters(logsQuery, query.CompanyId, query.CompanyUserId);
        logsQuery = ApplyCategoryFilter(logsQuery, query.Category);
        logsQuery = ApplyResultFilter(logsQuery, query.Result);

        var useIncrementalMode = query.AfterId is > 0 && query.Page <= 0;
        if (useIncrementalMode)
        {
            var take = Math.Clamp(query.Take <= 0 ? DefaultTake : query.Take, 1, MaxTake);
            var afterId = query.AfterId.GetValueOrDefault();
            var incrementalQuery = logsQuery.Where(x => x.ApiLogId > afterId);
            var items = await BuildListItemsAsync(incrementalQuery, 0, take, ct);
            var latestId = items.Count == 0 ? afterId : items.Max(x => x.ApiLogId);

            return ToActionResult(ApiResponse<ApiLogFeedDto>.Ok(new ApiLogFeedDto
            {
                Items = items,
                LatestId = latestId,
                Page = 1,
                PageSize = take,
                TotalCount = items.Count,
                TotalPages = items.Count > 0 ? 1 : 0
            }));
        }

        if (query.AfterId is > 0)
        {
            logsQuery = logsQuery.Where(x => x.ApiLogId > query.AfterId.Value);
        }

        var pageSizeInput = query.PageSize > 0 ? query.PageSize : query.Take;
        var pageSize = Math.Clamp(pageSizeInput <= 0 ? DefaultPageSize : pageSizeInput, 1, MaxPageSize);
        var totalCount = await logsQuery.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var currentPage = totalPages == 0 ? 1 : Math.Clamp(query.Page <= 0 ? 1 : query.Page, 1, totalPages);
        var skip = (currentPage - 1) * pageSize;
        var itemsPage = await BuildListItemsAsync(logsQuery, skip, pageSize, ct);
        var latestIdPage = totalCount == 0
            ? query.AfterId.GetValueOrDefault()
            : await logsQuery.Select(x => (long?)x.ApiLogId).MaxAsync(ct) ?? 0;

        return ToActionResult(ApiResponse<ApiLogFeedDto>.Ok(new ApiLogFeedDto
        {
            Items = itemsPage,
            LatestId = latestIdPage,
            Page = currentPage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        }));
    }

    [HttpGet("api/categories")]
    public async Task<IActionResult> GetApiLogCategories([FromQuery] ApiLogCategoryQuery query, CancellationToken ct)
    {
        if (!await IsSuperAdminAsync(ct))
        {
            return Forbid();
        }

        var logsQuery = _db.ApiLogs.AsNoTracking().AsQueryable();
        logsQuery = ApplyCompanyUserFilters(logsQuery, query.CompanyId, query.CompanyUserId);
        logsQuery = ApplyResultFilter(logsQuery, query.Result);

        var endpoints = await logsQuery
            .OrderByDescending(x => x.ApiLogId)
            .Select(x => x.Endpoint)
            .Take(CategorySampleSize)
            .ToListAsync(ct);

        var categories = endpoints
            .Select(ResolveCategoryFromEndpoint)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ToActionResult(ApiResponse<ApiLogCategoriesDto>.Ok(new ApiLogCategoriesDto
        {
            Items = categories
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

    private static IQueryable<ApiLogEntity> ApplyCompanyUserFilters(
        IQueryable<ApiLogEntity> query,
        int? companyId,
        int? companyUserId)
    {
        if (companyId is > 0)
        {
            query = query.Where(x => x.CompanyId == companyId);
        }

        if (companyUserId is > 0)
        {
            query = query.Where(x => x.CompanyUserId == companyUserId);
        }

        return query;
    }

    private static IQueryable<ApiLogEntity> ApplyCategoryFilter(
        IQueryable<ApiLogEntity> query,
        string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return query;
        }

        var normalized = category.Trim().ToLowerInvariant();
        if (normalized is "all" or "*")
        {
            return query;
        }

        if (normalized == "other")
        {
            return query.Where(x => !x.Endpoint.StartsWith("/api/"));
        }

        var prefix = $"/api/{normalized}";
        return query.Where(x => x.Endpoint.StartsWith(prefix));
    }

    private static IQueryable<ApiLogEntity> ApplyResultFilter(
        IQueryable<ApiLogEntity> query,
        string? result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return query;
        }

        return result.Trim().ToLowerInvariant() switch
        {
            "success" => query.Where(x => x.StatusCode >= 200 && x.StatusCode < 300),
            "non_success" => query.Where(x => x.StatusCode < 200 || x.StatusCode >= 300),
            _ => query
        };
    }

    private async Task<List<ApiLogListItemDto>> BuildListItemsAsync(
        IQueryable<ApiLogEntity> logsQuery,
        int skip,
        int take,
        CancellationToken ct)
    {
        var items = await logsQuery
            .OrderByDescending(x => x.ApiLogId)
            .Skip(skip)
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

        foreach (var item in items)
        {
            item.Category = ResolveCategoryFromEndpoint(item.Endpoint);
        }

        return items;
    }

    private static string ResolveCategoryFromEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return "other";
        }

        var path = endpoint.Trim();
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            return "other";
        }

        var remainder = path[5..];
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return "other";
        }

        var slashIndex = remainder.IndexOf('/');
        var queryIndex = remainder.IndexOf('?');
        var endIndex = remainder.Length;
        if (slashIndex >= 0)
        {
            endIndex = Math.Min(endIndex, slashIndex);
        }

        if (queryIndex >= 0)
        {
            endIndex = Math.Min(endIndex, queryIndex);
        }

        if (endIndex <= 0)
        {
            return "other";
        }

        return remainder[..endIndex].Trim().ToLowerInvariant();
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
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = DefaultPageSize;
        public long? AfterId { get; set; }
        public int? CompanyId { get; set; }
        public int? CompanyUserId { get; set; }
        public string? Category { get; set; }
        public string? Result { get; set; }
    }

    public sealed class ApiLogCategoryQuery
    {
        public int? CompanyId { get; set; }
        public int? CompanyUserId { get; set; }
        public string? Result { get; set; }
    }

    public sealed class ApiLogCategoriesDto
    {
        public IReadOnlyList<string> Items { get; set; } = [];
    }

    public sealed class ApiLogFeedDto
    {
        public long LatestId { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
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
        public string Category { get; set; } = "other";
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
