using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Newtonsoft.Json.Linq;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public interface ICatalogService {
    public Task<CatalogItem> GetCatalogItemByIdAsync(int catalogItemId);
    public Task<IEnumerable<CatalogBrand>> GetCatalogBrandsAsync();
    public Task<HttpStatusCode> UpdateCatalogPriceAsync(CatalogItem catalogItem);
}
