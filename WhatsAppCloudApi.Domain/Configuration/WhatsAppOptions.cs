using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.Configuration;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    [Required]
    public string BaseUrl { get; set; } = "https://graph.facebook.com/v18.0/";

    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string PhoneNumberId { get; set; } = string.Empty;

    [Required]
    public string BusinessAccountId { get; set; } = string.Empty;

    public string AppSecret { get; set; } = string.Empty;

    [Required]
    public string VerifyToken { get; set; } = string.Empty;
}
