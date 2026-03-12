namespace WhatsAppCloudApi.Domain.Models;

public sealed class FlowInboundMessage
{
    public string MessageType { get; set; } = "text";
    public string? Text { get; set; }
    public string? SelectionId { get; set; }
    public string? SelectionTitle { get; set; }
    public string? InteractiveType { get; set; }
    public string? StructuredDataJson { get; set; }
    public string? MetaMessageId { get; set; }
    public bool IsDryRun { get; set; }
}

public sealed class ConversationFlowRuntimeAction
{
    public string NodeId { get; init; } = string.Empty;
    public string ActionType { get; init; } = string.Empty;
    public string Preview { get; init; } = string.Empty;
    public string? MetadataJson { get; init; }
}

public sealed class ConversationFlowRuntimeResult
{
    public bool Handled { get; init; }
    public bool StartedNewSession { get; init; }
    public long? SessionId { get; init; }
    public string? SessionStatus { get; init; }
    public string? CurrentNodeId { get; init; }
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<ConversationFlowRuntimeAction> Actions { get; init; } = [];
}
