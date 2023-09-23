using Basket.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.Interceptors;

namespace Microsoft.eShopOnContainers.Services.Basket.API.IntegrationEvents.EventHandling;

public class ProductDiscountChangedIntegrationEventHandler : IIntegrationEventHandler<ProductDiscountChangedIntegrationEvent>
{
    private readonly ILogger<ProductDiscountChangedIntegrationEventHandler> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IBasketRepository _repository;
    private readonly RedisDatabaseInterceptor _redisDatabaseInterceptor;
    private readonly ISingletonWrapper _wrapper;
    private readonly IScopedMetadata _scope_metadata;

    public ProductDiscountChangedIntegrationEventHandler(
        ILogger<ProductDiscountChangedIntegrationEventHandler> logger,
        IBasketRepository repository,
        ISingletonWrapper wrapper,
        ILoggerFactory loggerFactory,
        IScopedMetadata scopeMetadata)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
        ILogger<RedisDatabaseInterceptor> redisLogger = loggerFactory.CreateLogger<RedisDatabaseInterceptor>();
        _scope_metadata = scopeMetadata ?? throw new ArgumentNullException(nameof(scopeMetadata));
        _redisDatabaseInterceptor = new RedisDatabaseInterceptor(wrapper, redisLogger, scopeMetadata);
    }

    public async Task Handle(ProductDiscountChangedIntegrationEvent @event)
    {
        using (LogContext.PushProperty("IntegrationEventContext", $"{@event.Id}-{Program.AppName}"))
        {
            _logger.LogInformation("----- Handling integration event: {IntegrationEventId} at {AppName} - ({@IntegrationEvent})", @event.Id, Program.AppName, @event);

            var userIds = _repository.GetUsers();

            foreach (var id in userIds)
            {
                var basket = await _repository.GetBasketAsync(id);

                await UpdateDiscountInBasketItems(@event.ProductId, @event.NewDiscount, @event.OldDiscount, basket);
            }
        }
    }

    private async Task UpdateDiscountInBasketItems(int productId, decimal newDiscount, decimal oldDiscount, CustomerBasket basket)
    {
        var itemsToUpdate = basket?.Items?.Where(x => x.ProductId == productId).ToList();

        if (itemsToUpdate != null)
        {
            _logger.LogInformation("----- ProductDiscountChangedIntegrationEventHandler - Updating items in basket for user: {BuyerId} ({@Items})", basket.BuyerId, itemsToUpdate);

            foreach (var item in itemsToUpdate)
            {
                if (item.Discount == oldDiscount)
                {
                    var originalDiscount = item.Discount;
                    item.Discount = newDiscount;
                    item.OldDiscount = originalDiscount;
                }
            }
            await _repository.UpdateBasketAsync(basket);
        }
    }
}
