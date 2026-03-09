namespace WhatsAppCloudApi.Domain.Models;

/// <summary>
/// Granular permission flags per user.
/// Admin users always have full access regardless of these settings.
/// </summary>
public sealed class UserPermissions
{
    // ─── Contacts ───
    public bool ContactsView { get; set; } = true;
    public bool ContactsCreate { get; set; } = true;
    public bool ContactsEdit { get; set; } = true;
    public bool ContactsDelete { get; set; } = true;
    public bool ContactsImport { get; set; } = true;

    // ─── Campaigns ───
    public bool CampaignsView { get; set; } = true;
    public bool CampaignsCreate { get; set; } = true;
    public bool CampaignsEdit { get; set; } = true;
    public bool CampaignsLaunch { get; set; } = true;

    // ─── Automation ───
    public bool AutomationView { get; set; } = true;
    public bool AutomationCreate { get; set; } = true;
    public bool AutomationEdit { get; set; } = true;
    public bool AutomationDelete { get; set; } = true;

    // ─── Conversations ───
    public bool ConversationsView { get; set; } = true;
    public bool ConversationsSend { get; set; } = true;
    public bool ConversationsAttach { get; set; } = true;
    public bool ConversationsAssign { get; set; } = false;

    // ─── Templates ───
    public bool TemplatesView { get; set; } = true;
    public bool TemplatesCreate { get; set; } = true;
    public bool TemplatesEdit { get; set; } = true;
    public bool TemplatesDelete { get; set; } = true;

    // ─── Messages (Logs) ───
    public bool MessagesView { get; set; } = true;

    /// <summary>Returns a fully-permissive instance (for admins or defaults).</summary>
    public static UserPermissions FullAccess() => new()
    {
        ContactsView = true, ContactsCreate = true, ContactsEdit = true, ContactsDelete = true, ContactsImport = true,
        CampaignsView = true, CampaignsCreate = true, CampaignsEdit = true, CampaignsLaunch = true,
        AutomationView = true, AutomationCreate = true, AutomationEdit = true, AutomationDelete = true,
        ConversationsView = true, ConversationsSend = true, ConversationsAttach = true, ConversationsAssign = true,
        TemplatesView = true, TemplatesCreate = true, TemplatesEdit = true, TemplatesDelete = true,
        MessagesView = true,
    };

    /// <summary>Returns a standard member permission set (no assign, all other features enabled).</summary>
    public static UserPermissions MemberDefault() => new()
    {
        ContactsView = true, ContactsCreate = true, ContactsEdit = true, ContactsDelete = false, ContactsImport = false,
        CampaignsView = true, CampaignsCreate = false, CampaignsEdit = false, CampaignsLaunch = false,
        AutomationView = true, AutomationCreate = false, AutomationEdit = false, AutomationDelete = false,
        ConversationsView = true, ConversationsSend = true, ConversationsAttach = true, ConversationsAssign = false,
        TemplatesView = true, TemplatesCreate = false, TemplatesEdit = false, TemplatesDelete = false,
        MessagesView = true,
    };
}
