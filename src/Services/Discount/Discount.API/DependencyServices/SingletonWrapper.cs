using Google.Protobuf.WellKnownTypes;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
//using NewRelic.Api.Agent;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
public class SingletonWrapper : ISingletonWrapper {
    public ILogger<SingletonWrapper> _logger;

    ConcurrentDictionary<string, ConcurrentBag<DiscountItem>> wrapped_discount_items = new ConcurrentDictionary<string, ConcurrentBag<DiscountItem>>();
    ConcurrentDictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, string>> proposed_discount_Items = new ConcurrentDictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, string>>();

    ConcurrentDictionary<(ProposedDiscountItem, long), List<ManualResetEvent>> discount_items_manual_reset_events = new ConcurrentDictionary<(ProposedDiscountItem, long), List<ManualResetEvent>>();
    // Store the proposed Timestamp for each functionality in Proposed State
    ConcurrentDictionary<string, long> proposed_functionalities = new ConcurrentDictionary<string, long>();

    // Dictionary that hold the state of a transaction: PREPARE or COMMITTED
    ConcurrentDictionary<string, bool> transaction_state = new ConcurrentDictionary<string, bool>();

    public SingletonWrapper(ILogger<SingletonWrapper> logger) {
        _logger = logger;
    }

    public ConcurrentDictionary<string, bool> SingletonTransactionState {
        get { return transaction_state; }
    }

    public ConcurrentDictionary<string, long> Proposed_functionalities {
        get { return proposed_functionalities; }
    }

   //[Trace]
        public ConcurrentBag<object[]> SingletonGetDiscountItems(string key) {
            // Get the wrapped discount items associated with the clientID
            var bag = wrapped_discount_items.GetValueOrDefault(key, new ConcurrentBag<DiscountItem>());

            // Convert the wrapped discount items to a list of object[]
            var list = new ConcurrentBag<object[]>();
            foreach (var item in bag) {
                list.Add(new object[] {
                    item.Id,
                    item.ItemName,
                    item.ItemBrand,
                    item.ItemType,
                    item.DiscountValue
                });
            }
            return list;
        }

   //[Trace]
    public bool SingletonGetTransactionState(string clientID) {
        if(!transaction_state.ContainsKey(clientID)) {
            // Initialize a new state: uncommitted
            transaction_state[clientID] = false;
        }
        // _logger.LogInformation($"CliendID: {clientID}, Transaction State: {transaction_state.GetValueOrDefault(clientID)}");
        return transaction_state.GetValueOrDefault(clientID);
    }

   //[Trace]
    public void SingletonAddDiscountItem(string clientID, IEnumerable<object[]> values) {
        foreach (object[] item in values) {
            // Create a new Wrapped Discount Item
            var wrapped_item = new DiscountItem {
                Id = (int)item[0],
                ItemName = (string)item[1],
                ItemBrand = (string)item[2],
                ItemType = (string)item[3],
                DiscountValue = (int)item[4]
            };
            wrapped_discount_items.AddOrUpdate(clientID, new ConcurrentBag<DiscountItem> { wrapped_item }, (key, bag) => {
                bag.Add(wrapped_item);
                return bag;
            });
        }
    }

   //[Trace]
    public bool SingletonSetTransactionState(string clientID, bool state) {
        return transaction_state[clientID] = state;
    }

   //[Trace]
    public void SingletonRemoveFunctionalityObjects(string clientID) {
        // Remove the objects associated with the clientID
        wrapped_discount_items.TryRemove(clientID, out _);
        transaction_state.TryRemove(clientID, out bool _);
    }

   //[Trace]
    public void SingletonAddProposedFunctionality(string clientID, long proposedTS) {
        proposed_functionalities.AddOrUpdate(clientID, proposedTS, (key, value) => {
            value = proposedTS;
            return value;
        });
    }

   //[Trace]
    public void SingletonRemoveProposedFunctionality(string clientID) {
        if(proposed_functionalities.ContainsKey(clientID))
            proposed_functionalities.TryRemove(clientID, out long _);
    }

