using System.Collections.Concurrent;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;

public interface ISingletonWrapper {
    ConcurrentDictionary<string, long> Proposed_functionalities { get; }
    ConcurrentDictionary<string, bool> SingletonTransactionState { get; }

    public ConcurrentBag<object[]> SingletonGetDiscountItems(string key);
    public bool SingletonGetTransactionState(string clientID);
    public void SingletonAddDiscountItem(string clientID, IEnumerable<object[]> values);
    public bool SingletonSetTransactionState(string clientID, bool state);
    public void SingletonRemoveFunctionalityObjects(string clientID);
    public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS);

    public void SingletonAddProposedFunctionality(string clientID, long proposedTS);
    public void SingletonRemoveProposedFunctionality(string clientID);
    public bool AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp);

    public List<DiscountItem> SingletonGetWrappedDiscountItemsToFlush(string clientID, bool onlyUpdate);
    public void CleanWrappedObjects(string clientID);
}
