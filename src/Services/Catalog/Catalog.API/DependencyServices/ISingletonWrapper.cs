using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace Catalog.API.DependencyServices {
    public interface ISingletonWrapper {
        ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogItems { get; }
        ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogTypes { get; }
        ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogBrands { get; }

        ConcurrentDictionary<string, ConcurrentBag<object[]>> Proposed_catalog_items { get; }
        ConcurrentDictionary<string, ConcurrentBag<object[]>> Proposed_catalog_types { get; } 
        ConcurrentDictionary<string, ConcurrentBag<object[]>> Proposed_catalog_brands { get; }
        ConcurrentDictionary<string, long> Proposed_functionalities { get; }
        ConcurrentDictionary<string, bool> SingletonTransactionState { get; }

        public ConcurrentBag<object[]> SingletonGetCatalogITems(string key);
        public ConcurrentBag<object[]> SingletonGetCatalogTypes(string key);
        public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key);
        public bool SingletonGetTransactionState(string funcId);

        public void SingletonAddCatalogItem(string funcID, IEnumerable<object[]> values);
        public void SingletonAddCatalogType(string funcID, IEnumerable<object[]> values);
        public void SingletonAddCatalogBrand(string funcID, IEnumerable<object[]> values);
        public bool SingletonSetTransactionState(string funcId, bool state);

        public void SingletonRemoveFunctionalityObjects(string funcID);
        public void SingletonAddWrappedItemsToProposedSet(string functionality_ID, long proposedTS);
        public void SingletonRemoveWrappedItemsFromProposedSet(string functionality_ID);

        public void SingletonAddProposedFunctionality(string funcID, long proposedTS);
        public void SingletonRemoveProposedFunctionality(string funcID);
    }
}
