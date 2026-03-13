using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Application.Exceptions;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private const string CompanyScope = "COMPANY";
    private const string CriticalScope = "CRITICAL";
    private const string PasswordResetCategory = "security";
    private const string PasswordResetTriggerType = "PASSWORD_RESET_OTP";

    private readonly EmailOptions _emailOptions;
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailCredentialProtector _credentialProtector;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<EmailOptions> emailOptions,
        ApplicationDbContext dbContext,
        IEmailCredentialProtector credentialProtector,
        ILogger<SmtpEmailSender> logger)
    {
        _emailOptions = emailOptions.Value;
        _dbContext = dbContext;
        _credentialProtector = credentialProtector;
        _logger = logger;
    }

    public async Task SendPasswordResetOtpAsync(
        string toEmail,
        string? recipientName,
        string otp,
        TimeSpan expiresIn,
        int? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var sender = await ResolveSenderAsync(companyId, cancellationToken);
        var subject = "Your password reset code";
        var body = BuildPasswordResetBody(recipientName, otp, expiresIn);
        var auditBody = BuildPasswordResetAuditBody(recipientName, expiresIn);

        using var message = new MailMessage
        {
            From = CreateMailAddress(sender.FromAddress, sender.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(CreateMailAddress(toEmail, recipientName));

        using var client = new SmtpClient(NormalizeSmtpHost(sender.Host), sender.Port)
        {
            EnableSsl = sender.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(sender.Username))
        {
            client.Credentials = new NetworkCredential(sender.Username, sender.Password ?? string.Empty);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message);

            await TryPersistPasswordResetQueueAuditAsync(
                sender,
                companyId,
                toEmail,
                subject,
                auditBody,
                status: "SENT",
                error: null,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            await TryPersistPasswordResetQueueAuditAsync(
                sender,
                companyId,
                toEmail,
                subject,
                auditBody,
                status: "FAILED",
                error: ex.Message,
                cancellationToken: CancellationToken.None);

            _logger.LogError(
                ex,
                "SMTP delivery failed while sending password reset email to {Email} using {Source}.",
                toEmail,
                sender.SourceDescription);

            throw new EmailDeliveryException(
                "Password reset email could not be sent. Check SMTP configuration.",
                ex);
        }
    }

    private async Task<ResolvedSmtpSender> ResolveSenderAsync(int? companyId, CancellationToken cancellationToken)
    {
        var accountSender = await TryResolveFromEmailAccountAsync(companyId, cancellationToken);
        if (accountSender is not null)
        {
            return accountSender;
        }

        if (HasCompleteGlobalSettings())
        {
            return new ResolvedSmtpSender(
                NormalizeSmtpHost(_emailOptions.Host),
                _emailOptions.Port,
                _emailOptions.EnableSsl,
                _emailOptions.FromAddress,
                _emailOptions.FromName,
                _emailOptions.Username,
                _emailOptions.Password,
                "Email options",
                null);
        }

        if (HasPartialGlobalSettings())
        {
            _logger.LogWarning("Global Email settings are partially configured and no active EmailAccount was available.");
        }

        EnsureConfigured();
        return new ResolvedSmtpSender(
            NormalizeSmtpHost(_emailOptions.Host),
            _emailOptions.Port,
            _emailOptions.EnableSsl,
            _emailOptions.FromAddress,
            _emailOptions.FromName,
            _emailOptions.Username,
            _emailOptions.Password,
            "Email options",
            null);
    }

    private async Task<ResolvedSmtpSender?> TryResolveFromEmailAccountAsync(int? companyId, CancellationToken cancellationToken)
    {
        var query = _dbContext.EmailAccounts
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (companyId.HasValue)
        {
            query = query.Where(a => a.CompanyId == companyId || a.CompanyId == null);
        }
        else
        {
            query = query.Where(a => a.CompanyId == null);
        }

        var candidates = await query.ToListAsync(cancellationToken);
        var orderedCandidates = candidates
            .OrderByDescending(a => companyId.HasValue && a.CompanyId == companyId.Value)
            .ThenByDescending(a => a.IsDefault)
            .ThenBy(a => a.EmailAccountId);

        foreach (var account in orderedCandidates)
        {
            var host = NormalizeSmtpHost(account.SmtpHost);
            if (string.IsNullOrWhiteSpace(host)
                || string.IsNullOrWhiteSpace(account.FromAddress)
                || string.IsNullOrWhiteSpace(account.Username)
                || string.IsNullOrWhiteSpace(account.PasswordProtected))
            {
                continue;
            }

            try
            {
                return new ResolvedSmtpSender(
                    host,
                    account.SmtpPort,
                    account.EnableSsl,
                    account.FromAddress,
                    account.FromName,
                    account.Username,
                    _credentialProtector.Unprotect(account.PasswordProtected),
                    $"EmailAccount #{account.EmailAccountId}",
                    account.EmailAccountId);
            }
            catch (Exception ex) when (ex is InvalidOperationException)
            {
                _logger.LogWarning(
                    ex,
                    "Unable to decrypt SMTP credentials for EmailAccountId={EmailAccountId}.",
                    account.EmailAccountId);
            }
        }

        return null;
    }

    private async Task TryPersistPasswordResetQueueAuditAsync(
        ResolvedSmtpSender sender,
        int? companyId,
        string recipientEmail,
        string subject,
        string auditBody,
        string status,
        string? error,
        CancellationToken cancellationToken)
    {
        var emailAccountId = sender.EmailAccountId
            ?? await TryResolveAuditEmailAccountIdAsync(companyId, cancellationToken);

        if (!emailAccountId.HasValue)
        {
            _logger.LogDebug(
                "Password reset queue audit skipped for {Email} because sender source {Source} is not linked to an EmailAccount.",
                recipientEmail,
                sender.SourceDescription);
            return;
        }

        try
        {
            var now = DateTime.UtcNow;
            var item = new EmailQueueItem
            {
                CompanyId = companyId,
                EmailAccountId = emailAccountId.Value,
                CreatedByUserId = null,
                Scope = companyId.HasValue ? CompanyScope : CriticalScope,
                Category = PasswordResetCategory,
                TriggerType = PasswordResetTriggerType,
                ToJson = SerializeEmailList(new[] { recipientEmail }),
                Subject = subject,
                Body = auditBody,
                IsBodyHtml = true,
                Status = status,
                Priority = 1000,
                RetryCount = 0,
                LastError = status == "FAILED" ? TruncateError(error) : null,
                ScheduledAtUtc = now,
                LastAttemptAtUtc = now,
                SentAtUtc = status == "SENT" ? now : null,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            _dbContext.EmailQueue.Add(item);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist password reset email queue audit entry for {Email}.", recipientEmail);
        }
    }

    private async Task<int?> TryResolveAuditEmailAccountIdAsync(int? companyId, CancellationToken cancellationToken)
    {
        var query = _dbContext.EmailAccounts
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (companyId.HasValue)
        {
            query = query.Where(a => a.CompanyId == companyId || a.CompanyId == null);
        }
        else
        {
            query = query.Where(a => a.CompanyId == null);
        }

        return await query
            .OrderByDescending(a => companyId.HasValue && a.CompanyId == companyId.Value)
            .ThenByDescending(a => a.IsDefault)
            .ThenBy(a => a.EmailAccountId)
            .Select(a => (int?)a.EmailAccountId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string SerializeEmailList(IEnumerable<string> emails)
    {
        var normalized = emails
            .Select(NormalizeEmail)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return JsonSerializer.Serialize(normalized);
    }

    private static string NormalizeEmail(string value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string? TruncateError(string? value, int maxLength = 2000)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(NormalizeSmtpHost(_emailOptions.Host)))
        {
            throw new EmailDeliveryException("Email sending is not configured. Configure Email:Host or an active SMTP account in Email Center.");
        }

        if (string.IsNullOrWhiteSpace(_emailOptions.FromAddress))
        {
            throw new EmailDeliveryException("Email sending is not configured. Set Email:FromAddress before using forgot-password.");
        }

        if (HasPartialGlobalSettings())
        {
            throw new EmailDeliveryException("Email SMTP credentials are incomplete. Set both Email:Username and Email:Password.");
        }
    }

    private bool HasCompleteGlobalSettings()
    {
        var host = NormalizeSmtpHost(_emailOptions.Host);
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(_emailOptions.FromAddress))
        {
            return false;
        }

        return !HasPartialGlobalSettings();
    }

    private bool HasPartialGlobalSettings() =>
        string.IsNullOrWhiteSpace(_emailOptions.Username) ^ string.IsNullOrWhiteSpace(_emailOptions.Password);

    private static MailAddress CreateMailAddress(string email, string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? new MailAddress(email)
            : new MailAddress(email, displayName);
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

    private static string BuildPasswordResetBody(string? recipientName, string otp, TimeSpan expiresIn)
        => BrandEmailTemplateRenderer.RenderPasswordResetOtp(recipientName, otp, expiresIn);

    private static string BuildPasswordResetAuditBody(string? recipientName, TimeSpan expiresIn)
        => BuildPasswordResetBody(recipientName, "***REDACTED***", expiresIn);

    private sealed record ResolvedSmtpSender(
        string Host,
        int Port,
        bool EnableSsl,
        string FromAddress,
        string? FromName,
        string? Username,
        string? Password,
        string SourceDescription,
        int? EmailAccountId);
}
