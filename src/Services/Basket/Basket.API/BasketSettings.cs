namespace Microsoft.eShopOnContainers.Services.Basket.API;

public class BasketSettings
{
    public string ConnectionString { get; set; }
    public string CatalogUrl { get; set; }
    public string DiscountUrl { get; set; }
    public string CoordinatorUrl { get; set; }
    public bool ThesisWrapperEnabled { get; set; }
    public bool PublishEventEnabled { get; set; }

}

