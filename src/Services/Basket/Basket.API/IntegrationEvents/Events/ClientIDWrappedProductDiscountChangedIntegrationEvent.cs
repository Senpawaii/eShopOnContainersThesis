namespace Basket.API.IntegrationEvents.Events {
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
        
        /* Warning! The names of the parameters must match the names of the variables inside the event! (Because of Json Deserialization) */
        public ClientIDWrappedProductDiscountChangedIntegrationEvent(string clientID, int tokens, ProductDiscountChangedIntegrationEvent productDiscountChangedIntegrationEvent) {
            ClientID = clientID;
            Tokens = tokens;
            ProductDiscountChangedIntegrationEvent = productDiscountChangedIntegrationEvent;
        }
    }
}
