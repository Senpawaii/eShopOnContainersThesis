namespace Microsoft.eShopOnContainers.Services.Catalog.API.Model;

public class CatalogTypeWithTimestamp {
    public int Id { get; set; }

    public string Type { get; set; }

    public DateTime Timestamp { get; set; }
}
