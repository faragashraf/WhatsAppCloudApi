namespace WhatsAppCloudApi.Application.Interfaces;

public interface IEmailSender
{
    Task SendPasswordResetOtpAsync(
        string toEmail,
        string? recipientName,
        string otp,
        TimeSpan expiresIn,
        CancellationToken cancellationToken = default);
}
