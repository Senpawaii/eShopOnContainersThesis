namespace Catalog.API.IntegrationEvents.Events.Factories {
    public interface IFactoryClientIDWrappedProductPriceChangedIntegrationEvent {
        public ClientIDWrappedProductPriceChangedIntegrationEvent getClientIDWrappedProductPriceChangedIntegrationEvent(int productId, decimal newPrice, decimal oldPrice);
    }
}
