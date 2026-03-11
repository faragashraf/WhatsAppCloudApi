using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailQueueInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailAccounts",
                columns: table => new
                {
                    EmailAccountId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReplyToAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FromName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    SmtpHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SmtpPort = table.Column<int>(type: "int", nullable: false, defaultValue: 587),
                    EnableSsl = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Username = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordProtected = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailAccounts", x => x.EmailAccountId);
                    table.ForeignKey(
                        name: "FK_EmailAccounts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailAccounts_CompanyUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "EmailNotificationRules",
                columns: table => new
                {
                    EmailNotificationRuleId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    EmailAccountId = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    RecipientMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecipientsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubjectTemplate = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBodyHtml = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastTriggeredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailNotificationRules", x => x.EmailNotificationRuleId);
                    table.ForeignKey(
                        name: "FK_EmailNotificationRules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailNotificationRules_CompanyUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_EmailNotificationRules_EmailAccounts_EmailAccountId",
                        column: x => x.EmailAccountId,
                        principalTable: "EmailAccounts",
                        principalColumn: "EmailAccountId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmailQueue",
                columns: table => new
                {
                    EmailQueueItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: true),
                    EmailAccountId = table.Column<int>(type: "int", nullable: false),
                    EmailNotificationRuleId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    Scope = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TriggerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ToJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BccJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBodyHtml = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PENDING"),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeduplicationKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailQueue", x => x.EmailQueueItemId);
                    table.ForeignKey(
                        name: "FK_EmailQueue_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmailQueue_CompanyUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_EmailQueue_EmailAccounts_EmailAccountId",
                        column: x => x.EmailAccountId,
                        principalTable: "EmailAccounts",
                        principalColumn: "EmailAccountId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmailQueue_EmailNotificationRules_EmailNotificationRuleId",
                        column: x => x.EmailNotificationRuleId,
                        principalTable: "EmailNotificationRules",
                        principalColumn: "EmailNotificationRuleId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateTable(
                name: "EmailQueueAttachments",
                columns: table => new
                {
                    EmailQueueAttachmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmailQueueItemId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ContentBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailQueueAttachments", x => x.EmailQueueAttachmentId);
                    table.ForeignKey(
                        name: "FK_EmailQueueAttachments_EmailQueue_EmailQueueItemId",
                        column: x => x.EmailQueueItemId,
                        principalTable: "EmailQueue",
                        principalColumn: "EmailQueueItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailAccounts_CompanyId_Name",
                table: "EmailAccounts",
                columns: new[] { "CompanyId", "Name" },
                unique: true,
                filter: "[CompanyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailAccounts_CreatedByUserId",
                table: "EmailAccounts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationRules_CompanyId_Scope_IsActive",
                table: "EmailNotificationRules",
                columns: new[] { "CompanyId", "Scope", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationRules_CreatedByUserId",
                table: "EmailNotificationRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailNotificationRules_EmailAccountId",
                table: "EmailNotificationRules",
                column: "EmailAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_CompanyId_Scope_CreatedAtUtc",
                table: "EmailQueue",
                columns: new[] { "CompanyId", "Scope", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_CreatedByUserId",
                table: "EmailQueue",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_DeduplicationKey",
                table: "EmailQueue",
                column: "DeduplicationKey",
                unique: true,
                filter: "[DeduplicationKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_EmailAccountId",
                table: "EmailQueue",
                column: "EmailAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_EmailNotificationRuleId",
                table: "EmailQueue",
                column: "EmailNotificationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueue_Scope_Status_ScheduledAtUtc",
                table: "EmailQueue",
                columns: new[] { "Scope", "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailQueueAttachments_EmailQueueItemId",
                table: "EmailQueueAttachments",
                column: "EmailQueueItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailQueueAttachments");

            migrationBuilder.DropTable(
                name: "EmailQueue");

            migrationBuilder.DropTable(
                name: "EmailNotificationRules");

            migrationBuilder.DropTable(
                name: "EmailAccounts");
        }
    }
}
