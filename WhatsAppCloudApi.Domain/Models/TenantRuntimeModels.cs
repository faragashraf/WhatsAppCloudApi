namespace WhatsAppCloudApi.Domain.Models;

public sealed class TenantContext
{
    public int CompanyId { get; init; }
    public int UserId { get; init; }
    public string Role { get; init; } = string.Empty;
}

public sealed class TenantWhatsAppConfig
{
    public int CompanyId { get; init; }
    public int WhatsAppAccountId { get; init; }
    public int WhatsAppPhoneNumberId { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public string PhoneNumberId { get; init; } = string.Empty;
    public string BusinessAccountId { get; init; } = string.Empty;
    public string VerifyToken { get; init; } = string.Empty;
    public string? AppSecret { get; init; }
}

public sealed class MessageQueuePayload
{
    public string Method { get; set; } = "POST";
    public string Path { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
