using Basket.API.DependencyServices;
using Basket.API.Infrastructure.RedisInterceptors;
using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;

namespace Microsoft.eShopOnContainers.Services.Basket.API.Infrastructure.Interceptors;
public class RedisDatabaseInterceptor : IRedisDatabaseInterceptor {
    public ISingletonWrapper _wrapper; // This is the wrapper that contains all pending updates to the Redis database
    private readonly ILogger<RedisDatabaseInterceptor> _logger;
    private readonly IScopedMetadata _scopedMetadata;
    private readonly ConnectionMultiplexer _redis;
    private readonly IDatabase _database;

    public RedisDatabaseInterceptor(ConnectionMultiplexer redis, ISingletonWrapper wrapper, IScopedMetadata scopedMetadata,
        ILogger<RedisDatabaseInterceptor> logger) {
        _wrapper = wrapper;
        _logger = logger;
        _scopedMetadata = scopedMetadata;
        _redis = redis;
        _database = redis.GetDatabase();
    }

    public async Task<bool> StringSetAsync(string buyerID, string jsonBasketSerialized) {
        string clientID = _scopedMetadata.ClientID.Value;
        PairedEvents events = _wrapper.GetEvents(clientID);
        
        if(events == null) {
            _logger.LogInformation($"ClientID: {clientID} - No events associated with this clientID.");
            return await _database.StringSetAsync(buyerID, jsonBasketSerialized);
        }

        int productID = events.PriceEvent.ProductId;
        decimal newPrice = events.PriceEvent.NewPrice;
        decimal oldPrice = events.PriceEvent.OldPrice;
        decimal newDiscount = events.DiscountEvent.NewDiscount;
        decimal oldDiscount = events.DiscountEvent.OldDiscount;
        CustomerBasket basket = JsonSerializer.Deserialize<CustomerBasket>(jsonBasketSerialized);
        
        basket.Items.Where(item => item.ProductId == productID).ToList().ForEach(item => {
            item.UnitPrice = newPrice;
            item.Discount = newDiscount;

            item.OldUnitPrice = oldPrice;
            item.OldDiscount = oldDiscount;

            _logger.LogInformation($"ClientID: {clientID} - ProductID: {productID} - OldPrice: {oldPrice} - NewPrice: {newPrice} - OldDiscount: {oldDiscount} - NewDiscount: {newDiscount}.");
        });

        var created = await _database.StringSetAsync(buyerID, JsonSerializer.Serialize(basket));
        return created;
    }
}
