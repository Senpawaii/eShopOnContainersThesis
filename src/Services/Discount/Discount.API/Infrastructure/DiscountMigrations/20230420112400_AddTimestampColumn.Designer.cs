using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;

namespace Discount.API.Infrastructure.Migrations {
    [DbContext(typeof(DiscountContext))]
    [Migration("20230420112400_AddTimestampColumn")]
    partial class AddTimestampColumn {
        protected override void BuildTargetModel(ModelBuilder modelBuilder) {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                //.HasAnnotation("Relational:Sequence:.discount_hilo", "'catalog_hilo', '', '1', '10', '', '', 'Int64', 'False'")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Microsoft.eShopOnContainers.Services.Discount.API.Model.DiscountItem", b => {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:HiLoSequenceName", "discount_hilo")
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.SequenceHiLo);

                b.Property<int>("CatalogItemId");

                b.Property<int>("Discount");

                b.Property<string>("CatalogItemName")
                    .IsRequired()
                    .HasMaxLength(50);

                b.HasKey("Id");

                b.ToTable("Discount");
            });
        }
    }
}
