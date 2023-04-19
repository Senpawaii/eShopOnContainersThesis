namespace Microsoft.eShopOnContainers.WebMVC.ViewModels;

public record Basket
{
    // Use property initializer syntax.
    // While this is often more useful for read only 
    // auto implemented properties, it can simplify logic
    // for read/write properties.
    public List<BasketItem> Items { get; init; } = new List<BasketItem>();

    public List<Discount> Discounts { get; init; } = new List<Discount>(); // Todo: To show the discounts this should be filled somewhere. The WebMVC should request the discounts to the Discount service
    public string BuyerId { get; init; }

    public decimal Total()
    {
        return Math.Round(Items.Sum(x => x.UnitPrice * x.Quantity), 2);
    }
}
