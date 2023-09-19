using Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;

namespace Catalog.API.IntegrationEvents.Events.Factories {
    public class FactoryClientIDWrappedProductPriceChangedIntegrationEvent : IFactoryClientIDWrappedProductPriceChangedIntegrationEvent {
        private readonly IScopedMetadata _scopedMetadata;

        public FactoryClientIDWrappedProductPriceChangedIntegrationEvent(IScopedMetadata scopedMetadata) {
            _scopedMetadata = scopedMetadata;
        }
        public ClientIDWrappedProductPriceChangedIntegrationEvent getClientIDWrappedProductPriceChangedIntegrationEvent(int productId, decimal newPrice, decimal oldPrice) {
            // Get the ClientID from the Service Wrapper (Wrapped Metadata)
            string clientID = _scopedMetadata.ClientID;
            ProductPriceChangedIntegrationEvent productPriceChangedIntegrationEvent = new ProductPriceChangedIntegrationEvent(productId, newPrice, oldPrice);
            return new ClientIDWrappedProductPriceChangedIntegrationEvent(clientID, productPriceChangedIntegrationEvent);
        }
    }
}
