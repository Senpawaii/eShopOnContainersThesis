using Basket.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
using System.Threading;

namespace Basket.API.IntegrationEvents.EventHandling {
    public class ClientIDWrappedProductPriceChangedIntegrationEventHandler : IIntegrationEventHandler<ClientIDWrappedProductPriceChangedIntegrationEvent> {

        private readonly ILogger<ClientIDWrappedProductPriceChangedIntegrationEventHandler> _logger;
        private readonly Func<ProductPriceChangedIntegrationEventHandler, Task> originalHandler;
        private readonly ISingletonWrapper _wrapper;
        private readonly ICoordinatorService _coordinator;
        private readonly IScopedMetadata _metadata;

        public ClientIDWrappedProductPriceChangedIntegrationEventHandler(ILogger<ClientIDWrappedProductPriceChangedIntegrationEventHandler> logger, 
            Func<ProductPriceChangedIntegrationEventHandler, Task> originalHandler, ISingletonWrapper wrapper, ICoordinatorService coordinator, IScopedMetadata metadata) {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.originalHandler = originalHandler;
            _wrapper = wrapper ?? throw new ArgumentNullException(nameof(wrapper));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        public async Task Handle(ClientIDWrappedProductPriceChangedIntegrationEvent @event) {
            using (LogContext.PushProperty("IntegrationEventContext", $"{@event.Id}-{Program.AppName}")) {
                _logger.LogInformation("----- Handling integration event: {IntegrationEventId} at {AppName} - ({@IntegrationEvent})", @event.Id, Program.AppName, @event);

                string clientID = @event.ClientID;
                int tokens = @event.Tokens;
                _metadata.Tokens.Value = tokens;
                _metadata.ClientID.Value = clientID;

                ProductPriceChangedIntegrationEvent @innerEvent = @event.ProductPriceChangedIntegrationEvent;
                _wrapper.StorePriceUpdateEvent(@innerEvent, clientID);

                // Send Tokens
                await _coordinator.SendEventTokens();

                //if(confirmed && _wrapper.isPairedEventReady(clientID)) {
                //    await _wrapper.PersistEvents(clientID);
                //}
            }
        }

    }
}
