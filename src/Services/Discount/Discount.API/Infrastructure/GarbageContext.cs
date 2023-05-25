using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.EntityConfigurations;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;

public class GarbageContext : DbContext {
    public GarbageContext(DbContextOptions<GarbageContext> options, ILogger<GarbageContext> logger) : base(options) {
        _logger = logger;
    }

    public readonly ILogger<GarbageContext> _logger;
    public DbSet<DiscountItemWithTimestamp> DiscountItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder) {
        builder.ApplyConfiguration(new DiscountItemWithTimestampEntityTypeConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        base.OnConfiguring(optionsBuilder);
    }
}
