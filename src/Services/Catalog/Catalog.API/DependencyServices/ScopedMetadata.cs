namespace Catalog.API.DependencyServices
{
    public class ScopedMetadata : IScopedMetadata {
        double tokens;
        Tuple<int, int> interval = Tuple.Create(0, 0);
        // The default timestamp should not be used by any functionality. This is used mainly for ContextSeed population.
        DateTime timestamp = DateTime.UtcNow.AddMinutes(5);
        string functionality_ID;
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

        public DateTime ScopedMetadataTimestamp {
            get { return timestamp; }
            set { timestamp = value; }
        }
        public string ScopedMetadataFunctionalityID {
            get { return functionality_ID; }
            set { functionality_ID = value; }
        }
    }
}
