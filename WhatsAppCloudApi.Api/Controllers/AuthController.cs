using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.DTOs;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting("auth")]
[RequestSizeLimit(64 * 1024)]
public sealed class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public AuthController(IAuthService authService, ITenantContextAccessor tenantContextAccessor)
    {
        _authService = authService;
        _tenantContextAccessor = tenantContextAccessor;
    }

    [HttpPost("register-company")]
    public async Task<IActionResult> RegisterCompany([FromBody] RegisterCompanyRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterCompanyAsync(request, cancellationToken);
        return ToActionResult(ApiResponse<AuthResultDto>.Ok(result, "Company registered."));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return ToActionResult(ApiResponse<AuthResultDto>.Ok(result, "Login succeeded."));
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, cancellationToken);
        return ToActionResult(ApiResponse<AuthResultDto>.Ok(result, "Token refreshed."));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);
        return ToActionResult(ApiResponse<string>.Ok("If the email exists, an OTP has been sent.", "OTP sent."));
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var valid = await _authService.VerifyOtpAsync(request, cancellationToken);
        return ToActionResult(ApiResponse<bool>.Ok(valid, "OTP verified."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request, cancellationToken);
        return ToActionResult(ApiResponse<string>.Ok("Password reset successfully.", "Password updated."));
    }

    [HttpGet("2fa/status")]
    [Authorize]
    public async Task<IActionResult> GetTwoFactorStatus(CancellationToken cancellationToken)
    {
        var ctx = _tenantContextAccessor.GetRequiredContext();
        var status = await _authService.GetTwoFactorStatusAsync(ctx.CompanyId, ctx.UserId, cancellationToken);
        return ToActionResult(ApiResponse<TwoFactorStatusDto>.Ok(status));
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<IActionResult> BeginTwoFactorSetup(CancellationToken cancellationToken)
    {
        var ctx = _tenantContextAccessor.GetRequiredContext();
        var setup = await _authService.BeginTwoFactorSetupAsync(ctx.CompanyId, ctx.UserId, cancellationToken);
        return ToActionResult(ApiResponse<TwoFactorSetupDto>.Ok(setup));
    }

    [HttpPost("2fa/activate")]
    [Authorize]
    public async Task<IActionResult> ActivateTwoFactor([FromBody] TwoFactorActivateRequest request, CancellationToken cancellationToken)
    {
        var ctx = _tenantContextAccessor.GetRequiredContext();
        var status = await _authService.ActivateTwoFactorAsync(ctx.CompanyId, ctx.UserId, request, cancellationToken);
        return ToActionResult(ApiResponse<TwoFactorStatusDto>.Ok(status, "Two-factor authentication enabled."));
    }

    [HttpPost("2fa/deactivate")]
    [Authorize]
    public async Task<IActionResult> DeactivateTwoFactor(CancellationToken cancellationToken)
    {
        var ctx = _tenantContextAccessor.GetRequiredContext();
        var status = await _authService.DeactivateTwoFactorAsync(ctx.CompanyId, ctx.UserId, cancellationToken);
        return ToActionResult(ApiResponse<TwoFactorStatusDto>.Ok(status, "Two-factor authentication disabled."));
    }
}
