using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
public class SingletonWrapper : ISingletonWrapper {
    ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_Discount_Items  = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();

    ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> proposed_Discount_Items = new ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>>();

    // Store the proposed Timestamp for each functionality in Proposed State
    ConcurrentDictionary<string, long> proposed_Client_Sessions = new ConcurrentDictionary<string, long>();

    // Dictionary that hold the state of a transaction: PREPARE or COMMITTED
    ConcurrentDictionary<string, bool> transaction_State = new ConcurrentDictionary<string, bool>();

    public SingletonWrapper() {
    }

    public ConcurrentDictionary<string, ConcurrentBag<object[]>> Singleton_Wrapped_DiscountItems {
        get { return wrapped_Discount_Items; }
    }

    public ConcurrentDictionary<string, bool> Singleton_Transaction_State {
        get { return transaction_State; }
    }

    public ConcurrentDictionary<object[], ConcurrentDictionary<DateTime, int>> Proposed_Discount_Items {
        get { return proposed_Discount_Items; }
    }

    public ConcurrentDictionary<string, long> Proposed_Client_Sessions {
        get { return proposed_Client_Sessions; }
    }

    public ConcurrentBag<object[]> SingletonGetDiscountItems(string key) {
        return wrapped_Discount_Items.GetValueOrDefault(key, new ConcurrentBag<object[]>());
    }

    public bool SingletonGetTransactionState(string clientID) {
        if (!transaction_State.ContainsKey(clientID)) {
            // Initialize a new state: uncommitted
            transaction_State[clientID] = false;
        }
        return transaction_State.GetValueOrDefault(clientID);
    }

    public void SingletonAddDiscountItem(string clientID, IEnumerable<object[]> values) {
        foreach (object[] item in values) {
            wrapped_Discount_Items.AddOrUpdate(clientID, new ConcurrentBag<object[]> { item }, (key, bag) => {
                bag.Add(item);
                return bag;
            });
        }
    }

    public bool SingletonSetTransactionState(string clientID, bool state) {
        return transaction_State[clientID] = state;
    }

    public void SingletonRemoveFunctionalityObjects(string clientID) {
        if (wrapped_Discount_Items.ContainsKey(clientID))
            wrapped_Discount_Items.TryRemove(clientID, out ConcurrentBag<object[]> _);
        if (transaction_State.ContainsKey(clientID))
            transaction_State.TryRemove(clientID, out bool _);
    }

    public void SingletonAddProposedFunctionality(string clientID, long proposedTS) {
        proposed_Client_Sessions.AddOrUpdate(clientID, proposedTS, (key, value) => {
            value = proposedTS;
            return value;
        });
    }

    public void SingletonRemoveProposedFunctionality(string clientID) {
        if (proposed_Client_Sessions.ContainsKey(clientID))
            proposed_Client_Sessions.TryRemove(clientID, out long _);
    }

    public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS) {
        // Gather the objects inside the wrapper with given clientID
        ConcurrentBag<object[]> objects_to_propose = wrapped_Discount_Items.GetValueOrDefault(clientID, new ConcurrentBag<object[]>());

        // For each object, add it to the proposed set using its identifiers, adding the proposed timestamp associated
        foreach (object[] object_to_propose in objects_to_propose) {
            object[] identifiers = new object[] { object_to_propose[1], object_to_propose[2], object_to_propose[3] };

            proposed_Discount_Items[identifiers] = new ConcurrentDictionary<DateTime, int>();
            proposed_Discount_Items[identifiers].AddOrUpdate(new DateTime(proposedTS), 1, (key, value) => {
                value = 1;
                return value;
            });
        }
    }

    public void SingletonRemoveWrappedItemsFromProposedSet(string clientID, ConcurrentBag<object[]> wrapped_objects) {
        // For each object, remove it from the proposed set using its identifiers, removing the proposed timestamp associated
        foreach (object[] object_to_remove in wrapped_objects) {
            object[] identifiers = new object[] { object_to_remove[1], object_to_remove[2], object_to_remove[3] };
            var original_proposedTS = new DateTime(proposed_Client_Sessions[clientID]);
            foreach (object[] item in proposed_Discount_Items.Keys) {
                if (item[0].ToString() == identifiers[0].ToString() && item[1].ToString() == identifiers[1].ToString() && item[2].ToString() == identifiers[2].ToString()) {
                    proposed_Discount_Items[item].TryRemove(original_proposedTS, out _);
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
