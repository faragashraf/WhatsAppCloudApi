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
            entity.Property(x => x.IsDefault).HasDefaultValue(true);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasColumnType("datetime2").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

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

        base.OnModelCreating(modelBuilder);
    }
}
