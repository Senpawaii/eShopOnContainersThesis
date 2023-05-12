using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.EntityConfigurations {
    public class DiscountItemEntityTypeConfiguration : IEntityTypeConfiguration<Model.DiscountItem>{
        public void Configure(EntityTypeBuilder<DiscountItem> builder) {
            builder.ToTable("Discount");
            builder.HasKey(di => di.Id);
            builder.Property(di => di.Id)
                .UseHiLo("discount_hilo")
                .IsRequired();

            builder.Property(dd => dd.DiscountValue)
                .IsRequired();

            builder.Property(dn => dn.ItemName)
                .IsRequired();

            builder.Property(dt  => dt.ItemType)
                .IsRequired();

            builder.Property(db  => db.ItemBrand)
                .IsRequired();
        }
    }
}
