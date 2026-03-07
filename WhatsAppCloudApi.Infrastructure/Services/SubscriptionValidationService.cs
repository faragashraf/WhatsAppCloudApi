using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class SubscriptionValidationService : ISubscriptionValidationService
{
    private readonly ApplicationDbContext _dbContext;

    public SubscriptionValidationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ValidateCanSendMessageAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var state = await GetSubscriptionStateAsync(companyId, cancellationToken);
        if (!state.IsMessageSendingAllowed)
        {
            throw new InvalidOperationException("Subscription expired.");
        }

        if (state.MaxMessagesPerMonth > 0)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1);

            var sentCount = await _dbContext.Messages
                .Where(x => x.CompanyId == companyId && x.CreatedAtUtc >= monthStart && x.CreatedAtUtc < monthEnd)
                .CountAsync(cancellationToken);

            if (sentCount >= state.MaxMessagesPerMonth)
            {
                throw new InvalidOperationException("Monthly message limit exceeded for your subscription plan.");
            }
        }
    }

    public async Task ValidateWhatsAppAccountLimitAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var state = await GetSubscriptionStateAsync(companyId, cancellationToken);
        if (!state.IsMessageSendingAllowed)
        {
            throw new InvalidOperationException("Subscription expired.");
        }

        if (state.MaxWhatsAppAccounts > 0)
        {
            var accountsCount = await _dbContext.WhatsAppAccounts
                .Where(x => x.CompanyId == companyId && x.IsActive)
                .CountAsync(cancellationToken);

            if (accountsCount >= state.MaxWhatsAppAccounts)
            {
                throw new InvalidOperationException("Maximum WhatsApp account limit exceeded for your subscription plan.");
            }
        }
    }

    private async Task<SubscriptionState> GetSubscriptionStateAsync(int companyId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var subscriptions = await _dbContext.CompanySubscriptions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Join(
                _dbContext.SubscriptionPlans.AsNoTracking(),
                subscription => subscription.SubscriptionPlanId,
                plan => plan.SubscriptionPlanId,
                (subscription, plan) => new
                {
                    subscription.Status,
                    subscription.IsActive,
                    subscription.TrialEndDate,
                    subscription.StartDate,
                    subscription.EndDate,
                    plan.MaxMessagesPerMonth,
                    plan.MaxWhatsAppAccounts
                })
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return SubscriptionState.Expired();
        }

        var activePaid = subscriptions
            .Where(x => x.IsActive && string.Equals(x.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            .Where(x => (x.StartDate is null || x.StartDate <= now) && (x.EndDate is null || x.EndDate >= now))
            .OrderByDescending(x => x.EndDate ?? DateTime.MaxValue)
            .FirstOrDefault();

        if (activePaid is not null)
        {
            return SubscriptionState.Allowed(activePaid.MaxMessagesPerMonth, activePaid.MaxWhatsAppAccounts);
        }

        var activeTrial = subscriptions
            .Where(x => x.IsActive && string.Equals(x.Status, "TRIAL", StringComparison.OrdinalIgnoreCase))
            .Where(x => x.TrialEndDate is not null && x.TrialEndDate >= now)
            .OrderByDescending(x => x.TrialEndDate)
            .FirstOrDefault();

        if (activeTrial is not null)
        {
            return SubscriptionState.Allowed(activeTrial.MaxMessagesPerMonth, activeTrial.MaxWhatsAppAccounts);
        }

        return SubscriptionState.Expired();
    }

    private sealed record SubscriptionState(bool IsMessageSendingAllowed, int MaxMessagesPerMonth, int MaxWhatsAppAccounts)
    {
        public static SubscriptionState Expired() => new(false, 0, 0);
        public static SubscriptionState Allowed(int maxMessagesPerMonth, int maxWhatsAppAccounts)
            => new(true, maxMessagesPerMonth, maxWhatsAppAccounts);
    }
}
