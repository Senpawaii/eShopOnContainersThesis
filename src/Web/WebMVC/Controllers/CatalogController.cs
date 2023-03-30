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
        var interval = (0, 0);
        var itemsPage = 9;

        if (page != null) {
            Console.WriteLine("Index, Page number: " + page.ToString());
        }
        
        // Deconstruct declaration
        (Catalog catalog, (int, int) new_interval) = await _catalogSvc.GetCatalogItems(page ?? 0, itemsPage, BrandFilterApplied, TypesFilterApplied, interval);
        
        // Assign the new interval to the used interval
        interval.Item1 = new_interval.Item1;
        interval.Item2 = new_interval.Item2;

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