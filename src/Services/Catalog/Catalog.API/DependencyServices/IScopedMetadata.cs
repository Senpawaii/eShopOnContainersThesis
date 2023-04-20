namespace Catalog.API.DependencyServices {
    public interface IScopedMetadata {
        double ScopedMetadataTokens { get; set; }
        Tuple<int, int> ScopedMetadataInterval { get; set; }
        int ScopedMetadataIntervalLow { get; set; }
        int ScopedMetadataIntervalHigh { get; set; }
        DateTime ScopedMetadataTimestamp { get; set; }
        string ScopedMetadataFunctionalityID { get; set; }
    }
}
