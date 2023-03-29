namespace Catalog.API.DependencyServices
{
    public class ScopedMetadata : IScopedMetadata {
        double tokens;

        public ScopedMetadata() { }   

        public double ScopedMetadataTokens { get; set; }
    }
}
