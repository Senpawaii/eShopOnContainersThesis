namespace Basket.API.Infrastructure.RedisInterceptors {
    public interface IRedisDatabaseInterceptor {
        public Task<bool> StringSetAsync(string buyerID, string jsonBasketSerialized);
    }
}
