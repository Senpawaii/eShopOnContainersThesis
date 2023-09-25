using Discount.API.IntegrationEvents.Events.Factories;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Discount.API.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;

namespace Discount.API.IntegrationEvents.Events.Factories {
    public class FactoryClientIDWrappedProductDiscountChangedIntegrationEvent : IFactoryClientIDWrappedProductDiscountChangedIntegrationEvent {
        private readonly IScopedMetadata _scopedMetadata;
        private readonly ILogger<FactoryClientIDWrappedProductDiscountChangedIntegrationEvent> _logger;

        public FactoryClientIDWrappedProductDiscountChangedIntegrationEvent(IScopedMetadata scopedMetadata, ILogger<FactoryClientIDWrappedProductDiscountChangedIntegrationEvent> logger) {
            _scopedMetadata = scopedMetadata;
            _logger = logger;
        }
        public ClientIDWrappedProductDiscountChangedIntegrationEvent getClientIDWrappedProductDiscountChangedIntegrationEvent(int productId, decimal newDiscount, decimal oldDiscount) {
            // Get the ClientID from the Service Wrapper (Wrapped Metadata)
            string clientID = _scopedMetadata.ClientID;
            int sessionTokens = _scopedMetadata.Tokens;

            // _logger.LogInformation("Event Factory | ClientID: {0} - Tokens: {1}", clientID, sessionTokens);

            // Split the tokens in half
            int partialTokensToSend = sessionTokens / 2;
            // Decrement the tokens in the Service Wrapper (Wrapped Metadata)
            _scopedMetadata.Tokens -= partialTokensToSend;

            // Create the ClientIDWrappedProductDiscountChangedIntegrationEvent
            ProductDiscountChangedIntegrationEvent productDiscountChangedIntegrationEvent = new ProductDiscountChangedIntegrationEvent(productId, newDiscount, oldDiscount);
            return new ClientIDWrappedProductDiscountChangedIntegrationEvent(clientID, partialTokensToSend, productDiscountChangedIntegrationEvent);
        }
    }
}
