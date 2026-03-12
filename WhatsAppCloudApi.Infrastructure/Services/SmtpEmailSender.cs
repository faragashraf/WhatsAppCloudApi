using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Application.Exceptions;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Infrastructure.Data;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class SmtpEmailSender : IEmailSender
{
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

        using var message = new MailMessage
        {
            From = CreateMailAddress(sender.FromAddress, sender.FromName),
            Subject = "Your password reset code",
            Body = BuildPasswordResetBody(recipientName, otp, expiresIn),
            IsBodyHtml = false
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
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
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
                "Email options");
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
            "Email options");
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
                    $"EmailAccount #{account.EmailAccountId}");
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
    {
        var greetingName = string.IsNullOrWhiteSpace(recipientName) ? "there" : recipientName.Trim();
        var minutes = Math.Max(1, (int)Math.Ceiling(expiresIn.TotalMinutes));

        return $"""
Hello {greetingName},

We received a request to reset your Bot Global Service password.

Your one-time password is: {otp}

This code expires in {minutes} minutes.

If you did not request this reset, you can ignore this email.

Bot Global Service
""";
    }

    private sealed record ResolvedSmtpSender(
        string Host,
        int Port,
        bool EnableSsl,
        string FromAddress,
        string? FromName,
        string? Username,
        string? Password,
        string SourceDescription);
}
