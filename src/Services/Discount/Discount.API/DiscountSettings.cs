namespace Microsoft.eShopOnContainers.Services.Discount.API;

public class DiscountSettings {

    public string EventBusConnection { get; set; }

    public bool UseCustomizationData { get; set; }

    public bool AzureStorageEnabled { get; set; }

    public bool ThesisWrapperEnabled { get; set; }

    public string CatalogUrl { get; set; }

    public string CoordinatorUrl { get; set; }
}
