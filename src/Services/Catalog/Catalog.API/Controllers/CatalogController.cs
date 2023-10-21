using Catalog.API.IntegrationEvents.Events;
using Catalog.API.IntegrationEvents.Events.Factories;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
//using NewRelic.Api.Agent;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class CatalogController : ControllerBase {
    private readonly CatalogContext _catalogContext;
    private readonly CatalogSettings _settings;
    private readonly ICatalogIntegrationEventService _catalogIntegrationEventService;
    private readonly IFactoryClientIDWrappedProductPriceChangedIntegrationEvent _clientIDWrappedEventFactory;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(CatalogContext context, IOptionsSnapshot<CatalogSettings> settings, ICatalogIntegrationEventService catalogIntegrationEventService, ILogger<CatalogController> logger, 
        IFactoryClientIDWrappedProductPriceChangedIntegrationEvent clientIDWrappedEventFactory) {
        _catalogContext = context ?? throw new ArgumentNullException(nameof(context));
        _catalogContext.Database.SetCommandTimeout(180);
        _catalogIntegrationEventService = catalogIntegrationEventService ?? throw new ArgumentNullException(nameof(catalogIntegrationEventService));
        _clientIDWrappedEventFactory = clientIDWrappedEventFactory ?? throw new ArgumentNullException(nameof(clientIDWrappedEventFactory));
        _settings = settings.Value;
        _logger = logger;

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    // GET api/v1/[controller]/items[?pageSize=3&pageIndex=10]
    [HttpGet]
    [Route("items")]
    [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), (int)HttpStatusCode.OK)]
    [ProducesResponseType(typeof(IEnumerable<CatalogItem>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    //[Trace]
    public async Task<IActionResult> ItemsAsync([FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0, string ids = null) {
        // _logger.LogInformation($"Checkpoint 2: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
        
        if (!string.IsNullOrEmpty(ids)) {
            var items = await GetItemsByIdsAsync(ids);

            if (!items.Any()) {
                return BadRequest("ids value invalid. Must be comma-separated list of numbers");
            }

            return Ok(items);
        }

        // 2 DB accesses are made
        var totalItems = await _catalogContext.CatalogItems
            .LongCountAsync();

        // _logger.LogInformation($"Checkpoint 2.1: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

        var itemsOnPage = await _catalogContext.CatalogItems
            .OrderBy(c => c.Name)
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        itemsOnPage = ChangeUriPlaceholder(itemsOnPage);

        var model = new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);

        // _logger.LogInformation($"Checkpoint 3: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

        return Ok(model);
    }

    private async Task<List<CatalogItem>> GetItemsByIdsAsync(string ids) {
        var numIds = ids.Split(',').Select(id => (Ok: int.TryParse(id, out int x), Value: x));

        if (!numIds.All(nid => nid.Ok)) {
            return new List<CatalogItem>();
        }

        var idsToSelect = numIds
            .Select(id => id.Value);

        var items = await _catalogContext.CatalogItems.Where(ci => idsToSelect.Contains(ci.Id)).ToListAsync();

        items = ChangeUriPlaceholder(items);

        return items;
    }

    [HttpGet]
    [Route("items/{id:int}")]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType(typeof(CatalogItem), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<CatalogItem>> ItemByIdAsync(int id) {
        if (id <= 0) {
            return BadRequest();
        }

        var item = await _catalogContext.CatalogItems.SingleOrDefaultAsync(ci => ci.Id == id);

        var baseUri = _settings.PicBaseUrl;
        var azureStorageEnabled = _settings.AzureStorageEnabled;

        item.FillProductUrl(baseUri, azureStorageEnabled: azureStorageEnabled);

        if (item != null) {
            return item;
        }

        if(_settings.Limit1Version) {
            return new CatalogItem();
        }

        return NotFound();
    }

    // GET api/v1/[controller]/items/withname/samplename[?pageSize=3&pageIndex=10]
    [HttpGet]
    [Route("items/withname/{name:minlength(1)}")]
    [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<PaginatedItemsViewModel<CatalogItem>>> ItemsWithNameAsync(string name, [FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0) {
        var totalItems = await _catalogContext.CatalogItems
            .Where(c => c.Name.StartsWith(name))
            .LongCountAsync();

        var itemsOnPage = await _catalogContext.CatalogItems
            .Where(c => c.Name.StartsWith(name))
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        itemsOnPage = ChangeUriPlaceholder(itemsOnPage);

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
    }

    // GET api/v1/[controller]/items/type/1/brand[?pageSize=3&pageIndex=10]
    [HttpGet]
    [Route("items/type/{catalogTypeId}/brand/{catalogBrandId:int?}")]
    [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), (int)HttpStatusCode.OK)]
   //[Trace]
    public async Task<ActionResult<PaginatedItemsViewModel<CatalogItem>>> ItemsByTypeIdAndBrandIdAsync(int catalogTypeId, int? catalogBrandId, [FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0) {
        var root = (IQueryable<CatalogItem>)_catalogContext.CatalogItems;

        root = root.Where(ci => ci.CatalogTypeId == catalogTypeId);

        if (catalogBrandId.HasValue) {
            root = root.Where(ci => ci.CatalogBrandId == catalogBrandId);
        }

        var totalItems = await root
            .LongCountAsync();

        var itemsOnPage = await root
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        itemsOnPage = ChangeUriPlaceholder(itemsOnPage);

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
    }

    // GET api/v1/[controller]/items/type/all/brand[?pageSize=3&pageIndex=10]
    [HttpGet]
    [Route("items/type/all/brand/{catalogBrandId:int?}")]
    [ProducesResponseType(typeof(PaginatedItemsViewModel<CatalogItem>), (int)HttpStatusCode.OK)]
   //[Trace]
    public async Task<ActionResult<PaginatedItemsViewModel<CatalogItem>>> ItemsByBrandIdAsync(int? catalogBrandId, [FromQuery] int pageSize = 10, [FromQuery] int pageIndex = 0) {
        var root = (IQueryable<CatalogItem>)_catalogContext.CatalogItems;

        if (catalogBrandId.HasValue) {
            root = root.Where(ci => ci.CatalogBrandId == catalogBrandId);
        }

        var totalItems = await root
            .LongCountAsync();

        var itemsOnPage = await root
            .Skip(pageSize * pageIndex)
            .Take(pageSize)
            .ToListAsync();

        itemsOnPage = ChangeUriPlaceholder(itemsOnPage);

        return new PaginatedItemsViewModel<CatalogItem>(pageIndex, pageSize, totalItems, itemsOnPage);
    }

    // GET api/v1/[controller]/CatalogTypes
    [HttpGet]
    [Route("catalogtypes")]
    [ProducesResponseType(typeof(List<CatalogType>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<List<CatalogType>>> CatalogTypesAsync() {
        return await _catalogContext.CatalogTypes.ToListAsync();
    }

    // GET api/v1/[controller]/CatalogBrands
    [HttpGet]
    [Route("catalogbrands")]
    [ProducesResponseType(typeof(List<CatalogBrand>), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<List<CatalogBrand>>> CatalogBrandsAsync() {
        return await _catalogContext.CatalogBrands.ToListAsync();
    }

    //PUT api/v1/[controller]/items
    [Route("items")]
    [HttpPut]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Created)]
   //[Trace]
    public async Task<ActionResult> UpdateProductPriceAsync([FromBody] CatalogItem productToUpdate) {
        // Log all updated catalog item paramaters
        // _logger.LogInformation($"Body Catalog Item: {productToUpdate.Id} with the following parameters: {productToUpdate.Name}, {productToUpdate.Description}, {productToUpdate.Price}, {productToUpdate.PictureFileName}, {productToUpdate.PictureUri}, {productToUpdate.CatalogBrandId}, {productToUpdate.CatalogTypeId}");

        //var items = (IQueryable<CatalogItem>)_catalogContext.CatalogItems;

        //items = items.Where(ci => ci.Name == name && ci.CatalogBrandId == brandId && ci.CatalogTypeId == typeId);

        //try {
        //    if (items.Count() == 0) {
        //        return NotFound(new { Message = $"Item with name: {name}, brandId: {brandId} and typeId: {typeId} not found." });
        //    }
        //} catch (Exception ex) {
        //    Console.WriteLine(ex.Message);
        //    return NotFound(new { Message = $"Item with name: {name}, brandId: {brandId} and typeId: {typeId} not found." });
        //}

        //var item = items.First();
        //var oldPrice = item.Price;
        //var raiseProductPriceChangedEvent = oldPrice != price;

        var catalogItem = await _catalogContext.CatalogItems.SingleOrDefaultAsync(i => i.Id == productToUpdate.Id);

        if (catalogItem == null) {
            return NotFound(new { Message = $"Item with id {productToUpdate.Id} not found." });
        }

        var oldPrice = catalogItem.Price;
        var raiseProductPriceChangedEvent = oldPrice != productToUpdate.Price;

        // Update current product
        catalogItem = productToUpdate;

        _catalogContext.CatalogItems.Update(catalogItem);

        if (raiseProductPriceChangedEvent) // Save product's data and publish integration event through the Event Bus if price has changed
        {
            if(_settings.PublishEventEnabled) {
                IntegrationEvent priceChangedEvent;

                if (_settings.ThesisWrapperEnabled) {
                    //Create Wrapped Integration Event to be published through the Event Bus
                    priceChangedEvent = _clientIDWrappedEventFactory.getClientIDWrappedProductPriceChangedIntegrationEvent(catalogItem.Id, catalogItem.Price, oldPrice);
                }
                else {
                    //Create Integration Event to be published through the Event Bus
                    priceChangedEvent = new ProductPriceChangedIntegrationEvent(catalogItem.Id, catalogItem.Price, oldPrice);
                }

                // Achieving atomicity between original Catalog database operation and the IntegrationEventLog thanks to a local transaction
                await _catalogIntegrationEventService.SaveEventAndCatalogContextChangesAsync(priceChangedEvent);

                // Publish through the Event Bus and mark the saved event as published
                await _catalogIntegrationEventService.PublishThroughEventBusAsync(priceChangedEvent);
            }
            else {
                await _catalogContext.SaveChangesAsync();
            }
        }
        else // Just save the updated product because the Product's Price hasn't changed.
        {
            _logger.LogInformation($"The catalog item: {productToUpdate.Name} THE NEW PRICE IS THE SAME AS THE OLD PRICE: {productToUpdate.Price} = {oldPrice}");
            await _catalogContext.SaveChangesAsync();
        }
        //_logger.LogInformation($"The catalog item: {productToUpdate.Name} THE NEW PRICE IS: {productToUpdate.Price} AND THE OLD PRICE WAS: {oldPrice}");
        return CreatedAtAction(nameof(ItemByIdAsync), new { id = productToUpdate.Id }, null);
    }

    //POST api/v1/[controller]/items
    [Route("items")]
    [HttpPost]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    public async Task<ActionResult> CreateProductAsync([FromBody] CatalogItem product, [FromQuery] string brand = "", [FromQuery] string type = "") {
        int brandId = 0;
        int typeId = 0;
        var types = (IQueryable<CatalogType>)_catalogContext.CatalogTypes;
        var brands = (IQueryable<CatalogBrand>)_catalogContext.CatalogBrands;

        if (String.IsNullOrEmpty(type)) {
            throw new Exception("catalog Type Name is empty");
        }

        if (String.IsNullOrEmpty(brand)) {
            throw new Exception("catalog Brand Name is empty");
        }

        // Check if Type and/or Brand need to be created as passed in the request
        type = type.Trim('"').Trim();
        brand = brand.Trim('"').Trim();

        types = types.Where(tp => tp.Type == type);
        if (await types.CountAsync() == 0) {
            // Create new Type
            var newType = new CatalogType {
                Type = type,
            };
            _catalogContext.CatalogTypes.Add(newType);
            await _catalogContext.SaveChangesAsync();

            typeId = newType.Id;
        } 
        else {
            typeId = types.Select(a => a.Id).First();
        }

        brands = brands.Where(bn => bn.Brand == brand);
        if (await brands.CountAsync() == 0) {
            // Create new Brand
            var newBrand = new CatalogBrand {
                Brand = brand,
            };

            _catalogContext.CatalogBrands.Add(newBrand);
            await _catalogContext.SaveChangesAsync();

            brandId = newBrand.Id;
        }
        else {
            brandId = brands.Select(a => a.Id).First();
        }

        var item = new CatalogItem {
            // Create new Item
            CatalogBrandId = brandId,
            CatalogTypeId = typeId,
            Description = product.Description,
            Name = product.Name,
            PictureFileName = product.PictureFileName,
            Price = product.Price
        };

        _catalogContext.CatalogItems.Add(item);

        await _catalogContext.SaveChangesAsync();

        return CreatedAtAction(nameof(ItemByIdAsync), new { id = item.Id }, null);
    }

    //DELETE api/v1/[controller]/id
    [Route("{id}")]
    [HttpDelete]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult> DeleteProductAsync(int id) {
        var product = _catalogContext.CatalogItems.SingleOrDefault(x => x.Id == id);

        if (product == null) {
            return NotFound();
        }

        _catalogContext.CatalogItems.Remove(product);

        await _catalogContext.SaveChangesAsync();

        return NoContent();
    }

    // GET api/v1/[controller]/items/name/brand/type
    [HttpGet]
    [Route("items/name/{name}/brand/{catalogBrand}/type/{catalogType}")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<int>> ItemIdByNameAndTypeIdAndBrandIdAsync(string name, string catalogBrand, string catalogType) {
        // _logger.LogInformation("Executing ItemIdByNameAndTypeIdAndBrandId request...");
        var items = (IQueryable<CatalogItem>)_catalogContext.CatalogItems;
        var brands = (IQueryable<CatalogBrand>)_catalogContext.CatalogBrands;
        var types = (IQueryable<CatalogType>)_catalogContext.CatalogTypes;

        brands = brands.Where(bn => bn.Brand == catalogBrand);
        if (await brands.CountAsync() == 0) {
            _logger.LogInformation($"The catalog brand: {catalogBrand} was not found");
            return NotFound();
        }
        var brandId = brands.Select(a => a.Id).First();

        types = types.Where(tn => tn.Type == catalogType);
        if (await types.CountAsync() == 0) {
            _logger.LogInformation($"The catalog type: {catalogType} was not found");
            return NotFound();
        }
        var typeId = types.Select(a => a.Id).First();

        items = items.Where(ci => ci.Name == name && ci.CatalogBrandId == brandId && ci.CatalogTypeId == typeId);

        var totalItems = await items
            .CountAsync();

        if (totalItems == 0) { 
            _logger.LogInformation($"The catalog item: {name} was not found");
            return NotFound(); 
        }
        var itemId = items.Select(a => a.Id).First();
        _logger.LogInformation("Catalog Item Id queried: {0}", itemId.ToString());
        // _logger.LogInformation("Finished ItemIdByNameAndTypeIdAndBrandId request!");

        return itemId;
    }

    // GET api/v1/[controller]/items/name/brand/type
    [HttpGet]
    [Route("items/name/{name}/brand/{catalogBrand}/type/{catalogType}/price")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<ActionResult<decimal>> ItemPriceGivenNameBrandType(string name, string catalogBrand, string catalogType) {
        //_logger.LogInformation("Executing ItemPriceGivenNameBrandType request...");
        var items = (IQueryable<CatalogItem>)_catalogContext.CatalogItems;
        var brands = (IQueryable<CatalogBrand>)_catalogContext.CatalogBrands;
        var types = (IQueryable<CatalogType>)_catalogContext.CatalogTypes;

        brands = brands.Where(bn => bn.Brand == catalogBrand);
        if (await brands.CountAsync() == 0) {
            _logger.LogInformation($"The catalog brand: {catalogBrand} was not found");
            return NotFound();
        }
        var brandId = brands.Select(a => a.Id).First();

        types = types.Where(tn => tn.Type == catalogType);
        if (await types.CountAsync() == 0) {
            _logger.LogInformation($"The catalog type: {catalogType} was not found");
            return NotFound();
        }
        var typeId = types.Select(a => a.Id).First();

        items = items.Where(ci => ci.Name == name && ci.CatalogBrandId == brandId && ci.CatalogTypeId == typeId);

        var totalItems = await items
            .CountAsync();

        if (totalItems == 0) { 
            _logger.LogInformation($"The catalog item: {name} was not found, generating a new price...");
            var generatedPrice = 10;
            return generatedPrice; 
        }
        var itemPrice = await items.Select(a => a.Price).FirstAsync();
        // _logger.LogInformation("Catalog Item Price queried: {0}", itemPrice.ToString());
        //_logger.LogInformation("Finished ItemPriceGivenNameBrandType request!");

        return itemPrice;
    }


    private List<CatalogItem> ChangeUriPlaceholder(List<CatalogItem> items) {
        var baseUri = _settings.PicBaseUrl;
        var azureStorageEnabled = _settings.AzureStorageEnabled;

        foreach (var item in items) {
            item.FillProductUrl(baseUri, azureStorageEnabled: azureStorageEnabled);
        }

        return items;
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

    [HttpGet]
    [Route("ping")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public Task Ping() {
        // Return the current timestamp
        return Task.CompletedTask;
    }
}
