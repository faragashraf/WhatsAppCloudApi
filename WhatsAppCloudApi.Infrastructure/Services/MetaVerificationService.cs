using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class MetaVerificationService : IMetaVerificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly ILogger<MetaVerificationService> _logger;
    private readonly WhatsAppOptions _options;

    public MetaVerificationService(
        ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<MetaVerificationService> logger,
        IOptions<WhatsAppOptions> options)
    {
        _dbContext = dbContext;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ConnectMetaResponse> ConnectBusinessAccountAsync(
        int companyId,
        ConnectMetaRequest request,
        string webhookBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var businessAccountId = request.BusinessAccountId.Trim();
        var accessToken = request.AccessToken.Trim();

        // ── Phase 3: Verify Business Account ──
        _logger.LogInformation("Verifying business account {BusinessAccountId} for company {CompanyId}",
            businessAccountId, companyId);

        JsonElement? wabaInfo;
        try
        {
            wabaInfo = await CallGraphApiAsync(
                $"{baseUrl}/{businessAccountId}?fields=id,name,account_review_status,on_behalf_of_business_info,ownership_type",
                accessToken, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error verifying business account {BusinessAccountId}", businessAccountId);
            return new ConnectMetaResponse
            {
                Status = "Error",
                ErrorMessage = "Could not reach Meta API. Please check your network connection."
            };
        }

        if (wabaInfo is null)
        {
            return new ConnectMetaResponse
            {
                Status = "InvalidToken",
                ErrorMessage = "The access token provided is invalid or does not have the required permissions."
            };
        }

        var wabaRoot = wabaInfo.Value;

        // Check if the response contains an error
        if (wabaRoot.TryGetProperty("error", out var errorElement))
        {
            var errorCode = errorElement.TryGetProperty("code", out var code) ? code.GetInt32() : 0;
            var errorMsg = errorElement.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";

            if (errorCode == 190 || errorCode == 102)
            {
                return new ConnectMetaResponse { Status = "InvalidToken", ErrorMessage = errorMsg };
            }
            if (errorCode == 100)
            {
                return new ConnectMetaResponse { Status = "AccountNotFound", ErrorMessage = errorMsg };
            }
            if (errorCode == 10 || errorCode == 200 || errorCode == 4)
            {
                return new ConnectMetaResponse { Status = "PermissionDenied", ErrorMessage = errorMsg };
            }

            return new ConnectMetaResponse { Status = "Error", ErrorMessage = errorMsg };
        }

        var businessName = wabaRoot.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "Unknown";

        // ── Phase 2: Securely Store Credentials ──
        // Create or update MetaBusinessAccount
        var metaBa = await _dbContext.MetaBusinessAccounts
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BusinessId == businessAccountId, cancellationToken);

        if (metaBa is null)
        {
            metaBa = new MetaBusinessAccount
            {
                CompanyId = companyId,
                BusinessId = businessAccountId,
                Name = businessName ?? "Unknown",
                AccessToken = accessToken,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.MetaBusinessAccounts.Add(metaBa);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            metaBa.Name = businessName ?? metaBa.Name;
            metaBa.AccessToken = accessToken;
            metaBa.IsActive = true;
            metaBa.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Create or update WhatsAppAccount
        var waAccount = await _dbContext.WhatsAppAccounts
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BusinessAccountId == businessAccountId, cancellationToken);

        if (waAccount is null)
        {
            waAccount = new WhatsAppAccount
            {
                WhatsAppAccountId = businessAccountId,
                CompanyId = companyId,
                MetaBusinessAccountId = metaBa.MetaBusinessAccountId,
                BusinessAccountId = businessAccountId,
                Name = businessName ?? "WhatsApp Business",
                AccessToken = accessToken,
                VerifyToken = GenerateVerifyToken(),
                IsDefault = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.WhatsAppAccounts.Add(waAccount);
        }
        else
        {
            waAccount.MetaBusinessAccountId = metaBa.MetaBusinessAccountId;
            waAccount.Name = businessName ?? waAccount.Name;
            waAccount.AccessToken = accessToken;
            waAccount.IsActive = true;
            waAccount.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── Phase 4: Retrieve Phone Numbers ──
        var phoneNumbers = new List<MetaPhoneNumberInfo>();
        try
        {
            var phonesJson = await CallGraphApiAsync(
                $"{baseUrl}/{businessAccountId}/phone_numbers?fields=id,display_phone_number,verified_name,code_verification_status,quality_rating,platform_type,throughput,last_onboarded_time",
                accessToken, cancellationToken);

            if (phonesJson.HasValue && phonesJson.Value.TryGetProperty("data", out var dataArray)
                && dataArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var phone in dataArray.EnumerateArray())
                {
                    var info = new MetaPhoneNumberInfo
                    {
                        PhoneNumberId = phone.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        DisplayPhoneNumber = phone.TryGetProperty("display_phone_number", out var dp) ? dp.GetString() ?? "" : "",
                        VerifiedName = phone.TryGetProperty("verified_name", out var vn) ? vn.GetString() : null,
                        CodeVerificationStatus = phone.TryGetProperty("code_verification_status", out var cvs) ? cvs.GetString() : null,
                        QualityRating = phone.TryGetProperty("quality_rating", out var qr) ? qr.GetString() : null,
                        PlatformType = phone.TryGetProperty("platform_type", out var pt) ? pt.GetString() : null,
                        LastOnboardedTime = phone.TryGetProperty("last_onboarded_time", out var lot) ? lot.GetString() : null,
                    };

                    // Extract throughput level from nested object
                    if (phone.TryGetProperty("throughput", out var throughput) && throughput.ValueKind == JsonValueKind.Object)
                    {
                        info.ThroughputLevel = throughput.TryGetProperty("level", out var level) ? level.GetString() : null;
                    }

                    phoneNumbers.Add(info);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve phone numbers for WABA {BusinessAccountId}", businessAccountId);
        }

        // ── Phase 5: Sync Phone Numbers Locally ──
        var syncResult = await SyncPhoneNumbersInternalAsync(companyId, waAccount.WhatsAppAccountId, phoneNumbers, cancellationToken);

        // ── Phase 6: Webhook URL ──
        var webhookUrl = webhookBaseUrl.TrimEnd('/') + "/api/webhook";

        return new ConnectMetaResponse
        {
            Status = "Connected",
            BusinessAccountId = businessAccountId,
            BusinessAccountName = businessName,
            PhoneNumbersImported = syncResult.Total,
            WebhookConfigured = true,
            WebhookUrl = webhookUrl,
            LastSyncUtc = DateTime.UtcNow,
            PhoneNumbers = phoneNumbers
        };
    }

    public async Task<WhatsAppConnectionStatus> VerifyConnectionAsync(
        int companyId,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');

        // Find the company's default WhatsApp account
        var waAccount = await _dbContext.WhatsAppAccounts
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.IsActive)
            .OrderByDescending(x => x.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        if (waAccount is null)
        {
            return new WhatsAppConnectionStatus
            {
                IsConnected = false,
                ConnectionStatus = "Disconnected"
            };
        }

        // Verify token is still valid by calling Meta API
        JsonElement? wabaInfo;
        try
        {
            wabaInfo = await CallGraphApiAsync(
                $"{baseUrl}/{waAccount.BusinessAccountId}?fields=id,name",
                waAccount.AccessToken, cancellationToken);
        }
        catch
        {
            return new WhatsAppConnectionStatus
            {
                IsConnected = false,
                ConnectionStatus = "Error",
                BusinessAccountId = waAccount.BusinessAccountId,
                TokenValid = false
            };
        }

        if (wabaInfo is null || wabaInfo.Value.TryGetProperty("error", out _))
        {
            return new WhatsAppConnectionStatus
            {
                IsConnected = false,
                ConnectionStatus = "InvalidToken",
                BusinessAccountId = waAccount.BusinessAccountId,
                TokenValid = false
            };
        }

        var businessName = wabaInfo.Value.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : waAccount.Name;

        // Refresh phone numbers
        var phoneNumbers = new List<MetaPhoneNumberInfo>();
        try
        {
            var phonesJson = await CallGraphApiAsync(
                $"{baseUrl}/{waAccount.BusinessAccountId}/phone_numbers?fields=id,display_phone_number,verified_name,code_verification_status,quality_rating,platform_type,throughput,last_onboarded_time",
                waAccount.AccessToken, cancellationToken);

            if (phonesJson.HasValue && phonesJson.Value.TryGetProperty("data", out var dataArray)
                && dataArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var phone in dataArray.EnumerateArray())
                {
                    var info = new MetaPhoneNumberInfo
                    {
                        PhoneNumberId = phone.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                        DisplayPhoneNumber = phone.TryGetProperty("display_phone_number", out var dp) ? dp.GetString() ?? "" : "",
                        VerifiedName = phone.TryGetProperty("verified_name", out var vn) ? vn.GetString() : null,
                        CodeVerificationStatus = phone.TryGetProperty("code_verification_status", out var cvs) ? cvs.GetString() : null,
                        QualityRating = phone.TryGetProperty("quality_rating", out var qr) ? qr.GetString() : null,
                        PlatformType = phone.TryGetProperty("platform_type", out var pt) ? pt.GetString() : null,
                        LastOnboardedTime = phone.TryGetProperty("last_onboarded_time", out var lot) ? lot.GetString() : null,
                    };

                    if (phone.TryGetProperty("throughput", out var throughput) && throughput.ValueKind == JsonValueKind.Object)
                    {
                        info.ThroughputLevel = throughput.TryGetProperty("level", out var level) ? level.GetString() : null;
                    }

                    phoneNumbers.Add(info);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh phone numbers for WABA {BusinessAccountId}", waAccount.BusinessAccountId);
        }

        // Sync any new numbers
        await SyncPhoneNumbersInternalAsync(companyId, waAccount.WhatsAppAccountId, phoneNumbers, cancellationToken);

        var localPhoneCount = await _dbContext.WhatsAppPhoneNumbers
            .CountAsync(x => x.CompanyId == companyId && x.WhatsAppAccountId == waAccount.WhatsAppAccountId, cancellationToken);

        var lastSync = await _dbContext.WhatsAppPhoneNumbers
            .Where(x => x.CompanyId == companyId)
            .MaxAsync(x => (DateTime?)x.LastSyncUtc, cancellationToken);

        return new WhatsAppConnectionStatus
        {
            IsConnected = true,
            BusinessAccountId = waAccount.BusinessAccountId,
            BusinessAccountName = businessName,
            ConnectionStatus = "Connected",
            PhoneNumberCount = localPhoneCount,
            LastSyncUtc = lastSync ?? DateTime.UtcNow,
            TokenValid = true,
            WebhookUrl = "/api/webhook",
            PhoneNumbers = phoneNumbers
        };
    }

    public async Task<PhoneNumberSyncResponse> SyncPhoneNumbersAsync(
        int companyId,
        PhoneNumberSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        var waAccount = await _dbContext.WhatsAppAccounts
            .FirstOrDefaultAsync(x => x.CompanyId == companyId && x.BusinessAccountId == request.BusinessAccountId.Trim(), cancellationToken);

        if (waAccount is null)
        {
            return new PhoneNumberSyncResponse();
        }

        var phoneInfos = request.PhoneNumbers.Select(p => new MetaPhoneNumberInfo
        {
            PhoneNumberId = p.PhoneNumberId,
            DisplayPhoneNumber = p.DisplayPhoneNumber,
            VerifiedName = p.VerifiedName,
            CodeVerificationStatus = p.CodeVerificationStatus,
            QualityRating = p.QualityRating,
            PlatformType = p.PlatformType,
            ThroughputLevel = p.ThroughputLevel,
            LastOnboardedTime = p.LastOnboardedTime,
        }).ToList();

        return await SyncPhoneNumbersInternalAsync(companyId, waAccount.WhatsAppAccountId, phoneInfos, cancellationToken);
    }

    // ── Private Helpers ──

    private async Task<PhoneNumberSyncResponse> SyncPhoneNumbersInternalAsync(
        int companyId,
        string whatsAppAccountId,
        List<MetaPhoneNumberInfo> phoneNumbers,
        CancellationToken cancellationToken)
    {
        var result = new PhoneNumberSyncResponse { Total = phoneNumbers.Count };

        var existingNumbers = await _dbContext.WhatsAppPhoneNumbers
            .Where(x => x.CompanyId == companyId && x.WhatsAppAccountId == whatsAppAccountId)
            .ToListAsync(cancellationToken);

        foreach (var phone in phoneNumbers)
        {
            var existing = existingNumbers.FirstOrDefault(x => x.PhoneNumberId == phone.PhoneNumberId);

            if (existing is not null)
            {
                // Update
                existing.DisplayPhoneNumber = phone.DisplayPhoneNumber;
                existing.VerifiedName = phone.VerifiedName;
                existing.CodeVerificationStatus = phone.CodeVerificationStatus;
                existing.QualityRating = phone.QualityRating;
                existing.PlatformType = phone.PlatformType;
                existing.ThroughputLevel = phone.ThroughputLevel;
                existing.LastOnboardedTime = phone.LastOnboardedTime;
                existing.IsActive = true;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                existing.LastSyncUtc = DateTime.UtcNow;
                result.Updated++;
            }
            else
            {
                // Create
                _dbContext.WhatsAppPhoneNumbers.Add(new WhatsAppPhoneNumber
                {
                    CompanyId = companyId,
                    WhatsAppAccountId = whatsAppAccountId,
                    PhoneNumberId = phone.PhoneNumberId,
                    DisplayPhoneNumber = phone.DisplayPhoneNumber,
                    VerifiedName = phone.VerifiedName,
                    CodeVerificationStatus = phone.CodeVerificationStatus,
                    QualityRating = phone.QualityRating,
                    PlatformType = phone.PlatformType,
                    ThroughputLevel = phone.ThroughputLevel,
                    LastOnboardedTime = phone.LastOnboardedTime,
                    IsDefault = existingNumbers.Count == 0 && result.Created == 0,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastSyncUtc = DateTime.UtcNow
                });
                result.Created++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<JsonElement?> CallGraphApiAsync(string url, string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogDebug("Meta API {Url} -> {StatusCode}: {Content}",
            url.Split('?')[0], (int)response.StatusCode, content?.Length > 500 ? content[..500] : content);

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GenerateVerifyToken()
    {
        return Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24))
            .Replace("+", "x")
            .Replace("/", "y")
            .Replace("=", "");
    }
}
