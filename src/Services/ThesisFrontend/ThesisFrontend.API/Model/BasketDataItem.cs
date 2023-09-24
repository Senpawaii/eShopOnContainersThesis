namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model;

public class BasketDataItem {
    public string id { get; set; }

    public int productId { get; set; }

    public string productName { get; set; }
    public string productBrand { get; set; }
    public string productType { get; set; }

    public decimal unitPrice { get; set; }

    public decimal oldUnitPrice { get; set; }

    public int quantity { get; set; }

    public string pictureUrl { get; set; }
    
    public decimal discount { get; set; }

    public decimal oldDiscount { get; set; }
}
