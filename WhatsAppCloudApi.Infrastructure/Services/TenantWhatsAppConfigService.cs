using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class TenantWhatsAppConfigService : ITenantWhatsAppConfigService
{
    private readonly ApplicationDbContext _dbContext;

    public TenantWhatsAppConfigService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantWhatsAppConfig> GetRequiredConfigAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var config = await QueryConfig()
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.IsDefaultPhoneNumber)
            .ThenByDescending(x => x.IsDefaultAccount)
            .FirstOrDefaultAsync(cancellationToken);

        if (config is null)
        {
            throw new InvalidOperationException("No active WhatsApp configuration found for the company.");
        }

        return ToTenantConfig(config);
    }

    public async Task<TenantWhatsAppConfig?> GetConfigByVerifyTokenAsync(string verifyToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(verifyToken))
        {
            return null;
        }

        var config = await QueryConfig()
            .Where(x => x.VerifyToken == verifyToken)
            .OrderByDescending(x => x.IsDefaultPhoneNumber)
            .ThenByDescending(x => x.IsDefaultAccount)
            .FirstOrDefaultAsync(cancellationToken);

        return config is null ? null : ToTenantConfig(config);
    }

    public async Task<TenantWhatsAppConfig?> GetConfigByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return null;
        }

        var config = await QueryConfig()
            .Where(x => x.PhoneNumberId == phoneNumberId)
            .OrderByDescending(x => x.IsDefaultPhoneNumber)
            .ThenByDescending(x => x.IsDefaultAccount)
            .FirstOrDefaultAsync(cancellationToken);

        return config is null ? null : ToTenantConfig(config);
    }

    private IQueryable<TenantConfigProjection> QueryConfig()
    {
        return from account in _dbContext.WhatsAppAccounts.AsNoTracking()
               join phone in _dbContext.WhatsAppPhoneNumbers.AsNoTracking()
                   on account.WhatsAppAccountId equals phone.WhatsAppAccountId
               where account.IsActive && phone.IsActive
               select new TenantConfigProjection
               {
                   CompanyId = account.CompanyId,
                   WhatsAppAccountId = account.WhatsAppAccountId,
                   WhatsAppPhoneNumberId = phone.WhatsAppPhoneNumberId,
                   AccessToken = account.AccessToken,
                   BusinessAccountId = account.BusinessAccountId,
                   PhoneNumberId = phone.PhoneNumberId,
                   VerifyToken = account.VerifyToken,
                   AppSecret = account.AppSecret,
                   IsDefaultAccount = account.IsDefault,
                   IsDefaultPhoneNumber = phone.IsDefault
               };
    }

    private static TenantWhatsAppConfig ToTenantConfig(TenantConfigProjection projection)
    {
        return new TenantWhatsAppConfig
        {
            CompanyId = projection.CompanyId,
            WhatsAppAccountId = projection.WhatsAppAccountId,
            WhatsAppPhoneNumberId = projection.WhatsAppPhoneNumberId,
            AccessToken = projection.AccessToken,
            BusinessAccountId = projection.BusinessAccountId,
            PhoneNumberId = projection.PhoneNumberId,
            VerifyToken = projection.VerifyToken,
            AppSecret = projection.AppSecret
        };
    }

    private sealed class TenantConfigProjection
    {
        public int CompanyId { get; set; }
        public int WhatsAppAccountId { get; set; }
        public int WhatsAppPhoneNumberId { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string BusinessAccountId { get; set; } = string.Empty;
        public string PhoneNumberId { get; set; } = string.Empty;
        public string VerifyToken { get; set; } = string.Empty;
        public string? AppSecret { get; set; }
        public bool IsDefaultAccount { get; set; }
        public bool IsDefaultPhoneNumber { get; set; }
    }
}
