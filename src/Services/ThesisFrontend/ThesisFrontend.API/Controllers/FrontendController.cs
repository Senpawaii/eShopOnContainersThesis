
using Grpc.Core;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;
using System.Net.Http;
//using NewRelic.Api.Agent;


namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Controllers;

[Route("api/v1/[Controller]")]
[ApiController]
public class FrontendController : ControllerBase {
    private readonly ThesisFrontendSettings _settings;
    private readonly ILogger<FrontendController> _logger;
    private readonly ICatalogService _catalogService;
    private readonly IBasketService _basketService;
    private readonly IDiscountService _discountService;

    public FrontendController(IOptionsSnapshot<ThesisFrontendSettings> settings, ILogger<FrontendController> logger, IBasketService basketService, ICatalogService catalogService, IDiscountService discountService) {
        _logger = logger;
        _settings = settings.Value;
        _basketService = basketService;
        _catalogService = catalogService;
        _discountService = discountService;
    }

    [HttpGet]
    [Route("readbasket")]
    [ProducesResponseType(typeof(BasketData), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
   //[Trace]
    public async Task<IActionResult> ReadBasketAsync([FromQuery] string basketId) {
        // Check if the basket id is valid
        if (string.IsNullOrEmpty(basketId)) {
            return BadRequest();
        }

        var basket = await _basketService.GetBasketAsync(basketId);
        if (basket == null) {
            return BadRequest();
        }
        else {
            return Ok(basket);
        }
    }

    [HttpGet]
    [Route("readcatalogitem/{id:int}")]
    [ProducesResponseType(typeof(CatalogItem), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
   //[Trace]
    public async Task<ActionResult<CatalogItem>> ReadCatalogItemAsync(int id) {
        // Check if the id is valid
        if (id <= 0) {
            return BadRequest();
        }

        var catalogItem = await _catalogService.GetCatalogItemByIdAsync(id);
        if (catalogItem == null) {
            return BadRequest();
        }
        else {
            return catalogItem;
        }
    }

    [HttpGet]
    [Route("catalogbrands")]
    [ProducesResponseType(typeof(IEnumerable<CatalogBrand>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
   //[Trace]
    public async Task<ActionResult<IEnumerable<CatalogBrand>>> ReadCatalogBrandsAsync() {
        var catalogBrands = await _catalogService.GetCatalogBrandsAsync();
        if (catalogBrands == null) {
            return BadRequest();
        }
        else {
            return Ok(catalogBrands);
        }
    }

    [HttpGet]
    [Route("catalogtypes")]
    [ProducesResponseType(typeof(IEnumerable<CatalogType>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
   //[Trace]
    public async Task<ActionResult<IEnumerable<CatalogType>>> ReadCatalogTypesAsync() {
        var catalogTypes = await _catalogService.GetCatalogTypesAsync();
        if (catalogTypes == null) {
            return BadRequest();
        }
        else {
            return Ok(catalogTypes);
        }
    }

    [HttpGet]
    [Route("readdiscounts")]
    [ProducesResponseType(typeof(IEnumerable<DiscountItem>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
   //[Trace]
    public async Task<ActionResult<IEnumerable<DiscountItem>>> ReadDiscountItems([FromQuery] List<string> itemNames, [FromQuery] List<string> itemBrands, [FromQuery] List<string> itemTypes) {
        // Check if the itemNames, itemBrands, and itemTypes are valid
        if (itemNames == null || itemBrands == null || itemTypes == null) {
            return BadRequest();
        }

        // Log the request
        // _logger.LogInformation($"Received request to read discount items with item names: {string.Join(",", itemNames)}, item brands: {string.Join(",", itemBrands)}, and item types: {string.Join(",", itemTypes)}");

        // Get the list of discount items from the database that match the triple of item name, brand, and type
        var discounts = await _discountService.GetDiscountItemsAsync(itemNames, itemBrands, itemTypes);
        return Ok(discounts);
    }

    [HttpPost]
    [Route("additemtobasket")]
    [ProducesResponseType(typeof(BasketData), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
   //[Trace]
    public async Task<IActionResult> AddItemToBasketAsync([FromBody] AddItemToBasketRequest request) {
        // Check if the request is valid
        if (request == null) {
            return BadRequest();
        }

        // Extract the basketId and catalogItemId from the request
        var basketId = request.BasketId;
        var catalogItemId = request.CatalogItemId;
        
        // Check if the basketId and catalogItemId are present
        if (string.IsNullOrEmpty(basketId) || catalogItemId <= 0) {
            return BadRequest();
        }

        // Get the item from the Catalog
        var catalogItem = await _catalogService.GetCatalogItemByIdAsync(catalogItemId);

        //// Get the discount item from the database that matches the item name, brand, and type
        var discountItems = await _discountService.GetDiscountItemsAsync(new List<string> { request.CatalogItemName }, new List<string> { request.CatalogItemBrandName }, new List<string> { request.CatalogItemTypeName });
        var discountItem = discountItems.SingleOrDefault();

        // Get the current basket
        var basket = await _basketService.GetBasketAsync(basketId);

        // Check if the product is already in the basket
        var basketItem = basket.items.SingleOrDefault(i => i.productName == catalogItem.Name && i.productBrand == request.CatalogItemBrandName && i.productType == request.CatalogItemTypeName);

        // If the product is already in the basket, increment the quantity
        if (basketItem != null) {
            basketItem.quantity += request.Quantity;
        }
        else {
            // If the product is not in the basket, add it to the basket
            basket.items.Add(new BasketDataItem() {
                unitPrice = catalogItem.Price,
                pictureUrl = catalogItem.PictureUri,
                productId = catalogItem.Id,
                productName = catalogItem.Name,
                quantity = request.Quantity,
                productBrand = request.CatalogItemBrandName,
                productType = request.CatalogItemTypeName,
                discount = discountItem.DiscountValue,
                id = Guid.NewGuid().ToString()
            });
        }

        // Update the basket
        try {
            var updatedBasket = await _basketService.UpdateBasketAsync(basket); // TODO
            // Check if the status code is OK
            if (updatedBasket == null) {
                return BadRequest();
            }
            return Ok(updatedBasket);
        } 
        catch (HttpRequestException) {
            _logger.LogError($"An error occurered while updating the basket async");
            return BadRequest();
        }
    }

    [HttpPut]
    [Route("updatepricediscount")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
   //[Trace]
    public async Task<IActionResult> UpdatePriceDiscountAsync([FromBody] UpdatePriceDiscountRequest request) {
        // Check if the CatalogItem and DiscountItem are valid
        if (request == null) {
            return BadRequest();
        }

        // Extract the catalogItem and discountItem from the request
        var catalogItem = request.CatalogItem;
        var discountItem = request.DiscountItem;

        // Check if the catalogItem and discountItem are present
        if (catalogItem == null || discountItem == null) {
            return BadRequest();
        }

        //Update the catalog item price
        //try {
        //    _logger.LogInformation($"Updating catalog item price to {catalogItem.Price}");
        //    var catalog_StatusCode = await _catalogService.UpdateCatalogPriceAsync(catalogItem);
        //    _logger.LogInformation($"Catalog item price updated to {catalogItem.Price}");
        //    // Check if the status code is CREATED
        //    if (catalog_StatusCode != HttpStatusCode.Created) {
        //        return BadRequest();
        //    }
        //} catch (HttpRequestException) {
        //    _logger.LogError($"An error occurered while updating the Catalog Price async");
        //    return BadRequest();
        //}

        try {
            _logger.LogInformation($"Updating discount value to {discountItem.DiscountValue}");
            var discount_StatusCode = await _discountService.UpdateDiscountValueAsync(discountItem);
            _logger.LogInformation($"Discount value updated to {discountItem.DiscountValue}");
            // Check if the status code is OK
            if (discount_StatusCode != HttpStatusCode.Created) {
                return BadRequest();
            }
        } catch (HttpRequestException) {
            _logger.LogError($"An error occurered while updating the Discount Value async");
            return BadRequest();
        }
        return Ok();
    }

    [HttpGet]
    [Route("commit")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
   //[Trace]
    public Task CommitTransaction() {
        return Task.CompletedTask;
    }
}

public class UpdatePriceDiscountRequest {
    public CatalogItem CatalogItem { get; set; }
    public DiscountItem DiscountItem { get; set; }
}
