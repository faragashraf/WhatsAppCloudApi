using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

// ─── Conversation DTOs ──────────────────────────────────────
public sealed class ConversationQueryParams
{
    [MaxLength(200)]
    public string? Search { get; set; }

    [MaxLength(50)]
    public string? Status { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}

public sealed class SendConversationMessageRequest
{
    [Required, MaxLength(50)]
    public string MessageType { get; set; } = "text";

    [Required, MaxLength(4096)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? MediaUrl { get; set; }

    [MaxLength(100)]
    public string? MediaMimeType { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    [MaxLength(200)]
    public string? TemplateName { get; set; }

    [MaxLength(20)]
    public string? LanguageCode { get; set; }
}

public sealed class ConversationMessageQueryParams
{
    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 50;

    public DateTime? Before { get; set; }
    public DateTime? After { get; set; }
}

public sealed class AssignConversationRequest
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    public bool? UpdateContactOwner { get; set; }

    [MaxLength(100)]
    public string? Reason { get; set; }
}
