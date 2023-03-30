namespace Catalog.API.DependencyServices
{
    public class ScopedMetadata : IScopedMetadata {
        double tokens;
        Tuple<int, int> interval = Tuple.Create(0, 0);

        public ScopedMetadata() { }   

        public double ScopedMetadataTokens { 
            get { return tokens; } 
            set { tokens = value; } 
        }
        public Tuple<int, int> ScopedMetadataInterval {
            get { return interval; }
            set { interval = Tuple.Create(value.Item1, value.Item2); }
        }
        public int ScopedMetadataIntervalLow {
            get { return interval.Item1; }
            set { interval = Tuple.Create(value, interval.Item2); }
        }
        public int ScopedMetadataIntervalHigh {
            get { return interval.Item2; }
            set { interval = Tuple.Create(interval.Item1, value); }
        }
    }
}
