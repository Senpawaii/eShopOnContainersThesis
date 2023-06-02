using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Newtonsoft.Json.Linq;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public interface IDiscountService {
    public Task<IEnumerable<DiscountItem>> GetDiscountItemsAsync(List<string> itemNames, List<string> itemBrands, List<string> itemTypes);
    public Task<HttpStatusCode> UpdateDiscountValueAsync(DiscountItem discountItem);
}
