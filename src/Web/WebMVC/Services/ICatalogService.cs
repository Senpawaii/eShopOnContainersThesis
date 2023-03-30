namespace Microsoft.eShopOnContainers.WebMVC.Services;

public interface ICatalogService
{
    Task<(Catalog, (int, int))> GetCatalogItems(int page, int take, int? brand, int? type, (int, int) interval);
    Task<IEnumerable<SelectListItem>> GetBrands();
    Task<IEnumerable<SelectListItem>> GetTypes();
}
