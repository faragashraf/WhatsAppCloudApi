using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Domain.Entities;

namespace WhatsAppCloudApi.Infrastructure.Data;

public sealed class ApplicationDbContext : DbContext
{
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
    public DbSet<ApiLog> ApiLogs => Set<ApiLog>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<CampaignContact> CampaignContacts => Set<CampaignContact>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<Notification> Notifications => Set<Notification>();

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
            entity.Property(x => x.ExternalMessageId).HasMaxLength(120);
            entity.Property(x => x.FailureReason).HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.WhatsAppPhoneNumber)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.WhatsAppPhoneNumberId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

            entity.HasIndex(x => new { x.CompanyId, x.PhoneNumber }).IsUnique();

            entity.HasOne(x => x.Company)
                .WithMany(x => x.Contacts)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
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

            entity.HasIndex(x => new { x.CompanyId, x.ContactNumber, x.WhatsAppPhoneNumberId }).IsUnique();

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
                .OnDelete(DeleteBehavior.SetNull);

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
            entity.Property(x => x.Direction).HasMaxLength(20).IsRequired();
            entity.Property(x => x.MetaMessageId).HasMaxLength(120);
            entity.Property(x => x.MessageType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.MediaUrl).HasMaxLength(2000);
            entity.Property(x => x.MediaMimeType).HasMaxLength(100);
            entity.Property(x => x.Status).HasMaxLength(50).HasDefaultValue("sent");
            entity.Property(x => x.FailureReason).HasColumnType("nvarchar(max)");
            entity.Property(x => x.TimestampUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.NoAction);
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

        base.OnModelCreating(modelBuilder);
    }
}
