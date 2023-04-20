using Microsoft.EntityFrameworkCore.Migrations;

namespace Discount.API.Infrastructure.Migrations {
    public partial class AddTimestampColumn : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<DateTime>(
                name: "Timestamp",
                table: "Discount",
                type: "datetime2(7)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "Discount");
        }
    }
}
