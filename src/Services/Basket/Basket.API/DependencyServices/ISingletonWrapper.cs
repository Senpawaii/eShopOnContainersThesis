using System.Threading;

namespace Basket.API.DependencyServices {
    public interface ISingletonWrapper {
        public Task StorePriceUpdateEvent(ProductPriceChangedIntegrationEvent @event, string clientID);
        public Task ConfirmFunctionality(string clientID);

        //public Task StoreDiscountUpdateEvent(ClientIDWrappedProductDiscountChangedIntegrationEvent @event, string clientID);
        //public ManualResetEvent GetPairCompleteEventMonitor(string clientID);
        }
    }
