namespace Microsoft.eShopOnContainers.Services.Catalog.API;

public class DiscountSettings {

    public string EventBusConnection { get; set; }

    public bool UseCustomizationData { get; set; }

    public bool AzureStorageEnabled { get; set; }
    public bool ThesisWrapperEnabled { get; set; }
}
