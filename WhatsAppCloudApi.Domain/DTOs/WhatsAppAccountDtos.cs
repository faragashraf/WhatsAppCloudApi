using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class MetaBusinessAccountUpsertRequest
{
    [Required, MaxLength(100)]
    public string BusinessId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? AccessToken { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WhatsAppAccountUpsertRequest
{
    public int? MetaBusinessAccountId { get; set; }

    [Required, MaxLength(100)]
    public string BusinessAccountId { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string AccessToken { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string VerifyToken { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? AppSecret { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
}

public sealed class WhatsAppPhoneNumberUpsertRequest
{
    [MaxLength(100)]
    public string? WhatsAppAccountId { get; set; }

    [MaxLength(100)]
    public string? BusinessAccountId { get; set; }

    [Required, MaxLength(100)]
    public string PhoneNumberId { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string DisplayPhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? VerifiedName { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
}
