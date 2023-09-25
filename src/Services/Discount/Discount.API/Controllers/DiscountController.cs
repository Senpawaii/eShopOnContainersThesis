using Discount.API.IntegrationEvents.Events.Factories;
using k8s.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.BuildingBlocks.EventBus.Events;
using Microsoft.eShopOnContainers.BuildingBlocks.IntegrationEventLogEF.Utilities;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using Microsoft.eShopOnContainers.Services.Discount.API.IntegrationEvents;
using Microsoft.eShopOnContainers.Services.Discount.API.IntegrationEvents.Events;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class DiscountController : ControllerBase {
    private readonly DiscountContext _discountContext;
    private readonly DiscountSettings _settings;
    private readonly IDiscountIntegrationEventService _discountIntegrationEventService;
    private readonly IFactoryClientIDWrappedProductDiscountChangedIntegrationEvent _clientIDWrappedEventFactory;
    private readonly ILogger<DiscountController> _logger;
    

    public DiscountController(DiscountContext discountContext, IOptionsSnapshot<DiscountSettings> settings, IDiscountIntegrationEventService discountIntegrationEventService, ILogger<DiscountController> logger,
        IFactoryClientIDWrappedProductDiscountChangedIntegrationEvent clientIDWrappedEventFactory) {
        _discountContext = discountContext ?? throw new ArgumentNullException(nameof(discountContext));
        _discountIntegrationEventService = discountIntegrationEventService ?? throw new ArgumentNullException(nameof(discountIntegrationEventService));
        _clientIDWrappedEventFactory = clientIDWrappedEventFactory ?? throw new ArgumentNullException(nameof(clientIDWrappedEventFactory));
        _settings = settings.Value;
        _logger = logger;

        // If changes need to be made to the objects queried from the Database, change this from NoTracking to TrackAll or TrackModified
        discountContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    /// <summary>
    /// Given a list of Discount Items, return all discounts associated with these items.
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("discounts")]
    [ProducesResponseType(typeof(IEnumerable<DiscountItem>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> DiscountsAsync([FromQuery] List<string> itemNames, [FromQuery] List<string> itemBrands, [FromQuery] List<string> itemTypes) {
        // Get the list of discount items from the database that match the triple of item name, brand, and type
        
        // Log the request
        // _logger.LogInformation($"Received request to read discount items with item names: {string.Join(",", itemNames)}, item brands: {string.Join(",", itemBrands)}, and item types: {string.Join(",", itemTypes)}");
        var discountsT = _discountContext.Discount.Where(i => itemNames.Contains(i.ItemName) && itemBrands.Contains(i.ItemBrand) && itemTypes.Contains(i.ItemType));
        List<DiscountItem> discounts;
        try {
            // _logger.LogInformation("Before ToListAsync()");
            discounts = await discountsT.ToListAsync();
        } catch (Exception e) {
            _logger.LogError(e, "Error occurred while reading discount items from the database");
            return BadRequest("Error occurred while reading discount items from the database");
        }
        if(discounts.IsNullOrEmpty()) {
            _logger.LogInformation("No discounts were found for the given item names, brands, and types");
            // Create a new default discount item
            var defaultDiscount = new DiscountItem {
                ItemName = "Default Item",
                ItemBrand = "Default Brand",
                ItemType = "Default Type",
                DiscountValue = 1
            };
            discounts.Add(defaultDiscount);
        }
        return Ok(discounts);
    }

    /// <summary>
    /// Create a new Discount Item.
    /// </summary>
    /// <param name="catalogItemId"></param>
    /// <param name="discountValue"></param>
    /// <returns></returns>
    // POST 
    [HttpPost]
    [Route("discounts")]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<ActionResult<int>> CreateDiscountAsync([FromQuery] string itemName, [FromQuery] string itemBrand, [FromQuery] string itemType, [FromQuery] string discountValue) {
        // Parse the query parameters
        if(itemName.IsNullOrEmpty()) {
            return BadRequest("Invalid Item Name");
        }
        if(itemBrand.IsNullOrEmpty()) {
            return BadRequest("Invalid Item Brand");
        }
        if(itemType.IsNullOrEmpty()) {
            return BadRequest("Invalid Item Type");
        }
        if(discountValue.IsNullOrEmpty() || !int.TryParse(discountValue, out int discount)) {
            return BadRequest("Invalid Discount Value (Discount value should not be empty or null and should be represented with by a number)");
        }
        
        var discountItem = new DiscountItem {
            ItemName = itemName,
            ItemBrand = itemBrand,
            ItemType = itemType,
            DiscountValue = discount
        };

        // Log the discount item creation
        // _logger.LogInformation($"Controller: Creating Discount Item with Name: {discountItem.ItemName}, Brand: {discountItem.ItemBrand}, Type: {discountItem.ItemType}, and Discount Value: {discountItem.DiscountValue}");

        _discountContext.Discount.Add(discountItem);
        await _discountContext.SaveChangesAsync();

        return discountItem.Id;
    }

    //PUT api/v1/[controller]/discounts
    [Route("discounts")]
    [HttpPut]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    public async Task<ActionResult> UpdateProductDiscountAsync([FromBody] DiscountItem discountToUpdate) {
        // Extract the item name, brand, and type from the discount item
        var itemName = discountToUpdate.ItemName;
        var itemBrand = discountToUpdate.ItemBrand;
        var itemType = discountToUpdate.ItemType;

        // Get the discount item from the database
        var discountItem = await _discountContext.Discount.SingleOrDefaultAsync(i => i.Id == discountToUpdate.Id);

        if (discountItem == null) {
            return NotFound($"Discount Item with Name: {itemName}, Brand: {itemBrand}, and Type: {itemType} was not found.");
        }

        var oldDiscount = discountItem.DiscountValue;
        var raiseProductDiscountChangedEvent = oldDiscount != discountToUpdate.DiscountValue;

        // Update current product
        discountItem = discountToUpdate;

        _discountContext.Discount.Update(discountItem);

        if(raiseProductDiscountChangedEvent) { // Save product's data and publish integration event through the Event Bus if price has changed

            if (_settings.PublishEventEnabled) {
                IntegrationEvent discountChangedEvent;
                
                if(_settings.ThesisWrapperEnabled) {
                    // Create Wrapped Integration Event to be published through the Event Bus
                    discountChangedEvent = _clientIDWrappedEventFactory.getClientIDWrappedProductDiscountChangedIntegrationEvent(discountItem.Id, discountItem.DiscountValue, oldDiscount);
                }
                else {
                    // Create Integration Event to be published through the Event Bus
                    discountChangedEvent = new ProductDiscountChangedIntegrationEvent(discountItem.Id, discountItem.DiscountValue, oldDiscount);
                }

                // Achieving atomicity between original Discountdatbase operation and the IntegrationEventLog thanks to a local transaction
                await _discountIntegrationEventService.SaveEventAndDiscountContextChangesAsync(discountChangedEvent);
                
                // Publish through the Event Bus and mark the saved event as published
                await _discountIntegrationEventService.PublishThroughEventBusAsync(discountChangedEvent);
            }
            else {
                await _discountContext.SaveChangesAsync();
            }
        } 
        else { // Just save the updated product because the Product's Price hasn't changed.
            _logger.LogInformation($"The item: {itemName} {itemBrand} {itemType} has the same discount value: {discountToUpdate.DiscountValue} as the previous discount value: {oldDiscount}");
            await _discountContext.SaveChangesAsync();
        }
        
        // Return the updated discount item
        return CreatedAtAction(nameof(DiscountsAsync), new { discountToUpdate.Id }, null);
    }

    [HttpGet]
    [Route("commit")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public Task Commit() {
        // Flush any data in the wrapper to the Database
        return Task.CompletedTask;
    }

    [HttpGet]
    [Route("proposeTS")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public Task<long> proposeTS() {
        // Return the current timestamp
        var ticks = DateTime.UtcNow.Ticks;
        return Task.FromResult(ticks);
    }
}
