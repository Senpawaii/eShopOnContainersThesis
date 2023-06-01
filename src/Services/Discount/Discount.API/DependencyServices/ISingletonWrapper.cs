using System.Collections.Concurrent;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;

public interface ISingletonWrapper {
    ConcurrentDictionary<string, ConcurrentBag<object[]>> Singleton_Wrapped_DiscountItems { get; }
    
    ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> Proposed_Discount_Items { get; }
    ConcurrentDictionary<string, long> Proposed_Client_Sessions { get; }

    ConcurrentDictionary<string, bool> Singleton_Transaction_State { get; }

    public ConcurrentBag<object[]> SingletonGetDiscountItems(string key);
    public bool SingletonGetTransactionState(string clientID);

    public void SingletonAddDiscountItem(string clientID, IEnumerable<object[]> values);
    public bool SingletonSetTransactionState(string clientID, bool state);
    public void SingletonRemoveFunctionalityObjects(string clientID);
    public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS);
    public void SingletonRemoveWrappedItemsFromProposedSet(string clientID, ConcurrentBag<object[]> wrapped_objects);

    public void SingletonAddProposedFunctionality(string clientID, long proposedTS);
    public void SingletonRemoveProposedFunctionality(string clientID);


}
