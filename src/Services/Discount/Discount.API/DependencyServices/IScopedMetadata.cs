namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
public interface IScopedMetadata {
    int Tokens { get; set; }
    DateTime Timestamp { get; set; }
    string ClientID { get; set; }
    bool ReadOnly { get; set; }

}
