using Catalog.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.Interceptors;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure;

public class CatalogContext : DbContext
{
    public CatalogContext(DbContextOptions<CatalogContext> options, IScopedMetadata scopedMetadata, ISingletonWrapper wrapper, IOptions<CatalogSettings> settings, ILogger<CatalogContext> logger) : base(options)
    {
        _wrapperThesis = settings.Value.ThesisWrapperEnabled;
        _scopedMetadata = scopedMetadata;
        _wrapper = wrapper;
        _settings = settings;
        _logger = logger;
    }

    public CatalogContext(DbContextOptions<CatalogContext> options, IOptions<CatalogSettings> settings) : base(options) {
        _wrapperThesis = settings.Value.ThesisWrapperEnabled;
    }
    public readonly IScopedMetadata _scopedMetadata;
    public readonly ISingletonWrapper _wrapper;
    public readonly bool _wrapperThesis;
    public readonly IOptions<CatalogSettings> _settings;
    public readonly ILogger<CatalogContext> _logger;
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
        if(_wrapperThesis) {
            optionsBuilder.AddInterceptors(new CatalogDBInterceptor(_scopedMetadata, _wrapper, _logger, _settings));
        }
    }
}


//public class CatalogContextDesignFactory : IDesignTimeDbContextFactory<CatalogContext> {
//    public CatalogContext CreateDbContext(string[] args) {
//        var optionsBuilder = new DbContextOptionsBuilder<CatalogContext>()
//            .UseSqlServer("Server=.;Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb;Integrated Security=true");

//        return new CatalogContext(optionsBuilder.Options);
//    }
//}
