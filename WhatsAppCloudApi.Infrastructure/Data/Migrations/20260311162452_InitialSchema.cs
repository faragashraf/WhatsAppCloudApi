using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    CompanyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true, defaultValue: "ACTIVE"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    SuspendedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrialStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrialEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubscriptionEndDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.CompanyId);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TrialDays = table.Column<int>(type: "int", nullable: false),
                    MaxMessagesPerMonth = table.Column<int>(type: "int", nullable: false),
                    MaxWhatsAppAccounts = table.Column<int>(type: "int", nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.SubscriptionPlanId);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    AutomationRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TriggerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TriggerValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResponseType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResponseValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 100),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    HitCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.AutomationRuleId);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyRoutingSettings",
                columns: table => new
                {
                    CompanyRoutingSettingsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    AssignmentMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "MANUAL"),
                    AutoAssignmentStrategy = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "ROUND_ROBIN"),
                    RespectExistingContactOwner = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ReassignWhenOwnerInactive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ManualReassignmentUpdatesContactOwner = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyRoutingSettings", x => x.CompanyRoutingSettingsId);
                    table.ForeignKey(
                        name: "FK_CompanyRoutingSettings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyUsers",
                columns: table => new
                {
                    CompanyUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Admin"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    PermissionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSuperAdmin = table.Column<bool>(type: "bit", nullable: false),
                    PasswordResetOtp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PasswordResetOtpExpiryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RefreshTokenExpiryUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyUsers", x => x.CompanyUserId);
                    table.ForeignKey(
                        name: "FK_CompanyUsers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetaBusinessAccounts",
                columns: table => new
                {
                    MetaBusinessAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    BusinessId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetaBusinessAccounts", x => x.MetaBusinessAccountId);
                    table.ForeignKey(
                        name: "FK_MetaBusinessAccounts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookInbox",
                columns: table => new
                {
                    WebhookInboxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    WhatsAppPhoneNumberId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceivedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookInbox", x => x.WebhookInboxId);
                    table.ForeignKey(
                        name: "FK_WebhookInbox_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookLogs",
                columns: table => new
                {
                    WebhookLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SignatureValid = table.Column<bool>(type: "bit", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookLogs", x => x.WebhookLogId);
                    table.ForeignKey(
                        name: "FK_WebhookLogs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanySubscriptions",
                columns: table => new
                {
                    CompanySubscriptionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    SubscriptionPlanId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "TRIAL"),
                    TrialStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrialEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanySubscriptions", x => x.CompanySubscriptionId);
                    table.ForeignKey(
                        name: "FK_CompanySubscriptions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanySubscriptions_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "SubscriptionPlanId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ApiLogs",
                columns: table => new
                {
                    ApiLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CompanyUserId = table.Column<int>(type: "int", nullable: true),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiLogs", x => x.ApiLogId);
                    table.ForeignKey(
                        name: "FK_ApiLogs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApiLogs_CompanyUsers_CompanyUserId",
                        column: x => x.CompanyUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CompanyUserRoutingSettings",
                columns: table => new
                {
                    CompanyUserRoutingSettingsId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CompanyUserId = table.Column<int>(type: "int", nullable: false),
                    CanReceiveManualAssignments = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CanReceiveAutoAssignments = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastAutoAssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyUserRoutingSettings", x => x.CompanyUserRoutingSettingsId);
                    table.ForeignKey(
                        name: "FK_CompanyUserRoutingSettings_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CompanyUserRoutingSettings_CompanyUsers_CompanyUserId",
                        column: x => x.CompanyUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Contacts",
                columns: table => new
                {
                    ContactId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastInboundMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOutboundMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OwnerAssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contacts", x => x.ContactId);
                    table.ForeignKey(
                        name: "FK_Contacts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Contacts_CompanyUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    CompanyUserId = table.Column<int>(type: "int", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "info"),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_CompanyUsers_CompanyUserId",
                        column: x => x.CompanyUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppAccounts",
                columns: table => new
                {
                    WhatsAppAccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    MetaBusinessAccountId = table.Column<int>(type: "int", nullable: true),
                    BusinessAccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AccessToken = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    VerifyToken = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AppSecret = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppAccounts", x => x.WhatsAppAccountId);
                    table.ForeignKey(
                        name: "FK_WhatsAppAccounts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WhatsAppAccounts_MetaBusinessAccounts_MetaBusinessAccountId",
                        column: x => x.MetaBusinessAccountId,
                        principalTable: "MetaBusinessAccounts",
                        principalColumn: "MetaBusinessAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppPhoneNumbers",
                columns: table => new
                {
                    WhatsAppPhoneNumberId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    WhatsAppAccountId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumberId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayPhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    VerifiedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodeVerificationStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    QualityRating = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PlatformType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ThroughputLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastOnboardedTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSyncUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppPhoneNumbers", x => x.WhatsAppPhoneNumberId);
                    table.ForeignKey(
                        name: "FK_WhatsAppPhoneNumbers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WhatsAppPhoneNumbers_WhatsAppAccounts_WhatsAppAccountId",
                        column: x => x.WhatsAppAccountId,
                        principalTable: "WhatsAppAccounts",
                        principalColumn: "WhatsAppAccountId");
                });

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    CampaignId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TemplateName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false, defaultValue: "ar"),
                    TemplateParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhatsAppPhoneNumberId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "DRAFT"),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalContacts = table.Column<int>(type: "int", nullable: false),
                    SentCount = table.Column<int>(type: "int", nullable: false),
                    DeliveredCount = table.Column<int>(type: "int", nullable: false),
                    ReadCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.CampaignId);
                    table.ForeignKey(
                        name: "FK_Campaigns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Campaigns_WhatsAppPhoneNumbers_WhatsAppPhoneNumberId",
                        column: x => x.WhatsAppPhoneNumberId,
                        principalTable: "WhatsAppPhoneNumbers",
                        principalColumn: "WhatsAppPhoneNumberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    ConversationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    WhatsAppPhoneNumberId = table.Column<int>(type: "int", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ContactNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastMessageContent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastMessageType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastInboundMessageAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "OPEN"),
                    UnreadCount = table.Column<int>(type: "int", nullable: false),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.ConversationId);
                    table.ForeignKey(
                        name: "FK_Conversations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Conversations_CompanyUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Conversations_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversations_WhatsAppPhoneNumbers_WhatsAppPhoneNumberId",
                        column: x => x.WhatsAppPhoneNumberId,
                        principalTable: "WhatsAppPhoneNumbers",
                        principalColumn: "WhatsAppPhoneNumberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CampaignContacts",
                columns: table => new
                {
                    CampaignContactId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<long>(type: "bigint", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignContacts", x => x.CampaignContactId);
                    table.ForeignKey(
                        name: "FK_CampaignContacts_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "CampaignId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignContacts_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ConversationAssignmentHistory",
                columns: table => new
                {
                    ConversationAssignmentHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    PreviousAssignedUserId = table.Column<int>(type: "int", nullable: true),
                    NewAssignedUserId = table.Column<int>(type: "int", nullable: true),
                    PreviousOwnerUserId = table.Column<int>(type: "int", nullable: true),
                    NewOwnerUserId = table.Column<int>(type: "int", nullable: true),
                    AssignmentMode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "MANUAL"),
                    Reason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationAssignmentHistory", x => x.ConversationAssignmentHistoryId);
                    table.ForeignKey(
                        name: "FK_ConversationAssignmentHistory_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationAssignmentHistory_CompanyUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConversationAssignmentHistory_CompanyUsers_NewAssignedUserId",
                        column: x => x.NewAssignedUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConversationAssignmentHistory_CompanyUsers_NewOwnerUserId",
                        column: x => x.NewOwnerUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConversationAssignmentHistory_CompanyUsers_PreviousAssignedUserId",
                        column: x => x.PreviousAssignedUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConversationAssignmentHistory_CompanyUsers_PreviousOwnerUserId",
                        column: x => x.PreviousOwnerUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConversationAssignmentHistory_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ConversationAssignmentHistory_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "Messages",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    WhatsAppPhoneNumberId = table.Column<int>(type: "int", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    ConversationId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    ToNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MessageType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MessageBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "DIRECT"),
                    ExternalMessageId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Messages", x => x.MessageId);
                    table.ForeignKey(
                        name: "FK_Messages_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Messages_CompanyUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Messages_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Messages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Messages_WhatsAppPhoneNumbers_WhatsAppPhoneNumberId",
                        column: x => x.WhatsAppPhoneNumberId,
                        principalTable: "WhatsAppPhoneNumbers",
                        principalColumn: "WhatsAppPhoneNumberId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMessages",
                columns: table => new
                {
                    ConversationMessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    MessageId = table.Column<long>(type: "bigint", nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MetaMessageId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MediaUrl = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MediaMimeType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "sent"),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMessages", x => x.ConversationMessageId);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId");
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMessages_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MessageQueue",
                columns: table => new
                {
                    MessageQueueId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageQueue", x => x.MessageQueueId);
                    table.ForeignKey(
                        name: "FK_MessageQueue_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MessageQueue_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "MessageId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_CompanyId",
                table: "ApiLogs",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_CompanyUserId",
                table: "ApiLogs",
                column: "CompanyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_CompanyId",
                table: "AutomationRules",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignContacts_CampaignId",
                table: "CampaignContacts",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignContacts_ContactId",
                table: "CampaignContacts",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_CompanyId",
                table: "Campaigns",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_WhatsAppPhoneNumberId",
                table: "Campaigns",
                column: "WhatsAppPhoneNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyRoutingSettings_CompanyId",
                table: "CompanyRoutingSettings",
                column: "CompanyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_CompanyId",
                table: "CompanySubscriptions",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanySubscriptions_SubscriptionPlanId",
                table: "CompanySubscriptions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUserRoutingSettings_CompanyId_CanReceiveAutoAssignments_LastAutoAssignedAtUtc",
                table: "CompanyUserRoutingSettings",
                columns: new[] { "CompanyId", "CanReceiveAutoAssignments", "LastAutoAssignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUserRoutingSettings_CompanyId_CanReceiveManualAssignments_CompanyUserId",
                table: "CompanyUserRoutingSettings",
                columns: new[] { "CompanyId", "CanReceiveManualAssignments", "CompanyUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUserRoutingSettings_CompanyId_CompanyUserId",
                table: "CompanyUserRoutingSettings",
                columns: new[] { "CompanyId", "CompanyUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUserRoutingSettings_CompanyUserId",
                table: "CompanyUserRoutingSettings",
                column: "CompanyUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyUsers_CompanyId_Email",
                table: "CompanyUsers",
                columns: new[] { "CompanyId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CompanyId_LastSeenAtUtc",
                table: "Contacts",
                columns: new[] { "CompanyId", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CompanyId_OwnerUserId_IsActive",
                table: "Contacts",
                columns: new[] { "CompanyId", "OwnerUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_CompanyId_PhoneNumber",
                table: "Contacts",
                columns: new[] { "CompanyId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_OwnerUserId",
                table: "Contacts",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_ChangedByUserId",
                table: "ConversationAssignmentHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_CompanyId_ContactId_ChangedAtUtc",
                table: "ConversationAssignmentHistory",
                columns: new[] { "CompanyId", "ContactId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_CompanyId_ConversationId_ChangedAtUtc",
                table: "ConversationAssignmentHistory",
                columns: new[] { "CompanyId", "ConversationId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_ContactId",
                table: "ConversationAssignmentHistory",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_ConversationId",
                table: "ConversationAssignmentHistory",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_NewAssignedUserId",
                table: "ConversationAssignmentHistory",
                column: "NewAssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_NewOwnerUserId",
                table: "ConversationAssignmentHistory",
                column: "NewOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_PreviousAssignedUserId",
                table: "ConversationAssignmentHistory",
                column: "PreviousAssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_PreviousOwnerUserId",
                table: "ConversationAssignmentHistory",
                column: "PreviousOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_CompanyId",
                table: "ConversationMessages",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_ConversationId_TimestampUtc",
                table: "ConversationMessages",
                columns: new[] { "ConversationId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_MessageId",
                table: "ConversationMessages",
                column: "MessageId",
                unique: true,
                filter: "[MessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMessages_MetaMessageId",
                table: "ConversationMessages",
                column: "MetaMessageId",
                unique: true,
                filter: "[MetaMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedUserId",
                table: "Conversations",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompanyId_AssignedUserId_Status_LastMessageAtUtc",
                table: "Conversations",
                columns: new[] { "CompanyId", "AssignedUserId", "Status", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompanyId_ContactId_LastMessageAtUtc",
                table: "Conversations",
                columns: new[] { "CompanyId", "ContactId", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompanyId_ContactId_WhatsAppPhoneNumberId",
                table: "Conversations",
                columns: new[] { "CompanyId", "ContactId", "WhatsAppPhoneNumberId" },
                unique: true,
                filter: "[WhatsAppPhoneNumberId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ContactId",
                table: "Conversations",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_WhatsAppPhoneNumberId",
                table: "Conversations",
                column: "WhatsAppPhoneNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageQueue_CompanyId",
                table: "MessageQueue",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageQueue_MessageId",
                table: "MessageQueue",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CompanyId_ContactId_CreatedAtUtc",
                table: "Messages",
                columns: new[] { "CompanyId", "ContactId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CompanyId_ConversationId_CreatedAtUtc",
                table: "Messages",
                columns: new[] { "CompanyId", "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CompanyId_ExternalMessageId",
                table: "Messages",
                columns: new[] { "CompanyId", "ExternalMessageId" },
                unique: true,
                filter: "[ExternalMessageId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ContactId",
                table: "Messages",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ConversationId",
                table: "Messages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CreatedByUserId",
                table: "Messages",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_WhatsAppPhoneNumberId",
                table: "Messages",
                column: "WhatsAppPhoneNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_MetaBusinessAccounts_CompanyId",
                table: "MetaBusinessAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CompanyId",
                table: "Notifications",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CompanyUserId",
                table: "Notifications",
                column: "CompanyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Code",
                table: "SubscriptionPlans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookInbox_CompanyId_ReceivedAtUtc",
                table: "WebhookInbox",
                columns: new[] { "CompanyId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookInbox_Status_RetryCount_ReceivedAtUtc",
                table: "WebhookInbox",
                columns: new[] { "Status", "RetryCount", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookLogs_CompanyId_CreatedAtUtc",
                table: "WebhookLogs",
                columns: new[] { "CompanyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppAccounts_CompanyId",
                table: "WhatsAppAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppAccounts_MetaBusinessAccountId",
                table: "WhatsAppAccounts",
                column: "MetaBusinessAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppPhoneNumbers_CompanyId",
                table: "WhatsAppPhoneNumbers",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppPhoneNumbers_WhatsAppAccountId",
                table: "WhatsAppPhoneNumbers",
                column: "WhatsAppAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiLogs");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropTable(
                name: "CampaignContacts");

            migrationBuilder.DropTable(
                name: "CompanyRoutingSettings");

            migrationBuilder.DropTable(
                name: "CompanySubscriptions");

            migrationBuilder.DropTable(
                name: "CompanyUserRoutingSettings");

            migrationBuilder.DropTable(
                name: "ConversationAssignmentHistory");

            migrationBuilder.DropTable(
                name: "ConversationMessages");

            migrationBuilder.DropTable(
                name: "MessageQueue");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "WebhookInbox");

            migrationBuilder.DropTable(
                name: "WebhookLogs");

            migrationBuilder.DropTable(
                name: "Campaigns");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropTable(
                name: "Messages");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "Contacts");

            migrationBuilder.DropTable(
                name: "WhatsAppPhoneNumbers");

            migrationBuilder.DropTable(
                name: "CompanyUsers");

            migrationBuilder.DropTable(
                name: "WhatsAppAccounts");

            migrationBuilder.DropTable(
                name: "MetaBusinessAccounts");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
