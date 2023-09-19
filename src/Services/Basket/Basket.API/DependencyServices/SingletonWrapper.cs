using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Basket.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, PairedEvents> updateEvents = new ConcurrentDictionary<string, PairedEvents>();
        //ConcurrentDictionary<string, ManualResetEvent> completed_pairs_to_persist = new ConcurrentDictionary<string, ManualResetEvent>();
        IServiceScopeFactory _serviceScopeFactory;

        public SingletonWrapper(IServiceScopeFactory serviceScope) { 
            _serviceScopeFactory = serviceScope;
        }

        //public ManualResetEvent GetPairCompleteEventMonitor(string clientID) {
        //    if(completed_pairs_to_persist.TryGetValue(clientID, out var MRE)) {
        //        return MRE;
        //    }
        //    throw new Exception("No ManualResetEvent found for the given clientID");
        //}

        public Task UpdateBasketItemDiscountAsync(CustomerBasket basket, string clientID) {
            throw new NotImplementedException();
        }

        public async Task StorePriceUpdateEvent(ProductPriceChangedIntegrationEvent @event, string clientID) {
            Task updatePriceTask = null;
            Task updateDiscountTask = null;

            updateEvents.TryAdd(clientID, new PairedEvents() { PriceEvent = null, ConfirmedFunctionality = false });            
            lock (updateEvents[clientID]) {
                //TryGenerateNewMonitor(clientID);

                // Add the event to the updateEvents dictionary
                updateEvents[clientID].PriceEvent = @event;

                if (isPairedEventReady(clientID)) {
                    // We have both events and the functionality has been confirmed, so we can update the Redis Database by calling the original handlers    
                    using var handlerScope = _serviceScopeFactory.CreateScope();
                    var originalPriceEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductPriceChangedIntegrationEventHandler>();
                    //var originalDiscountEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductDiscountChangedIntegrationEventHandler>();
                    updatePriceTask = originalPriceEventHandler.Handle(updateEvents[clientID].PriceEvent);
                    //updateDiscountTask = originalDiscountEventHandler.Handle(updateEvents[clientID].DiscountEvent);

                    // The Update Price and Update Discount are complete
                    //updateEvents.TryRemove(clientID, out PairedEvents _);
                    //completed_pairs_to_persist.TryRemove(clientID, out ManualResetEvent _);
                    updateEvents[clientID].Persisted = true;
                }
            }

            if (updatePriceTask != null) {
                await updatePriceTask;
            }
            if(updateDiscountTask != null) {
                await updateDiscountTask;
            }
        }

        public async Task ConfirmFunctionality(string clientID) {
            Task updatePriceTask = null;
            Task updateDiscountTask = null;

            updateEvents.TryAdd(clientID, new PairedEvents() { PriceEvent = null, ConfirmedFunctionality = false });
            lock (updateEvents[clientID]) {
                updateEvents[clientID].ConfirmedFunctionality = true;

                if (isPairedEventReady(clientID)) {
                    // We have both events and the functionality has been confirmed, so we can update the Redis Database by calling the original handlers    
                    using var handlerScope = _serviceScopeFactory.CreateScope();
                    var originalPriceEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductPriceChangedIntegrationEventHandler>();
                    //var originalDiscountEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductDiscountChangedIntegrationEventHandler>();
                    updatePriceTask = originalPriceEventHandler.Handle(updateEvents[clientID].PriceEvent);
                    //updateDiscountTask = originalDiscountEventHandler.Handle(updateEvents[clientID].DiscountEvent);

                    // The Update Price and Update Discount are complete
                    //updateEvents.TryRemove(clientID, out PairedEvents _);
                    //completed_pairs_to_persist.TryRemove(clientID, out ManualResetEvent _);
                }
            }

            if (updatePriceTask != null) {
                await updatePriceTask;
            }
            if (updateDiscountTask != null) {
                await updateDiscountTask;
            }
        }

        public void UpdateConfirmation(string clientID) {
            updateEvents[clientID].ConfirmedFunctionality = true;
        }

        //private void TryGenerateNewMonitor(string clientID) {
        //    if (!completed_pairs_to_persist.TryGetValue(clientID, out var _)) {
        //        // This is the first event for this clientID, so we need to create a new ManualResetEvent
        //        ManualResetEvent MRE = new ManualResetEvent(false);
        //        completed_pairs_to_persist.TryAdd(clientID, MRE);
        //    }
        //}

        public bool isPairedEventReady(string clientID) {
            return updateEvents[clientID].PriceEvent != null && updateEvents[clientID].ConfirmedFunctionality; // TODO: Add Discount Event verification
        }

        public async Task PersistEvents(string clientID) {
            Task updatePriceTask;
            
            // The events have already been persisted
            if (updateEvents[clientID].Persisted) {
                return;
            }

            lock (updateEvents[clientID]) {
                // We have both events and the functionality has been confirmed, so we can update the Redis Database by calling the original handlers    
                using var handlerScope = _serviceScopeFactory.CreateScope();
                var originalPriceEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductPriceChangedIntegrationEventHandler>();
                //var originalDiscountEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductDiscountChangedIntegrationEventHandler>();
                updatePriceTask = originalPriceEventHandler.Handle(updateEvents[clientID].PriceEvent);
                //updateDiscountTask = originalDiscountEventHandler.Handle(updateEvents[clientID].DiscountEvent);

                updateEvents[clientID].Persisted = true;
            }

            await updatePriceTask;
        }
    }

    internal class PairedEvents {
        public ProductPriceChangedIntegrationEvent PriceEvent { get; set; }
        //public ProductDiscountChangedIntegrationEvent DiscountEvent { get; set; }
        public bool ConfirmedFunctionality { get; set;}
        public bool Persisted { get; set; }
    }
}
