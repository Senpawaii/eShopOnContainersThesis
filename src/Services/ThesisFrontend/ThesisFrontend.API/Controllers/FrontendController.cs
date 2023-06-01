
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
using Newtonsoft.Json.Linq;
using System.Linq.Expressions;
using System.Net.Http;

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
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
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

    [HttpPut]
    [Route("updatepricediscount")]
    [ProducesResponseType((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
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

        // Update the catalog item price
        try {
            await _catalogService.UpdateCatalogPriceAsync(catalogItem);
        } 
        catch (HttpRequestException) {
            _logger.LogError($"An error occurered while updating the Catalog Price async");
            return BadRequest();
        }

        // Update the discount item value
        try {
            await _discountService.UpdateDiscountValueAsync(discountItem);
        } 
        catch (HttpRequestException) {
            _logger.LogError($"An error occurered while updating the Discount Value async");
            return BadRequest();
        }
        return Ok();
    }
}

public class UpdatePriceDiscountRequest {
    public CatalogItem CatalogItem { get; set; }
    public DiscountItem DiscountItem { get; set; }
}
