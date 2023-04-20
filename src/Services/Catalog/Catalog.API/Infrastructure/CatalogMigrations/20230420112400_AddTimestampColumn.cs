using Microsoft.EntityFrameworkCore.Migrations;

namespace Catalog.API.Infrastructure.Migrations {
    public partial class AddTimestampColumn : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<DateTime>(
                name: "Timestamp",
                table: "Catalog",
                type: "datetime2(7)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "Timestamp",
                table: "CatalogBrand",
                type: "datetime2(7)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "Timestamp",
                table: "CatalogType",
                type: "datetime2(7)",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "Catalog");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "CatalogBrand");

            migrationBuilder.DropColumn(
                name: "Timestamp",
                table: "CatalogType");
        }
    }
}
