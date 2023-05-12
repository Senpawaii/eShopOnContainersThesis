using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;

namespace Discount.API.Infrastructure.Migrations
{
    [DbContext(typeof(DiscountContext))]
    partial class DiscountContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                //.HasAnnotation("Relational:Sequence:.discount_hilo", "'catalog_hilo', '', '1', '10', '', '', 'Int64', 'False'")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Microsoft.eShopOnContainers.Services.Discount.API.Model.DiscountItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:HiLoSequenceName", "discount_hilo")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.SequenceHiLo);

                    b.Property<string>("ItemName")
                        .IsRequired()
                        .HasMaxLength(50);

                    b.Property<string>("ItemBrand")
                        .IsRequired()
                        .HasMaxLength(50);

                    b.Property<string>("ItemType")
                        .IsRequired()
                        .HasMaxLength(50);

                    b.Property<int>("DiscountValue");

                    b.HasKey("Id");

                    b.ToTable("Discount");
                });

        }
    }
}
