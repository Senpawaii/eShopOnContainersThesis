namespace Microsoft.eShopOnContainers.WebMVC.ViewModels;

public record CatalogItemWithDiscount {
    public CatalogItem CatalogItem { get; init; }
    public Discount Discount { get; init; }
}
