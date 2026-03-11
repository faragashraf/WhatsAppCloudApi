using WhatsAppCloudApi.Domain.Entities;

namespace WhatsAppCloudApi.Domain.Models;

public sealed class ResolvedConversationContext
{
    public string NormalizedPhoneNumber { get; init; } = string.Empty;
    public Contact Contact { get; init; } = null!;
    public Conversation Conversation { get; init; } = null!;
}

public sealed class AssignmentChangeResult
{
    public bool Changed { get; init; }
    public bool AssignmentChanged { get; init; }
    public bool OwnerChanged { get; init; }
    public bool NoAvailableAgent { get; init; }
    public Conversation Conversation { get; init; } = null!;
    public Contact Contact { get; init; } = null!;
}
