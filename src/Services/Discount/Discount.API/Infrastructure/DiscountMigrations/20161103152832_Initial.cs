using Microsoft.EntityFrameworkCore.Migrations;

namespace Discount.API.Infrastructure.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "discount_hilo",
                incrementBy: 10);

            migrationBuilder.CreateTable(
                name: "discount",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false),
                    CatalogItemId = table.Column<int>(nullable: false),
                    CatalogItemName = table.Column<string>(maxLength: 50, nullable: false),
                    Discount = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discount", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSequence(
                name: "discount_hilo");

            migrationBuilder.DropTable(
                name: "discount");

        }
    }
}
