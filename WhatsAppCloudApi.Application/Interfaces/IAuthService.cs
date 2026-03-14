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
    Task<TwoFactorSetupDto> BeginTwoFactorSetupAsync(int companyId, int userId, CancellationToken cancellationToken = default);
    Task<TwoFactorStatusDto> ActivateTwoFactorAsync(int companyId, int userId, TwoFactorActivateRequest request, CancellationToken cancellationToken = default);
    Task<TwoFactorStatusDto> DeactivateTwoFactorAsync(int companyId, int userId, CancellationToken cancellationToken = default);
    Task<TwoFactorStatusDto> GetTwoFactorStatusAsync(int companyId, int userId, CancellationToken cancellationToken = default);
}
