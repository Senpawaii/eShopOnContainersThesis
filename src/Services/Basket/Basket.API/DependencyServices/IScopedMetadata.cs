using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
public interface IScopedMetadata {
    AsyncLocal<double> ScopedMetadataTokens { get; set; }
    AsyncLocal<int> ScopedMetadataLowInterval { get; set; }
    AsyncLocal<int> ScopedMetadataHighInterval { get; set; }
    AsyncLocal<DateTime> ScopedMetadataTimestamp { get; set; }
    AsyncLocal<string> ScopedMetadataFunctionalityID { get; set; }
}
