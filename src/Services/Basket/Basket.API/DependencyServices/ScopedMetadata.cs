using Google.Protobuf.WellKnownTypes;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
public class ScopedMetadata : IScopedMetadata {
    // The reason behind AsyncLocals is that they are not shared across threads. This is important because we want to be able to track the metadata for each request.
    // HttpHandlers convert scoped dependencies into transient dependencies. This means that the same instance of the dependency is not shared across requests.
    // This is a "hack" to get around the fact that we cannot inject scoped dependencies into HttpHandlers.

    public static AsyncLocal<int> tokens = new AsyncLocal<int>();

    // The default timestamp should not be used by any functionality. This is used mainly for ContextSeed population.
    public static AsyncLocal<DateTime> timestamp = new AsyncLocal<DateTime>() { Value = DateTime.UtcNow };
    public static AsyncLocal<string> clientID = new AsyncLocal<string>();

    public static AsyncLocal<bool> readOnly = new AsyncLocal<bool>();
    public ScopedMetadata() { }

    public AsyncLocal<int> Tokens {
        get { return tokens; }
        set { tokens = value; }
    }
    public AsyncLocal<DateTime> Timestamp {
        get { return timestamp; }
        set { timestamp = value; }
    }
    public AsyncLocal<string> ClientID {
        get { return clientID; }
        set { clientID = value; }
    }
    public AsyncLocal<bool> ReadOnly {
        get { return readOnly; }
        set { readOnly = value; }
    }
}
