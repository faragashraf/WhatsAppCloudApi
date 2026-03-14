using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignRecipientVerificationAndUserTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TwoFactorEnabled",
                table: "CompanyUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TwoFactorEnabledAtUtc",
                table: "CompanyUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorSecretProtected",
                table: "CompanyUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TwoFactorUpdatedAtUtc",
                table: "CompanyUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsWhatsAppAccountConfirmed",
                table: "CampaignContacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAtUtc",
                table: "CampaignContacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppLookupCheckedAtUtc",
                table: "CampaignContacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppLookupError",
                table: "CampaignContacts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppLookupStatus",
                table: "CampaignContacts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "UNVERIFIED");

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppLookupWaId",
                table: "CampaignContacts",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwoFactorEnabled",
                table: "CompanyUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorEnabledAtUtc",
                table: "CompanyUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorSecretProtected",
                table: "CompanyUsers");

            migrationBuilder.DropColumn(
                name: "TwoFactorUpdatedAtUtc",
                table: "CompanyUsers");

            migrationBuilder.DropColumn(
                name: "IsWhatsAppAccountConfirmed",
                table: "CampaignContacts");

            migrationBuilder.DropColumn(
                name: "QueuedAtUtc",
                table: "CampaignContacts");

            migrationBuilder.DropColumn(
                name: "WhatsAppLookupCheckedAtUtc",
                table: "CampaignContacts");

            migrationBuilder.DropColumn(
                name: "WhatsAppLookupError",
                table: "CampaignContacts");

            migrationBuilder.DropColumn(
                name: "WhatsAppLookupStatus",
                table: "CampaignContacts");

            migrationBuilder.DropColumn(
                name: "WhatsAppLookupWaId",
                table: "CampaignContacts");
        }
    }
}
