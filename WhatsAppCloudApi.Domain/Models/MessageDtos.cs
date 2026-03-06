using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace WhatsAppCloudApi.Domain.Models;

public sealed class SendTextMessageRequest
{
    [Required]
    public string To { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public bool PreviewUrl { get; set; }
}

public sealed class SendTemplateMessageRequest
{
    [Required]
    public string To { get; set; } = string.Empty;

    [Required]
    public string TemplateName { get; set; } = string.Empty;

    [Required]
    public string LanguageCode { get; set; } = "en_US";

    public List<TemplateComponentDto> Components { get; set; } = [];
}

public sealed class TemplateComponentDto
{
    [Required]
    public string Type { get; set; } = string.Empty;

    public List<TemplateParameterDto> Parameters { get; set; } = [];
}

public sealed class TemplateParameterDto
{
    [Required]
    public string Type { get; set; } = string.Empty;

    public string? Text { get; set; }
}

public sealed class SendMediaMessageRequest
{
    [Required]
    public string To { get; set; } = string.Empty;

    [Required]
    public string MediaType { get; set; } = string.Empty;

    public string? MediaId { get; set; }
    public string? Link { get; set; }
    public string? Caption { get; set; }
    public string? FileName { get; set; }
}

public sealed class UploadMediaRequest
{
    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string ContentType { get; set; } = string.Empty;

    [Required]
    public string Base64Data { get; set; } = string.Empty;
}

public sealed class MarkAsReadRequest
{
    [Required]
    public string MessageId { get; set; } = string.Empty;
}

public sealed class RegisterPhoneNumberRequest
{
    [Required]
    public string Pin { get; set; } = string.Empty;
}

public sealed class CreateTemplateRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Language { get; set; } = "en_US";

    [Required]
    public string Category { get; set; } = "MARKETING";

    [Required]
    public List<object> Components { get; set; } = [];
}

public sealed class UpdateBusinessProfileRequest
{
    public string? About { get; set; }
    public string? Address { get; set; }
    public string? Description { get; set; }
    public string? Email { get; set; }
    public string? ProfilePictureHandle { get; set; }
    public List<string>? Websites { get; set; }
    public string? Vertical { get; set; }
}

public sealed class RequestVerificationCodeRequest
{
    [Required]
    public string CodeMethod { get; set; } = "SMS";

    [Required]
    public string Locale { get; set; } = "en_US";
}

public sealed class VerifyCodeRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;
}

public sealed class GraphApiRequest
{
    [Required]
    public string Method { get; set; } = "GET";

    [Required]
    public string Path { get; set; } = string.Empty;

    public Dictionary<string, string?> Query { get; set; } = [];

    public object? Body { get; set; }
}

public sealed class GenericGraphResponse
{
    public string? MessagingProduct { get; set; }
    public List<object>? Contacts { get; set; }
    public List<object>? Messages { get; set; }
    public string? Id { get; set; }
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
