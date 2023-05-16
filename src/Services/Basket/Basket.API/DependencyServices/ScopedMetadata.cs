using Google.Protobuf.WellKnownTypes;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Basket.API.DependencyServices;
public class ScopedMetadata : IScopedMetadata {
    // The reason behind AsyncLocals is that they are not shared across threads. This is important because we want to be able to track the metadata for each request.
    // HttpHandlers convert scoped dependencies into transient dependencies. This means that the same instance of the dependency is not shared across requests.
    // This is a "hack" to get around the fact that we cannot inject scoped dependencies into HttpHandlers.

    public static AsyncLocal<double> tokens = new AsyncLocal<double>();
    public static AsyncLocal<int> low_interval = new AsyncLocal<int>();
    public static AsyncLocal<int> high_interval = new AsyncLocal<int>();

    // The default timestamp should not be used by any functionality. This is used mainly for ContextSeed population.
    public static AsyncLocal<DateTime> timestamp = new AsyncLocal<DateTime>() { Value = DateTime.UtcNow };
    public static AsyncLocal<string> functionality_ID = new AsyncLocal<string>();
    public ScopedMetadata() { }

    public AsyncLocal<double> ScopedMetadataTokens {
        get { return tokens; }
        set { tokens = value; }
    }
    public AsyncLocal<int> ScopedMetadataLowInterval {
        get { return low_interval; }
        set { low_interval = value; }
    }
    public AsyncLocal<int> ScopedMetadataHighInterval {
        get { return high_interval; }
        set { high_interval = value; }
    }
    public AsyncLocal<DateTime> ScopedMetadataTimestamp {
        get { return timestamp; }
        set { timestamp = value; }
    }
    public AsyncLocal<string> ScopedMetadataFunctionalityID {
        get { return functionality_ID; }
        set { functionality_ID = value; }
    }
}
