using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.EntityConfigurations;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
    public class DiscountContext : DbContext {
        public DiscountContext(DbContextOptions<DiscountContext> options) : base(options) { }

    public DbSet<DiscountItem> DiscountItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder) {
        builder.ApplyConfiguration(new DiscountItemEntityTypeConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        // Add here the DB Interceptor as needed
    }
}
