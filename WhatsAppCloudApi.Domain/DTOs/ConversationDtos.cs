using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

// ─── Conversation DTOs ──────────────────────────────────────
public sealed class ConversationQueryParams
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class SendConversationMessageRequest
{
    [Required]
    public string MessageType { get; set; } = "text";

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? MediaUrl { get; set; }
    public string? MediaMimeType { get; set; }
    public string? FileName { get; set; }
    public string? TemplateName { get; set; }
    public string? LanguageCode { get; set; }
}

public sealed class ConversationMessageQueryParams
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public DateTime? Before { get; set; }
    public DateTime? After { get; set; }
}

public sealed class AssignConversationRequest
{
    [Required]
    public int UserId { get; set; }
}
