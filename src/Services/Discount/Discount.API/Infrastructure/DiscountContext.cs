using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.EntityConfigurations;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.Interceptors;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
public class DiscountContext : DbContext {
    public DiscountContext(DbContextOptions<DiscountContext> options, IScopedMetadata scopedMetadata, IOptions<DiscountSettings> settings) : base(options) {
        _wrapperThesis = settings.Value.ThesisWrapperEnabled;
        _scopedMetadata = scopedMetadata;
    }

    public DiscountContext(DbContextOptions<DiscountContext> options, IOptions<DiscountSettings> settings) : base(options) {
        _wrapperThesis = settings.Value.ThesisWrapperEnabled;
    }

    public readonly IScopedMetadata _scopedMetadata;
    public readonly bool _wrapperThesis;

    public DbSet<DiscountItem> Discount { get; set; }

    protected override void OnModelCreating(ModelBuilder builder) {
        builder.ApplyConfiguration(new DiscountItemEntityTypeConfiguration());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
        // Add here the DB Interceptor as needed
        if (_wrapperThesis) {
            optionsBuilder.AddInterceptors(new DiscountDBInterceptor(_scopedMetadata));
        }
    }
}
