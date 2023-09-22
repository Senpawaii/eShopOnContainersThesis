namespace Basket.API.IntegrationEvents.Events {
    // Integration Events notes: 
    // An Event is “something that has happened in the past”, therefore its name has to be past tense
    // An Integration Event is an event that can cause side effects to other microservices, Bounded-Contexts or external systems.
    public record ClientIDWrappedProductPriceChangedIntegrationEvent : IntegrationEvent {
        /** 
         * This Event class wraps the ProductPriceChangedIntegrationEvent class, adding the ClientID property, used to identify the functionality being executed.
         * **/
        public string ClientID { get; private init; }

        public int Tokens { get; private init; }
        
        public ProductPriceChangedIntegrationEvent ProductPriceChangedIntegrationEvent { get; private init; }
        
        /* Warning! The names of the parameters must match the names of the variables inside the event! (Because of Json Deserialization) */
        public ClientIDWrappedProductPriceChangedIntegrationEvent(string clientID, int tokens, ProductPriceChangedIntegrationEvent productPriceChangedIntegrationEvent) {
            ClientID = clientID;
            Tokens = tokens;
            ProductPriceChangedIntegrationEvent = productPriceChangedIntegrationEvent;
        }
    }
}
