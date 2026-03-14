using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class SubscriptionValidationService : ISubscriptionValidationService
{
    private readonly ApplicationDbContext _dbContext;

    public SubscriptionValidationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task ValidateCanSendMessageAsync(int companyId, CancellationToken cancellationToken = default)
    {
        return ValidateCanSendMessagesAsync(companyId, 1, cancellationToken);
    }

    public async Task ValidateCanSendMessagesAsync(int companyId, int requestedMessageCount, CancellationToken cancellationToken = default)
    {
        if (requestedMessageCount <= 0)
        {
            throw new InvalidOperationException("Requested message count must be greater than zero.");
        }

        var state = await GetSubscriptionStateAsync(companyId, cancellationToken);
        if (!state.IsMessageSendingAllowed)
        {
            throw new InvalidOperationException("Subscription expired.");
        }

        if (state.MaxMessagesPerMonth > 0)
        {
            var sentCount = await GetCurrentMonthMessageCountAsync(companyId, cancellationToken);
            if (sentCount + requestedMessageCount > state.MaxMessagesPerMonth)
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
            var accountsCount = await GetActiveWhatsAppAccountsCountAsync(companyId, cancellationToken);
            if (accountsCount >= state.MaxWhatsAppAccounts)
            {
                throw new InvalidOperationException("Maximum WhatsApp account limit exceeded for your subscription plan.");
            }
        }
    }

    public async Task<SubscriptionUsageSnapshotDto> GetUsageSnapshotAsync(int companyId, CancellationToken cancellationToken = default)
    {
        var state = await GetSubscriptionStateAsync(companyId, cancellationToken);
        var sentCount = await GetCurrentMonthMessageCountAsync(companyId, cancellationToken);
        var activeAccounts = await GetActiveWhatsAppAccountsCountAsync(companyId, cancellationToken);

        var remainingMessages = state.MaxMessagesPerMonth > 0
            ? Math.Max(0, state.MaxMessagesPerMonth - sentCount)
            : -1;
        var remainingAccounts = state.MaxWhatsAppAccounts > 0
            ? Math.Max(0, state.MaxWhatsAppAccounts - activeAccounts)
            : -1;

        return new SubscriptionUsageSnapshotDto
        {
            IsSubscriptionActive = state.IsMessageSendingAllowed,
            SubscriptionStatus = state.SubscriptionStatus,
            CompanySubscriptionId = state.CompanySubscriptionId,
            PlanCode = state.PlanCode,
            PlanName = state.PlanName,
            ExpiresAtUtc = state.ExpiresAtUtc,
            MaxMessagesPerMonth = state.MaxMessagesPerMonth,
            MessagesUsedThisMonth = sentCount,
            RemainingMessagesThisMonth = remainingMessages,
            IsMessageLimitReached = state.MaxMessagesPerMonth > 0 && sentCount >= state.MaxMessagesPerMonth,
            MaxWhatsAppAccounts = state.MaxWhatsAppAccounts,
            ActiveWhatsAppAccounts = activeAccounts,
            RemainingWhatsAppAccounts = remainingAccounts,
            IsWhatsAppAccountLimitReached = state.MaxWhatsAppAccounts > 0 && activeAccounts >= state.MaxWhatsAppAccounts
        };
    }

    private async Task<int> GetCurrentMonthMessageCountAsync(int companyId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        return await _dbContext.Messages
            .Where(x => x.CompanyId == companyId && x.CreatedAtUtc >= monthStart && x.CreatedAtUtc < monthEnd)
            .CountAsync(cancellationToken);
    }

    private Task<int> GetActiveWhatsAppAccountsCountAsync(int companyId, CancellationToken cancellationToken)
    {
        return _dbContext.WhatsAppAccounts
            .Where(x => x.CompanyId == companyId && x.IsActive)
            .CountAsync(cancellationToken);
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
                    subscription.CompanySubscriptionId,
                    subscription.Status,
                    subscription.IsActive,
                    subscription.TrialEndDate,
                    subscription.StartDate,
                    subscription.EndDate,
                    plan.Code,
                    plan.Name,
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
            return SubscriptionState.Allowed(
                "ACTIVE",
                activePaid.CompanySubscriptionId,
                activePaid.Code,
                activePaid.Name,
                activePaid.EndDate,
                activePaid.MaxMessagesPerMonth,
                activePaid.MaxWhatsAppAccounts);
        }

        var activeTrial = subscriptions
            .Where(x => x.IsActive && string.Equals(x.Status, "TRIAL", StringComparison.OrdinalIgnoreCase))
            .Where(x => x.TrialEndDate is not null && x.TrialEndDate >= now)
            .OrderByDescending(x => x.TrialEndDate)
            .FirstOrDefault();

        if (activeTrial is not null)
        {
            return SubscriptionState.Allowed(
                "TRIAL",
                activeTrial.CompanySubscriptionId,
                activeTrial.Code,
                activeTrial.Name,
                activeTrial.TrialEndDate,
                activeTrial.MaxMessagesPerMonth,
                activeTrial.MaxWhatsAppAccounts);
        }

        return SubscriptionState.Expired();
    }

    private sealed record SubscriptionState(
        bool IsMessageSendingAllowed,
        string SubscriptionStatus,
        int? CompanySubscriptionId,
        string? PlanCode,
        string? PlanName,
        DateTime? ExpiresAtUtc,
        int MaxMessagesPerMonth,
        int MaxWhatsAppAccounts)
    {
        public static SubscriptionState Expired()
            => new(false, "EXPIRED", null, null, null, null, 0, 0);

        public static SubscriptionState Allowed(
            string subscriptionStatus,
            int companySubscriptionId,
            string? planCode,
            string? planName,
            DateTime? expiresAtUtc,
            int maxMessagesPerMonth,
            int maxWhatsAppAccounts)
            => new(
                true,
                subscriptionStatus,
                companySubscriptionId,
                planCode,
                planName,
                expiresAtUtc,
                maxMessagesPerMonth,
                maxWhatsAppAccounts);
    }
}
