using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WhatsAppCloudApi.Domain.Entities;

namespace WhatsAppCloudApi.Infrastructure.Data;

public sealed class ApplicationDbContext : DbContext
{
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        toDb => toDb.Kind == DateTimeKind.Utc ? toDb : toDb.ToUniversalTime(),
        fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcDateTimeConverter = new(
        toDb => toDb.HasValue
            ? (toDb.Value.Kind == DateTimeKind.Utc ? toDb.Value : toDb.Value.ToUniversalTime())
            : toDb,
        fromDb => fromDb.HasValue ? DateTime.SpecifyKind(fromDb.Value, DateTimeKind.Utc) : fromDb);

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<CompanySubscription> CompanySubscriptions => Set<CompanySubscription>();
    public DbSet<MetaBusinessAccount> MetaBusinessAccounts => Set<MetaBusinessAccount>();
    public DbSet<WhatsAppAccount> WhatsAppAccounts => Set<WhatsAppAccount>();
    public DbSet<WhatsAppPhoneNumber> WhatsAppPhoneNumbers => Set<WhatsAppPhoneNumber>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<MessageQueueItem> MessageQueue => Set<MessageQueueItem>();
    public DbSet<EmailAccount> EmailAccounts => Set<EmailAccount>();
    public DbSet<EmailQueueItem> EmailQueue => Set<EmailQueueItem>();
    public DbSet<EmailQueueAttachment> EmailQueueAttachments => Set<EmailQueueAttachment>();
    public DbSet<EmailNotificationRule> EmailNotificationRules => Set<EmailNotificationRule>();
    public DbSet<ApiLog> ApiLogs => Set<ApiLog>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<CompanyRoutingSettings> CompanyRoutingSettings => Set<CompanyRoutingSettings>();
    public DbSet<CompanyUserRoutingSettings> CompanyUserRoutingSettings => Set<CompanyUserRoutingSettings>();
    public DbSet<ConversationAssignmentHistory> ConversationAssignmentHistory => Set<ConversationAssignmentHistory>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignContact> CampaignContacts => Set<CampaignContact>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<ConversationFlow> ConversationFlows => Set<ConversationFlow>();
    public DbSet<ConversationFlowSession> ConversationFlowSessions => Set<ConversationFlowSession>();
    public DbSet<ConversationFlowExecutionLog> ConversationFlowExecutionLogs => Set<ConversationFlowExecutionLog>();
    public DbSet<ConversationFlowFormSubmission> ConversationFlowFormSubmissions => Set<ConversationFlowFormSubmission>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WebhookLog> WebhookLogs => Set<WebhookLog>();
    public DbSet<WebhookInboxItem> WebhookInbox => Set<WebhookInboxItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Companies ──────────────────────────────────────────────
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Companies");
            entity.HasKey(x => x.CompanyId);
            entity.Property(x => x.CompanyId).UseIdentityColumn();
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("ACTIVE");
            entity.Property(x => x.CreatedAt).HasColumnType("datetime2");
            entity.Property(x => x.TrialStartDate).HasColumnType("datetime2");
            entity.Property(x => x.TrialEndDate).HasColumnType("datetime2");
            entity.Property(x => x.SubscriptionEndDate).HasColumnType("datetime2");

