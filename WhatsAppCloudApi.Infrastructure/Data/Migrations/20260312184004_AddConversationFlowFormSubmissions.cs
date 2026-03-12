using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationFlowFormSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConversationFlowFormSubmissions",
                columns: table => new
                {
                    ConversationFlowFormSubmissionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ConversationFlowId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationFlowSessionId = table.Column<long>(type: "bigint", nullable: true),
                    ConversationId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    NodeId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InboundMessageType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MetaMessageId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtractedValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFlowFormSubmissions", x => x.ConversationFlowFormSubmissionId);
                    table.ForeignKey(
                        name: "FK_ConversationFlowFormSubmissions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId");
                    table.ForeignKey(
                        name: "FK_ConversationFlowFormSubmissions_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId");
                    table.ForeignKey(
                        name: "FK_ConversationFlowFormSubmissions_ConversationFlowSessions_ConversationFlowSessionId",
                        column: x => x.ConversationFlowSessionId,
                        principalTable: "ConversationFlowSessions",
                        principalColumn: "ConversationFlowSessionId");
                    table.ForeignKey(
                        name: "FK_ConversationFlowFormSubmissions_ConversationFlows_ConversationFlowId",
                        column: x => x.ConversationFlowId,
                        principalTable: "ConversationFlows",
                        principalColumn: "ConversationFlowId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationFlowFormSubmissions_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowFormSubmissions_CompanyId_ContactId_CreatedAtUtc",
                table: "ConversationFlowFormSubmissions",
                columns: new[] { "CompanyId", "ContactId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowFormSubmissions_CompanyId_ConversationFlowId_CreatedAtUtc",
                table: "ConversationFlowFormSubmissions",
                columns: new[] { "CompanyId", "ConversationFlowId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowFormSubmissions_CompanyId_ConversationId_CreatedAtUtc",
                table: "ConversationFlowFormSubmissions",
                columns: new[] { "CompanyId", "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowFormSubmissions_CompanyId_Source_CreatedAtUtc",
                table: "ConversationFlowFormSubmissions",
                columns: new[] { "CompanyId", "Source", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowFormSubmissions_ContactId",
                table: "ConversationFlowFormSubmissions",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowFormSubmissions_ConversationFlowId",
                table: "ConversationFlowFormSubmissions",
                column: "ConversationFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowFormSubmissions_ConversationFlowSessionId",
                table: "ConversationFlowFormSubmissions",
                column: "ConversationFlowSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlowFormSubmissions_ConversationId",
                table: "ConversationFlowFormSubmissions",
                column: "ConversationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationFlowFormSubmissions");
        }
    }
}
