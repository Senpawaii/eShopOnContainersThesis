namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public interface IBasketService {
    public Task<string> GetBasketAsync(string id);
}
