using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public interface IBasketService {
    public Task<BasketData> GetBasketAsync(string id);
    public Task<BasketData> UpdateBasketAsync(BasketData currentBasket);
}
