using WebMVC.ViewModels;

namespace Microsoft.eShopOnContainers.WebMVC.Services;

public interface IDiscountService {
    Task<(IEnumerable<Discount>, TCCMetadata)> GetDiscountsById(List<int> itemIds, TCCMetadata metadata);
}
