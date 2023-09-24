using Microsoft.EntityFrameworkCore.Migrations;

namespace Discount.API.Infrastructure.IntegrationEventMigrations
{
    public partial class AddTransactionId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "IntegrationEventLog",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "IntegrationEventLog");
        }
    }
}
