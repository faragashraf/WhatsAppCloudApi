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
        var config = await QueryConfigAsync(companyId: companyId, cancellationToken: cancellationToken);
        if (config is null)
        {
            throw new InvalidOperationException("No active WhatsApp configuration found for the company.");
        }

        return config;
    }

    public async Task<TenantWhatsAppConfig?> GetConfigByVerifyTokenAsync(string verifyToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(verifyToken))
        {
            return null;
        }

        return await QueryConfigAsync(verifyToken: verifyToken, cancellationToken: cancellationToken);
    }

    public async Task<TenantWhatsAppConfig?> GetConfigByPhoneNumberIdAsync(string phoneNumberId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(phoneNumberId))
        {
            return null;
        }

        return await QueryConfigAsync(phoneNumberId: phoneNumberId, cancellationToken: cancellationToken);
    }

    private async Task<TenantWhatsAppConfig?> QueryConfigAsync(
        int? companyId = null,
        string? verifyToken = null,
        string? phoneNumberId = null,
        CancellationToken cancellationToken = default)
    {
        var query = from account in _dbContext.WhatsAppAccounts.AsNoTracking()
                    join phone in _dbContext.WhatsAppPhoneNumbers.AsNoTracking()
                        on account.WhatsAppAccountId equals phone.WhatsAppAccountId
                    where account.IsActive && phone.IsActive
                    select new
                    {
                        account.CompanyId,
                        account.WhatsAppAccountId,
                        phone.WhatsAppPhoneNumberId,
                        account.AccessToken,
                        account.BusinessAccountId,
                        phone.PhoneNumberId,
                        account.VerifyToken,
                        account.AppSecret,
                        account.IsDefault,
                        IsDefaultPhone = phone.IsDefault
                    };

        if (companyId.HasValue)
        {
            query = query.Where(x => x.CompanyId == companyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(verifyToken))
        {
            query = query.Where(x => x.VerifyToken == verifyToken);
        }

        if (!string.IsNullOrWhiteSpace(phoneNumberId))
        {
            query = query.Where(x => x.PhoneNumberId == phoneNumberId);
        }

        var result = await query
            .OrderByDescending(x => x.IsDefaultPhone)
            .ThenByDescending(x => x.IsDefault)
            .FirstOrDefaultAsync(cancellationToken);

        if (result is null)
        {
            return null;
        }

        return new TenantWhatsAppConfig
        {
            CompanyId = result.CompanyId,
            WhatsAppAccountId = result.WhatsAppAccountId,
            WhatsAppPhoneNumberId = result.WhatsAppPhoneNumberId,
            AccessToken = result.AccessToken,
            BusinessAccountId = result.BusinessAccountId,
            PhoneNumberId = result.PhoneNumberId,
            VerifyToken = result.VerifyToken,
            AppSecret = result.AppSecret
        };
    }
}
