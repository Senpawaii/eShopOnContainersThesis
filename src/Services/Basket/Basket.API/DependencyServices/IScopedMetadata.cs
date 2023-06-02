using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
public interface IScopedMetadata {
    AsyncLocal<int> Tokens { get; set; }
    AsyncLocal<DateTime> Timestamp { get; set; }
    AsyncLocal<string> ClientID { get; set; }
    AsyncLocal<bool> ReadOnly { get; set; }
}
