using Catalog.API.Model;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure;

public class GarbageContext : DbContext {
    public GarbageContext(DbContextOptions<GarbageContext> options, ILogger<GarbageContext> logger) : base(options) {
        _logger = logger;
    }

    public readonly ILogger<GarbageContext> _logger;
    public DbSet<CatalogItemWithTimestamp> CatalogItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder) {
        builder.ApplyConfiguration(new CatalogItemWithTimestampEntityTypeConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        base.OnConfiguring(optionsBuilder);
    }
}
