using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Events;
using Microsoft.eShopOnContainers.Services.Discount.API.IntegrationEvents.Events;

namespace Discount.API.IntegrationEvents.Events {
    // Integration Events notes: 
    // An Event is “something that has happened in the past”, therefore its name has to be past tense
    // An Integration Event is an event that can cause side effects to other microservices, Bounded-Contexts or external systems.
    public record ClientIDWrappedProductDiscountChangedIntegrationEvent : IntegrationEvent {
        /** 
         * This Event class wraps the ProductDiscountChangedIntegrationEvent class, adding the ClientID property, used to identify the functionality being executed.
         * **/
        public string ClientID { get; private init; }
        public int Tokens { get; private init; }
        public ProductDiscountChangedIntegrationEvent ProductDiscountChangedIntegrationEvent { get; private init; }
        public ClientIDWrappedProductDiscountChangedIntegrationEvent(string clientID, int tokens, ProductDiscountChangedIntegrationEvent wrapped_event) {
            // Get the ClientID from the Service Wrapper (Wrapped Metadata)
            ClientID = clientID;
            Tokens = tokens;
            ProductDiscountChangedIntegrationEvent = wrapped_event;
        }
    }
}
