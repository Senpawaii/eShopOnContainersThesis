using System.Collections.Concurrent;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;

public interface ISingletonWrapper {
    ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedDiscountItems { get; }
    ConcurrentDictionary<string, bool> SingletonTransactionState { get; }

    public ConcurrentBag<object[]> SingletonGetDiscountItems(string key);
    public bool SingletonGetTransactionState(string funcId);

    public void SingletonAddDiscountItem(string funcID, IEnumerable<object[]> values);
    public bool SingletonSetTransactionState(string funcId, bool state);
    public void SingletonRemoveFunctionalityObjects(string funcID);


}
