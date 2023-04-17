using Microsoft.EntityFrameworkCore.Migrations;

namespace Discount.API.Infrastructure.Migrations
{
    public partial class UpdateTableNames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_discount",
                table: "discount");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Discount",
                table: "discount",
                column: "Id");

            migrationBuilder.RenameTable(
                name: "discount",
                newName: "Discount");

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Discount",
                table: "Discount");

            migrationBuilder.AddPrimaryKey(
                name: "PK_discount",
                table: "Discount",
                column: "Id");

            migrationBuilder.RenameTable(
                name: "Discount",
                newName: "discount");

        }
    }
}
