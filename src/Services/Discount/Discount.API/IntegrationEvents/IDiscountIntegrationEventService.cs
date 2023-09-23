using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Events;

namespace Microsoft.eShopOnContainers.Services.Discount.API.IntegrationEvents;

public interface IDiscountIntegrationEventService
{
    Task SaveEventAndDiscountContextChangesAsync(IntegrationEvent evt);
    Task PublishThroughEventBusAsync(IntegrationEvent evt);
}
