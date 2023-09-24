using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;

namespace Discount.API.Infrastructure.Migrations {
    [DbContext(typeof(DiscountContext))]
    [Migration("20170314083211_AddEventTable")]
    partial class AddEventTable {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.0-rtm-22752")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Microsoft.eShopOnContainers.Services.Discount.API.Model.DiscountItem", b =>
                {
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

            modelBuilder.Entity("Microsoft.eShopOnContainers.Services.Common.Infrastructure.Data.IntegrationEvent", b =>
                {
                    b.Property<Guid>("EventId")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Content")
                        .IsRequired();

                    b.Property<DateTime>("CreationTime");

                    b.Property<string>("EventTypeName")
                        .IsRequired()
                        .HasMaxLength(200);

                    b.Property<int>("State");

                    b.Property<int>("TimesSent");

                    b.HasKey("EventId");

                    b.ToTable("IntegrationEvent");
                });
        }
    }
}
