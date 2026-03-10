using WhatsAppCloudApi.Domain.DTOs;

namespace WhatsAppCloudApi.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> RegisterCompanyAsync(RegisterCompanyRequest request, CancellationToken cancellationToken = default);
    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResultDto> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);
    Task<bool> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}
