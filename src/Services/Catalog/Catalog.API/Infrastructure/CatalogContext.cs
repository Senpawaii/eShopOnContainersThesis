using Catalog.API.DependencyServices;
using Catalog.API.Infrastructure.Interceptors;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure;

public class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options, IScopedMetadata scopedMetadata) : base(options)
    {
        _scopedMetadata = scopedMetadata;
    }
    public readonly IScopedMetadata _scopedMetadata;
    public DbSet<CatalogItem> CatalogItems { get; set; }
    public DbSet<CatalogBrand> CatalogBrands { get; set; }
    public DbSet<CatalogType> CatalogTypes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfiguration(new CatalogBrandEntityTypeConfiguration());
        builder.ApplyConfiguration(new CatalogTypeEntityTypeConfiguration());
        builder.ApplyConfiguration(new CatalogItemEntityTypeConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        optionsBuilder.AddInterceptors(new CatalogDBInterceptor(_scopedMetadata));
    }
}


//public class CatalogContextDesignFactory : IDesignTimeDbContextFactory<CatalogContext> {
//    public CatalogContext CreateDbContext(string[] args) {
//        var optionsBuilder = new DbContextOptionsBuilder<CatalogContext>()
//            .UseSqlServer("Server=.;Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb;Integrated Security=true");

//        return new CatalogContext(optionsBuilder.Options);
//    }
//}
