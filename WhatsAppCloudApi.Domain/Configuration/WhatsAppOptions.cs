using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.Configuration;

public sealed class WhatsAppOptions
{
    public const string SectionName = "WhatsApp";

    [Required]
    public string BaseUrl { get; set; } = "https://graph.facebook.com/v18.0/";

    public string? AccessToken { get; set; }

    public string? PhoneNumberId { get; set; }

    public string? BusinessAccountId { get; set; }

    public string? BusinessId { get; set; }

    public string? AppSecret { get; set; }

    public string? VerifyToken { get; set; }
}
