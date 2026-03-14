using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContactProfileHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactProfileHistory",
                columns: table => new
                {
                    ContactProfileHistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompanyId = table.Column<int>(type: "int", nullable: false),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: true),
                    ChangeType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreviousValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false, defaultValue: "system"),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactProfileHistory", x => x.ContactProfileHistoryId);
                    table.ForeignKey(
                        name: "FK_ContactProfileHistory_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "CompanyId");
                    table.ForeignKey(
                        name: "FK_ContactProfileHistory_CompanyUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "CompanyUsers",
                        principalColumn: "CompanyUserId");
                    table.ForeignKey(
                        name: "FK_ContactProfileHistory_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "ContactId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContactProfileHistory_ChangedByUserId",
                table: "ContactProfileHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactProfileHistory_CompanyId_ContactId_CreatedAtUtc",
                table: "ContactProfileHistory",
                columns: new[] { "CompanyId", "ContactId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ContactProfileHistory_ContactId",
                table: "ContactProfileHistory",
                column: "ContactId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactProfileHistory");
        }
    }
}
