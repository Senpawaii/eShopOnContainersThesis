using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
public class SingletonWrapper : ISingletonWrapper {
    ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_discount_items  = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();

    ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> proposed_discount_items = new ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>>();

    // Store the proposed Timestamp for each functionality in Proposed State
    ConcurrentDictionary<string, long> proposed_functionalities = new ConcurrentDictionary<string, long>();

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

    public ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> Proposed_discount_items {
        get { return proposed_discount_items; }
    }

    public ConcurrentDictionary<string, long> Proposed_functionalities {
        get { return proposed_functionalities; }
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

    public void SingletonAddProposedFunctionality(string functionality_ID, long proposedTS) {
        proposed_functionalities.AddOrUpdate(functionality_ID, proposedTS, (key, value) => {
            value = proposedTS;
            return value;
        });
    }

    public void SingletonRemoveProposedFunctionality(string functionality_ID) {
        if (proposed_functionalities.ContainsKey(functionality_ID))
            proposed_functionalities.TryRemove(functionality_ID, out long _);
    }

    public void SingletonAddWrappedItemsToProposedSet(string functionality_ID, long proposedTS) {
        // Gather the objects inside the wrapper with given functionality_ID
        ConcurrentBag<object[]> objects_to_propose = wrapped_discount_items.GetValueOrDefault(functionality_ID, new ConcurrentBag<object[]>());

        // For each object, add it to the proposed set using its identifiers, adding the proposed timestamp associated
        foreach (object[] object_to_propose in objects_to_propose) {
            object[] identifiers = new object[] { object_to_propose[1], object_to_propose[2], object_to_propose[3] };

            proposed_discount_items[identifiers] = new ConcurrentDictionary<DateTime, int>();
            proposed_discount_items[identifiers].AddOrUpdate(new DateTime(proposedTS), 1, (key, value) => {
                value = 1;
                return value;
            });
        }
    }

    public void SingletonRemoveWrappedItemsFromProposedSet(string functionality_ID, ConcurrentBag<object[]> wrapped_objects) {
        // For each object, remove it from the proposed set using its identifiers, removing the proposed timestamp associated
        foreach (object[] object_to_remove in wrapped_objects) {
            object[] identifiers = new object[] { object_to_remove[1], object_to_remove[2], object_to_remove[3] };
            var original_proposedTS = new DateTime(proposed_functionalities[functionality_ID]);
            foreach (object[] item in proposed_discount_items.Keys) {
                if (item[0].ToString() == identifiers[0].ToString() && item[1].ToString() == identifiers[1].ToString() && item[2].ToString() == identifiers[2].ToString()) {
                    proposed_discount_items[item].TryRemove(original_proposedTS, out _);
                }
            }
        }
    }
}

//public class DiscountItemsComparer : IEqualityComparer<object[]> {
//    public int GetHashCode(object[] objectX) {
//        return objectX.GetHashCode();
//    }

//    public bool Equals(object[] discount1, object[] discount2) {
//        return discount1[0].ToString() == discount2[0].ToString() && discount1[1].ToString() == discount2[1].ToString() && discount1[2].ToString() == discount2[2].ToString();
//    }

//}
