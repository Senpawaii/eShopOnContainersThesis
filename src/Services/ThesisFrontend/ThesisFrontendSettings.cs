namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API;
public class ThesisFrontendSettings {
    public bool UseCustomizationData { get; set; }

    public bool AzureStorageEnabled { get; set; }

    public bool ThesisWrapperEnabled { get; set; }

    public string CatalogUrl { get; set; }

    public string DiscountUrl { get; set; }

    public string BasketUrl { get; set; }
}
