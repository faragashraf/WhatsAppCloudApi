using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamRoutingAndLeadPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedTeamId",
                table: "Conversations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewAssignedTeamId",
                table: "ConversationAssignmentHistory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousAssignedTeamId",
                table: "ConversationAssignmentHistory",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoutingTeams",
                columns: table => new
                {
                    RoutingTeamId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    AutoAssignmentEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    ManualAssignmentEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastAutoAssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingTeams", x => x.RoutingTeamId);
                    table.ForeignKey(
                        name: "FK_RoutingTeams_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeadDepartments",
                columns: table => new
                {
                    LeadDepartmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    RoutingTeamId = table.Column<int>(type: "int", nullable: true),
                    DepartmentKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadDepartments", x => x.LeadDepartmentId);
                    table.ForeignKey(
                        name: "FK_LeadDepartments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadDepartments_RoutingTeams_RoutingTeamId",
                        column: x => x.RoutingTeamId,
                        principalTable: "RoutingTeams",
                        principalColumn: "RoutingTeamId");
                });

            migrationBuilder.CreateTable(
                name: "RoutingTeamMembers",
                columns: table => new
                {
                    RoutingTeamMemberId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    RoutingTeamId = table.Column<int>(type: "int", nullable: false),
                    CompanyUserId = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastAutoAssignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoutingTeamMembers", x => x.RoutingTeamMemberId);
                    table.ForeignKey(
                        name: "FK_RoutingTeamMembers_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoutingTeamMembers_CompanyUsers_CompanyUserId",
                        column: x => x.CompanyUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId");
                    table.ForeignKey(
                        name: "FK_RoutingTeamMembers_RoutingTeams_RoutingTeamId",
                        column: x => x.RoutingTeamId,
                        principalTable: "RoutingTeams",
                        principalColumn: "RoutingTeamId");
                });

            migrationBuilder.CreateTable(
                name: "LeadRecords",
                columns: table => new
                {
                    LeadRecordId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ConversationFlowFormSubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationFlowId = table.Column<long>(type: "bigint", nullable: false),
                    ConversationFlowSessionId = table.Column<long>(type: "bigint", nullable: true),
                    ConversationId = table.Column<long>(type: "bigint", nullable: true),
                    ContactId = table.Column<long>(type: "bigint", nullable: true),
                    LeadDepartmentId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "NEW"),
                    ExtractedValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadRecords", x => x.LeadRecordId);
                    table.ForeignKey(
                        name: "FK_LeadRecords_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeadRecords_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId");
                    table.ForeignKey(
                        name: "FK_LeadRecords_ConversationFlowFormSubmissions_ConversationFlowFormSubmissionId",
                        column: x => x.ConversationFlowFormSubmissionId,
                        principalTable: "ConversationFlowFormSubmissions",
                        principalColumn: "ConversationFlowFormSubmissionId");
                    table.ForeignKey(
                        name: "FK_LeadRecords_ConversationFlowSessions_ConversationFlowSessionId",
                        column: x => x.ConversationFlowSessionId,
                        principalTable: "ConversationFlowSessions",
                        principalColumn: "ConversationFlowSessionId");
                    table.ForeignKey(
                        name: "FK_LeadRecords_ConversationFlows_ConversationFlowId",
                        column: x => x.ConversationFlowId,
                        principalTable: "ConversationFlows",
                        principalColumn: "ConversationFlowId");
                    table.ForeignKey(
                        name: "FK_LeadRecords_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "ConversationId");
                    table.ForeignKey(
                        name: "FK_LeadRecords_LeadDepartments_LeadDepartmentId",
                        column: x => x.LeadDepartmentId,
                        principalTable: "LeadDepartments",
                        principalColumn: "LeadDepartmentId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedTeamId",
                table: "Conversations",
                column: "AssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CompanyId_AssignedTeamId_Status_LastMessageAtUtc",
                table: "Conversations",
                columns: new[] { "CompanyId", "AssignedTeamId", "Status", "LastMessageAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_NewAssignedTeamId",
                table: "ConversationAssignmentHistory",
                column: "NewAssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationAssignmentHistory_PreviousAssignedTeamId",
                table: "ConversationAssignmentHistory",
                column: "PreviousAssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadDepartments_CompanyId_DepartmentKey",
                table: "LeadDepartments",
                columns: new[] { "CompanyId", "DepartmentKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadDepartments_CompanyId_IsActive_SortOrder_NameEn",
                table: "LeadDepartments",
                columns: new[] { "CompanyId", "IsActive", "SortOrder", "NameEn" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadDepartments_CompanyId_RoutingTeamId",
                table: "LeadDepartments",
                columns: new[] { "CompanyId", "RoutingTeamId" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadDepartments_RoutingTeamId",
                table: "LeadDepartments",
                column: "RoutingTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_CompanyId_ContactId_CreatedAtUtc",
                table: "LeadRecords",
                columns: new[] { "CompanyId", "ContactId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_CompanyId_ConversationId_CreatedAtUtc",
                table: "LeadRecords",
                columns: new[] { "CompanyId", "ConversationId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_CompanyId_CreatedAtUtc",
                table: "LeadRecords",
                columns: new[] { "CompanyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_CompanyId_LeadDepartmentId_CreatedAtUtc",
                table: "LeadRecords",
                columns: new[] { "CompanyId", "LeadDepartmentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_CompanyId_Source_CreatedAtUtc",
                table: "LeadRecords",
                columns: new[] { "CompanyId", "Source", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_ContactId",
                table: "LeadRecords",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_ConversationFlowFormSubmissionId",
                table: "LeadRecords",
                column: "ConversationFlowFormSubmissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_ConversationFlowId",
                table: "LeadRecords",
                column: "ConversationFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_ConversationFlowSessionId",
                table: "LeadRecords",
                column: "ConversationFlowSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_ConversationId",
                table: "LeadRecords",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_LeadRecords_LeadDepartmentId",
                table: "LeadRecords",
                column: "LeadDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingTeamMembers_CompanyId_CompanyUserId_IsActive",
                table: "RoutingTeamMembers",
                columns: new[] { "CompanyId", "CompanyUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingTeamMembers_CompanyId_RoutingTeamId_CompanyUserId",
                table: "RoutingTeamMembers",
                columns: new[] { "CompanyId", "RoutingTeamId", "CompanyUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoutingTeamMembers_CompanyId_RoutingTeamId_IsActive_LastAutoAssignedAtUtc",
                table: "RoutingTeamMembers",
                columns: new[] { "CompanyId", "RoutingTeamId", "IsActive", "LastAutoAssignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingTeamMembers_CompanyUserId",
                table: "RoutingTeamMembers",
                column: "CompanyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingTeamMembers_RoutingTeamId",
                table: "RoutingTeamMembers",
                column: "RoutingTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_RoutingTeams_CompanyId_IsActive_AutoAssignmentEnabled_LastAutoAssignedAtUtc",
                table: "RoutingTeams",
                columns: new[] { "CompanyId", "IsActive", "AutoAssignmentEnabled", "LastAutoAssignedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RoutingTeams_CompanyId_Name",
                table: "RoutingTeams",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationAssignmentHistory_RoutingTeams_NewAssignedTeamId",
                table: "ConversationAssignmentHistory",
                column: "NewAssignedTeamId",
                principalTable: "RoutingTeams",
                principalColumn: "RoutingTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConversationAssignmentHistory_RoutingTeams_PreviousAssignedTeamId",
                table: "ConversationAssignmentHistory",
                column: "PreviousAssignedTeamId",
                principalTable: "RoutingTeams",
                principalColumn: "RoutingTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_RoutingTeams_AssignedTeamId",
                table: "Conversations",
                column: "AssignedTeamId",
                principalTable: "RoutingTeams",
                principalColumn: "RoutingTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConversationAssignmentHistory_RoutingTeams_NewAssignedTeamId",
                table: "ConversationAssignmentHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_ConversationAssignmentHistory_RoutingTeams_PreviousAssignedTeamId",
                table: "ConversationAssignmentHistory");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_RoutingTeams_AssignedTeamId",
                table: "Conversations");

            migrationBuilder.DropTable(
                name: "LeadRecords");

            migrationBuilder.DropTable(
                name: "RoutingTeamMembers");

            migrationBuilder.DropTable(
                name: "LeadDepartments");

            migrationBuilder.DropTable(
                name: "RoutingTeams");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_AssignedTeamId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_CompanyId_AssignedTeamId_Status_LastMessageAtUtc",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_ConversationAssignmentHistory_NewAssignedTeamId",
                table: "ConversationAssignmentHistory");

            migrationBuilder.DropIndex(
                name: "IX_ConversationAssignmentHistory_PreviousAssignedTeamId",
                table: "ConversationAssignmentHistory");

            migrationBuilder.DropColumn(
                name: "AssignedTeamId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "NewAssignedTeamId",
                table: "ConversationAssignmentHistory");

            migrationBuilder.DropColumn(
                name: "PreviousAssignedTeamId",
                table: "ConversationAssignmentHistory");
        }
    }
}
