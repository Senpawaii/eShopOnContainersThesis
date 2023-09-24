using System.Threading;

namespace Basket.API.DependencyServices {
    public interface ISingletonWrapper {
        public void StorePriceUpdateEvent(ProductPriceChangedIntegrationEvent @event, string clientID);
        public void StoreDiscountUpdateEvent(ProductDiscountChangedIntegrationEvent @event, string clientID);
        public Task ConfirmFunctionality(string clientID);
        public bool isPairedEventReady(string clientID);
        public PairedEvents GetEvents(string clientID);
    }
}
