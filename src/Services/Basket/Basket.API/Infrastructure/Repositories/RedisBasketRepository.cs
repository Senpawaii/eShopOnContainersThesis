﻿using Basket.API.Infrastructure.RedisInterceptors;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.Repositories;

public class RedisBasketRepository : IBasketRepository
{
    private readonly ILogger<RedisBasketRepository> _logger;
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ICatalogService _catalogService;
    private readonly IDiscountService _discountService;
    private readonly IRedisDatabaseInterceptor _redisDatabaseInterceptor;
    private readonly IOptions<BasketSettings> _settings;

    public RedisBasketRepository(ILoggerFactory loggerFactory, ConnectionMultiplexer redis, ICatalogService catalogSvc, IDiscountService discountSvc,
        IRedisDatabaseInterceptor redisDatabaseInterceptor, IOptions<BasketSettings> settings)
    {
        _logger = loggerFactory.CreateLogger<RedisBasketRepository>();
        _redis = redis;
        _database = redis.GetDatabase();
        _catalogService = catalogSvc;
        _discountService = discountSvc;
        _redisDatabaseInterceptor = redisDatabaseInterceptor;
        _settings = settings;
    }

    public async Task<bool> DeleteBasketAsync(string id)
    {
        return await _database.KeyDeleteAsync(id);
    }

    public IEnumerable<string> GetUsers()
    {
        var server = GetServer();
        var data = server.Keys();

        return data?.Select(k => k.ToString());
    }

    public async Task<CustomerBasket> GetBasketAsync(string customerId)
    {
        var data = await _database.StringGetAsync(customerId);

        if (data.IsNullOrEmpty)
        {
            return null;
        }

        CustomerBasket basket = JsonSerializer.Deserialize<CustomerBasket>(data, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if(!_settings.Value.PublishEventEnabled) {
            // Update each basket item price and discount from the catalog and discount services
            foreach (var item in basket.Items) {
                // _logger.LogInformation($"Updating item {item.ProductId} of basket {basket.BuyerId} with product price and discount");
                var catalogItemPrice = await _catalogService.GetCatalogItemPriceAsync(item.ProductName, item.ProductBrand, item.ProductType);
                var discountValue = await _discountService.GetDiscountValueAsync(item.ProductName, item.ProductBrand, item.ProductType);
                item.UnitPrice = catalogItemPrice;
                item.Discount = discountValue;
            }
        }

        return basket;
    }

    public async Task<CustomerBasket> UpdateBasketAsync(CustomerBasket basket)
    {
        bool created;

        if(_settings.Value.ThesisWrapperEnabled) {
            created = await _redisDatabaseInterceptor.StringSetAsync(basket.BuyerId, JsonSerializer.Serialize(basket));
        } 
        else {
            created = await _database.StringSetAsync(basket.BuyerId, JsonSerializer.Serialize(basket));
        }

        if (!created)
        {
            _logger.LogInformation("Problem occur persisting the item.");
            return null;
        }

        // _logger.LogInformation("Basket item persisted succesfully.");

        return await GetBasketAsync(basket.BuyerId);
    }

    private IServer GetServer()
    {
        var endpoint = _redis.GetEndPoints();
        return _redis.GetServer(endpoint.First());
    }
}
