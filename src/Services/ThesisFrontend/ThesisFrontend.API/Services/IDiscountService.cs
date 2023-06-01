using Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;
using Newtonsoft.Json.Linq;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Services;
public interface IDiscountService {
    public Task<HttpStatusCode> UpdateDiscountValueAsync(DiscountItem discountItem);
}
