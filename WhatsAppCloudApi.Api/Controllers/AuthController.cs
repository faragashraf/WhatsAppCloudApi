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

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
}
