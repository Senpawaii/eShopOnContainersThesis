using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class DiscountController : ControllerBase {
    private readonly DiscountContext _discountContext;
    private readonly DiscountSettings _settings;
    private readonly ILogger<DiscountController> _logger;

    public DiscountController(DiscountContext discountContext, IOptionsSnapshot<DiscountSettings> settings, ILogger<DiscountController> logger) {
        _discountContext = discountContext ?? throw new ArgumentNullException(nameof(discountContext));
        _settings = settings.Value;
        _logger = logger;

        // If changes need to be made to the objects queried from the Database, change this from NoTracking to TrackAll or TrackModified
        discountContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
    }

    /// <summary>
    /// Given a list of Catalog Item Ids, return all discounts associated with these items.
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [HttpGet]
    [Route("discounts")]
    [ProducesResponseType(typeof(IEnumerable<DiscountItem>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    public async Task<IActionResult> DiscountsAsync([FromQuery] List<int> ids) {
        var discounts = await _discountContext.Discount.Where(item => ids.Contains(item.CatalogItemId)).ToListAsync();
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
    public async Task<ActionResult<int>> CreateDiscountAsync([FromQuery] string catalogItemId, [FromQuery] string discountValue, [FromQuery] string catalogItemName) {
        if(catalogItemId.IsNullOrEmpty() || !int.TryParse(catalogItemId, out int itemId)) {
            return BadRequest("Invalid Catalog Item Id (ID should not be empty or null and should be represented with by a number)");
        }

        if(discountValue.IsNullOrEmpty() || !int.TryParse(discountValue, out int discount)) {
            return BadRequest("Invalid Discount Value (Discount value should not be empty or null and should be represented with by a number)");
        }

        if (catalogItemName.IsNullOrEmpty()) {
            return BadRequest("Invalid Catalog Item Name (Catalog Item Name should not be empty or null)");
        }

        // Possibly add a verification so that no invalid catalogItemIds are registered

        var discountItem = new DiscountItem {
            CatalogItemId = itemId,
            CatalogItemName = catalogItemName,
            DiscountValue = discount
        };

        _discountContext.Discount.Add(discountItem);
        await _discountContext.SaveChangesAsync();

        return discountItem.Id;
    }

}
