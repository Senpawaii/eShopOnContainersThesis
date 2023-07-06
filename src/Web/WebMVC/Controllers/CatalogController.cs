using WebMVC.ViewModels;

namespace Microsoft.eShopOnContainers.WebMVC.Controllers;

public class CatalogController : Controller
{
    private ICatalogService _catalogSvc;
    private IDiscountService _discountSvc;
    private IOptions<AppSettings> _options;

    public CatalogController(ICatalogService catalogSvc, IOptions<AppSettings> options, IDiscountService discountSvc) {
        _catalogSvc = catalogSvc;
        _discountSvc = discountSvc;
        _options = options;
    }

    public async Task<IActionResult> Index(int? BrandFilterApplied, int? TypesFilterApplied, int? page, [FromQuery] string errorMsg)
    {
        /**
         * Issue: 3 async calls are executed to the Catalog.API microservice, without guaranteeing that they always use the same snapshot. 
         * -- Possible read-consistency anomaly
         * 
         *  Define functionality: 
         * WebMVC call to Catalog.API -> get items
         * WebMVC call to Catalog.API -> get brands
         * WebMVC call to Catalog.API -> get catalog types
         * WebMVC call to Discount.API -> get discounts for items retrieved
         * **/
        TCCMetadata metadata = null;
        // Initialize the Metadata Fields if Testing with Wrappers
        if(_options.Value.ThesisWrapperEnabled) {
            metadata = new TCCMetadata {
                ClientID = Guid.NewGuid().ToString(),
                Timestamp = DateTime.Now
            };
            Console.WriteLine($"Initialized functionality: <{metadata.ClientID}>");
        }
        
        var itemsPage = 9;

        // Deconstruct declaration
        (Catalog catalog, metadata) = await _catalogSvc.GetCatalogItems(page ?? 0, itemsPage, BrandFilterApplied, TypesFilterApplied, metadata);

        // Get list of Catalog Items Ids
        List<int> catalogIds = catalog.Data.Select(item => item.Id).ToList();

        (var discounts, metadata) = await _discountSvc.GetDiscountsById(catalogIds, metadata);

        // Sleep for a long period (This is only necessary as an extreme case to demonstrate the consistency anomaly)

        (var brands, metadata) = await _catalogSvc.GetBrands(metadata);
        (var types, metadata) = await _catalogSvc.GetTypes(metadata);
       

        var vm = new IndexViewModel() {
            CatalogItems = catalog.Data,
            Brands = brands,
            Types = types,
            Discounts = discounts,
            BrandFilterApplied = BrandFilterApplied ?? 0,
            TypesFilterApplied = TypesFilterApplied ?? 0,
            PaginationInfo = new PaginationInfo() {
                ActualPage = page ?? 0,
                ItemsPerPage = catalog.Data.Count,
                TotalItems = catalog.Count,
                TotalPages = (int)Math.Ceiling(((decimal)catalog.Count / itemsPage))
            }
        };

        vm.PaginationInfo.Next = (vm.PaginationInfo.ActualPage == vm.PaginationInfo.TotalPages - 1) ? "is-disabled" : "";
        vm.PaginationInfo.Previous = (vm.PaginationInfo.ActualPage == 0) ? "is-disabled" : "";

        ViewBag.BasketInoperativeMsg = errorMsg;

        return View(vm);
    }
}