   //[Trace]
    public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS) {
        // Gather the items inside the wrapper with given clientID
        ConcurrentBag<DiscountItem> discount_items_to_propose = wrapped_discount_items.GetValueOrDefault(clientID, new ConcurrentBag<DiscountItem>());

        // For each item, add it to the proposed set, adding the proposed timestamp associated
        foreach (DiscountItem discount_item_to_propose in discount_items_to_propose) {
            DateTime proposedTSDate = new DateTime(proposedTS);
            ProposedDiscountItem proposedItem = new ProposedDiscountItem {
                ItemName = discount_item_to_propose.ItemName,
                ItemBrand = discount_item_to_propose.ItemBrand,
                ItemType = discount_item_to_propose.ItemType
            };

            proposed_discount_Items.AddOrUpdate(proposedItem, _ => {
                var innerDict = new ConcurrentDictionary<DateTime, string>();
                innerDict.TryAdd(proposedTSDate, clientID);
                return innerDict;
            }, (_, value) => {
                value.TryAdd(proposedTSDate, clientID);
                return value;
            });

            // Create ManualEvent for Discount item
            discount_items_manual_reset_events.AddOrUpdate((proposedItem, proposedTS), _ => {
                var manualResetEvent = new ManualResetEvent(false);
                return new List<ManualResetEvent> { manualResetEvent };
            }, (_, value) => {
                var manualResetEvent = new ManualResetEvent(false);
                value.Add(manualResetEvent);
                return value;
            });
        }
    }

   //[Trace]
    public void SingletonRemoveWrappedItemsFromProposedSet(string clientID) {
        // TODO: Possible optimization: add structure that indexes proposed objects by the client ID that proposed them

        // Get the proposed objects from the wrapper with given clientID
        ConcurrentBag<DiscountItem> discount_items_to_remove = wrapped_discount_items.GetValueOrDefault(clientID, new ConcurrentBag<DiscountItem>());

        // Remove entries from the proposed set. These are identified by their key table identifiers.
        foreach(DiscountItem item in discount_items_to_remove) {
            ProposedDiscountItem proposedItem = new ProposedDiscountItem {
                ItemName = item.ItemName,
                ItemBrand = item.ItemBrand,
                ItemType = item.ItemType
            };
            // Remove each item from the proposed set
            proposed_discount_Items.TryRemove(proposedItem, out _);
        }
    }

   //[Trace]
    public List<ManualResetEvent> AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp, string clientID) {
        switch (targetTable) {
            case "Discount":
                // Apply all the conditions to the set of proposed discount items, this set will keep shrinking as we apply more conditions.
                // Note that the original set of proposed discount items is not modified, we are just creating a new set with the results of the conditions.
                var filtered_proposed_discount_items2 = new Dictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, string>>(this.proposed_discount_Items);
                if (conditions != null) {
                    filtered_proposed_discount_items2 = ApplyFiltersToDiscountItems(conditions, filtered_proposed_discount_items2);
                }
                // Check if the there any proposed disocount items 2 with a lower timestamp than the reader's timestamp
                foreach (var proposed_discount_item in filtered_proposed_discount_items2) {
                    foreach (DateTime proposedTS in proposed_discount_item.Value.Keys) {
                        if (proposedTS < readerTimestamp) {
                            // Console.WriteLine($"The item {proposed_discount_item.Key.ItemName} has a lower timestamp than the reader's timestamp: {proposedTS.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")} < {readerTimestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}. Proposed ticks: {proposedTS.Ticks}, proposed by client: {proposed_discount_item.Value[proposedTS]}");
                            // There is at least one proposed discount item 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                            List<ManualResetEvent> manualResetEvents = discount_items_manual_reset_events.GetValueOrDefault((proposed_discount_item.Key, proposedTS.Ticks), null);
                            if(manualResetEvents == null || manualResetEvents.Count == 0) {
                                _logger.LogError($"ClientID: {clientID} - ManualResetEvent null: Could not find ManualResetEvent for item {proposed_discount_item.Key.ItemName} with proposed timestamp {proposedTS}");
                            }
                            return manualResetEvents ?? null;
                        }
                    }
                }
                break;
        }
        return null;
    }

   //[Trace]
    private static Dictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, string>> ApplyFiltersToDiscountItems(List<Tuple<string, string>> conditions, Dictionary<ProposedDiscountItem, ConcurrentDictionary<DateTime, string>> filtered_proposed_discount_items2) {
        foreach (Tuple<string, string> condition in conditions) {
            switch(condition.Item1) {
                case "ItemName":
                    filtered_proposed_discount_items2 = filtered_proposed_discount_items2.Where(x => x.Key.ItemName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                    break;
                case "ItemBrand":
                    filtered_proposed_discount_items2 = filtered_proposed_discount_items2.Where(x => x.Key.ItemBrand == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                    break;
                case "ItemType":
                    filtered_proposed_discount_items2 = filtered_proposed_discount_items2.Where(x => x.Key.ItemType == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                    break;
            }
        }

        return filtered_proposed_discount_items2;
    }

   //[Trace]
    public List<DiscountItem> SingletonGetWrappedDiscountItemsToFlush(string clientID, bool onlyUpdate) {
        // Check the Discount Items associated with the clientID and flush them to the database
        
        var discountItems = new List<DiscountItem>();
        
        // Console.WriteLine("Getting proposed data to flush from client: " + clientID + " ...");
        if (wrapped_discount_items.TryGetValue(clientID, out ConcurrentBag<DiscountItem> clientDiscountItems)) {
            foreach (DiscountItem wrappedDiscountItem in clientDiscountItems) {
                if (onlyUpdate) {
                    discountItems.Add(wrappedDiscountItem);
                }
                else {
                    DiscountItem discountItem = new DiscountItem {
                        ItemName = wrappedDiscountItem.ItemName,
                        ItemBrand = wrappedDiscountItem.ItemBrand,
                        ItemType = wrappedDiscountItem.ItemType,
                        DiscountValue = wrappedDiscountItem.DiscountValue
                    };
                    discountItems.Add(discountItem);
                }
            }
            return discountItems;
        }
        // _logger.LogInformation("No items were registered in the wrapper for clientID " + clientID);
        return null;
    }

   //[Trace]
    public void CleanWrappedObjects(string clientID) {
        // _logger.LogInformation($"Cleaning wrapped objects for client {clientID} ...");
        // Clean up the proposed items;
        SingletonRemoveWrappedItemsFromProposedSet(clientID);

        // Clean up wrapped objects;
        SingletonRemoveFunctionalityObjects(clientID);

        // Clean up the proposed functionality state
        SingletonRemoveProposedFunctionality(clientID);
    }

    public void NotifyReaderThreads(string clientID, List<DiscountItem> committedItems) {
        foreach(DiscountItem item in committedItems) {
            // Locate the wrappedDiscountItem associated with the committed item
            ProposedDiscountItem proposedItem = new ProposedDiscountItem {
                ItemName = item.ItemName,
                ItemBrand = item.ItemBrand,
                ItemType = item.ItemType
            };
            // Get proposed timestamp for the wrapped item for client with ID clientID
            var proposedTS = proposed_functionalities.GetValueOrDefault(clientID, 0);

            if(proposedTS == 0) {
                _logger.LogError($"ClientID: {clientID} - DateTime 0: Could not find proposed timestamp for client {clientID}");
            }

            lock(discount_items_manual_reset_events) {
                List<ManualResetEvent> manualResetEvents = discount_items_manual_reset_events.GetValueOrDefault((proposedItem, proposedTS), null);
                if(manualResetEvents.Any()) {
                    manualResetEvents.First(x => !x.WaitOne(0)).Set(); // Set the first ManualResetEvent that is not set
                }
                else {
                    _logger.LogError($"ClientID: {clientID} - ManualResetEvent null: Could not find ManualResetEvent for item {proposedItem.ItemName} with proposed timestamp {proposedTS}");
                }
            }
            // TODO: a thread should clear the manual reset events dictionary from time to time to avoid unnecessary memory consumption
        }
    }
    public struct ProposedDiscountItem {
        public string ItemName { get; set; }
        public string ItemBrand { get; set; }
        public string ItemType { get; set; }
    }
}
