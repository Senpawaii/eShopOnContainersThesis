using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
public class SingletonWrapper : ISingletonWrapper {
    ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_discount_items  = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
    // Dictionary that hold the state of a transaction: PREPARE or COMMITTED
    ConcurrentDictionary<string, bool> transaction_state = new ConcurrentDictionary<string, bool>();

    public SingletonWrapper() {
    }

    public ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedDiscountItems {
        get { return wrapped_discount_items; }
    }

    public ConcurrentDictionary<string, bool> SingletonTransactionState {
        get { return transaction_state; }
    }

    public ConcurrentBag<object[]> SingletonGetDiscountItems(string key) {
        return wrapped_discount_items.GetValueOrDefault(key, new ConcurrentBag<object[]>());
    }

    public bool SingletonGetTransactionState(string funcId) {
        if (!transaction_state.ContainsKey(funcId)) {
            // Initialize a new state: uncommitted
            transaction_state[funcId] = false;
        }
        return transaction_state.GetValueOrDefault(funcId);
    }

    public void SingletonAddDiscountItem(string funcID, IEnumerable<object[]> values) {
        foreach (object[] item in values) {
            wrapped_discount_items.AddOrUpdate(funcID, new ConcurrentBag<object[]> { item }, (key, bag) => {
                bag.Add(item);
                return bag;
            });
        }
    }

    public bool SingletonSetTransactionState(string funcId, bool state) {
        return transaction_state[funcId] = state;
    }

    public void SingletonRemoveFunctionalityObjects(string funcId) {
        if (wrapped_discount_items.ContainsKey(funcId))
            wrapped_discount_items.TryRemove(funcId, out ConcurrentBag<object[]> _);
        if (transaction_state.ContainsKey(funcId))
            transaction_state.TryRemove(funcId, out bool _);
    }
}
