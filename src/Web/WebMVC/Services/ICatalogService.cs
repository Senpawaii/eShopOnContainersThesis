using WebMVC.ViewModels;

namespace Microsoft.eShopOnContainers.WebMVC.Services;

public interface ICatalogService
{
    Task<(Catalog, TCCMetadata)> GetCatalogItems(int page, int take, int? brand, int? type, TCCMetadata metadata);
    Task<(IEnumerable<SelectListItem>, TCCMetadata)> GetBrands(TCCMetadata metadata);
    Task<(IEnumerable<SelectListItem>, TCCMetadata)> GetTypes(TCCMetadata metadata);
}