            entity.HasOne(x => x.RoutingSettings)
                .WithOne(x => x.Company)
                .HasForeignKey<CompanyRoutingSettings>(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── CompanyUsers ───────────────────────────────────────────
        modelBuilder.Entity<CompanyUser>(entity =>
        {
            entity.ToTable("CompanyUsers");
            entity.HasKey(x => x.CompanyUserId);
            entity.Property(x => x.CompanyUserId).UseIdentityColumn();
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(50).HasDefaultValue("Admin");
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.RefreshToken).HasMaxLength(2000);
            entity.Property(x => x.RefreshTokenExpiryUtc).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.Email }).IsUnique();

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.RoutingSettings)
                .WithOne(x => x.CompanyUser)
                .HasForeignKey<CompanyUserRoutingSettings>(x => x.CompanyUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<CompanyRoutingSettings>(entity =>
        {
            entity.ToTable("CompanyRoutingSettings");
            entity.HasKey(x => x.CompanyRoutingSettingsId);
            entity.Property(x => x.CompanyRoutingSettingsId).UseIdentityColumn();
            entity.Property(x => x.AssignmentMode).HasMaxLength(20).HasDefaultValue("MANUAL");
            entity.Property(x => x.AutoAssignmentStrategy).HasMaxLength(30).HasDefaultValue("ROUND_ROBIN");
            entity.Property(x => x.RespectExistingContactOwner).HasDefaultValue(true);
            entity.Property(x => x.ReassignWhenOwnerInactive).HasDefaultValue(true);
            entity.Property(x => x.ManualReassignmentUpdatesContactOwner).HasDefaultValue(false);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => x.CompanyId).IsUnique();
        });

        modelBuilder.Entity<CompanyUserRoutingSettings>(entity =>
        {
            entity.ToTable("CompanyUserRoutingSettings");
            entity.HasKey(x => x.CompanyUserRoutingSettingsId);
            entity.Property(x => x.CompanyUserRoutingSettingsId).UseIdentityColumn();
            entity.Property(x => x.CanReceiveManualAssignments).HasDefaultValue(true);
            entity.Property(x => x.CanReceiveAutoAssignments).HasDefaultValue(true);
            entity.Property(x => x.LastAutoAssignedAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.CompanyUserId }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.CanReceiveManualAssignments, x.CompanyUserId });
            entity.HasIndex(x => new { x.CompanyId, x.CanReceiveAutoAssignments, x.LastAutoAssignedAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.UserRoutingSettings)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CompanyUser)
                .WithOne(x => x.RoutingSettings)
                .HasForeignKey<CompanyUserRoutingSettings>(x => x.CompanyUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ── SubscriptionPlans ──────────────────────────────────────
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans");
            entity.HasKey(x => x.SubscriptionPlanId);
            entity.Property(x => x.SubscriptionPlanId).UseIdentityColumn();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => x.Code).IsUnique();
        });

        // ── CompanySubscriptions ───────────────────────────────────
        modelBuilder.Entity<CompanySubscription>(entity =>
        {
            entity.ToTable("CompanySubscriptions");
            entity.HasKey(x => x.CompanySubscriptionId);
            entity.Property(x => x.CompanySubscriptionId).UseIdentityColumn();
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("TRIAL");
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.TrialStartDate).HasColumnType("datetime2");
            entity.Property(x => x.TrialEndDate).HasColumnType("datetime2");
            entity.Property(x => x.StartDate).HasColumnType("datetime2");
            entity.Property(x => x.EndDate).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.SubscriptionPlan)
                .WithMany(x => x.CompanySubscriptions)
                .HasForeignKey(x => x.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── MetaBusinessAccounts ───────────────────────────────────
        modelBuilder.Entity<MetaBusinessAccount>(entity =>
        {
            entity.ToTable("MetaBusinessAccounts");
            entity.HasKey(x => x.MetaBusinessAccountId);
            entity.Property(x => x.MetaBusinessAccountId).UseIdentityColumn();
            entity.Property(x => x.BusinessId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AccessToken).HasMaxLength(2000);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.MetaBusinessAccounts)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── WhatsAppAccounts ───────────────────────────────────────
        modelBuilder.Entity<WhatsAppAccount>(entity =>
        {
            entity.ToTable("WhatsAppAccounts");
            entity.HasKey(x => x.WhatsAppAccountId);
            entity.Property(x => x.WhatsAppAccountId).HasMaxLength(100);
            entity.Property(x => x.BusinessAccountId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AccessToken).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.VerifyToken).HasMaxLength(256).IsRequired();
            entity.Property(x => x.AppSecret).HasMaxLength(500);
            entity.Property(x => x.IsDefault).HasDefaultValue(true);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.WhatsAppAccounts)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.MetaBusinessAccount)
                .WithMany(x => x.WhatsAppAccounts)
                .HasForeignKey(x => x.MetaBusinessAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── WhatsAppPhoneNumbers ───────────────────────────────────
        modelBuilder.Entity<WhatsAppPhoneNumber>(entity =>
        {
            entity.ToTable("WhatsAppPhoneNumbers");
            entity.HasKey(x => x.WhatsAppPhoneNumberId);
            entity.Property(x => x.WhatsAppPhoneNumberId).UseIdentityColumn();
            entity.Property(x => x.WhatsAppAccountId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PhoneNumberId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.DisplayPhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.VerifiedName).HasMaxLength(200);
            entity.Property(x => x.CodeVerificationStatus).HasMaxLength(50);
            entity.Property(x => x.QualityRating).HasMaxLength(50);
            entity.Property(x => x.PlatformType).HasMaxLength(50);
            entity.Property(x => x.ThroughputLevel).HasMaxLength(50);
            entity.Property(x => x.LastOnboardedTime).HasMaxLength(100);
            entity.Property(x => x.IsDefault).HasDefaultValue(true);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.LastSyncUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.WhatsAppPhoneNumbers)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.WhatsAppAccount)
                .WithMany(x => x.PhoneNumbers)
                .HasForeignKey(x => x.WhatsAppAccountId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // ── Messages ───────────────────────────────────────────────
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(x => x.MessageId);
            entity.Property(x => x.MessageId).UseIdentityColumn();
            entity.Property(x => x.ToNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.MessageType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.MessageBody).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("PENDING");
            entity.Property(x => x.Source).HasMaxLength(30).HasDefaultValue("DIRECT");
            entity.Property(x => x.ExternalMessageId).HasMaxLength(120);
            entity.Property(x => x.FailureReason).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.ConversationId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ContactId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ExternalMessageId })
                .HasFilter("[ExternalMessageId] IS NOT NULL")
                .IsUnique();

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.WhatsAppPhoneNumber)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.WhatsAppPhoneNumberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Contact)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.OutboundMessages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── MessageQueue ───────────────────────────────────────────
        modelBuilder.Entity<MessageQueueItem>(entity =>
        {
            entity.ToTable("MessageQueue");
            entity.HasKey(x => x.MessageQueueId);
            entity.Property(x => x.MessageQueueId).UseIdentityColumn();
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("PENDING");
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.LastError).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.LastAttemptAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.MessageQueueItems)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Message)
                .WithMany(x => x.QueueItems)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EmailAccount>(entity =>
        {
            entity.ToTable("EmailAccounts");
            entity.HasKey(x => x.EmailAccountId);
            entity.Property(x => x.EmailAccountId).UseIdentityColumn();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.FromAddress).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ReplyToAddress).HasMaxLength(200);
            entity.Property(x => x.FromName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SmtpHost).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SmtpPort).HasDefaultValue(587);
            entity.Property(x => x.EnableSsl).HasDefaultValue(true);
            entity.Property(x => x.Username).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PasswordProtected).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.IsDefault).HasDefaultValue(false);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();

            entity.HasOne(x => x.Company)
                .WithMany(x => x.EmailAccounts)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EmailNotificationRule>(entity =>
        {
            entity.ToTable("EmailNotificationRules");
            entity.HasKey(x => x.EmailNotificationRuleId);
            entity.Property(x => x.EmailNotificationRuleId).UseIdentityColumn();
            entity.Property(x => x.Scope).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TriggerType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.RecipientMode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.RecipientsJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.SubjectTemplate).HasMaxLength(300).IsRequired();
            entity.Property(x => x.BodyTemplate).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.LastTriggeredAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.Scope, x.IsActive });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.EmailNotificationRules)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.EmailAccount)
                .WithMany(x => x.NotificationRules)
                .HasForeignKey(x => x.EmailAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EmailQueueItem>(entity =>
        {
            entity.ToTable("EmailQueue");
            entity.HasKey(x => x.EmailQueueItemId);
            entity.Property(x => x.EmailQueueItemId).UseIdentityColumn();
            entity.Property(x => x.Scope).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TriggerType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ToJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.CcJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.BccJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Body).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("PENDING");
            entity.Property(x => x.LastError).HasColumnType("nvarchar(max)");
            entity.Property(x => x.DeduplicationKey).HasMaxLength(300);
            entity.Property(x => x.ScheduledAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.LastAttemptAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.SentAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.Scope, x.Status, x.ScheduledAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.Scope, x.CreatedAtUtc });
            entity.HasIndex(x => x.DeduplicationKey)
                .HasFilter("[DeduplicationKey] IS NOT NULL")
                .IsUnique();

            entity.HasOne(x => x.Company)
                .WithMany(x => x.EmailQueueItems)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.EmailAccount)
                .WithMany(x => x.QueueItems)
                .HasForeignKey(x => x.EmailAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.EmailNotificationRule)
                .WithMany(x => x.QueueItems)
                .HasForeignKey(x => x.EmailNotificationRuleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.CreatedByUser)
                .WithMany()
                .HasForeignKey(x => x.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<EmailQueueAttachment>(entity =>
        {
            entity.ToTable("EmailQueueAttachments");
            entity.HasKey(x => x.EmailQueueAttachmentId);
            entity.Property(x => x.EmailQueueAttachmentId).UseIdentityColumn();
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
            entity.Property(x => x.ContentBase64).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(x => x.EmailQueueItem)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.EmailQueueItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ApiLogs ────────────────────────────────────────────────
        modelBuilder.Entity<ApiLog>(entity =>
        {
            entity.ToTable("ApiLogs");
            entity.HasKey(x => x.ApiLogId);
            entity.Property(x => x.ApiLogId).UseIdentityColumn();
            entity.Property(x => x.Endpoint).HasMaxLength(500).IsRequired();
            entity.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            entity.Property(x => x.RequestBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.IpAddress).HasMaxLength(100);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.ApiLogs)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.CompanyUser)
                .WithMany()
                .HasForeignKey(x => x.CompanyUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Contacts ───────────────────────────────────────────────
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("Contacts");
            entity.HasKey(x => x.ContactId);
            entity.Property(x => x.ContactId).UseIdentityColumn();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Tags).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CustomFields).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Source).HasMaxLength(100);
            entity.Property(x => x.Notes).HasColumnType("nvarchar(max)");
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.FirstSeenAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.LastSeenAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.LastInboundMessageAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.LastOutboundMessageAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.OwnerAssignedAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.PhoneNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.OwnerUserId, x.IsActive });
            entity.HasIndex(x => new { x.CompanyId, x.LastSeenAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Contacts)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.OwnerUser)
                .WithMany()
                .HasForeignKey(x => x.OwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Conversations ──────────────────────────────────────────
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("Conversations");
            entity.HasKey(x => x.ConversationId);
            entity.Property(x => x.ConversationId).UseIdentityColumn();
            entity.Property(x => x.ContactNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(200);
            entity.Property(x => x.LastMessageContent).HasMaxLength(1000);
            entity.Property(x => x.LastMessageType).HasMaxLength(50);
            entity.Property(x => x.LastMessageAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.LastInboundMessageAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("OPEN");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.ContactId, x.WhatsAppPhoneNumberId }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.AssignedUserId, x.Status, x.LastMessageAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ContactId, x.LastMessageAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Conversations)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.WhatsAppPhoneNumber)
                .WithMany()
                .HasForeignKey(x => x.WhatsAppPhoneNumberId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Contact)
                .WithMany(x => x.Conversations)
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedUser)
                .WithMany()
                .HasForeignKey(x => x.AssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── ConversationMessages ───────────────────────────────────
        modelBuilder.Entity<ConversationMessage>(entity =>
        {
            entity.ToTable("ConversationMessages");
            entity.HasKey(x => x.ConversationMessageId);
            entity.Property(x => x.ConversationMessageId).UseIdentityColumn();
            entity.Ignore(x => x.IsFromAutomation);
            entity.Property(x => x.MessageId);
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.MetaMessageId).HasMaxLength(120);
            entity.Property(x => x.MessageType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.MediaUrl).HasMaxLength(2000);
            entity.Property(x => x.MediaMimeType).HasMaxLength(100);
            entity.Property(x => x.FileName).HasMaxLength(255);
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("sent");
            entity.Property(x => x.FailureReason).HasColumnType("nvarchar(max)");
            entity.Property(x => x.TimestampUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(x => x.MessageId)
                .HasFilter("[MessageId] IS NOT NULL")
                .IsUnique();
            entity.HasIndex(x => x.MetaMessageId)
                .HasFilter("[MetaMessageId] IS NOT NULL")
                .IsUnique();
            entity.HasIndex(x => new { x.ConversationId, x.TimestampUtc });

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Message)
                .WithMany(x => x.ConversationMessages)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ConversationAssignmentHistory>(entity =>
        {
            entity.ToTable("ConversationAssignmentHistory");
            entity.HasKey(x => x.ConversationAssignmentHistoryId);
            entity.Property(x => x.ConversationAssignmentHistoryId).UseIdentityColumn();
            entity.Property(x => x.AssignmentMode).HasMaxLength(20).HasDefaultValue("MANUAL");
            entity.Property(x => x.Reason).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Notes).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ChangedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(x => new { x.CompanyId, x.ConversationId, x.ChangedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ContactId, x.ChangedAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.ConversationAssignmentHistory)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.AssignmentHistory)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Contact)
                .WithMany(x => x.AssignmentHistory)
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.PreviousAssignedUser)
                .WithMany()
                .HasForeignKey(x => x.PreviousAssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.NewAssignedUser)
                .WithMany()
                .HasForeignKey(x => x.NewAssignedUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.PreviousOwnerUser)
                .WithMany()
                .HasForeignKey(x => x.PreviousOwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.NewOwnerUser)
                .WithMany()
                .HasForeignKey(x => x.NewOwnerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.ChangedByUser)
                .WithMany()
                .HasForeignKey(x => x.ChangedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Campaigns ──────────────────────────────────────────────
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.ToTable("Campaigns");
            entity.HasKey(x => x.CampaignId);
            entity.Property(x => x.CampaignId).UseIdentityColumn();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.TemplateName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LanguageCode).HasMaxLength(10).HasDefaultValue("ar");
            entity.Property(x => x.TemplateParametersJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("DRAFT");
            entity.Property(x => x.ScheduledAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.StartedAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Campaigns)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.WhatsAppPhoneNumber)
                .WithMany()
                .HasForeignKey(x => x.WhatsAppPhoneNumberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── CampaignContacts ───────────────────────────────────────
        modelBuilder.Entity<CampaignContact>(entity =>
        {
            entity.ToTable("CampaignContacts");
            entity.HasKey(x => x.CampaignContactId);
            entity.Property(x => x.CampaignContactId).UseIdentityColumn();
            entity.Property(x => x.PhoneNumber).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("PENDING");
            entity.Property(x => x.ExternalMessageId).HasMaxLength(120);
            entity.Property(x => x.FailureReason).HasColumnType("nvarchar(max)");
            entity.Property(x => x.SentAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.DeliveredAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.ReadAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(x => x.Campaign)
                .WithMany(x => x.CampaignContacts)
                .HasForeignKey(x => x.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Contact)
                .WithMany(x => x.CampaignContacts)
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ── AutomationRules ───────────────────────────────────────
        modelBuilder.Entity<AutomationRule>(entity =>
        {
            entity.ToTable("AutomationRules");
            entity.HasKey(x => x.AutomationRuleId);
            entity.Property(x => x.AutomationRuleId).UseIdentityColumn();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.TriggerType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.TriggerValue).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ResponseType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ResponseValue).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.TemplateName).HasMaxLength(200);
            entity.Property(x => x.LanguageCode).HasMaxLength(10);
            entity.Property(x => x.Priority).HasDefaultValue(100);
            entity.Property(x => x.TriggerCount).HasColumnName("HitCount").HasDefaultValue(0L);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.AutomationRules)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Notifications ──────────────────────────────────────────
        modelBuilder.Entity<ConversationFlow>(entity =>
        {
            entity.ToTable("ConversationFlows");
            entity.HasKey(x => x.ConversationFlowId);
            entity.Property(x => x.ConversationFlowId).UseIdentityColumn();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.EntryTriggerType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EntryTriggerValue).HasMaxLength(500);
            entity.Property(x => x.DraftDefinitionJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.PublishedDefinitionJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.DraftVersion).HasDefaultValue(1);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.IsPublished).HasDefaultValue(false);
            entity.Property(x => x.TriggerCount).HasDefaultValue(0L);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.PublishedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.IsActive, x.IsPublished, x.EntryTriggerType });
            entity.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();

            entity.HasOne(x => x.Company)
                .WithMany(x => x.ConversationFlows)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ConversationFlowSession>(entity =>
        {
            entity.ToTable("ConversationFlowSessions");
            entity.HasKey(x => x.ConversationFlowSessionId);
            entity.Property(x => x.ConversationFlowSessionId).UseIdentityColumn();
            entity.Property(x => x.Status).HasMaxLength(30).HasDefaultValue("ACTIVE");
            entity.Property(x => x.CurrentNodeId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.VariablesJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.StartedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.LastInteractionAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.CompletedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.ConversationId, x.Status, x.StartedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ConversationFlowId, x.Status, x.StartedAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.ConversationFlowSessions)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.ConversationFlow)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.ConversationFlowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Conversation)
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Contact)
                .WithMany()
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConversationFlowExecutionLog>(entity =>
        {
            entity.ToTable("ConversationFlowExecutionLogs");
            entity.HasKey(x => x.ConversationFlowExecutionLogId);
            entity.Property(x => x.ConversationFlowExecutionLogId).UseIdentityColumn();
            entity.Property(x => x.NodeId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EventType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Direction).HasMaxLength(20);
            entity.Property(x => x.Message).HasColumnType("nvarchar(max)");
            entity.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(x => new { x.CompanyId, x.ConversationFlowId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ConversationId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ContactId, x.CreatedAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.ConversationFlowExecutionLogs)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.ConversationFlow)
                .WithMany(x => x.ExecutionLogs)
                .HasForeignKey(x => x.ConversationFlowId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.ConversationFlowSession)
                .WithMany(x => x.ExecutionLogs)
                .HasForeignKey(x => x.ConversationFlowSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Conversation)
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Contact)
                .WithMany()
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ConversationFlowFormSubmission>(entity =>
        {
            entity.ToTable("ConversationFlowFormSubmissions");
            entity.HasKey(x => x.ConversationFlowFormSubmissionId);
            entity.Property(x => x.ConversationFlowFormSubmissionId).UseIdentityColumn();
            entity.Property(x => x.NodeId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(30).IsRequired();
            entity.Property(x => x.InboundMessageType).HasMaxLength(50);
            entity.Property(x => x.MetaMessageId).HasMaxLength(120);
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ExtractedValuesJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(x => new { x.CompanyId, x.ConversationFlowId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ConversationId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ContactId, x.CreatedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.Source, x.CreatedAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.ConversationFlowFormSubmissions)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.ConversationFlow)
                .WithMany(x => x.FormSubmissions)
                .HasForeignKey(x => x.ConversationFlowId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ConversationFlowSession)
                .WithMany(x => x.FormSubmissions)
                .HasForeignKey(x => x.ConversationFlowSessionId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Conversation)
                .WithMany()
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Contact)
                .WithMany()
                .HasForeignKey(x => x.ContactId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(x => x.NotificationId);
            entity.Property(x => x.NotificationId).UseIdentityColumn();
            entity.Property(x => x.Type).HasMaxLength(50).HasDefaultValue("info");
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Body).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100);
            entity.Property(x => x.MetadataJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.IsRead).HasDefaultValue(false);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.ReadAtUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Notifications)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.CompanyUser)
                .WithMany()
                .HasForeignKey(x => x.CompanyUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // WebhookLogs
        modelBuilder.Entity<WebhookLog>(entity =>
        {
            entity.ToTable("WebhookLogs");
            entity.HasKey(x => x.WebhookLogId);
            entity.Property(x => x.WebhookLogId).UseIdentityColumn();
            entity.Property(x => x.PhoneNumberId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(200);
            entity.Property(x => x.CorrelationId).HasMaxLength(100);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(x => new { x.CompanyId, x.CreatedAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.WebhookLogs)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WebhookInbox
        modelBuilder.Entity<WebhookInboxItem>(entity =>
        {
            entity.ToTable("WebhookInbox");
            entity.HasKey(x => x.WebhookInboxId);
            entity.Property(x => x.WebhookInboxId).UseIdentityColumn();
            entity.Property(x => x.PhoneNumberId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).IsRequired().HasDefaultValue("PENDING");
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.LastError).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ReceivedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.LastAttemptAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.ProcessedAtUtc).HasColumnType("datetime2");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.Status, x.RetryCount, x.ReceivedAtUtc });
            entity.HasIndex(x => new { x.CompanyId, x.ReceivedAtUtc });

            entity.HasOne(x => x.Company)
                .WithMany(x => x.WebhookInboxItems)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        ApplyUtcDateTimeConverters(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private static void ApplyUtcDateTimeConverters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(UtcDateTimeConverter);
                    continue;
                }

                if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(NullableUtcDateTimeConverter);
                }
            }
        }
    }
}
