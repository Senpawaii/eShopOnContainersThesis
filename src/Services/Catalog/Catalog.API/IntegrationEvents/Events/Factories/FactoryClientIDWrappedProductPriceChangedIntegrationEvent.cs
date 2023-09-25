using Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
using Microsoft.Extensions.Logging;

namespace Catalog.API.IntegrationEvents.Events.Factories {
    public class FactoryClientIDWrappedProductPriceChangedIntegrationEvent : IFactoryClientIDWrappedProductPriceChangedIntegrationEvent {
        private readonly IScopedMetadata _scopedMetadata;
        private readonly ILogger<FactoryClientIDWrappedProductPriceChangedIntegrationEvent> _logger;

        public FactoryClientIDWrappedProductPriceChangedIntegrationEvent(IScopedMetadata scopedMetadata, ILogger<FactoryClientIDWrappedProductPriceChangedIntegrationEvent> logger) {
            _scopedMetadata = scopedMetadata;
            _logger = logger;
        }
        public ClientIDWrappedProductPriceChangedIntegrationEvent getClientIDWrappedProductPriceChangedIntegrationEvent(int productId, decimal newPrice, decimal oldPrice) {
            // Get the ClientID from the Service Wrapper (Wrapped Metadata)
            string clientID = _scopedMetadata.ClientID;
            int sessionTokens = _scopedMetadata.Tokens;

            // _logger.LogInformation("Event Factory | ClientID: {0} - Tokens: {1}", clientID, sessionTokens);

            // Split the tokens in half
            int partialTokensToSend = sessionTokens / 2;
            // Decrement the tokens in the Service Wrapper (Wrapped Metadata)
            _scopedMetadata.Tokens -= partialTokensToSend;

            // Create the ClientIDWrappedProductPriceChangedIntegrationEvent
            ProductPriceChangedIntegrationEvent productPriceChangedIntegrationEvent = new ProductPriceChangedIntegrationEvent(productId, newPrice, oldPrice);
            return new ClientIDWrappedProductPriceChangedIntegrationEvent(clientID, partialTokensToSend, productPriceChangedIntegrationEvent);
        }
    }
}
