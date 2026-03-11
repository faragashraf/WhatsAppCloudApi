using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.Configuration;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Email:Port must be between 1 and 65535.")]
    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = "noreply@botglobalservice.com";

    public string FromName { get; set; } = "Bot Global Service";

    public string? Username { get; set; }

    public string? Password { get; set; }
}
