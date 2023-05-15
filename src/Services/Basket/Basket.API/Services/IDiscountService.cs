namespace Microsoft.eShopOnContainers.Services.Basket.API.Services;
public interface IDiscountService {
    public Task<decimal> GetDiscountValueAsync(string itemName, string brandName, string typeName);
}
