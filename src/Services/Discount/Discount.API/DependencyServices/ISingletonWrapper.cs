using System.Collections.Concurrent;
using System.Threading;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.SharedStructs;
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
    public List<(string, EventMonitor)> AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp, string clientID);

    public List<DiscountItem> SingletonGetWrappedDiscountItemsToFlush(string clientID, bool onlyUpdate);
    public void CleanWrappedObjects(string clientID);
    public void NotifyReaderThreads(string clientID, List<DiscountItem> committedItems);
    public void RemoveFromDependencyList((string, EventMonitor) guid_EM, string clientID);
    public void DisposeCommittedDataMREs();
}
