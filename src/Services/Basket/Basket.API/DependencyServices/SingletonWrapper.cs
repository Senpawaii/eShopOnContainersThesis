using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Basket.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        ConcurrentDictionary<string, PairedEvents> updateEvents = new ConcurrentDictionary<string, PairedEvents>();
        IServiceScopeFactory _serviceScopeFactory;
        ILogger<SingletonWrapper> _logger;

        public SingletonWrapper(IServiceScopeFactory serviceScope, ILogger<SingletonWrapper> logger) { 
            _serviceScopeFactory = serviceScope;
            _logger = logger;
        }

        public void StoreDiscountUpdateEvent(ProductDiscountChangedIntegrationEvent @event, string clientID) {
            updateEvents.TryAdd(clientID, new PairedEvents() { PriceEvent = null, ConfirmedFunctionality = false });
            lock (updateEvents[clientID]) {
                // Add the event to the updateEvents dictionary
                updateEvents[clientID].DiscountEvent = @event;
            }
        }

        public void StorePriceUpdateEvent(ProductPriceChangedIntegrationEvent @event, string clientID) {
            updateEvents.TryAdd(clientID, new PairedEvents() { PriceEvent = null, ConfirmedFunctionality = false });
            lock (updateEvents[clientID]) {
                // Add the event to the updateEvents dictionary
                updateEvents[clientID].PriceEvent = @event;
            }
        }

        public async Task ConfirmFunctionality(string clientID) {
            Task updatePriceTask = null;
            //Task updateDiscountTask = null;

            lock (updateEvents[clientID]) {
                updateEvents.TryGetValue(clientID, out PairedEvents pairEvs);
                pairEvs.ConfirmedFunctionality = true;

                // We have both events and the functionality has been confirmed, so we can update the Redis Database by calling the original handlers    
                using var handlerScope = _serviceScopeFactory.CreateScope();
                var originalPriceEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductPriceChangedIntegrationEventHandler>();
                updatePriceTask = originalPriceEventHandler.Handle(updateEvents[clientID].PriceEvent);

                // We drop the discount event, as we will fetch it from the wrapper once we intercept the call to the Basket Update on Redis
                //var originalDiscountEventHandler = handlerScope.ServiceProvider.GetRequiredService<ProductDiscountChangedIntegrationEventHandler>();
                //updateDiscountTask = originalDiscountEventHandler.Handle(updateEvents[clientID].DiscountEvent);
            }

            // TODO: Create N threads (in this case 2) to update the database in parallel
            if (updatePriceTask != null) {
                _logger.LogInformation($"Waiting for Price Update Task to complete...");
                await updatePriceTask;
                _logger.LogInformation($"Price Update Task completed!");

            }
            //if (updateDiscountTask != null) {
            //    _logger.LogInformation($"Waiting for Discount Update Task to complete...");
            //    await updateDiscountTask;
            //    _logger.LogInformation($"Discount Update Task completed!");
            //}
        }

        public bool isPairedEventReady(string clientID) {
            return updateEvents[clientID].PriceEvent != null && updateEvents[clientID].ConfirmedFunctionality; // TODO: Add Discount Event verification
        }

        public PairedEvents GetEvents(string clientID) {
            // Return the events for the given clientID. If the clientID is not present, return null
            updateEvents.TryGetValue(clientID, out PairedEvents pairEvs);
            return pairEvs;
        }
    }
}
