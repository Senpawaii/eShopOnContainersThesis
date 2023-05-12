using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.EntityConfigurations;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.Interceptors;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
public class DiscountContext : DbContext {
    public DiscountContext(DbContextOptions<DiscountContext> options, IScopedMetadata scopedMetadata, ISingletonWrapper wrapper, IOptions<DiscountSettings> settings) : base(options) {
        _wrapperThesis = settings.Value.ThesisWrapperEnabled;
        _scopedMetadata = scopedMetadata;
        _wrapper = wrapper;
        _settings = settings;
    }

    public DiscountContext(DbContextOptions<DiscountContext> options, IOptions<DiscountSettings> settings) : base(options) {
        _wrapperThesis = settings.Value.ThesisWrapperEnabled;
    }

    public readonly IScopedMetadata _scopedMetadata;
    public readonly ISingletonWrapper _wrapper;
    public readonly bool _wrapperThesis;
    public readonly IOptions<DiscountSettings> _settings;

    public DbSet<DiscountItem> Discount { get; set; }

    protected override void OnModelCreating(ModelBuilder builder) {
        builder.ApplyConfiguration(new DiscountItemEntityTypeConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        // Add here the DB Interceptor as needed
        if (_wrapperThesis) {
            optionsBuilder.AddInterceptors(new DiscountDBInterceptor(this, _settings));
        }
    }
}
