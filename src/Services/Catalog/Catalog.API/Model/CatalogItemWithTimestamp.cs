namespace Catalog.API.Model;
public class CatalogItemWithTimestamp {
    public int Id { get; set; }

    public string Name { get; set; }

    public int CatalogTypeId { get; set; }

    public int CatalogBrandId { get; set; }

    public DateTime Timestamp { get; set; }

    public CatalogItemWithTimestamp() { }
}
