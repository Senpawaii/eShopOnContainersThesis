namespace Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
public class ScopedMetadata : IScopedMetadata {
    int tokens;
    // The default timestamp should not be used by any functionality. This is used mainly for ContextSeed population.
    DateTime timestamp = DateTime.UtcNow;
    string clientID;
    public bool readOnly = true;

    public ScopedMetadata() { }

    public int Tokens {
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
