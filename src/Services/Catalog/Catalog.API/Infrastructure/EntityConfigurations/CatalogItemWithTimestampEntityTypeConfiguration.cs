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

        builder.Property(ci => ci.CatalogBrandId)
            .IsRequired(true);

        builder.Property(ci => ci.CatalogTypeId)
            .IsRequired(true);

        builder.Property(ci => ci.Timestamp)
            .IsRequired(true);
    }
}
