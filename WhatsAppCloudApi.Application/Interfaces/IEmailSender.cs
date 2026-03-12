namespace WhatsAppCloudApi.Application.Interfaces;

public interface IEmailSender
{
    Task SendPasswordResetOtpAsync(
        string toEmail,
        string? recipientName,
        string otp,
        TimeSpan expiresIn,
        int? companyId = null,
        CancellationToken cancellationToken = default);
}
