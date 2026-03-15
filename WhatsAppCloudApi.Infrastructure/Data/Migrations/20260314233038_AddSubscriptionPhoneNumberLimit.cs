using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPhoneNumberLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxPhoneNumbers",
                table: "SubscriptionPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
UPDATE [SubscriptionPlans]
SET [MaxPhoneNumbers] = 1
WHERE UPPER([Code]) IN (N'BASIC', N'STARTER');
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxPhoneNumbers",
                table: "SubscriptionPlans");
        }
    }
}
