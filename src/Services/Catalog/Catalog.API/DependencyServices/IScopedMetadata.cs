namespace Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
public interface IScopedMetadata {
    double Tokens { get; set; }
    DateTime Timestamp { get; set; }
    string ClientID { get; set; }
    bool ReadOnly { get; set; }
}
