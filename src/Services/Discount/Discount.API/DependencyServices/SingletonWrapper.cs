using Google.Protobuf.WellKnownTypes;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.SharedStructs;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
//using NewRelic.Api.Agent;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
public class SingletonWrapper : ISingletonWrapper {
    public ILogger<SingletonWrapper> _logger;
    private readonly ReaderWriterLockSlim dictionaryLock = new ReaderWriterLockSlim(); // Lock used by the Garbage Collection Service to lock the dictionaries

    ConcurrentDictionary<string, ConcurrentBag<DiscountItem>> wrapped_discount_items = new ConcurrentDictionary<string, ConcurrentBag<DiscountItem>>();
    ConcurrentDictionary<ProposedDiscountItem, SynchronizedCollection<(long, string)>> proposed_discount_Items = new ConcurrentDictionary<ProposedDiscountItem, SynchronizedCollection<(long, string)>>();

    ConcurrentDictionary<ProposedDiscountItem, ConcurrentDictionary<string, EventMonitor>> discount_items_manual_reset_events = new ConcurrentDictionary<ProposedDiscountItem, ConcurrentDictionary<string, EventMonitor>>();
    ConcurrentDictionary<string, (ManualResetEvent, SynchronizedCollection<string>)> dependent_client_ids = new ConcurrentDictionary<string, (ManualResetEvent, SynchronizedCollection<string>)>();
    ConcurrentDictionary<string, (ManualResetEvent, string)> committed_Data_MREs = new ConcurrentDictionary<string, (ManualResetEvent, string)>();
    
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
            ProposedDiscountItem proposedItem = new ProposedDiscountItem {
                ItemName = discount_item_to_propose.ItemName,
                ItemBrand = discount_item_to_propose.ItemBrand,
                ItemType = discount_item_to_propose.ItemType,
                Id = discount_item_to_propose.Id
            };

            var EM = new EventMonitor {
                Event = new ManualResetEvent(false),
                ClientID = clientID,
                Timestamp = proposedTS,
            };

            // Generate a GUID for the event
            Guid guid = Guid.NewGuid();
            string guidString = guid.ToString();

            // Create ManualEvent for Discount item
            lock(discount_items_manual_reset_events) {
                discount_items_manual_reset_events.AddOrUpdate(proposedItem,
                    key => new ConcurrentDictionary<string, EventMonitor> { [guidString] = EM },
                    (key, value) => {
                        if (!value.TryAdd(guidString, EM)) { 
                            _logger.LogError($"ClientID: {clientID}, Error: Could not add event monitor to proposed discount item");                        
                        }
                        return value; 
                    });
            }

