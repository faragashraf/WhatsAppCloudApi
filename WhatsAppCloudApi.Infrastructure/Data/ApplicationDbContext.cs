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
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Companies");
            entity.HasKey(x => x.CompanyId);
            entity.Property(x => x.CompanyName).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(50);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.CreatedAt).HasColumnType("datetime");
            entity.Property(x => x.TrialStartDate).HasColumnType("datetime");
            entity.Property(x => x.TrialEndDate).HasColumnType("datetime");
            entity.Property(x => x.SubscriptionEndDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<CompanyUser>(entity =>
        {
            entity.ToTable("CompanyUsers");
            entity.HasKey(x => x.CompanyUserId);
            entity.Property(x => x.FullName).HasMaxLength(200);
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.Role).HasMaxLength(50);
            entity.Property(x => x.RefreshToken).HasMaxLength(500);
            entity.HasIndex(x => x.Email).IsUnique(false);
            entity.HasOne(x => x.Company)
                .WithMany(x => x.Users)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans");
            entity.HasKey(x => x.SubscriptionPlanId);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.Code).HasMaxLength(50);
            entity.Property(x => x.MonthlyPrice).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CompanySubscription>(entity =>
        {
            entity.ToTable("CompanySubscriptions");
            entity.HasKey(x => x.CompanySubscriptionId);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.HasOne(x => x.Company)
                .WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SubscriptionPlan)
                .WithMany(x => x.CompanySubscriptions)
                .HasForeignKey(x => x.SubscriptionPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MetaBusinessAccount>(entity =>
        {
            entity.ToTable("MetaBusinessAccounts");
            entity.HasKey(x => x.MetaBusinessAccountId);
            entity.Property(x => x.BusinessId).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.AccessToken).HasMaxLength(2000);
            entity.HasOne(x => x.Company)
                .WithMany(x => x.MetaBusinessAccounts)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WhatsAppAccount>(entity =>
        {
            entity.ToTable("WhatsAppAccounts");
            entity.HasKey(x => x.WhatsAppAccountId);
            entity.Property(x => x.BusinessAccountId).HasMaxLength(100);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.AccessToken).HasMaxLength(2000);
            entity.Property(x => x.VerifyToken).HasMaxLength(256);
            entity.Property(x => x.AppSecret).HasMaxLength(500);
            entity.HasOne(x => x.Company)
                .WithMany(x => x.WhatsAppAccounts)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MetaBusinessAccount)
                .WithMany(x => x.WhatsAppAccounts)
                .HasForeignKey(x => x.MetaBusinessAccountId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WhatsAppPhoneNumber>(entity =>
        {
            entity.ToTable("WhatsAppPhoneNumbers");
            entity.HasKey(x => x.WhatsAppPhoneNumberId);
            entity.Property(x => x.PhoneNumberId).HasMaxLength(100);
            entity.Property(x => x.DisplayPhoneNumber).HasMaxLength(30);
            entity.Property(x => x.VerifiedName).HasMaxLength(200);
            entity.HasOne(x => x.Company)
                .WithMany(x => x.WhatsAppPhoneNumbers)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WhatsAppAccount)
                .WithMany(x => x.PhoneNumbers)
                .HasForeignKey(x => x.WhatsAppAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(x => x.MessageId);
            entity.Property(x => x.ToNumber).HasMaxLength(30);
            entity.Property(x => x.MessageType).HasMaxLength(50);
            entity.Property(x => x.MessageBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.ExternalMessageId).HasMaxLength(120);
            entity.Property(x => x.FailureReason).HasColumnType("nvarchar(max)");
            entity.HasOne(x => x.Company)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.WhatsAppPhoneNumber)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.WhatsAppPhoneNumberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MessageQueueItem>(entity =>
        {
            entity.ToTable("MessageQueue");
            entity.HasKey(x => x.MessageQueueId);
            entity.Property(x => x.Status).HasMaxLength(50);
            entity.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.LastError).HasColumnType("nvarchar(max)");
            entity.HasOne(x => x.Company)
                .WithMany(x => x.MessageQueueItems)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Message)
                .WithMany(x => x.QueueItems)
                .HasForeignKey(x => x.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiLog>(entity =>
        {
            entity.ToTable("ApiLogs");
            entity.HasKey(x => x.ApiLogId);
            entity.Property(x => x.Endpoint).HasMaxLength(500);
            entity.Property(x => x.HttpMethod).HasMaxLength(10);
            entity.Property(x => x.RequestBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ResponseBody).HasColumnType("nvarchar(max)");
            entity.Property(x => x.IpAddress).HasMaxLength(100);
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
