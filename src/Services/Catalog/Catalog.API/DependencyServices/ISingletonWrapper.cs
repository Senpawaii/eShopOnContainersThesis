using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.SharedStructs;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Threading;

namespace Catalog.API.DependencyServices {
    public interface ISingletonWrapper {
        ConcurrentDictionary<string, long> Proposed_functionalities { get; }
        ConcurrentDictionary<string, bool> SingletonTransactionState { get; }

        public ConcurrentBag<object[]> SingletonGetCatalogItems(string key);
        public ConcurrentBag<object[]> SingletonGetCatalogTypes(string key);
        public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key);

        public bool SingletonGetTransactionState(string clientID);

        public void SingletonAddCatalogItem(string clientID, IEnumerable<object[]> values);
        public void SingletonAddCatalogType(string clientID, IEnumerable<object[]> values);
        public void SingletonAddCatalogBrand(string clientID, IEnumerable<object[]> values);
        public bool SingletonSetTransactionState(string clientID, bool state);

        public void SingletonRemoveFunctionalityObjects(string clientID);
        public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS);

        public void SingletonAddProposedFunctionality(string clientID, long proposedTS);
        public void SingletonRemoveProposedFunctionality(string clientID);
        public List<EventMonitor> AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp, string clientID);

        public List<CatalogItem> SingletonGetWrappedCatalogItemsToFlush(string clientID, bool onlyUpdate);
        public void CleanWrappedObjects(string clientID);
        public void NotifyReaderThreads(string clientID, List<CatalogItem> committedItems);

        public void RemoveFromDependencyList(ManualResetEvent MRE, string clientID);
        public void DisposeCommittedDataMREs();
    }
}
