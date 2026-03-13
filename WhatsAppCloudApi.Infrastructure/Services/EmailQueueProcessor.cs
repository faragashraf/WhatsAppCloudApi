using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class EmailQueueProcessor : IEmailQueueProcessor
{
    private const string CompanyScope = "COMPANY";
    private const string CriticalScope = "CRITICAL";

    private readonly ApplicationDbContext _db;
    private readonly IEmailCredentialProtector _credentialProtector;
    private readonly ILogger<EmailQueueProcessor> _logger;

    public EmailQueueProcessor(
        ApplicationDbContext db,
        IEmailCredentialProtector credentialProtector,
        ILogger<EmailQueueProcessor> logger)
    {
        _db = db;
        _credentialProtector = credentialProtector;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(int take, CancellationToken cancellationToken = default)
    {
        var createdFromRules = await MaterializeRuleEmailsAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var queueItems = await _db.EmailQueue
            .Include(q => q.EmailAccount)
            .Include(q => q.Attachments)
            .Where(q =>
                (q.Status == "PENDING" || q.Status == "RETRY") &&
                q.ScheduledAtUtc <= now &&
                q.RetryCount < 5)
            .OrderByDescending(q => q.Priority)
            .ThenBy(q => q.ScheduledAtUtc)
            .ThenBy(q => q.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        foreach (var item in queueItems)
        {
            await ProcessOneAsync(item, cancellationToken);
        }

        return createdFromRules + queueItems.Count;
    }

    private async Task<int> MaterializeRuleEmailsAsync(CancellationToken cancellationToken)
    {
        var rules = await _db.EmailNotificationRules
            .Include(r => r.EmailAccount)
            .Where(r => r.IsActive && r.TriggerType == "COMPANY_SUBSCRIPTION_EXPIRY")
            .ToListAsync(cancellationToken);

        var created = 0;
        var today = DateTime.UtcNow.Date;

        foreach (var rule in rules)
        {
            if (rule.EmailAccount is null || !rule.EmailAccount.IsActive)
            {
                continue;
            }

            var companiesQuery = _db.Companies
                .AsNoTracking()
                .Where(c => !c.IsDeleted && (c.Status == null || c.Status == "ACTIVE"));

            if (rule.Scope == CompanyScope && rule.CompanyId.HasValue)
            {
                companiesQuery = companiesQuery.Where(c => c.CompanyId == rule.CompanyId.Value);
            }

            var targetCompanies = await companiesQuery
                .Select(c => new
                {
                    c.CompanyId,
                    c.CompanyName,
                    c.Email,
                    ExpiryDate = c.SubscriptionEndDate
                })
                .ToListAsync(cancellationToken);

            foreach (var company in targetCompanies)
            {
                if (!company.ExpiryDate.HasValue)
                {
                    continue;
                }

                var expiryDate = company.ExpiryDate.Value.Date;
                var daysUntilExpiry = (expiryDate - today).Days;
                if (daysUntilExpiry < 0 || daysUntilExpiry > rule.LeadTimeDays)
                {
                    continue;
                }

                var recipients = await ResolveRecipientsAsync(rule, company.CompanyId, company.Email, cancellationToken);
                if (recipients.Count == 0)
                {
                    continue;
                }

                var dedupeKey = $"{rule.TriggerType}:{rule.EmailNotificationRuleId}:{company.CompanyId}:{expiryDate:yyyyMMdd}";
                if (await _db.EmailQueue.AnyAsync(q => q.DeduplicationKey == dedupeKey, cancellationToken))
                {
                    continue;
                }

                var renderedSubject = RenderTemplate(rule.SubjectTemplate, company.CompanyName, company.Email, expiryDate, daysUntilExpiry);
                var renderedBody = RenderTemplate(rule.BodyTemplate, company.CompanyName, company.Email, expiryDate, daysUntilExpiry);
                var brandedBody = BrandEmailTemplateRenderer.RenderSubscriptionExpiryNotice(
                    company.CompanyName,
                    company.Email,
                    expiryDate,
                    daysUntilExpiry,
                    renderedBody,
                    bodyIsHtml: rule.IsBodyHtml);

                _db.EmailQueue.Add(new EmailQueueItem
                {
                    CompanyId = company.CompanyId,
                    EmailAccountId = rule.EmailAccountId,
                    EmailNotificationRuleId = rule.EmailNotificationRuleId,
                    Scope = rule.Scope == CriticalScope ? CriticalScope : CompanyScope,
                    Category = rule.Category,
                    TriggerType = rule.TriggerType,
                    ToJson = System.Text.Json.JsonSerializer.Serialize(recipients),
                    Subject = renderedSubject,
                    Body = brandedBody,
                    IsBodyHtml = true,
                    Status = "PENDING",
                    Priority = rule.Scope == CriticalScope ? 300 : 200,
                    DeduplicationKey = dedupeKey,
                    ScheduledAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow
                });

                rule.LastTriggeredAtUtc = DateTime.UtcNow;
                created++;
            }
        }

        if (created > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    private async Task ProcessOneAsync(EmailQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            if (item.EmailAccount is null || !item.EmailAccount.IsActive)
            {
                throw new InvalidOperationException("Email account is missing or inactive.");
            }

            using var message = new MailMessage
            {
                From = new MailAddress(item.EmailAccount.FromAddress, item.EmailAccount.FromName),
                Subject = item.Subject,
                Body = item.Body,
                IsBodyHtml = item.IsBodyHtml,
                BodyEncoding = Encoding.UTF8,
                SubjectEncoding = Encoding.UTF8
            };

            foreach (var email in DeserializeEmails(item.ToJson))
            {
                message.To.Add(email);
            }

            foreach (var email in DeserializeEmails(item.CcJson))
            {
                message.CC.Add(email);
            }

            foreach (var email in DeserializeEmails(item.BccJson))
            {
                message.Bcc.Add(email);
            }

            if (!string.IsNullOrWhiteSpace(item.EmailAccount.ReplyToAddress))
            {
                message.ReplyToList.Add(item.EmailAccount.ReplyToAddress);
            }

            foreach (var attachment in item.Attachments)
            {
                var bytes = Convert.FromBase64String(attachment.ContentBase64);
                var stream = new MemoryStream(bytes);
                message.Attachments.Add(new Attachment(stream, attachment.FileName, attachment.ContentType));
            }

            using var client = new SmtpClient(item.EmailAccount.SmtpHost, item.EmailAccount.SmtpPort)
            {
                EnableSsl = item.EmailAccount.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(
                    item.EmailAccount.Username,
                    _credentialProtector.Unprotect(item.EmailAccount.PasswordProtected))
            };

            await client.SendMailAsync(message);

            item.Status = "SENT";
            item.LastError = null;
            item.SentAtUtc = DateTime.UtcNow;
            item.LastAttemptAtUtc = DateTime.UtcNow;
            item.UpdatedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            item.RetryCount += 1;
            item.Status = item.RetryCount >= 5 ? "FAILED" : "RETRY";
            item.LastError = FlattenExceptionMessage(ex);
            item.LastAttemptAtUtc = DateTime.UtcNow;
            item.UpdatedAtUtc = DateTime.UtcNow;
            _logger.LogError(ex, "Email queue processing failed for EmailQueueItemId={EmailQueueItemId}", item.EmailQueueItemId);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<string>> ResolveRecipientsAsync(EmailNotificationRule rule, int companyId, string companyEmail, CancellationToken cancellationToken)
    {
        if (rule.RecipientMode == "COMPANY_EMAIL")
        {
            return string.IsNullOrWhiteSpace(companyEmail)
                ? []
                : NormalizeEmailList([companyEmail]);
        }

        if (rule.RecipientMode == "CUSTOM")
        {
            return NormalizeEmailList(DeserializeEmails(rule.RecipientsJson));
        }

        var companyAdminEmails = await _db.CompanyUsers
            .AsNoTracking()
            .Where(u => u.CompanyId == companyId && u.IsActive && u.Role == "Admin")
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        return NormalizeEmailList(companyAdminEmails);
    }

    private static string RenderTemplate(string template, string companyName, string companyEmail, DateTime expiryDate, int daysUntilExpiry)
    {
        return template
            .Replace("{{companyName}}", companyName, StringComparison.OrdinalIgnoreCase)
            .Replace("{{companyEmail}}", companyEmail ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{expiryDate}}", expiryDate.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{{daysUntilExpiry}}", daysUntilExpiry.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> DeserializeEmails(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
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
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            try
            {
                var normalized = new MailAddress(email.Trim()).Address.Trim().ToLowerInvariant();
                if (!results.Any(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(normalized);
                }
            }
            catch (FormatException)
            {
                continue;
            }
        }

        return results;
    }

    private static string FlattenExceptionMessage(Exception exception)
    {
        var messages = new List<string>();
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(current.Message)
                && !messages.Contains(current.Message, StringComparer.OrdinalIgnoreCase))
            {
                messages.Add(current.Message.Trim());
            }
        }

        return messages.Count == 0 ? "Unknown email delivery failure." : string.Join(" | ", messages);
    }
}
