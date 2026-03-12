using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using WhatsAppCloudApi.Domain.Models;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class CustomWebhookDispatchRequest
{
    [Required, MaxLength(30)]
    public string To { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [Range(1, int.MaxValue)]
    public int? WhatsAppPhoneNumberId { get; set; }

    [MaxLength(64)]
    public string? PhoneNumberId { get; set; }

    public Dictionary<string, JsonElement> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public JsonElement? Data { get; set; }
    public bool StrictVariables { get; set; }
    public bool KeepUnresolvedPlaceholders { get; set; }
    public bool AllowOutside24HourWindow { get; set; }

    /// <summary>
    /// Controls behavior when the 24-hour window is closed: block, allow_text, template.
    /// </summary>
    [MaxLength(32)]
    public string? OutsideWindowAction { get; set; }

    public CustomWebhookOutsideWindowTemplateDto? OutsideWindowTemplate { get; set; }
}

public sealed class CustomWebhookOutsideWindowTemplateDto
{
    [Required, MaxLength(200)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string LanguageCode { get; set; } = "en_US";

    public List<TemplateComponentDto> Components { get; set; } = [];
}

public sealed class CustomWebhookDispatchResultDto
{
    public int CompanyId { get; set; }
    public int WhatsAppPhoneNumberId { get; set; }
    public long ContactId { get; set; }
    public long ConversationId { get; set; }
    public long MessageId { get; set; }
    public long ConversationMessageId { get; set; }
    public string To { get; set; } = string.Empty;
    public string RenderedMessage { get; set; } = string.Empty;
    public List<string> MissingVariables { get; set; } = [];
    public string DispatchMode { get; set; } = "text";
    public bool ConversationWindowOpen { get; set; }
}
