
namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Controllers;

[Route("api/v1/[Controller]")]
[ApiController]
public class FrontendController : ControllerBase {
    private readonly ThesisFrontendSettings _settings;
    private readonly ILogger<FrontendController> _logger;

    public FrontendController(IOptionsSnapshot<ThesisFrontendSettings> settings, ILogger<FrontendController> logger) {
        _logger = logger;
        _settings = settings.Value;
    }

    //[HttpGet]
    //[Route("readBasket")]
    //[ProducesResponseType(typeof(), (int)HttpStatusCode.OK)]
    //[ProducesResponseType((int)HttpStatusCode.BadRequest)]
    //public async Task<IActionResult> ReadBasketAsync([FromQuery] string basketId) {
    //    // Send a ReadBasket request to the Basket.API
    //    //var readBasketRequest = new HttpRequestMessage(HttpMethod.Get, $"{_settings.CoordinatorOperations.ReadBasket}?basketId={basketId}");
    //}
}
