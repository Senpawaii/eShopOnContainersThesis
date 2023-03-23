namespace Microsoft.eShopOnContainers.WebMVC.Controllers;

public class CatalogController : Controller
{
    private ICatalogService _catalogSvc;

    public CatalogController(ICatalogService catalogSvc) =>
        _catalogSvc = catalogSvc;

    public async Task<IActionResult> Index(int? BrandFilterApplied, int? TypesFilterApplied, int? page, [FromQuery] string errorMsg)
    {
        var itemsPage = 9;

        // 3 async calls are executed to the Catalog.API microservice, without guaranteeing that they always use the same snapshot. -- Possible read-consistency anomaly

        // Generate the wrapper metadata
        // Step 1: Generate the number of tokens to be split across the microservice calls along the functionality
        List<int> tokens = new List<int> { 100, 100, 100 } ;

        if(page != null) {
            Console.WriteLine("Index, Page number: " + page.ToString());
        }
           
        var catalog = await _catalogSvc.GetCatalogItems(page ?? 0, itemsPage, BrandFilterApplied, TypesFilterApplied, tokens[0]);
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