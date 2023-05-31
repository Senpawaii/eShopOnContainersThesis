
using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Controllers;

[Route("api/v1/[Controller]")]
[ApiController]
public class FrontendController : ControllerBase {
    private readonly ThesisFrontendSettings _settings;
    private readonly ILogger<FrontendController> _logger;
    //private readonly ICatalogService _catalogService;
    private readonly IBasketService _basketService;
    //private readonly IDiscountService _discountService;

    public FrontendController(IOptionsSnapshot<ThesisFrontendSettings> settings, ILogger<FrontendController> logger, IBasketService basketService) {
        _logger = logger;
        _settings = settings.Value;
        _basketService = basketService;
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
}
