namespace Microsoft.eShopOnContainers.WebMVC.ViewModels;

public record Discount {
    public int Id { get; init; }
    public int CatalogItemId { get; init; }
    public string CatalogItemName { get; init; }
    public int DiscountValue { get; init; }

}
