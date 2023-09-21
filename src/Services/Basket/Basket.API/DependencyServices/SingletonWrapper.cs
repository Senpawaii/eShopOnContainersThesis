using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Basket.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, PairedEvents> updateEvents = new ConcurrentDictionary<string, PairedEvents>();
        IServiceScopeFactory _serviceScopeFactory;

        public SingletonWrapper(IServiceScopeFactory serviceScope) { 
            _serviceScopeFactory = serviceScope;
        }

        public Task UpdateBasketItemDiscountAsync(CustomerBasket basket, string clientID) {
            throw new NotImplementedException();
        }

        public void StorePriceUpdateEvent(ProductPriceChangedIntegrationEvent @event, string clientID) {
            //Task updatePriceTask = null;
            //Task updateDiscountTask = null;

            updateEvents.TryAdd(clientID, new PairedEvents() { PriceEvent = null, ConfirmedFunctionality = false });
            lock (updateEvents[clientID]) {
                // Add the event to the updateEvents dictionary
                updateEvents[clientID].PriceEvent = @event;
            }
        }

        public async Task ConfirmFunctionality(string clientID) {
            Task updatePriceTask = null;
            Task updateDiscountTask = null;

            lock (updateEvents[clientID]) {
                updateEvents.TryGetValue(clientID, out PairedEvents pairEvs);
                pairEvs.ConfirmedFunctionality = true;

                // We have both events and the functionality has been confirmed, so we can update the Redis Database by calling the original handlers    
                using var handlerScope = _serviceScopeFactory.CreateScope();
                var originalPriceEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductPriceChangedIntegrationEventHandler>();
                //var originalDiscountEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductDiscountChangedIntegrationEventHandler>();
                updatePriceTask = originalPriceEventHandler.Handle(updateEvents[clientID].PriceEvent);
                //updateDiscountTask = originalDiscountEventHandler.Handle(updateEvents[clientID].DiscountEvent);
            }

            // TODO: Create N threads (in this case 2) to update the database in parallel
            if (updatePriceTask != null) {
                await updatePriceTask;
            }
            if (updateDiscountTask != null) {
                await updateDiscountTask;
            }
        }

        public bool isPairedEventReady(string clientID) {
            return updateEvents[clientID].PriceEvent != null && updateEvents[clientID].ConfirmedFunctionality; // TODO: Add Discount Event verification
        }
    }

    internal class PairedEvents {
        public ProductPriceChangedIntegrationEvent PriceEvent { get; set; }
        //public ProductDiscountChangedIntegrationEvent DiscountEvent { get; set; }
        public bool ConfirmedFunctionality { get; set;}
    }
}
