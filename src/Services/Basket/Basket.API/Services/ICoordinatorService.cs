namespace Microsoft.eShopOnContainers.Services.Basket.API.Services;
public interface ICoordinatorService {
    Task SendTokens();
    Task SendEventTokens();
}
