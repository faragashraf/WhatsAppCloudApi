namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class MetaBusinessAccountUpsertRequest
{
    public string BusinessId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AccessToken { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class WhatsAppAccountUpsertRequest
{
    public int? MetaBusinessAccountId { get; set; }
    public string BusinessAccountId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public string? AppSecret { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
}

public sealed class WhatsAppPhoneNumberUpsertRequest
{
    public string? WhatsAppAccountId { get; set; }
    public string? BusinessAccountId { get; set; }
    public string PhoneNumberId { get; set; } = string.Empty;
    public string DisplayPhoneNumber { get; set; } = string.Empty;
    public string? VerifiedName { get; set; }
    public bool IsDefault { get; set; } = false;
    public bool IsActive { get; set; } = true;
}
