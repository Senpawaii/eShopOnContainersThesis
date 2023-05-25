namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.EntityConfigurations;

class CatalogTypeWithTimestampEntityTypeConfiguration
    : IEntityTypeConfiguration<CatalogTypeWithTimestamp> {
    public void Configure(EntityTypeBuilder<CatalogTypeWithTimestamp> builder) {
        builder.ToTable("CatalogType");

        builder.HasKey(ci => ci.Id);

        builder.Property(ci => ci.Id)
            .UseHiLo("catalog_type_hilo")
            .IsRequired();

        builder.Property(cb => cb.Type)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(cb => cb.Timestamp)
            .IsRequired(true);
    }
}
