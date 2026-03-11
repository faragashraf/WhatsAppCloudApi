using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Application.Exceptions;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> emailOptions, ILogger<SmtpEmailSender> logger)
    {
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetOtpAsync(
        string toEmail,
        string? recipientName,
        string otp,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var message = new MailMessage
        {
            From = CreateMailAddress(_emailOptions.FromAddress, _emailOptions.FromName),
            Subject = "Your password reset code",
            Body = BuildPasswordResetBody(recipientName, otp, expiresIn),
            IsBodyHtml = false
        };
        message.To.Add(CreateMailAddress(toEmail, recipientName));

        using var client = new SmtpClient(NormalizeSmtpHost(_emailOptions.Host), _emailOptions.Port)
        {
            EnableSsl = _emailOptions.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false
        };

        if (!string.IsNullOrWhiteSpace(_emailOptions.Username))
        {
            client.Credentials = new NetworkCredential(_emailOptions.Username, _emailOptions.Password ?? string.Empty);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await client.SendMailAsync(message);
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException)
        {
            _logger.LogError(ex, "SMTP delivery failed while sending password reset email to {Email}.", toEmail);
            throw new EmailDeliveryException(
                "Password reset email could not be sent. Check the SMTP settings for noreply@botglobalservice.com.",
                ex);
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(NormalizeSmtpHost(_emailOptions.Host)))
        {
            throw new EmailDeliveryException("Email sending is not configured. Set Email:Host before using forgot-password.");
        }

        if (string.IsNullOrWhiteSpace(_emailOptions.FromAddress))
        {
            throw new EmailDeliveryException("Email sending is not configured. Set Email:FromAddress before using forgot-password.");
        }

        if (string.IsNullOrWhiteSpace(_emailOptions.Username) ^ string.IsNullOrWhiteSpace(_emailOptions.Password))
        {
            throw new EmailDeliveryException("Email SMTP credentials are incomplete. Set both Email:Username and Email:Password.");
        }
    }

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
}
