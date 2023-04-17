using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.EntityConfigurations {
    public class DiscountItemEntityTypeConfiguration : IEntityTypeConfiguration<DiscountItem>{
        public void Configure(EntityTypeBuilder<DiscountItem> builder) {
            builder.ToTable("DiscountItem");
            builder.HasKey(di => di.Id);
            builder.Property(di => di.Id)
                .UseHiLo("discount_item_hilo")
                .IsRequired();

            builder.Property(dd => dd.Discount)
                .IsRequired();

            builder.Property(di => di.CatalogItemId)
                .IsRequired();

            builder.Property(dn  => dn.CatalogItemName)
                .IsRequired();
        }
    }
}
