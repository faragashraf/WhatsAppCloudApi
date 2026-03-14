using System.ComponentModel.DataAnnotations;
using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class RegisterCompanyRequest
{
    [Required, MaxLength(200)]
    public string CompanyName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string CompanyCode { get; set; } = string.Empty;

    [EmailAddress, MaxLength(200)]
    public string CompanyEmail { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string AdminFullName { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(200)]
    public string AdminEmail { get; set; } = string.Empty;

    [Required, MinLength(10), MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(256)]
    public string Password { get; set; } = string.Empty;

    [RegularExpression("^\\d{6}$")]
    public string? TwoFactorCode { get; set; }
}

public sealed class RefreshTokenRequest
{
    [Required, MinLength(20), MaxLength(4096)]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class AuthTokensDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }
}

public sealed class AuthResultDto
{
    public int UserId { get; set; }
    public int CompanyId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsSuperAdmin { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public UserPermissions Permissions { get; set; } = new();
    public AuthTokensDto Tokens { get; set; } = new();
}

public sealed class ForgotPasswordRequest
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;
}

public sealed class VerifyOtpRequest
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression("^\\d{6}$")]
    public string Otp { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress, MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression("^\\d{6}$")]
    public string Otp { get; set; } = string.Empty;

    [Required, MinLength(10), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class TwoFactorSetupDto
{
    public bool IsEnabled { get; set; }
    public string Issuer { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string ManualEntryKey { get; set; } = string.Empty;
    public string OtpAuthUri { get; set; } = string.Empty;
}

public sealed class TwoFactorStatusDto
{
    public bool IsEnabled { get; set; }
    public DateTime? EnabledAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class TwoFactorActivateRequest
{
    [Required, RegularExpression("^\\d{6}$")]
    public string Code { get; set; } = string.Empty;
}
