using DiscountApi;
using static DiscountApi.Discount;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Grpc;

using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using Microsoft.Extensions.Logging;

public class DiscountService : DiscountBase
{
    private readonly DiscountContext _discountContext;
    private readonly DiscountSettings _settings;
    private readonly ILogger _logger;
    
    public DiscountService(DiscountContext dbContext, IOptions<DiscountSettings> settings, ILogger<DiscountService> logger)
    {
        _settings = settings.Value;
        _discountContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger;
    }

   
}
