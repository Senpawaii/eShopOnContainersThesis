using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Events;

namespace Discount.API.Infrastructure.Migrations
{
    [DbContext(typeof(DiscountContext))]
    [Migration("20230923183702_RefactoringEventBusNamespaces")]
    partial class RefactoringEventBusNamespaces
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.1")
                //.HasAnnotation("SqlServer:Sequence:.catalog_brand_hilo", "'catalog_brand_hilo', '', '1', '10', '', '', 'Int64', 'False'")
                //.HasAnnotation("SqlServer:Sequence:.catalog_hilo", "'catalog_hilo', '', '1', '10', '', '', 'Int64', 'False'")
                //.HasAnnotation("SqlServer:Sequence:.catalog_type_hilo", "'catalog_type_hilo', '', '1', '10', '', '', 'Int64', 'False'")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Events.IntegrationEventLogEntry", b =>
                {
                    b.Property<Guid>("EventId")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Content")
                        .IsRequired();

                    b.Property<DateTime>("CreationTime");

                    b.Property<string>("EventTypeName")
                        .IsRequired();

                    b.Property<int>("State");

                    b.Property<int>("TimesSent");

                    b.HasKey("EventId");

                    b.ToTable("IntegrationEventLog");
                });

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
