using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class RegisterCompanyRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string AdminFullName { get; set; } = string.Empty;
    public string AdminEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshTokenRequest
{
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
    public string Role { get; set; } = string.Empty;
    public UserPermissions Permissions { get; set; } = new();
    public AuthTokensDto Tokens { get; set; } = new();
}
