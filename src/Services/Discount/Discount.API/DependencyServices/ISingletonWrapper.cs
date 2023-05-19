using System.Collections.Concurrent;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;

public interface ISingletonWrapper {
    ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedDiscountItems { get; }
    
    ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> Proposed_discount_items { get; }
    ConcurrentDictionary<string, long> Proposed_functionalities { get; }

    ConcurrentDictionary<string, bool> SingletonTransactionState { get; }

    public ConcurrentBag<object[]> SingletonGetDiscountItems(string key);
    public bool SingletonGetTransactionState(string funcId);

    public void SingletonAddDiscountItem(string funcID, IEnumerable<object[]> values);
    public bool SingletonSetTransactionState(string funcId, bool state);
    public void SingletonRemoveFunctionalityObjects(string funcID);
    public void SingletonAddWrappedItemsToProposedSet(string functionality_ID, long proposedTS);
    public void SingletonRemoveWrappedItemsFromProposedSet(string functionality_ID, ConcurrentBag<object[]> wrapped_objects);

    public void SingletonAddProposedFunctionality(string funcID, long proposedTS);
    public void SingletonRemoveProposedFunctionality(string funcID);


}
