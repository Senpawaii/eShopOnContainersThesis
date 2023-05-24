using Catalog.API.Model;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.EntityConfigurations;

class CatalogItemWithTimestampEntityTypeConfiguration
    : IEntityTypeConfiguration<CatalogItemWithTimestamp>
{
    public void Configure(EntityTypeBuilder<CatalogItemWithTimestamp> builder)
    {
        builder.ToTable("Catalog");

        builder.Property(ci => ci.Id)
            .UseHiLo("catalog_hilo")
            .IsRequired();

        builder.Property(ci => ci.Name)
            .IsRequired(true)
            .HasMaxLength(50);

        builder.Property(ci => ci.Price)
            .IsRequired(true);

        builder.Property(ci => ci.PictureFileName)
            .IsRequired(false);

        builder.Ignore(ci => ci.PictureUri);

        builder.HasOne(ci => ci.CatalogBrand)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogBrandId);

        builder.HasOne(ci => ci.CatalogType)
            .WithMany()
            .HasForeignKey(ci => ci.CatalogTypeId);

        builder.Property(ci => ci.Timestamp)
            .IsRequired(true);
    }
}
