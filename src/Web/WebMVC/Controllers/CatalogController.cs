using WebMVC.ViewModels;

namespace Microsoft.eShopOnContainers.WebMVC.Controllers;

public class CatalogController : Controller
{
    private ICatalogService _catalogSvc;

    public CatalogController(ICatalogService catalogSvc) =>
        _catalogSvc = catalogSvc;

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
         * **/
        var metadata = new TCCMetadata {
            Interval = Tuple.Create(0,0),
            FunctionalityID = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.Now
        };

        var itemsPage = 9;

        Console.WriteLine($"Initialized functionality: <{metadata.FunctionalityID}>");
        
        // Deconstruct declaration
        (Catalog catalog, metadata) = await _catalogSvc.GetCatalogItems(page ?? 0, itemsPage, BrandFilterApplied, TypesFilterApplied, metadata);
        
        // Sleep for a long period (This is only necessary as an extreme case to demonstrate the consistency anomaly)
        Thread.Sleep(10000);

        // Added this line for testing, check if the number of items returned is still the same.
        (catalog, metadata) = await _catalogSvc.GetCatalogItems(page ?? 0, itemsPage, BrandFilterApplied, TypesFilterApplied, metadata);


        var vm = new IndexViewModel()
        {
            CatalogItems = catalog.Data,
            Brands = await _catalogSvc.GetBrands(),
            Types = await _catalogSvc.GetTypes(),
            BrandFilterApplied = BrandFilterApplied ?? 0,
            TypesFilterApplied = TypesFilterApplied ?? 0,
            PaginationInfo = new PaginationInfo()
            {
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