using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    [MinLength(64, ErrorMessage = "Jwt:Key must be at least 64 characters.")]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(5, 30, ErrorMessage = "Jwt:AccessTokenMinutes must be between 5 and 30.")]
    public int AccessTokenMinutes { get; set; } = 20;

    [Range(1, 14, ErrorMessage = "Jwt:RefreshTokenDays must be between 1 and 14.")]
    public int RefreshTokenDays { get; set; } = 7;
}