            lock(proposed_discount_Items) {
                _logger.LogInformation($"ClientID: {clientID} - There are {proposed_discount_Items.Keys.Count} proposed discount Items.");
                proposed_discount_Items.AddOrUpdate(proposedItem, 
                    key => new SynchronizedCollection<(long, string)> (new List<(long, string)> { (proposedTS, clientID) }),
                    (key, value) => { 
                        value.Add((proposedTS, clientID)); 
                        _logger.LogInformation($"ClientID: {clientID} - Added proposed to existing list.");
                        return value;
                });
                _logger.LogInformation($"ClientID: {clientID} - There are {proposed_discount_Items.Keys.Count} proposed discount Items (after).");
            }
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
                ItemType = item.ItemType,
                Id = item.Id
            };
            
            // Remove entry in synchronized collection associated with ProposedItem, for the given ClientID
            lock (proposed_discount_Items) {
                if (proposed_discount_Items.ContainsKey(proposedItem)) {
                    var collection = proposed_discount_Items[proposedItem];
                    foreach((long, string) tuple in collection) {
                        if(tuple.Item2 == clientID) {
                            collection.Remove(tuple);
                            break;
                        }
                    }
                    if(collection.Count == 0) {
                        proposed_discount_Items.TryRemove(proposedItem, out _);
                    }
                }
                else {
                    _logger.LogError($"ClientID: {clientID} - ProposedItem: {proposedItem.Id} - {proposedItem.ItemName} - {proposedItem.ItemBrand} - {proposedItem.ItemType} - not found in proposed_discount_Items");
                }
            }
        }
    }

   //[Trace]
    public List<(string, EventMonitor)> AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp, string clientID) {
        switch (targetTable) {
            case "Discount":
                // Apply all the conditions to the set of proposed discount items, this set will keep shrinking as we apply more conditions.
                // Note that the original set of proposed discount items is not modified, we are just creating a new set with the results of the conditions.
                var filtered_proposed_discount_items2 = new Dictionary<ProposedDiscountItem, SynchronizedCollection<(long, string)>>(this.proposed_discount_Items);
                if (conditions != null) {
                    filtered_proposed_discount_items2 = ApplyFiltersToDiscountItems(conditions, filtered_proposed_discount_items2, clientID);
                }

                var guid_EMsToWait = new List<(string, EventMonitor)>();
                foreach (var proposed_discount_item in filtered_proposed_discount_items2) {
                    var MREsForPropItem_dict = discount_items_manual_reset_events.GetValueOrDefault(proposed_discount_item.Key, null); // Get all the MREs for the proposed item
                    if(MREsForPropItem_dict == null || MREsForPropItem_dict.Keys.Count == 0) {
                        _logger.LogInformation($"ClientID: {clientID} - \t Could not find any ManualResetEvent for catalog item with ID {proposed_discount_item.Key.ItemName}.");
                        continue;
                    }

                    // Get List of MREs (Manual Reset Events) for the proposed items that have a proposed timestamp lower than the reader's timestamp
                    foreach(var guid_EM in MREsForPropItem_dict) {
                        if(guid_EM.Value.Timestamp < readerTimestamp.Ticks) {
                            // Associate the clientID as a dependent of the Proposed Item's MRE
                            lock(dependent_client_ids) {
                                dependent_client_ids.AddOrUpdate(guid_EM.Key,
                                    key => (guid_EM.Value.Event, new SynchronizedCollection<string>(new List<string> { clientID })),
                                    (key, value) => { value.Item2.Add(clientID); return value; });
                            }
                            guid_EMsToWait.Add((guid_EM.Key, guid_EM.Value));
                        }
                    }
                }
                return guid_EMsToWait ?? null;
        }
        return null;
    }

   //[Trace]
    private Dictionary<ProposedDiscountItem, SynchronizedCollection<(long, string)>> ApplyFiltersToDiscountItems(List<Tuple<string, string>> conditions, Dictionary<ProposedDiscountItem, SynchronizedCollection<(long, string)>> filtered_proposed_discount_items2, string clientID) {
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
                case "Id":
                    filtered_proposed_discount_items2 = filtered_proposed_discount_items2.Where(x => x.Key.Id == int.Parse(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                    break;
            }
        }
        return filtered_proposed_discount_items2;
    }

   //[Trace]
    public List<DiscountItem> SingletonGetWrappedDiscountItemsToFlush(string clientID, bool onlyUpdate) {
        // Check the Discount Items associated with the clientID and flush them to the database
        
        var discountItems = new List<DiscountItem>();
        
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
                        DiscountValue = wrappedDiscountItem.DiscountValue,
                        Id = wrappedDiscountItem.Id
                    };
                    discountItems.Add(discountItem);
                }
            }
            return discountItems;
        }
        return null;
    }

    public List<(string, EventMonitor)> RemoveFromManualResetEvents(List<DiscountItem> wrappedItems, string clientID) {
        var EMPairs = new List<(string, EventMonitor)>();
        // Remove the MREs associated with the wrapped items
        foreach (DiscountItem wrappedItem in wrappedItems) {
            ProposedDiscountItem proposedItem = new ProposedDiscountItem {
                ItemName = wrappedItem.ItemName,
                ItemBrand = wrappedItem.ItemBrand,
                ItemType = wrappedItem.ItemType,
                Id = wrappedItem.Id
            };

            lock(discount_items_manual_reset_events) {
                if (discount_items_manual_reset_events.TryGetValue(proposedItem, out var EM_dict)) {
                    var EMsToRemove = EM_dict.Where(EM => EM.Value.ClientID == clientID).ToList();
                    EMPairs = EMsToRemove.Select(EM => (EM.Key, EM.Value)).ToList();
                    foreach (var EMPair in EMPairs) {
                        if (!EM_dict.TryRemove(EMPair.Item1, out var EM)) {
                            _logger.LogError($"ClientID: {clientID} - Could not remove EM for catalog item with ID {proposedItem.ItemName}, from Discount Items Manual Reset Events, by client {EM.ClientID}, GUID={EMPair.Item1}");
                        }
                    }
                    if (EM_dict.Count == 0) {
                        if(!discount_items_manual_reset_events.TryRemove(proposedItem, out var dict)) {
                            _logger.LogError($"ClientID: {clientID} - Could not remove Discount Item with ID {proposedItem.ItemName}, from Catalog Items Manual Reset Events");
                        }
                    }
                }
            }
        }
        return EMPairs;
    }

    public void AddToCommittedDataMREs(List<(string, EventMonitor)> guid_MREs, string clientID) {
        // Add the MREs to the committed data MREs, so that the garbage collection service can dispose them
        foreach (var guid_MRE in guid_MREs) {
            lock(committed_Data_MREs) {
                committed_Data_MREs.AddOrUpdate(guid_MRE.Item1,
                    key => (guid_MRE.Item2.Event, clientID),
                    (key, value) => { value = (guid_MRE.Item2.Event, clientID); _logger.LogError($"ClientID: {clientID} - The GUID={guid_MRE.Item1}, was found to be duplicate."); return value; });
            }
        }
    }

    public void DisposeCommittedDataMREs() {
        // Dispose the MREs associated with the committed data only if there is currently no other client waiting on them
        lock (committed_Data_MREs) {
            int numberOfActiveMREs = 0;
            foreach (var guid_MRE in committed_Data_MREs) {
                var dependentClientIDs = dependent_client_ids.GetValueOrDefault(guid_MRE.Key, (null, null));
                // Either there is no one waiting on this MRE (and never was), or there are / used to be clients waiting on it, but they have all been removed
                if (dependentClientIDs.Item1 == null || dependentClientIDs.Item2.Count == 0) {
                    // There are no other clients waiting on this MRE, so we can dispose it
                    guid_MRE.Value.Item1.Dispose();
                    if (!committed_Data_MREs.TryRemove(guid_MRE.Key, out var kvp)) {
                        _logger.LogError($"Garbage Collector: \t Could not remove MRE for committed data with client ID {guid_MRE.Value.Item2}");
                    }
                }
                else {
                    _logger.LogInformation($"Garbage Collector: \t Not disposing MRE for committed data with client ID {guid_MRE.Value.Item2}, because there are {dependentClientIDs.Item2.Count} clients waiting on it");
                    numberOfActiveMREs++;
                }
            }
            _logger.LogInformation($"Garbage Collector: Number of active MREs: {numberOfActiveMREs}");
        }
    }

   //[Trace]
    public void CleanWrappedObjects(string clientID) {
        // _logger.LogInformation($"Cleaning wrapped objects for client {clientID} ...");
        // Clean up the proposed items;
        SingletonRemoveWrappedItemsFromProposedSet(clientID);

        // Start garbage collection process
        var wrappedItem = SingletonGetWrappedDiscountItemsToFlush(clientID, false);
        var MREsToDispose = RemoveFromManualResetEvents(wrappedItem, clientID);
        AddToCommittedDataMREs(MREsToDispose, clientID);

        // Clean up wrapped objects;
        SingletonRemoveFunctionalityObjects(clientID);

        // Clean up the proposed functionality state
        SingletonRemoveProposedFunctionality(clientID);
    }

    public void NotifyReaderThreads(string clientID, List<DiscountItem> committedItems) {
        foreach (DiscountItem item in committedItems) {
            ProposedDiscountItem proposedItem = new ProposedDiscountItem {
                ItemName = item.ItemName,
                ItemBrand = item.ItemBrand,
                ItemType = item.ItemType,
                Id = item.Id
            };
            // Get proposed timestamp for the wrapped item for client with ID clientID
            var proposedTS = proposed_functionalities.GetValueOrDefault(clientID, 0);

            if (proposedTS == 0) {
                _logger.LogError($"ClientID: {clientID} - DateTime 0: Could not find proposed timestamp for client {clientID}");
            }
            lock (discount_items_manual_reset_events) {
                var EMs_PropItem_dict = discount_items_manual_reset_events.GetValueOrDefault(proposedItem, null);
                if (EMs_PropItem_dict == null || EMs_PropItem_dict.Keys.Count == 0) {
                    _logger.LogError($"ClientID: {clientID} - A: Could not find any ManualResetEvent for discount item with ID {proposedItem.ItemName} with proposed TS {proposedTS}");
                }

                // Find the MRE associated with the notifier ClientID
                var guid_EM = EMs_PropItem_dict.Where(EM => EM.Value.ClientID == clientID).Single();
                guid_EM.Value.Event.Set(); // Set the first ManualResetEvent that is not set yet
            }
        }
    }

     public void RemoveFromDependencyList((string, EventMonitor) guid_EM, string clientID) {
        string guid = guid_EM.Item1;
        // Remove the clientID from the list of dependent client IDs for the MRE
        lock(dependent_client_ids) {
            if (dependent_client_ids.TryGetValue(guid, out var MRE_depends)) {
                MRE_depends.Item2.Remove(clientID);
            }
            else {
                _logger.LogError($"ClientID: {clientID} - Unable to get MRE dependencies");
            }
        }
    }
    
    public struct ProposedDiscountItem : IEquatable<ProposedDiscountItem> {
        public string ItemName { get; set; }
        public string ItemBrand { get; set; }
        public string ItemType { get; set; }
        public int Id { get; set; }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ProposedDiscountItem && Equals((ProposedDiscountItem)obj);
        }

        public bool Equals(ProposedDiscountItem other) {
            return ItemName.Equals(other.ItemName) && ItemBrand.Equals(other.ItemBrand) && ItemType.Equals(other.ItemType) && Id == other.Id;
        }

        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 23 + ItemName.GetHashCode();
                hash = hash * 23 + ItemBrand.GetHashCode();
                hash = hash * 23 + ItemType.GetHashCode();
                hash = hash * 23 + Id.GetHashCode();
                return hash;
            }
        }
    }
}
