using Basket.API.DependencyServices;
using System.Threading;

namespace Basket.API.IntegrationEvents.EventHandling {
    public class ClientIDWrappedProductPriceChangedIntegrationEventHandler : IIntegrationEventHandler<ClientIDWrappedProductPriceChangedIntegrationEvent> {

        private readonly ILogger<ClientIDWrappedProductPriceChangedIntegrationEventHandler> _logger;
        private readonly Func<ProductPriceChangedIntegrationEventHandler, Task> originalHandler;
        private readonly ISingletonWrapper _wrapper;
        private readonly ICoordinatorService _coordinator;

        public ClientIDWrappedProductPriceChangedIntegrationEventHandler(ILogger<ClientIDWrappedProductPriceChangedIntegrationEventHandler> logger, 
            Func<ProductPriceChangedIntegrationEventHandler, Task> originalHandler, ISingletonWrapper wrapper, ICoordinatorService coordinator) {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.originalHandler = originalHandler;
            _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public async Task Handle(ClientIDWrappedProductPriceChangedIntegrationEvent @event) {
            using (LogContext.PushProperty("IntegrationEventContext", $"{@event.Id}-{Program.AppName}")) {
                _logger.LogInformation("----- Handling integration event: {IntegrationEventId} at {AppName} - ({@IntegrationEvent})", @event.Id, Program.AppName, @event);

                string clientID = @event.ClientID;
                ProductPriceChangedIntegrationEvent @innerEvent = @event.ProductPriceChangedIntegrationEvent;
                await _wrapper.StorePriceUpdateEvent(@innerEvent, clientID);

                // Send OK to the coordinator and check if the response is OK (funcionality confirmed) or NOK (funcionality not confirmed)
                bool confirmed = await _coordinator.QueryConfirmation();

                if(confirmed && _wrapper.isPairedEventReady(clientID)) {
                    await _wrapper.PersistEvents(clientID);
                }

            }
        }

    }
}
