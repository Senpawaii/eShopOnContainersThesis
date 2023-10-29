using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Abstractions;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Events;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF.Services;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF.Utilities;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using System.Data.Common;

namespace Microsoft.eShopOnContainers.Services.Discount.API.IntegrationEvents;

public class DiscountIntegrationEventService : IDiscountIntegrationEventService, IDisposable
{
    private readonly Func<DbConnection, IIntegrationEventLogService> _integrationEventLogServiceFactory;
    private readonly IEventBus _eventBus;
    private readonly DiscountContext _discountContext;
    private readonly IIntegrationEventLogService _eventLogService;
    private readonly ILogger<DiscountIntegrationEventService> _logger;
    private volatile bool disposedValue;

    public DiscountIntegrationEventService(
        ILogger<DiscountIntegrationEventService> logger,
        IEventBus eventBus,
        DiscountContext discountContext,
        Func<DbConnection, IIntegrationEventLogService> integrationEventLogServiceFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _discountContext = discountContext ?? throw new ArgumentNullException(nameof(discountContext));
        _integrationEventLogServiceFactory = integrationEventLogServiceFactory ?? throw new ArgumentNullException(nameof(integrationEventLogServiceFactory));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _eventLogService = _integrationEventLogServiceFactory(_discountContext.Database.GetDbConnection());
    }

    public async Task PublishThroughEventBusAsync(IntegrationEvent evt)
    {
        try
        {
            // _logger.LogInformation("----- Publishing integration event: {IntegrationEventId_published} from {AppName} - ({@IntegrationEvent})", evt.Id, Program.AppName, evt);

            //await _eventLogService.MarkEventAsInProgressAsync(evt.Id);
            _eventBus.Publish(evt);
            //await _eventLogService.MarkEventAsPublishedAsync(evt.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ERROR Publishing integration event: {IntegrationEventId} from {AppName} - ({@IntegrationEvent})", evt.Id, Program.AppName, evt);
            await _eventLogService.MarkEventAsFailedAsync(evt.Id);
        }
    }

    public async Task SaveEventAndDiscountContextChangesAsync(IntegrationEvent evt)
    {
        // _logger.LogInformation("----- DiscountIntegrationEventService - Saving changes and integrationEvent: {IntegrationEventId}", evt.Id);

        //Use of an EF Core resiliency strategy when using multiple DbContexts within an explicit BeginTransaction():
        //See: https://docs.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency            
        await ResilientTransaction.New(_discountContext).ExecuteAsync(async () =>
        {
            // Achieving atomicity between original discount database operation and the IntegrationEventLog thanks to a local transaction
            await _discountContext.SaveChangesAsync();
            await _eventLogService.SaveEventAsync(evt, _discountContext.Database.CurrentTransaction); // Disabled to simplify the sample
        });
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                (_eventLogService as IDisposable)?.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
