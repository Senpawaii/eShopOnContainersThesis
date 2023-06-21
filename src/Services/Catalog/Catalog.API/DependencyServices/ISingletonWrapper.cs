using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace Catalog.API.DependencyServices {
    public interface ISingletonWrapper {
        //ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogItems { get; }
        //ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogTypes { get; }
        //ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogBrands { get; }

        //ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_items { get; }
        //ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_types { get; } 
        //ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_brands { get; }
        ConcurrentDictionary<string, long> Proposed_functionalities { get; }
        ConcurrentDictionary<string, bool> SingletonTransactionState { get; }

        //public ConcurrentBag<object[]> SingletonGetCatalogITems(string key);
        //public ConcurrentBag<object[]> SingletonGetCatalogTypes(string key);
        //public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key);

        public ConcurrentBag<object[]> SingletonGetCatalogItemsV2(string key);
        public ConcurrentBag<object[]> SingletonGetCatalogTypesV2(string key);
        public ConcurrentBag<object[]> SingletonGetCatalogBrandsV2(string key);

        public bool SingletonGetTransactionState(string clientID);

        public void SingletonAddCatalogItem(string clientID, IEnumerable<object[]> values);
        public void SingletonAddCatalogType(string clientID, IEnumerable<object[]> values);
        public void SingletonAddCatalogBrand(string clientID, IEnumerable<object[]> values);
        public bool SingletonSetTransactionState(string clientID, bool state);

        public void SingletonRemoveFunctionalityObjects(string clientID);
        //public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS);
        public void SingletonAddWrappedItemsToProposedSetV2(string clientID, long proposedTS);
        //public void SingletonRemoveWrappedItemsFromProposedSet(string clientID, ConcurrentBag<object[]> wrapped_objects, string target_table);
        //public void SingletonRemoveWrappedItemsFromProposedSetV2(string clientID);

        public void SingletonAddProposedFunctionality(string clientID, long proposedTS);
        public void SingletonRemoveProposedFunctionality(string clientID);
        public bool AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp);

        public List<CatalogItem> SingletonGetWrappedCatalogItemsToFlush(string clientID, bool onlyUpdate);
        public void CleanWrappedObjects(string clientID);
    }
}
