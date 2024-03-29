﻿using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure;

namespace Catalog.API.Infrastructure.Migrations {
    [DbContext(typeof(CatalogContext))]
    [Migration("20230420112400_AddTimestampColumn")]
    partial class AddTimestampColumn {
        protected override void BuildTargetModel(ModelBuilder modelBuilder) {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                //.HasAnnotation("Relational:Sequence:.catalog_hilo", "'catalog_hilo', '', '1', '10', '', '', 'Int64', 'False'")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Microsoft.eShopOnContainers.Services.Catalog.API.Model.CatalogBrand", b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:HiLoSequenceName", "catalog_brand_hilo")
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.SequenceHiLo);

                b.Property<string>("Brand")
                    .IsRequired()
                    .HasMaxLength(100);

                b.HasKey("Id");

                b.ToTable("CatalogBrand");
            });

            modelBuilder.Entity("Microsoft.eShopOnContainers.Services.Catalog.API.Model.CatalogItem", b => {
                b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:HiLoSequenceName", "catalog_hilo")
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.SequenceHiLo);

                b.Property<int>("CatalogBrandId");

                b.Property<int>("CatalogTypeId");

                b.Property<string>("Description");

                b.Property<string>("Name")
                    .IsRequired()
                    .HasMaxLength(50);

                b.Property<string>("PictureFileName");

                b.Property<decimal>("Price");

                b.HasKey("Id");

                b.HasIndex("CatalogBrandId");

                b.HasIndex("CatalogTypeId");

                b.ToTable("Catalog");
            });

            modelBuilder.Entity("Microsoft.eShopOnContainers.Services.Catalog.API.Model.CatalogType", b =>
            {
                b.Property<int>("Id")
                    .ValueGeneratedOnAdd()
                    .HasAnnotation("SqlServer:HiLoSequenceName", "catalog_type_hilo")
                    .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.SequenceHiLo);

                b.Property<string>("Type")
                    .IsRequired()
                    .HasMaxLength(100);

                b.HasKey("Id");

                b.ToTable("CatalogType");
            });

            modelBuilder.Entity("Microsoft.eShopOnContainers.Services.Catalog.API.Model.CatalogItem", b =>
            {
                b.HasOne("Microsoft.eShopOnContainers.Services.Catalog.API.Model.CatalogBrand", "CatalogBrand")
                    .WithMany()
                    .HasForeignKey("CatalogBrandId")
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne("Microsoft.eShopOnContainers.Services.Catalog.API.Model.CatalogType", "CatalogType")
                    .WithMany()
                    .HasForeignKey("CatalogTypeId")
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
