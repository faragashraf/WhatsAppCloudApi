using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace WhatsAppCloudApi.Domain.Models;

public sealed class SendTextMessageRequest
{
    [Required, MaxLength(30)]
    public string To { get; set; } = string.Empty;

    [Required, MaxLength(4096)]
    public string Body { get; set; } = string.Empty;

    public bool PreviewUrl { get; set; }

    /// <summary>Optional: WhatsApp phone number ID to send from. If omitted, the company default is used.</summary>
    public string? PhoneNumberId { get; set; }
}

public sealed class SendTemplateMessageRequest
{
    [Required, MaxLength(30)]
    public string To { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string TemplateName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string LanguageCode { get; set; } = "en_US";

    public List<TemplateComponentDto> Components { get; set; } = [];

    /// <summary>Optional: WhatsApp phone number ID to send from. If omitted, the company default is used.</summary>
    public string? PhoneNumberId { get; set; }
}

public sealed class TemplateComponentDto
{
    [Required, MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    public List<TemplateParameterDto> Parameters { get; set; } = [];
}

public sealed class TemplateParameterDto
{
    [Required, MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Text { get; set; }
}

public sealed class SendMediaMessageRequest
{
    [Required, MaxLength(30)]
    public string To { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string MediaType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? MediaId { get; set; }
    [MaxLength(2000)]
    public string? Link { get; set; }
    [MaxLength(1024)]
    public string? Caption { get; set; }
    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(100)]
    public string? PhoneNumberId { get; set; }
}

public sealed class SendDirectMediaFileMessageRequest
{
    [Required, MaxLength(30)]
    public string To { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? MediaType { get; set; }

    [MaxLength(1024)]
    public string? Caption { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(100)]
    public string? ContentType { get; set; }

    [MaxLength(100)]
    public string? PhoneNumberId { get; set; }

    [Required]
    public byte[] FileData { get; set; } = [];
}

public sealed class UploadMediaRequest
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required, MaxLength(14_000_000)]
    public string Base64Data { get; set; } = string.Empty;
}

public sealed class UploadMediaFileRequest
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public byte[] FileData { get; set; } = [];
}

public sealed class MarkAsReadRequest
{
    [Required, MaxLength(200)]
    public string MessageId { get; set; } = string.Empty;
}

public sealed class RegisterPhoneNumberRequest
{
    [Required, RegularExpression("^\\d{6,8}$")]
    public string Pin { get; set; } = string.Empty;
}

public sealed class CreateTemplateRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Language { get; set; } = "en_US";

    [Required, MaxLength(50)]
    public string Category { get; set; } = "MARKETING";

    [Required]
    [MaxLength(100)]
    public List<object> Components { get; set; } = [];
}

public sealed class UpdateBusinessProfileRequest
{
    [MaxLength(140)]
    public string? About { get; set; }
    [MaxLength(256)]
    public string? Address { get; set; }
    [MaxLength(512)]
    public string? Description { get; set; }
    [EmailAddress, MaxLength(200)]
    public string? Email { get; set; }
    [MaxLength(255)]
    public string? ProfilePictureHandle { get; set; }
    [MaxLength(20)]
    public List<string>? Websites { get; set; }
    [MaxLength(100)]
    public string? Vertical { get; set; }
}

public sealed class RequestVerificationCodeRequest
{
    [Required, MaxLength(20)]
    public string CodeMethod { get; set; } = "SMS";

    [Required, MaxLength(20)]
    public string Locale { get; set; } = "en_US";
}

public sealed class VerifyCodeRequest
{
    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;
}

public sealed class GraphApiRequest
{
    [Required, MaxLength(10)]
    public string Method { get; set; } = "GET";

    [Required, MaxLength(1024)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(50)]
    public Dictionary<string, string?> Query { get; set; } = [];

    public object? Body { get; set; }
}

public sealed class GenericGraphResponse
{
    public string? MessagingProduct { get; set; }
    public List<object>? Contacts { get; set; }
    public List<object>? Messages { get; set; }
    public string? Id { get; set; }
    public long? MessageId { get; set; }
    public long? ContactId { get; set; }
    public long? ConversationId { get; set; }
    public bool? Success { get; set; }
    public string? Url { get; set; }
    // For list responses such as message templates or phone numbers — keep raw JSON elements to preserve schema
    [JsonPropertyName("data")]
    public List<JsonElement>? Data { get; set; }

    [JsonPropertyName("paging")]
    public Paging? Paging { get; set; }

    [JsonPropertyName("rawContent")]
    public string? RawContent { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class TemplateDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("message_send_ttl_seconds")]
    public int? MessageSendTtlSeconds { get; set; }

    [JsonPropertyName("parameter_format")]
    public string? ParameterFormat { get; set; }

    [JsonPropertyName("components")]
    public List<ComponentDto>? Components { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("library_template_name")]
    public string? LibraryTemplateName { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public sealed class ComponentDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("example")]
    public ExampleDto? Example { get; set; }

    [JsonPropertyName("buttons")]
    public List<ButtonDto>? Buttons { get; set; }
}

public sealed class ExampleDto
{
    [JsonPropertyName("body_text")]
    public List<List<string>>? BodyText { get; set; }
}

public sealed class ButtonDto
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("example")]
    public List<string>? Example { get; set; }
}

public sealed class Paging
{
    [JsonPropertyName("cursors")]
    public Cursors? Cursors { get; set; }
}

public sealed class Cursors
{
    [JsonPropertyName("before")]
    public string? Before { get; set; }

    [JsonPropertyName("after")]
    public string? After { get; set; }
}
