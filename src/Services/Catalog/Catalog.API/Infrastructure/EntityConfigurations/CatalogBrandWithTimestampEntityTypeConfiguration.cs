namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.EntityConfigurations;

class CatalogBrandWithTimestampEntityTypeConfiguration
    : IEntityTypeConfiguration<CatalogBrandWithTimestamp> {
    public void Configure(EntityTypeBuilder<CatalogBrandWithTimestamp> builder) {
        builder.ToTable("CatalogBrand");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .UseHiLo("catalog_brand_hilo")
            .IsRequired();

        builder.Property(cb => cb.Brand)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cb => cb.Timestamp)
            .IsRequired(true);
    }
}
