﻿namespace Microsoft.eShopOnContainers.Services.Discount.API;

public class DiscountSettings {

    public string EventBusConnection { get; set; }

    public bool UseCustomizationData { get; set; }

    public bool AzureStorageEnabled { get; set; }

    public bool ThesisWrapperEnabled { get; set; }
    public bool Limit1Version { get; set; }
    public bool PublishEventEnabled { get; set; }

    public string CatalogUrl { get; set; }

    public string CoordinatorUrl { get; set; }
}
