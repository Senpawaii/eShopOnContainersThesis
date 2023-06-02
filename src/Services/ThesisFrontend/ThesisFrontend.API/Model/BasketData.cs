namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;

public class BasketData {
    public string buyerId { get; set; }

    public List<BasketDataItem> items { get; set; } = new();

    public BasketData()
    {
    }

    public BasketData(string buyerId_c)
    {
        buyerId = buyerId_c;
    }
}

