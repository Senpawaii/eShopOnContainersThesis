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
        var discounts = await _discountContext.Discount.Where(i => itemNames.Contains(i.ItemName) && itemBrands.Contains(i.ItemBrand) && itemTypes.Contains(i.ItemType)).ToListAsync();
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

        _discountContext.Discount.Add(discountItem);
        await _discountContext.SaveChangesAsync();

        return discountItem.Id;
    }

    //PUT api/v1/[controller]/discounts
    [Route("discounts")]
    [HttpPut]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [ProducesResponseType((int)HttpStatusCode.Created)]
    public async Task<ActionResult> UpdateProductPriceAsync([FromBody] DiscountItem discountToUpdate) {
        // Extract the item name, brand, and type from the discount item
        var itemName = discountToUpdate.ItemName;
        var itemBrand = discountToUpdate.ItemBrand;
        var itemType = discountToUpdate.ItemType;

        // Get the discount item from the database
        var discountItem = await _discountContext.Discount.SingleOrDefaultAsync(i => i.ItemName == itemName && i.ItemBrand == itemBrand && i.ItemType == itemType);

        if (discountItem == null) {
            return NotFound($"Discount Item with Name: {itemName}, Brand: {itemBrand}, and Type: {itemType} was not found.");
        }

        var oldDiscount = discountItem.DiscountValue;

        // Update current product
        discountItem = discountToUpdate;
        _discountContext.Discount.Update(discountItem);
        await _discountContext.SaveChangesAsync();

        // Return the updated discount item
        return CreatedAtAction(nameof(DiscountsAsync), new { itemName = discountItem.ItemName, itemBrand = discountItem.ItemBrand, itemType = discountItem.ItemType }, null);
    }
}
