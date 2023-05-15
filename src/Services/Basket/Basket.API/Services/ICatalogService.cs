namespace Microsoft.eShopOnContainers.Services.Basket.API.Services;
public interface ICatalogService {
    public Task<decimal> GetCatalogItemPriceAsync(string ProductName, string ProductBrand, string ProductType);
}
