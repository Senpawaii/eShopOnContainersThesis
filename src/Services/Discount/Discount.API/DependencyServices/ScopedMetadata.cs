namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;

public class ScopedMetadata : IScopedMetadata {
    double tokens;
    // The default timestamp should not be used by any functionality. This is used mainly for ContextSeed population.
    DateTime timestamp = DateTime.UtcNow;
    string clientID;
    public static bool readOnly;

    public ScopedMetadata() { }

    public double Tokens {
        get { return tokens; }
        set { tokens = value; }
    }
    public DateTime Timestamp {
        get { return timestamp; }
        set { timestamp = value; }
    }
    public string ClientID {
        get { return clientID; }
        set { clientID = value; }
    }
    public bool ReadOnly {
        get { return readOnly; }
        set { readOnly = value; }
    }
}
