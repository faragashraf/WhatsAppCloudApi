using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationFlowBuilder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationFlows",
                columns: table => new
                {
                    ConversationFlowId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EntryTriggerType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntryTriggerValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DraftDefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedDefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DraftVersion = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    PublishedVersion = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    TriggerCount = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFlows", x => x.ConversationFlowId);
                    table.ForeignKey(
                        name: "FK_ConversationFlows_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationFlowSessions",
                columns: table => new
                {
                    ConversationFlowSessionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ConversationFlowId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationId = table.Column<long>(type: "bigint", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    FlowVersion = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "ACTIVE"),
                    CurrentNodeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VariablesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvalidReplyCount = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    LastInteractionAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFlowSessions", x => x.ConversationFlowSessionId);
                    table.ForeignKey(
                        name: "FK_ConversationFlowSessions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId");
                    table.ForeignKey(
                        name: "FK_ConversationFlowSessions_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConversationFlowSessions_ConversationFlows_ConversationFlowId",
                        column: x => x.ConversationFlowId,
                        principalTable: "ConversationFlows",
                        principalColumn: "ConversationFlowId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationFlowSessions_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationId");
                });

            migrationBuilder.CreateTable(
                name: "ConversationFlowExecutionLogs",
                columns: table => new
                {
                    ConversationFlowExecutionLogId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ConversationFlowId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationFlowSessionId = table.Column<long>(type: "bigint", nullable: true),
                    ConversationId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    NodeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFlowExecutionLogs", x => x.ConversationFlowExecutionLogId);
                    table.ForeignKey(
                        name: "FK_ConversationFlowExecutionLogs_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId");
                    table.ForeignKey(
                        name: "FK_ConversationFlowExecutionLogs_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId");
                    table.ForeignKey(
                        name: "FK_ConversationFlowExecutionLogs_ConversationFlowSessions_ConversationFlowSessionId",
                        column: x => x.ConversationFlowSessionId,
                        principalTable: "ConversationFlowSessions",
                        principalColumn: "ConversationFlowSessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationFlowExecutionLogs_ConversationFlows_ConversationFlowId",
                        column: x => x.ConversationFlowId,
                        principalTable: "ConversationFlows",
                        principalColumn: "ConversationFlowId");
                    table.ForeignKey(
                        name: "FK_ConversationFlowExecutionLogs_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowExecutionLogs_CompanyId_ContactId_CreatedAtUtc",
                table: "ConversationFlowExecutionLogs",
                columns: new[] { "CompanyId", "ContactId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowExecutionLogs_CompanyId_ConversationFlowId_CreatedAtUtc",
                table: "ConversationFlowExecutionLogs",
                columns: new[] { "CompanyId", "ConversationFlowId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowExecutionLogs_CompanyId_ConversationId_CreatedAtUtc",
                table: "ConversationFlowExecutionLogs",
                columns: new[] { "CompanyId", "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowExecutionLogs_ContactId",
                table: "ConversationFlowExecutionLogs",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowExecutionLogs_ConversationFlowId",
                table: "ConversationFlowExecutionLogs",
                column: "ConversationFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowExecutionLogs_ConversationFlowSessionId",
                table: "ConversationFlowExecutionLogs",
                column: "ConversationFlowSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowExecutionLogs_ConversationId",
                table: "ConversationFlowExecutionLogs",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlows_CompanyId_IsActive_IsPublished_EntryTriggerType",
                table: "ConversationFlows",
                columns: new[] { "CompanyId", "IsActive", "IsPublished", "EntryTriggerType" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlows_CompanyId_Name",
                table: "ConversationFlows",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowSessions_CompanyId_ConversationFlowId_Status_StartedAtUtc",
                table: "ConversationFlowSessions",
                columns: new[] { "CompanyId", "ConversationFlowId", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowSessions_CompanyId_ConversationId_Status_StartedAtUtc",
                table: "ConversationFlowSessions",
                columns: new[] { "CompanyId", "ConversationId", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowSessions_ContactId",
                table: "ConversationFlowSessions",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowSessions_ConversationFlowId",
                table: "ConversationFlowSessions",
                column: "ConversationFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowSessions_ConversationId",
                table: "ConversationFlowSessions",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationFlowExecutionLogs");

            migrationBuilder.DropTable(
                name: "ConversationFlowSessions");

            migrationBuilder.DropTable(
                name: "ConversationFlows");
        }
    }
}
