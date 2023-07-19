using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.SharedStructs {
    public struct EventMonitor {
        // This struct is used to monitor the events that are being waited by the reader threads
        public ManualResetEvent Event { get; set; }
        public string ClientID { get; set; }
        public long Timestamp { get; set; }
    }
}