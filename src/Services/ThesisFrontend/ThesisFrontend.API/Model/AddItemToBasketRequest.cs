namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;

public class AddItemToBasketRequest {
    public int CatalogItemId { get; set; }

    public string BasketId { get; set; }

    public int Quantity { get; set; }

    public string CatalogItemName { get; set; }
    public string CatalogItemBrandName { get; set; }
    public string CatalogItemTypeName { get; set; }
    public decimal Discount { get; set; }

    public AddItemToBasketRequest()
    {
        Quantity = 1;
    }
}
