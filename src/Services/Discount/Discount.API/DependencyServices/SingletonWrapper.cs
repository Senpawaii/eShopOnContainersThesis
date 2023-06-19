using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
public class SingletonWrapper : ISingletonWrapper {
    ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_Discount_Items  = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();

    ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> proposed_Discount_Items = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>>();
    ConcurrentDictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, int>> proposed_Discount_Items2 = new ConcurrentDictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, int>>();

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

    public ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_Discount_Items {
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
            // Concat the identifiers in a single string
            string identifiersStr = object_to_propose[1].ToString() + "_" + object_to_propose[2].ToString() + "_" + object_to_propose[3].ToString();

            // If the identifiers have yet to be inserted in the proposed_discount_items set, add them as a new key
            // If the identifiers have already been inserted in the proposed_discount_items set, add the proposed timestamp to the list of timestamps associated with the identifiers
            proposed_Discount_Items.AddOrUpdate(identifiersStr, _ => {
                var innerDict = new ConcurrentDictionary<DateTime, int>();
                innerDict.TryAdd(new DateTime(proposedTS), 1);
                return innerDict;
            }, (_, value) => {
                value.TryAdd(new DateTime(proposedTS), 1);
                return value;
            });
        }
    }

    public void SingletonRemoveWrappedItemsFromProposedSet(string clientID, ConcurrentBag<object[]> wrapped_objects) {
        Console.WriteLine($"ClientID: <{clientID}>: Removing wrapped objects from proposed set");
        // For each object, remove it from the proposed set using its identifiers, removing the proposed timestamp associated
        foreach (object[] object_to_remove in wrapped_objects) {
            // Log the object to remove
            Console.WriteLine($"ClientID: <{clientID}>: Removing object: " + object_to_remove[1].ToString() + "_" + object_to_remove[2].ToString() + "_" + object_to_remove[3].ToString());
            // The identifiers of the object to remove consist of the item name, the item brand and the item type
            string objectToRemoveIdentifier = object_to_remove[1].ToString() + "_" + object_to_remove[2].ToString() + "_" + object_to_remove[3].ToString();
            var original_proposedTS = new DateTime(proposed_Client_Sessions[clientID]);
            proposed_Discount_Items[objectToRemoveIdentifier].TryRemove(original_proposedTS, out _);
        }
    }

    public bool AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp) {
        switch (targetTable) {
            case "Discount":
                // Apply all the conditions to the set of proposed catalog items 2, this set will keep shrinking as we apply more conditions.
                // Note that the original set of proposed catalog items 2 is not modified, we are just creating a new set with the results of the conditions.
                var filtered_proposed_Discount_items2 = new Dictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, int>>(this.proposed_Discount_Items2);
                if (conditions != null) {
                    filtered_proposed_Discount_items2 = ApplyFiltersToDiscountItems(conditions, filtered_proposed_Discount_items2);
                }
                // Check if the there any proposed catalog items 2 with a lower timestamp than the reader's timestamp
                foreach (KeyValuePair<ProposedDiscountItem, ConcurrentDictionary<DateTime, int>> proposed_catalog_item in filtered_proposed_Discount_items2) {
                    foreach (DateTime proposedTS in proposed_catalog_item.Value.Keys) {
                        if (proposedTS < readerTimestamp) {
                            // There is at least one proposed catalog item 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                            return true;
                        }
                    }
                }
                break;
        }
        return false;
    }

    private static Dictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, int>> ApplyFiltersToDiscountItems(List<Tuple<string, string>> conditions, Dictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, int>> filtered_proposed_Discount_items2) {
        foreach (Tuple<string, string> condition in conditions) {
            switch (condition.Item1) {
                case "Name":
                    filtered_proposed_Discount_items2 = filtered_proposed_Discount_items2.Where(x => x.Key.Name == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                    break;
                case "BrandName":
                    filtered_proposed_Discount_items2 = filtered_proposed_Discount_items2.Where(x => x.Key.BrandName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                    break;
                case "TypeName":
                    filtered_proposed_Discount_items2 = filtered_proposed_Discount_items2.Where(x => x.Key.TypeName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                    break;
            }
        }
        return filtered_proposed_Discount_items2;
    }

    public struct ProposedDiscountItem {
        public string Name { get; set; }
        public string BrandName { get; set; }
        public string TypeName { get; set; }
    }
}
