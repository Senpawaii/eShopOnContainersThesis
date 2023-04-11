namespace Catalog.API.DependencyServices
{
    public class ScopedMetadata : IScopedMetadata {
        double tokens;
        Tuple<int, int> interval = Tuple.Create(0, 0);
        DateTimeOffset timestamp = DateTimeOffset.Now;
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

        public DateTimeOffset ScopedMetadataTimestamp {
            get { return timestamp; }
            set { timestamp = value; }
        }
        public string ScopedMetadataFunctionalityID {
            get { return functionality_ID; }
            set { functionality_ID = value; }
        }
    }
}
