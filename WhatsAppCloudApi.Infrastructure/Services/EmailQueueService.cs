using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class EmailQueueService : IEmailQueueService
{
    private const string CompanyScope = "COMPANY";
    private const string CriticalScope = "CRITICAL";
    private static readonly HashSet<string> SupportedTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "MANUAL",
        "COMPANY_SUBSCRIPTION_EXPIRY"
    };
    private static readonly HashSet<string> SupportedRecipientModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "COMPANY_EMAIL",
        "COMPANY_ADMINS",
        "CUSTOM"
    };

    private readonly ApplicationDbContext _db;
    private readonly IEmailCredentialProtector _credentialProtector;

    public EmailQueueService(ApplicationDbContext db, IEmailCredentialProtector credentialProtector)
    {
        _db = db;
        _credentialProtector = credentialProtector;
    }

    public async Task<ApiResponse<EmailQueueDashboardDto>> GetDashboardAsync(int? companyId, string scope, CancellationToken ct = default)
    {
        var normalizedScope = NormalizeScope(scope);
        var queueQuery = FilterQueue(_db.EmailQueue.AsNoTracking(), companyId, normalizedScope);
        var now = DateTime.UtcNow;
        var tomorrow = now.AddDays(1);

        var dashboard = new EmailQueueDashboardDto
        {
            ActiveAccountCount = await FilterAccounts(_db.EmailAccounts.AsNoTracking(), companyId, normalizedScope).CountAsync(a => a.IsActive, ct),
            ActiveRuleCount = await FilterRules(_db.EmailNotificationRules.AsNoTracking(), companyId, normalizedScope).CountAsync(r => r.IsActive, ct),
            PendingCount = await queueQuery.CountAsync(q => q.Status == "PENDING" || q.Status == "RETRY", ct),
            ScheduledCount = await queueQuery.CountAsync(q => (q.Status == "PENDING" || q.Status == "RETRY") && q.ScheduledAtUtc > now, ct),
            SentCount = await queueQuery.CountAsync(q => q.Status == "SENT", ct),
            FailedCount = await queueQuery.CountAsync(q => q.Status == "FAILED", ct),
            DueInNext24Hours = await queueQuery.CountAsync(q => (q.Status == "PENDING" || q.Status == "RETRY") && q.ScheduledAtUtc >= now && q.ScheduledAtUtc <= tomorrow, ct),
            TotalQueued = await queueQuery.CountAsync(ct)
        };

        return ApiResponse<EmailQueueDashboardDto>.Ok(dashboard);
    }

    public async Task<ApiResponse<PagedResult<EmailQueueItemDto>>> GetQueueAsync(int? companyId, string scope, EmailQueueQueryParams query, CancellationToken ct = default)
    {
        var normalizedScope = NormalizeScope(scope);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var queueQuery = FilterQueue(_db.EmailQueue.AsNoTracking(), companyId, normalizedScope);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToUpperInvariant();
            queueQuery = queueQuery.Where(q => q.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            queueQuery = queueQuery.Where(q => q.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            queueQuery = queueQuery.Where(q =>
                q.Subject.Contains(search) ||
                q.ToJson.Contains(search) ||
                q.Body.Contains(search));
        }

        var total = await queueQuery.CountAsync(ct);
        var items = await queueQuery
            .Include(q => q.Company)
            .Include(q => q.EmailAccount)
            .Include(q => q.Attachments)
            .OrderByDescending(q => q.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return ApiResponse<PagedResult<EmailQueueItemDto>>.Ok(new PagedResult<EmailQueueItemDto>
        {
            Items = items.Select(MapQueueItem).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<ApiResponse<EmailQueueItemDto>> QueueEmailAsync(int? companyId, int currentUserId, string scope, QueueEmailRequest request, CancellationToken ct = default)
    {
        var queueValidationError = ValidateQueueRequest(request);
        if (queueValidationError is not null)
        {
            return ApiResponse<EmailQueueItemDto>.Fail(queueValidationError, HttpStatusCode.BadRequest);
        }

        var normalizedScope = NormalizeScope(scope);
        var account = await GetAccessibleAccountAsync(companyId, normalizedScope, request.EmailAccountId, ct);
        if (account is null || !account.IsActive)
        {
            return ApiResponse<EmailQueueItemDto>.Fail("Email account not found or inactive.", HttpStatusCode.BadRequest);
        }

        var to = NormalizeEmailList(request.To);
        if (to.Count == 0)
        {
            return ApiResponse<EmailQueueItemDto>.Fail("At least one valid recipient is required.", HttpStatusCode.BadRequest);
        }

        var attachments = CreateAttachmentEntities(request.Attachments ?? []);
        var item = new EmailQueueItem
        {
            CompanyId = normalizedScope == CompanyScope ? companyId : companyId,
            EmailAccountId = account.EmailAccountId,
            CreatedByUserId = currentUserId,
            Scope = normalizedScope,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "manual" : request.Category.Trim(),
            TriggerType = NormalizeTrigger(request.TriggerType),
            ToJson = SerializeEmails(to),
            CcJson = SerializeOptionalEmails(request.Cc),
            BccJson = SerializeOptionalEmails(request.Bcc),
            Subject = request.Subject.Trim(),
            Body = request.Body,
            IsBodyHtml = request.IsBodyHtml,
            Priority = request.Priority,
            ScheduledAtUtc = request.ScheduledAtUtc?.ToUniversalTime() ?? DateTime.UtcNow,
            Status = "PENDING",
            Attachments = attachments,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.EmailQueue.Add(item);
        await _db.SaveChangesAsync(ct);

        item.EmailAccount = account;
        if (companyId.HasValue)
        {
            item.Company = await _db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.CompanyId == companyId.Value, ct);
        }

        return ApiResponse<EmailQueueItemDto>.Ok(MapQueueItem(item), "Email queued.");
    }

    public async Task<ApiResponse<bool>> CancelQueueItemAsync(int? companyId, string scope, long queueItemId, CancellationToken ct = default)
    {
        var item = await GetAccessibleQueueItemAsync(companyId, NormalizeScope(scope), queueItemId, ct);
        if (item is null)
        {
            return ApiResponse<bool>.Fail("Queued email was not found.", HttpStatusCode.NotFound);
        }

        if (item.Status == "SENT")
        {
            return ApiResponse<bool>.Fail("Sent email cannot be canceled.", HttpStatusCode.BadRequest);
        }

        item.Status = "CANCELED";
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Queued email canceled.");
    }

    public async Task<ApiResponse<bool>> RetryQueueItemAsync(int? companyId, string scope, long queueItemId, CancellationToken ct = default)
    {
        var item = await GetAccessibleQueueItemAsync(companyId, NormalizeScope(scope), queueItemId, ct);
        if (item is null)
        {
            return ApiResponse<bool>.Fail("Queued email was not found.", HttpStatusCode.NotFound);
        }

        if (item.Status == "SENT")
        {
            return ApiResponse<bool>.Fail("Sent email cannot be retried.", HttpStatusCode.BadRequest);
        }

        item.Status = "PENDING";
        item.LastError = null;
        item.RetryCount = 0;
        item.ScheduledAtUtc = DateTime.UtcNow;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Queued email reset for retry.");
    }

    public async Task<ApiResponse<IReadOnlyList<EmailAccountDto>>> GetAccountsAsync(int? companyId, string scope, CancellationToken ct = default)
    {
        var accounts = await FilterAccounts(_db.EmailAccounts.AsNoTracking(), companyId, NormalizeScope(scope))
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.Name)
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<EmailAccountDto>>.Ok(accounts.Select(MapAccount).ToList());
    }

    public async Task<ApiResponse<EmailAccountDto>> UpsertAccountAsync(int? companyId, int currentUserId, string scope, int? accountId, EmailAccountUpsertRequest request, CancellationToken ct = default)
    {
        var normalizedScope = NormalizeScope(scope);
        if (accountId.HasValue)
        {
            var existing = await GetAccessibleAccountAsync(companyId, normalizedScope, accountId.Value, ct);
            if (existing is null)
            {
                return ApiResponse<EmailAccountDto>.Fail("Email account was not found.", HttpStatusCode.NotFound);
            }

            var accountValidationError = ValidateAccountRequest(request, requirePassword: string.IsNullOrWhiteSpace(existing.PasswordProtected));
            if (accountValidationError is not null)
            {
                return ApiResponse<EmailAccountDto>.Fail(accountValidationError, HttpStatusCode.BadRequest);
            }

            existing.Name = request.Name.Trim();
            existing.FromAddress = request.FromAddress.Trim().ToLowerInvariant();
            existing.ReplyToAddress = string.IsNullOrWhiteSpace(request.ReplyToAddress) ? null : request.ReplyToAddress.Trim().ToLowerInvariant();
            existing.FromName = request.FromName.Trim();
            existing.SmtpHost = NormalizeSmtpHost(request.SmtpHost);
            existing.SmtpPort = request.SmtpPort;
            existing.EnableSsl = request.EnableSsl;
            existing.Username = request.Username.Trim();
            existing.IsDefault = request.IsDefault;
            existing.IsActive = request.IsActive;
            existing.UpdatedAtUtc = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                existing.PasswordProtected = _credentialProtector.Protect(request.Password.Trim());
            }

            if (string.IsNullOrWhiteSpace(existing.PasswordProtected))
            {
                return ApiResponse<EmailAccountDto>.Fail("SMTP password is required.", HttpStatusCode.BadRequest);
            }

            if (existing.IsDefault)
            {
                await ClearOtherDefaultAccountsAsync(companyId, normalizedScope, existing.EmailAccountId, ct);
            }

            await _db.SaveChangesAsync(ct);
            return ApiResponse<EmailAccountDto>.Ok(MapAccount(existing), "Email account updated.");
        }

        var createAccountValidationError = ValidateAccountRequest(request, requirePassword: true);
        if (createAccountValidationError is not null)
        {
            return ApiResponse<EmailAccountDto>.Fail(createAccountValidationError, HttpStatusCode.BadRequest);
        }

        var account = new EmailAccount
        {
            CompanyId = normalizedScope == CompanyScope ? companyId : null,
            CreatedByUserId = currentUserId,
            Name = request.Name.Trim(),
            FromAddress = request.FromAddress.Trim().ToLowerInvariant(),
            ReplyToAddress = string.IsNullOrWhiteSpace(request.ReplyToAddress) ? null : request.ReplyToAddress.Trim().ToLowerInvariant(),
            FromName = request.FromName.Trim(),
            SmtpHost = NormalizeSmtpHost(request.SmtpHost),
            SmtpPort = request.SmtpPort,
            EnableSsl = request.EnableSsl,
            Username = request.Username.Trim(),
            PasswordProtected = _credentialProtector.Protect(request.Password!.Trim()),
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (account.IsDefault)
        {
            await ClearOtherDefaultAccountsAsync(companyId, normalizedScope, null, ct);
        }

        _db.EmailAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<EmailAccountDto>.Ok(MapAccount(account), "Email account created.");
    }

    public async Task<ApiResponse<bool>> DeleteAccountAsync(int? companyId, string scope, int accountId, CancellationToken ct = default)
    {
        var normalizedScope = NormalizeScope(scope);
        var account = await GetAccessibleAccountAsync(companyId, normalizedScope, accountId, ct);
        if (account is null)
        {
            return ApiResponse<bool>.Fail("Email account was not found.", HttpStatusCode.NotFound);
        }

        if (await _db.EmailNotificationRules.AnyAsync(r => r.EmailAccountId == accountId, ct))
        {
            return ApiResponse<bool>.Fail("Email account is still used by notification rules.", HttpStatusCode.BadRequest);
        }

        if (await _db.EmailQueue.AnyAsync(q => q.EmailAccountId == accountId, ct))
        {
            return ApiResponse<bool>.Fail("Email account is still used by queued or historical emails.", HttpStatusCode.BadRequest);
        }

        _db.EmailAccounts.Remove(account);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Email account deleted.");
    }

    public async Task<ApiResponse<IReadOnlyList<EmailNotificationRuleDto>>> GetRulesAsync(int? companyId, string scope, CancellationToken ct = default)
    {
        var rules = await FilterRules(_db.EmailNotificationRules.AsNoTracking(), companyId, NormalizeScope(scope))
            .Include(r => r.EmailAccount)
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        return ApiResponse<IReadOnlyList<EmailNotificationRuleDto>>.Ok(rules.Select(MapRule).ToList());
    }

    public async Task<ApiResponse<EmailNotificationRuleDto>> UpsertRuleAsync(int? companyId, int currentUserId, string scope, long? ruleId, EmailNotificationRuleUpsertRequest request, CancellationToken ct = default)
    {
        var ruleValidationError = ValidateRuleRequest(request);
        if (ruleValidationError is not null)
        {
            return ApiResponse<EmailNotificationRuleDto>.Fail(ruleValidationError, HttpStatusCode.BadRequest);
        }

        var normalizedScope = NormalizeScope(scope);
        var account = await GetAccessibleAccountAsync(companyId, normalizedScope, request.EmailAccountId, ct);
        if (account is null || !account.IsActive)
        {
            return ApiResponse<EmailNotificationRuleDto>.Fail("Email account not found or inactive.", HttpStatusCode.BadRequest);
        }

        var normalizedTrigger = NormalizeTrigger(request.TriggerType);
        if (normalizedTrigger != "COMPANY_SUBSCRIPTION_EXPIRY")
        {
            return ApiResponse<EmailNotificationRuleDto>.Fail("Only COMPANY_SUBSCRIPTION_EXPIRY is supported for notification rules right now.", HttpStatusCode.BadRequest);
        }

        var recipientMode = NormalizeRecipientMode(request.RecipientMode);
        if (recipientMode is null)
        {
            return ApiResponse<EmailNotificationRuleDto>.Fail("Unsupported recipient mode.", HttpStatusCode.BadRequest);
        }

        var recipients = recipientMode == "CUSTOM"
            ? NormalizeEmailList(request.Recipients)
            : [];

        if (recipientMode == "CUSTOM" && recipients.Count == 0)
        {
            return ApiResponse<EmailNotificationRuleDto>.Fail("Custom recipient mode requires at least one valid email address.", HttpStatusCode.BadRequest);
        }

        EmailNotificationRule rule;
        if (ruleId.HasValue)
        {
            rule = await GetAccessibleRuleAsync(companyId, normalizedScope, ruleId.Value, ct) ?? new EmailNotificationRule();
            if (rule.EmailNotificationRuleId == 0)
            {
                return ApiResponse<EmailNotificationRuleDto>.Fail("Notification rule was not found.", HttpStatusCode.NotFound);
            }
        }
        else
        {
            rule = new EmailNotificationRule
            {
                CompanyId = normalizedScope == CompanyScope ? companyId : null,
                Scope = normalizedScope,
                CreatedByUserId = currentUserId,
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.EmailNotificationRules.Add(rule);
        }

        rule.EmailAccountId = account.EmailAccountId;
        rule.EmailAccount = account;
        rule.Name = request.Name.Trim();
        rule.Category = string.IsNullOrWhiteSpace(request.Category) ? "subscription" : request.Category.Trim();
        rule.TriggerType = normalizedTrigger;
        rule.LeadTimeDays = request.LeadTimeDays;
        rule.RecipientMode = recipientMode;
        rule.RecipientsJson = recipientMode == "CUSTOM" ? SerializeEmails(recipients) : null;
        rule.SubjectTemplate = request.SubjectTemplate.Trim();
        rule.BodyTemplate = request.BodyTemplate;
        rule.IsBodyHtml = request.IsBodyHtml;
        rule.IsActive = request.IsActive;
        rule.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ApiResponse<EmailNotificationRuleDto>.Ok(MapRule(rule), ruleId.HasValue ? "Notification rule updated." : "Notification rule created.");
    }

    public async Task<ApiResponse<bool>> DeleteRuleAsync(int? companyId, string scope, long ruleId, CancellationToken ct = default)
    {
        var rule = await GetAccessibleRuleAsync(companyId, NormalizeScope(scope), ruleId, ct);
        if (rule is null)
        {
            return ApiResponse<bool>.Fail("Notification rule was not found.", HttpStatusCode.NotFound);
        }

        var affectedQueueItems = await _db.EmailQueue
            .Where(q => q.EmailNotificationRuleId == ruleId)
            .ToListAsync(ct);

        foreach (var item in affectedQueueItems)
        {
            item.EmailNotificationRuleId = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }

        _db.EmailNotificationRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Notification rule deleted.");
    }

    private IQueryable<EmailAccount> FilterAccounts(IQueryable<EmailAccount> query, int? companyId, string scope) =>
        scope == CriticalScope ? query.Where(a => a.CompanyId == null) : query.Where(a => a.CompanyId == companyId);

    private IQueryable<EmailNotificationRule> FilterRules(IQueryable<EmailNotificationRule> query, int? companyId, string scope) =>
        scope == CriticalScope ? query.Where(r => r.Scope == CriticalScope) : query.Where(r => r.CompanyId == companyId && r.Scope == CompanyScope);

    private IQueryable<EmailQueueItem> FilterQueue(IQueryable<EmailQueueItem> query, int? companyId, string scope) =>
        scope == CriticalScope ? query.Where(q => q.Scope == CriticalScope) : query.Where(q => q.CompanyId == companyId && q.Scope == CompanyScope);

    private async Task<EmailAccount?> GetAccessibleAccountAsync(int? companyId, string scope, int accountId, CancellationToken ct) =>
        await FilterAccounts(_db.EmailAccounts, companyId, scope).FirstOrDefaultAsync(a => a.EmailAccountId == accountId, ct);

    private async Task<EmailNotificationRule?> GetAccessibleRuleAsync(int? companyId, string scope, long ruleId, CancellationToken ct) =>
        await FilterRules(_db.EmailNotificationRules.Include(r => r.EmailAccount), companyId, scope)
            .FirstOrDefaultAsync(r => r.EmailNotificationRuleId == ruleId, ct);

    private async Task<EmailQueueItem?> GetAccessibleQueueItemAsync(int? companyId, string scope, long queueItemId, CancellationToken ct) =>
        await FilterQueue(_db.EmailQueue.Include(q => q.Attachments).Include(q => q.EmailAccount), companyId, scope)
            .FirstOrDefaultAsync(q => q.EmailQueueItemId == queueItemId, ct);

    private async Task ClearOtherDefaultAccountsAsync(int? companyId, string scope, int? exceptEmailAccountId, CancellationToken ct)
    {
        var accounts = await FilterAccounts(_db.EmailAccounts, companyId, scope)
            .Where(a => a.IsDefault && (!exceptEmailAccountId.HasValue || a.EmailAccountId != exceptEmailAccountId.Value))
            .ToListAsync(ct);

        foreach (var account in accounts)
        {
            account.IsDefault = false;
            account.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    private static string? ValidateQueueRequest(QueueEmailRequest request)
    {
        if (request.EmailAccountId <= 0)
        {
            return "A valid email account is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return "Email subject is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return "Email body is required.";
        }

        if (request.Priority is < 0 or > 1000)
        {
            return "Email priority must be between 0 and 1000.";
        }

        var invalidRecipients = GetInvalidEmails(request.To);
        if (invalidRecipients.Count > 0)
        {
            return $"Invalid To recipients: {string.Join(", ", invalidRecipients)}.";
        }

        var invalidCc = GetInvalidEmails(request.Cc);
        if (invalidCc.Count > 0)
        {
            return $"Invalid Cc recipients: {string.Join(", ", invalidCc)}.";
        }

        var invalidBcc = GetInvalidEmails(request.Bcc);
        if (invalidBcc.Count > 0)
        {
            return $"Invalid Bcc recipients: {string.Join(", ", invalidBcc)}.";
        }

        return null;
    }

    private static string? ValidateAccountRequest(EmailAccountUpsertRequest request, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Account name is required.";
        }

        if (!TryNormalizeEmail(request.FromAddress, out _))
        {
            return "From address must be a valid email.";
        }

        if (!string.IsNullOrWhiteSpace(request.ReplyToAddress) && !TryNormalizeEmail(request.ReplyToAddress, out _))
        {
            return "Reply-to address must be a valid email.";
        }

        if (string.IsNullOrWhiteSpace(request.FromName))
        {
            return "From name is required.";
        }

        if (!IsValidSmtpHost(request.SmtpHost))
        {
            return "SMTP host must be a valid host name or URL.";
        }

        if (request.SmtpPort is < 1 or > 65535)
        {
            return "SMTP port must be between 1 and 65535.";
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return "SMTP username is required.";
        }

        if (requirePassword && string.IsNullOrWhiteSpace(request.Password))
        {
            return "SMTP password is required.";
        }

        if (request.IsDefault && !request.IsActive)
        {
            return "Default SMTP accounts must stay active.";
        }

        return null;
    }

    private static string? ValidateRuleRequest(EmailNotificationRuleUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Rule name is required.";
        }

        if (request.EmailAccountId <= 0)
        {
            return "A valid email account is required.";
        }

        if (request.LeadTimeDays is < 0 or > 365)
        {
            return "Lead time must be between 0 and 365 days.";
        }

        if (string.IsNullOrWhiteSpace(request.SubjectTemplate))
        {
            return "Subject template is required.";
        }

        if (string.IsNullOrWhiteSpace(request.BodyTemplate))
        {
            return "Body template is required.";
        }

        var recipientMode = NormalizeRecipientMode(request.RecipientMode);
        if (recipientMode is null)
        {
            return "Unsupported recipient mode.";
        }

        if (recipientMode == "CUSTOM")
        {
            var invalidRecipients = GetInvalidEmails(request.Recipients);
            if (invalidRecipients.Count > 0)
            {
                return $"Invalid custom recipients: {string.Join(", ", invalidRecipients)}.";
            }

            if (NormalizeEmailList(request.Recipients).Count == 0)
            {
                return "Custom recipient mode requires at least one valid email address.";
            }
        }

        return null;
    }

    private static EmailQueueItemDto MapQueueItem(EmailQueueItem item) => new()
    {
        EmailQueueItemId = item.EmailQueueItemId,
        CompanyId = item.CompanyId,
        CompanyName = item.Company?.CompanyName,
        EmailAccountId = item.EmailAccountId,
        EmailAccountName = item.EmailAccount?.Name ?? string.Empty,
        FromAddress = item.EmailAccount?.FromAddress ?? string.Empty,
        Scope = item.Scope,
        Category = item.Category,
        TriggerType = item.TriggerType,
        To = DeserializeEmails(item.ToJson),
        Cc = DeserializeEmails(item.CcJson),
        Bcc = DeserializeEmails(item.BccJson),
        Subject = item.Subject,
        Body = item.Body,
        IsBodyHtml = item.IsBodyHtml,
        Status = item.Status,
        Priority = item.Priority,
        RetryCount = item.RetryCount,
        LastError = item.LastError,
        ScheduledAtUtc = item.ScheduledAtUtc,
        LastAttemptAtUtc = item.LastAttemptAtUtc,
        SentAtUtc = item.SentAtUtc,
        CreatedAtUtc = item.CreatedAtUtc,
        Attachments = item.Attachments.Select(a => new EmailAttachmentDto
        {
            EmailQueueAttachmentId = a.EmailQueueAttachmentId,
            FileName = a.FileName,
            ContentType = a.ContentType,
            SizeBytes = a.SizeBytes
        }).ToList()
    };

    private static EmailAccountDto MapAccount(EmailAccount account) => new()
    {
        EmailAccountId = account.EmailAccountId,
        CompanyId = account.CompanyId,
        Name = account.Name,
        FromAddress = account.FromAddress,
        ReplyToAddress = account.ReplyToAddress,
        FromName = account.FromName,
        SmtpHost = account.SmtpHost,
        SmtpPort = account.SmtpPort,
        EnableSsl = account.EnableSsl,
        Username = account.Username,
        HasPassword = !string.IsNullOrWhiteSpace(account.PasswordProtected),
        IsDefault = account.IsDefault,
        IsActive = account.IsActive,
        CreatedAtUtc = account.CreatedAtUtc,
        UpdatedAtUtc = account.UpdatedAtUtc
    };

    private static EmailNotificationRuleDto MapRule(EmailNotificationRule rule) => new()
    {
        EmailNotificationRuleId = rule.EmailNotificationRuleId,
        CompanyId = rule.CompanyId,
        EmailAccountId = rule.EmailAccountId,
        EmailAccountName = rule.EmailAccount?.Name ?? string.Empty,
        Scope = rule.Scope,
        Name = rule.Name,
        Category = rule.Category,
        TriggerType = rule.TriggerType,
        LeadTimeDays = rule.LeadTimeDays,
        RecipientMode = rule.RecipientMode,
        Recipients = DeserializeEmails(rule.RecipientsJson),
        SubjectTemplate = rule.SubjectTemplate,
        BodyTemplate = rule.BodyTemplate,
        IsBodyHtml = rule.IsBodyHtml,
        IsActive = rule.IsActive,
        LastTriggeredAtUtc = rule.LastTriggeredAtUtc,
        CreatedAtUtc = rule.CreatedAtUtc,
        UpdatedAtUtc = rule.UpdatedAtUtc
    };

    private static List<EmailQueueAttachment> CreateAttachmentEntities(IEnumerable<EmailAttachmentRequest> attachments)
    {
        const int maxAttachmentCount = 10;
        const int maxBytesPerAttachment = 5 * 1024 * 1024;
        const int maxBytesTotal = 10 * 1024 * 1024;

        var requests = attachments.ToList();
        if (requests.Count > maxAttachmentCount)
        {
            throw new InvalidOperationException($"A maximum of {maxAttachmentCount} attachments is allowed.");
        }

        var totalBytes = 0;
        var results = new List<EmailQueueAttachment>(requests.Count);
        foreach (var attachment in requests)
        {
            if (string.IsNullOrWhiteSpace(attachment.FileName))
            {
                throw new InvalidOperationException("Attachment file name is required.");
            }

            if (string.IsNullOrWhiteSpace(attachment.ContentType))
            {
                throw new InvalidOperationException($"Attachment '{attachment.FileName}' is missing a content type.");
            }

            if (string.IsNullOrWhiteSpace(attachment.ContentBase64))
            {
                throw new InvalidOperationException($"Attachment '{attachment.FileName}' is empty.");
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(attachment.ContentBase64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException($"Attachment '{attachment.FileName}' has invalid base64 content.");
            }

            if (bytes.Length > maxBytesPerAttachment)
            {
                throw new InvalidOperationException($"Attachment '{attachment.FileName}' exceeds the 5 MB limit.");
            }

            totalBytes += bytes.Length;
            if (totalBytes > maxBytesTotal)
            {
                throw new InvalidOperationException("Combined attachment size exceeds the 10 MB limit.");
            }

            results.Add(new EmailQueueAttachment
            {
                FileName = attachment.FileName.Trim(),
                ContentType = attachment.ContentType.Trim(),
                ContentBase64 = attachment.ContentBase64,
                SizeBytes = bytes.Length,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        return results;
    }

    private static List<string> GetInvalidEmails(IEnumerable<string>? emails)
    {
        var invalid = new List<string>();
        if (emails is null)
        {
            return invalid;
        }

        foreach (var email in emails)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            if (!TryNormalizeEmail(email, out _))
            {
                invalid.Add(email.Trim());
            }
        }

        return invalid;
    }

    private static List<string> NormalizeEmailList(IEnumerable<string>? emails)
    {
        var results = new List<string>();
        if (emails is null)
        {
            return results;
        }

        foreach (var email in emails)
        {
            if (!TryNormalizeEmail(email, out var normalized))
            {
                continue;
            }

            if (!results.Any(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                results.Add(normalized);
            }
        }

        return results;
    }

    private static string SerializeEmails(IEnumerable<string> emails) => JsonSerializer.Serialize(emails.ToList());

    private static string? SerializeOptionalEmails(IEnumerable<string>? emails)
    {
        var normalized = NormalizeEmailList(emails);
        return normalized.Count == 0 ? null : SerializeEmails(normalized);
    }

    private static List<string> DeserializeEmails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeScope(string scope) =>
        string.Equals(scope, CriticalScope, StringComparison.OrdinalIgnoreCase) ? CriticalScope : CompanyScope;

    private static string NormalizeTrigger(string triggerType)
    {
        var normalized = string.IsNullOrWhiteSpace(triggerType) ? "MANUAL" : triggerType.Trim().ToUpperInvariant();
        return SupportedTriggers.Contains(normalized) ? normalized : normalized;
    }

    private static string NormalizeSmtpHost(string smtpHost)
    {
        var normalized = (smtpHost ?? string.Empty).Trim();
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            normalized = uri.Host;
        }

        return normalized.Trim().TrimEnd('/');
    }

    private static bool IsValidSmtpHost(string smtpHost)
    {
        var normalized = NormalizeSmtpHost(smtpHost);
        return !string.IsNullOrWhiteSpace(normalized)
            && !normalized.Contains(' ', StringComparison.Ordinal)
            && Uri.CheckHostName(normalized) != UriHostNameType.Unknown;
    }

    private static bool TryNormalizeEmail(string? email, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var parsed = new MailAddress(email.Trim());
            normalized = parsed.Address.Trim().ToLowerInvariant();
            return !string.IsNullOrWhiteSpace(normalized);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? NormalizeRecipientMode(string recipientMode)
    {
        var normalized = string.IsNullOrWhiteSpace(recipientMode) ? null : recipientMode.Trim().ToUpperInvariant();
        return normalized is not null && SupportedRecipientModes.Contains(normalized) ? normalized : null;
    }
}
