namespace Microsoft.eShopOnContainers.Services.Catalog.API;

public class CatalogSettings
{
    public string PicBaseUrl { get; set; }

    public string EventBusConnection { get; set; }

    public bool UseCustomizationData { get; set; }

    public bool AzureStorageEnabled { get; set; }
    public bool ThesisWrapperEnabled { get; set; }
    public bool Limit1Version { get; set; }

    public string CoordinatorUrl { get; set; }
}
