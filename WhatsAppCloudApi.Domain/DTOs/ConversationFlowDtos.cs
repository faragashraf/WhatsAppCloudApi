using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class ConversationFlowUpsertRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required, MaxLength(50)]
    public string EntryTriggerType { get; set; } = "any_message";

    [MaxLength(500)]
    public string? EntryTriggerValue { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    public ConversationFlowGraphDto Definition { get; set; } = new();
}

public sealed class ConversationFlowGraphDto
{
    public List<ConversationFlowNodeDto> Nodes { get; set; } = [];
    public List<ConversationFlowEdgeDto> Edges { get; set; } = [];
}

public sealed class ConversationFlowNodeDto
{
    [Required, MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Type { get; set; } = "message";

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Range(-100000, 100000)]
    public decimal X { get; set; }

    [Range(-100000, 100000)]
    public decimal Y { get; set; }

    [MaxLength(4000)]
    public string? BodyText { get; set; }

    [MaxLength(1000)]
    public string? FooterText { get; set; }

    [MaxLength(120)]
    public string? ButtonText { get; set; }

    [MaxLength(100)]
    public string? VariableName { get; set; }

    [MaxLength(1000)]
    public string? InvalidInputMessage { get; set; }

    [MaxLength(30)]
    public string? MenuPresentation { get; set; }

    public List<ConversationFlowOptionDto> Options { get; set; } = [];
    public List<ConversationFlowListSectionDto> Sections { get; set; } = [];

    [MaxLength(20)]
    public string? AssignMode { get; set; }

    public int? AssignToUserId { get; set; }
    public bool UpdateContactOwner { get; set; }

    [MaxLength(100)]
    public string? AssignReason { get; set; }

    [MaxLength(2000)]
    public string? Url { get; set; }

    [MaxLength(120)]
    public string? LinkLabel { get; set; }

    [MaxLength(120)]
    public string? MetaFlowId { get; set; }

    [MaxLength(120)]
    public string? MetaFlowName { get; set; }

    [MaxLength(120)]
    public string? MetaFlowCta { get; set; }

    [MaxLength(30)]
    public string? MetaFlowMode { get; set; }

    [MaxLength(30)]
    public string? MetaFlowAction { get; set; }

    [MaxLength(120)]
    public string? MetaFlowScreen { get; set; }

    [MaxLength(4000)]
    public string? MetaFlowDataJson { get; set; }
}

public sealed class ConversationFlowOptionDto
{
    [Required, MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }
}

public sealed class ConversationFlowListSectionDto
{
    [Required, MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    public List<ConversationFlowOptionDto> Options { get; set; } = [];
}

public sealed class ConversationFlowEdgeDto
{
    [Required, MaxLength(100)]
    public string Id { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string SourceNodeId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string TargetNodeId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SourceHandle { get; set; }

    [MaxLength(200)]
    public string? Label { get; set; }
}

public sealed class ConversationFlowDto
{
    public long ConversationFlowId { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntryTriggerType { get; set; } = "any_message";
    public string? EntryTriggerValue { get; set; }
    public bool IsActive { get; set; }
    public bool IsPublished { get; set; }
    public int DraftVersion { get; set; }
    public int? PublishedVersion { get; set; }
    public long TriggerCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public ConversationFlowGraphDto Definition { get; set; } = new();
    public int ActiveSessionCount { get; set; }
}

public sealed class ConversationFlowSimulationRequest
{
    [Required, MaxLength(30)]
    public string ContactNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ContactName { get; set; }

    [Range(1, int.MaxValue)]
    public int? WhatsAppPhoneNumberId { get; set; }

    [Required, MaxLength(50)]
    public string MessageType { get; set; } = "text";

    [MaxLength(4000)]
    public string? Content { get; set; }

    [MaxLength(100)]
    public string? SelectionId { get; set; }

    [MaxLength(200)]
    public string? SelectionTitle { get; set; }

    [MaxLength(120)]
    public string? MetaMessageId { get; set; }

    public bool DryRun { get; set; } = true;
    public bool UsePublishedVersion { get; set; }
}

public sealed class ConversationFlowSimulationActionDto
{
    public string NodeId { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string Preview { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
}

public sealed class ConversationFlowSimulationResultDto
{
    public long ConversationId { get; set; }
    public long ContactId { get; set; }
    public bool Handled { get; set; }
    public bool StartedNewSession { get; set; }
    public long? SessionId { get; set; }
    public string? SessionStatus { get; set; }
    public string? CurrentNodeId { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ConversationFlowSimulationActionDto> Actions { get; set; } = [];
}

public sealed class ConversationFlowFormSubmissionDto
{
    public long ConversationFlowFormSubmissionId { get; set; }
    public long ConversationFlowId { get; set; }
    public long? ConversationFlowSessionId { get; set; }
    public long? ConversationId { get; set; }
    public long? ContactId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? InboundMessageType { get; set; }
    public string? MetaMessageId { get; set; }
    public string? PayloadJson { get; set; }
    public Dictionary<string, string> ExtractedValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class ConversationFlowFormSubmissionListDto
{
    public int TotalCount { get; set; }
    public List<ConversationFlowFormSubmissionDto> Items { get; set; } = [];
}
