using System.Threading;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
public interface IScopedMetadata {
    AsyncLocal<int> Tokens { get; set; }
    AsyncLocal<int> Invocations { get; set; }
    AsyncLocal<string> Timestamp { get; set; }
    AsyncLocal<string> ClientID { get; set; }
}
