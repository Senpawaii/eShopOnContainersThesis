using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Newtonsoft.Json.Linq;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public interface ICatalogService {
    public Task<HttpStatusCode> UpdateCatalogPriceAsync(CatalogItem catalogItem);
}
