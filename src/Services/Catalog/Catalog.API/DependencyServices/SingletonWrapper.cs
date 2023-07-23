using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;
//using NewRelic.Api.Agent;
using System.Threading;
using System.Diagnostics;
using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.SharedStructs;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        // Benchmarking stuff for code spans (function level)
        public static ConcurrentBag<TimeSpan> Timespans = new ConcurrentBag<TimeSpan>();

        public static TimeSpan Average(IEnumerable<TimeSpan> spans) {
            return TimeSpan.FromSeconds(spans.Select(s => s.TotalSeconds).Average());
        }

        public ILogger<SingletonWrapper> _logger;
        private readonly ReaderWriterLockSlim dictionaryLock = new ReaderWriterLockSlim(); // Lock used by the Garbage Collection Service to lock the dictionaries

        ConcurrentDictionary<string, ConcurrentBag<CatalogItem>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<CatalogItem>>();
        ConcurrentDictionary<string, ConcurrentBag<CatalogType>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<CatalogType>>();
        ConcurrentDictionary<string, ConcurrentBag<CatalogBrand>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<CatalogBrand>>();

        ConcurrentDictionary<ProposedCatalogItem, SynchronizedCollection<(long, string)>> proposed_catalog_items = new ConcurrentDictionary<ProposedCatalogItem, SynchronizedCollection<(long, string)>>();
        ConcurrentDictionary<ProposedCatalogBrand, ConcurrentDictionary<long, string>> proposed_catalog_brands = new ConcurrentDictionary<ProposedCatalogBrand, ConcurrentDictionary<long, string>>();
        ConcurrentDictionary<ProposedCatalogType, ConcurrentDictionary<long, string>> proposed_catalog_types = new ConcurrentDictionary<ProposedCatalogType, ConcurrentDictionary<long, string>>();

        //ConcurrentDictionary<ProposedCatalogItem, ConcurrentDictionary<EventMonitor, int>> catalog_items_manual_reset_events = new ConcurrentDictionary<ProposedCatalogItem, ConcurrentDictionary<EventMonitor, int>>(); // Holds the MonitorEvent for each proposed catalog item
        //ConcurrentDictionary<ManualResetEvent, ConcurrentDictionary<string, int>> dependent_client_ids = new ConcurrentDictionary<ManualResetEvent, ConcurrentDictionary<string, int>>(); // Holds which clients are waiting on a MRE
        //ConcurrentDictionary<ManualResetEvent, string> committed_Data_MREs = new ConcurrentDictionary<ManualResetEvent, string>(); // Holds the MREs that are associated with committed data (for garbage collection-purposes)
        ConcurrentDictionary<ProposedCatalogItem, ConcurrentDictionary<string, EventMonitor>> catalog_items_manual_reset_events = new ConcurrentDictionary<ProposedCatalogItem, ConcurrentDictionary<string, EventMonitor>>(); // Holds the MonitorEvent for each proposed catalog item
        ConcurrentDictionary<string, (ManualResetEvent, SynchronizedCollection<string>)> dependent_client_ids = new ConcurrentDictionary<string, (ManualResetEvent, SynchronizedCollection<string>)>(); // Holds which clients are waiting on a MRE
        ConcurrentDictionary<string, (ManualResetEvent, string)> committed_Data_MREs = new ConcurrentDictionary<string, (ManualResetEvent, string)>(); // Holds the MREs that are associated with committed data (for garbage collection-purposes)

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
        public ConcurrentBag<object[]> SingletonGetCatalogItems(string key) {
            // Get the wrapped catalog items associated with the clientID
            var bag = wrapped_catalog_items.GetValueOrDefault(key, new ConcurrentBag<CatalogItem>());

            // Convert the wrapped catalog items to a list of object[]
            var list = new ConcurrentBag<object[]>();
            foreach (var item in bag) {
                list.Add(new object[] {
                    item.Id,
                    item.CatalogBrandId,
                    item.CatalogTypeId,
                    item.Description,
                    item.Name,
                    item.PictureFileName,
                    item.Price,
                    item.AvailableStock,
                    item.MaxStockThreshold,
                    item.OnReorder,
                    item.RestockThreshold
                });
            }
            return list;
        }

        //[Trace]
        public ConcurrentBag<object[]> SingletonGetCatalogTypes(string key) {
            // Get the wrapped catalog types associated with the clientID
            var bag = wrapped_catalog_types.GetValueOrDefault(key, new ConcurrentBag<CatalogType>());
            
            // Convert the wrapped catalog types to a list of object[]
            var list = new ConcurrentBag<object[]>();
            foreach (var item in bag) {
                list.Add(new object[] {
                    item.Id,
                    item.Type
                });
            }
            return list;
        }

        //[Trace]
        public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key) {
            // Get the wrapped catalog brands associated with the clientID
            var bag = wrapped_catalog_brands.GetValueOrDefault(key, new ConcurrentBag<CatalogBrand>());

            var list = new ConcurrentBag<object[]>();
            foreach (var item in bag) {
                list.Add(new object[] {
                    item.Id,
                    item.Brand
                });
            }
            return list;
        }

        //[Trace]
        public bool SingletonGetTransactionState(string clientID) {
            if(!transaction_state.ContainsKey(clientID)) {
                // Initialize a new state: uncommitted = false; to be commit = true;
                transaction_state[clientID] = false;
            }
            return transaction_state.GetValueOrDefault(clientID);
        }

        //[Trace]
        public void SingletonAddCatalogItem(string clientID, IEnumerable<object[]> values) {
            foreach (object[] item in values) {
                // Create a new Wrapped Catalog Item
                var wrapped_item = new CatalogItem {
                    Id = (int)item[0],
                    CatalogBrandId = (int)item[1],
                    CatalogTypeId = (int)item[2],
                    Description = (string)item[3],
                    Name = (string)item[4],
                    PictureFileName = (string)item[5],
                    Price = (decimal)item[6],
                    AvailableStock = (int)item[7],
                    MaxStockThreshold = (int)item[8],
                    OnReorder = (bool)item[9],
                    RestockThreshold = (int)item[10]
                };
                _logger.LogInformation($"ClientID: {clientID} - the catalogItem id: {wrapped_item.Id} when inserting into the wrapper.");
                wrapped_catalog_items.AddOrUpdate(clientID, new ConcurrentBag<CatalogItem> { wrapped_item }, (key, bag) => {
                    bag.Add(wrapped_item);
                    return bag;
                });
            }
        }

        //[Trace]
        public void SingletonAddCatalogType(string clientID, IEnumerable<object[]> values) {
            foreach (object[] type in values) {
                // Create a new Wrapped Catalog Type
                var wrapped_type = new CatalogType {
                    Id = (int)type[0],
                    Type = (string)type[1]
                };
                wrapped_catalog_types.AddOrUpdate(clientID, new ConcurrentBag<CatalogType> { wrapped_type }, (key, bag) => {
                    bag.Add(wrapped_type);
                    return bag;
                });
            }
        }

        //[Trace]
        public void SingletonAddCatalogBrand(string clientID, IEnumerable<object[]> values) {
            foreach (object[] brand in values) {
                // Create a new Wrapped Catalog Brand
                var wrapped_brand = new CatalogBrand {
                    Id = (int)brand[0],
                    Brand = (string)brand[1]
                };
                wrapped_catalog_brands.AddOrUpdate(clientID, new ConcurrentBag<CatalogBrand> { wrapped_brand }, (key, bag) => {
                    bag.Add(wrapped_brand);
                    return bag;
                });
            }
        }

        //[Trace]
        public bool SingletonSetTransactionState(string clientID, bool state) {
            return transaction_state[clientID] = state;
        }


        // Remove the objects associated with the clientID
        //[Trace]
        public void SingletonRemoveFunctionalityObjects(string clientID) {
            wrapped_catalog_brands.TryRemove(clientID, out _);
            wrapped_catalog_types.TryRemove(clientID, out _);
            wrapped_catalog_items.TryRemove(clientID, out _);
            transaction_state.TryRemove(clientID, out bool _);
            // _logger.LogInformation($"Number of elements in the wrapped catalog items 2: {wrapped_catalog_items2.Count}, and transaction states: {transaction_state.Count}");
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
            ConcurrentBag<CatalogItem> catalog_items_to_propose = wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<CatalogItem>());
            ConcurrentBag<CatalogType> catalog_types_to_propose = wrapped_catalog_types.GetValueOrDefault(clientID, new ConcurrentBag<CatalogType>());
            ConcurrentBag<CatalogBrand> catalog_brands_to_propose = wrapped_catalog_brands.GetValueOrDefault(clientID, new ConcurrentBag<CatalogBrand>());

            // For each item, add it to the proposed set, adding the proposed timestamp associated
            foreach (CatalogItem catalog_item_to_propose in catalog_items_to_propose) {
                _logger.LogInformation($"ClientID: {clientID} - is proposing {catalog_items_to_propose.Count} items.");
                foreach(var item in catalog_items_to_propose) {
                    _logger.LogInformation($"ClientID: {clientID} - \t adding catalogItem id: {item.Id} to proposed set.");
                }
                ProposedCatalogItem proposedItem = new ProposedCatalogItem {
                    Name = catalog_item_to_propose.Name,
                    CatalogBrandId = catalog_item_to_propose.CatalogBrandId,
                    CatalogTypeId = catalog_item_to_propose.CatalogTypeId,
                    Id = catalog_item_to_propose.Id
                };

                if(proposed_catalog_items.ContainsKey(proposedItem)) {
                    _logger.LogInformation($"ClientID: {clientID} - \t proposed catalog item already exists. Before adding count = {proposed_catalog_items[proposedItem].Count}");
                } else {
                    _logger.LogInformation($"ClientID: {clientID} - \t proposed catalog does not exist yet.");
                }
                _logger.LogInformation($"ClientID: {clientID} - \t .");
                proposed_catalog_items.AddOrUpdate(proposedItem, new SynchronizedCollection<(long, string)>( new List<(long, string)> { (proposedTS, clientID) }), (key, value) => {
                    value.Add((proposedTS, clientID));
                    return value;
                });

                _logger.LogInformation($"ClientID: {clientID} - \t added catalog item to proposed set. After adding count = {proposed_catalog_items[proposedItem].Count}");

                var EM = new EventMonitor {
                    Event = new ManualResetEvent(false),
                    ClientID = clientID,
                    Timestamp = proposedTS,
                };

                // Generate a GUID for the event
                Guid guid = Guid.NewGuid();
                string guidString = guid.ToString();

                // Create ManualEvent for catalog Item
                catalog_items_manual_reset_events.AddOrUpdate(proposedItem, new ConcurrentDictionary<string, EventMonitor>(new KeyValuePair<string, EventMonitor>[] { new KeyValuePair<string, EventMonitor>(guidString, EM) } ), (_, value) => {
                    if(value.TryAdd(guidString, EM)) {
                        _logger.LogInformation($"ClientID: {clientID} - \t Added ManualResetEvent for catalog item with parameters: {proposedItem.Name} - {proposedItem.CatalogBrandId} - {proposedItem.CatalogTypeId} - {proposedItem.Id} with GUID: {guidString}");
                    }
                    else {
                        _logger.LogError($"ClientID: {clientID} - \t Could not add ManualResetEvent for catalog item with parameters: {proposedItem.Name} - {proposedItem.CatalogBrandId} - {proposedItem.CatalogTypeId} - {proposedItem.Id} with GUID: {guidString}");
                    }
                    return value;
                });
            }

            foreach (CatalogType catalog_type_to_propose in catalog_types_to_propose) {
                ProposedCatalogType proposedCatalogType = new ProposedCatalogType {
                    TypeName = catalog_type_to_propose.Type
                };
                
                proposed_catalog_types.AddOrUpdate(proposedCatalogType, _ => {
                    var innerDict = new ConcurrentDictionary<long, string>();
                    innerDict.TryAdd(proposedTS, clientID);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(proposedTS, clientID);
                    return value;
                });
            }

            foreach (CatalogBrand catalog_brand_to_propose in catalog_brands_to_propose) {
                ProposedCatalogBrand proposedCatalogType = new ProposedCatalogBrand {
                    BrandName = catalog_brand_to_propose.Brand
                };
                //ProposedBrand newBrand = new ProposedBrand(catalog_brand_to_propose.BrandName);
                proposed_catalog_brands.AddOrUpdate(proposedCatalogType, _ => {
                    var innerDict = new ConcurrentDictionary<long, string>();
                    innerDict.TryAdd(proposedTS, clientID);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(proposedTS, clientID);
                    return value;
                });
            }
        }

        //[Trace]
        public void SingletonRemoveWrappedItemsFromProposedSet(string clientID) {
            // TODO: Possible optimization: add structure that indexes proposed objects by the client ID that proposed them

            // Get the proposed objects from the wrapper with given clientID
            ConcurrentBag<CatalogItem> catalog_items_to_remove = wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<CatalogItem>());
            ConcurrentBag<CatalogBrand> catalog_brands_to_remove = wrapped_catalog_brands.GetValueOrDefault(clientID, new ConcurrentBag<CatalogBrand>());
            ConcurrentBag<CatalogType> catalog_types_to_remove = wrapped_catalog_types.GetValueOrDefault(clientID, new ConcurrentBag<CatalogType>());

            // Remove entries from the proposed set. These are identified by their key table identifiers.
            foreach(CatalogItem item in catalog_items_to_remove) {
                _logger.LogInformation($"ClientID: {clientID} - the catalogItem id: {item.Id}");
                ProposedCatalogItem proposedItem = new ProposedCatalogItem {
                    Name = item.Name,
                    CatalogBrandId = item.CatalogBrandId,
                    CatalogTypeId = item.CatalogTypeId,
                    Id = item.Id
                };
                // Remove entry in synchronized collection associated with ProposedItem, for the given clientID
                lock (proposed_catalog_items[proposedItem]) {
                    _logger.LogInformation($"ClientID: {clientID} - \t removing catalog item from proposed set. Before removing count = {proposed_catalog_items[proposedItem].Count}");
                    foreach((long, string) tuple in proposed_catalog_items[proposedItem]) {
                        if(tuple.Item2 == clientID) {
                            proposed_catalog_items[proposedItem].Remove(tuple);
                            break;
                        }
                    }
                    _logger.LogInformation($"ClientID: {clientID} - \t removing catalog item from proposed set. After removing count = {proposed_catalog_items[proposedItem].Count}");
                }
            }
            foreach(CatalogBrand brand in catalog_brands_to_remove) {
                ProposedCatalogBrand proposedBrand = new ProposedCatalogBrand {
                    BrandName = brand.Brand
                };
                // Remove each brand from the proposed set
                proposed_catalog_brands.TryRemove(proposedBrand, out _);
            }
            foreach(CatalogType type in catalog_types_to_remove) {
                ProposedCatalogType proposedType = new ProposedCatalogType {
                    TypeName = type.Type
                };
                // Remove each type from the proposed set
                proposed_catalog_types.TryRemove(proposedType, out _);
            }
        }

        //[Trace]
        public List<(string, EventMonitor)> AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp, string clientID) {
            switch (targetTable) {
                case "Catalog":
                    // Apply all the conditions to the set of proposed catalog items 2, this set will keep shrinking as we apply more conditions.
                    // Note that the original set of proposed catalog items 2 is not modified, we are just creating a new set with the results of the conditions.
                    var filtered_proposed_catalog_items2 = new Dictionary<ProposedCatalogItem, SynchronizedCollection<(long, string)>>(this.proposed_catalog_items);
                    _logger.LogInformation($"ClientID: {clientID} - Number of proposed catalog items 2: {filtered_proposed_catalog_items2.Count}");
                    if (conditions != null) {
                        filtered_proposed_catalog_items2 = ApplyFiltersToCatalogItems(conditions, filtered_proposed_catalog_items2, clientID);
                    }
                    
                    var guid_EMsToWait = new List<(string, EventMonitor)>();
                    _logger.LogInformation($"ClientID: {clientID} - Number of proposed catalog items 2 after applying filters: {filtered_proposed_catalog_items2.Count}");
                    
                    // Iterate the list of interesting proposed_catalog_items
                    foreach (var proposed_catalog_item in filtered_proposed_catalog_items2) {
                        _logger.LogInformation($"ClientID: {clientID} - \t Searching for MREs associated with catalog item with parameters: {proposed_catalog_item.Key.Name} - {proposed_catalog_item.Key.CatalogBrandId} - {proposed_catalog_item.Key.CatalogTypeId} - {proposed_catalog_item.Key.Id}");
                        _logger.LogInformation($"ClientID: {clientID} - \t Number of MREs associated with this item: {proposed_catalog_item}");
                        
                        // Iterate the list of MREs
                        foreach(var kvp_proposed_EM_list in catalog_items_manual_reset_events) {
                            _logger.LogInformation($"ClientID: {clientID} - \t \t There is a list of MRE for item: {kvp_proposed_EM_list.Key.Name} - {kvp_proposed_EM_list.Key.CatalogBrandId} - {kvp_proposed_EM_list.Key.CatalogTypeId} - {kvp_proposed_EM_list.Key.Id}");
                            _logger.LogInformation($"ClientID: {clientID} - \t \t Number of MREs for this item: {kvp_proposed_EM_list.Value.Count}");
                            // var kvps = kvp_proposed_EM_list.Value.ToArray();
                            // foreach(var kvp in kvps) {
                            //     _logger.LogInformation($"ClientID: {clientID} - \t \t \t \t MRE timestamp: {kvp.Value.Timestamp}, proposed by client: {kvp.Value.ClientID}, GUID = {kvp.Key}");
                            // }
                        }
                        // There is at least one proposed catalog item 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                        var MREsForPropItem_dict = catalog_items_manual_reset_events.GetValueOrDefault(proposed_catalog_item.Key, null); // Get all the MREs for the proposed item
                        // var proposed_catalog_item_1 = proposed_catalog_item;
                        // var proposed_catalog_item_2 = 
                        if(MREsForPropItem_dict == null || MREsForPropItem_dict.Keys.Count == 0) {
                            _logger.LogError($"ClientID: {clientID} - \t Could not find any ManualResetEvent for catalog item with ID {proposed_catalog_item.Key.Name}");
                        }

                        // Get the list of MREs (Manual Reset Events) for the proposed items that have a proposed timestamp lower than the reader's timestamp
                        foreach(var guid_EM in MREsForPropItem_dict) {
                            _logger.LogInformation($"ClientID: {clientID} - \t MRE timestamp: {guid_EM.Value.Timestamp}, proposed by client: {guid_EM.Value.ClientID}, is older than own timestamp: {readerTimestamp.Ticks}? {guid_EM.Value.Timestamp < readerTimestamp.Ticks}, GUID={guid_EM.Key}");
                            if(guid_EM.Value.Timestamp < readerTimestamp.Ticks) {
                                _logger.LogInformation($"ClientID: {clientID} - \t Added MRE timestamp: {guid_EM.Value.Timestamp}, proposed by client: {guid_EM.Value.ClientID}, to the list of MREs to wait for.");
                                guid_EMsToWait.Add((guid_EM.Key, guid_EM.Value));
                                
                                // Associate the clientID as a dependent of the Proposed Item's MRE
                                dependent_client_ids.AddOrUpdate(guid_EM.Key, (guid_EM.Value.Event, new SynchronizedCollection<string>(new List<string>{ clientID } )) , (_, value) => {
                                    value.Item2.Add(clientID);
                                    _logger.LogInformation($"ClientID: {clientID} - \t Added (Update) clientID: {clientID} to the list of dependent client IDs for the MRE of client {guid_EM.Value.ClientID}, GUID={guid_EM.Key}");
                                    return value;
                                });
                            }
                        }
                    }
                    _logger.LogInformation($"ClientID: {clientID} - \t Total Number of MREs to wait for: {guid_EMsToWait.Count}");
                    return guid_EMsToWait ?? null;
                case "CatalogBrand":
                    var filtered_proposed_catalog_brands2 = new Dictionary<ProposedCatalogBrand, ConcurrentDictionary<long, string>>(this.proposed_catalog_brands);
                    if (conditions != null) {
                        filtered_proposed_catalog_brands2 = ApplyFiltersToCatalogBrands(conditions, filtered_proposed_catalog_brands2);
                    }
                    // Check if the there any proposed catalog brands 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<ProposedCatalogBrand, ConcurrentDictionary<long, string>> proposed_catalog_brand in filtered_proposed_catalog_brands2) {
                        foreach (long proposedTS in proposed_catalog_brand.Value.Keys) {
                            if (proposedTS < readerTimestamp.Ticks) {
                                // There is at least one proposed catalog brand 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                // TODO: Complete this method
                                return null;
                            }
                        }
                    }

                    break;
                case "CatalogType":
                    var filtered_proposed_catalog_types2 = new Dictionary<ProposedCatalogType, ConcurrentDictionary<long, string>>(this.proposed_catalog_types);
                    if (conditions != null) {
                        filtered_proposed_catalog_types2 = ApplyFiltersToCatalogTypes(conditions, filtered_proposed_catalog_types2);
                    }
                    // Check if the there any proposed catalog types 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<ProposedCatalogType, ConcurrentDictionary<long, string>> proposed_catalog_type in filtered_proposed_catalog_types2) {
                        foreach (long proposedTS in proposed_catalog_type.Value.Keys) {
                            if (proposedTS < readerTimestamp.Ticks) {
                                // There is at least one proposed catalog type 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                // TODO: Complete this method
                                return null;
                            }
                        }
                    }
                    break;
            }
            return null;
        }

        //[Trace]
        private static Dictionary<ProposedCatalogType, ConcurrentDictionary<long, string>> ApplyFiltersToCatalogTypes(List<Tuple<string, string>> conditions, Dictionary<ProposedCatalogType, ConcurrentDictionary<long, string>> filtered_proposed_catalog_types2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_types2 = filtered_proposed_catalog_types2.Where(x => x.Key.TypeName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_types2;
        }

        //[Trace]
        private static Dictionary<ProposedCatalogBrand, ConcurrentDictionary<long, string>> ApplyFiltersToCatalogBrands(List<Tuple<string, string>> conditions, Dictionary<ProposedCatalogBrand, ConcurrentDictionary<long, string>> filtered_proposed_catalog_brands2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_brands2 = filtered_proposed_catalog_brands2.Where(x => x.Key.BrandName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_brands2;
        }

        //[Trace]
        private Dictionary<ProposedCatalogItem, SynchronizedCollection<(long, string)>> ApplyFiltersToCatalogItems(List<Tuple<string, string>> conditions, Dictionary<ProposedCatalogItem, SynchronizedCollection<(long, string)>> filtered_proposed_catalog_items2, string clientID) {
            foreach (Tuple<string, string> condition in conditions) {
                _logger.LogInformation($"ClientID: {clientID} - Condition: {condition.Item1} - {condition.Item2}");
                switch (condition.Item1) {
                    case "Name":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.Name == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "CatalogTypeId":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.CatalogTypeId == int.Parse(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "CatalogBrandId":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.CatalogBrandId == int.Parse(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "Id":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.Id == int.Parse(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    default:
                        break;
                }
            }
            foreach (var item in filtered_proposed_catalog_items2) {
                _logger.LogInformation($"ClientID: {clientID} - Proposed Item: {item.Key.Name} - {item.Key.CatalogBrandId} - {item.Key.CatalogTypeId}");
            }
            return filtered_proposed_catalog_items2;
        }

        //[Trace]
        public List<CatalogItem> SingletonGetWrappedCatalogItemsToFlush(string clientID, bool onlyUpdate) {
            // Check the Catalog Items, Brands, and Types associated with the clientID and flush them to the database
            
            var catalogItems = new List<CatalogItem>();
            
            // Console.WriteLine("Getting proposed data to flush from client: " + clientID + " ...");
            if (wrapped_catalog_items.TryGetValue(clientID, out ConcurrentBag<CatalogItem> clientCatalogItems)) {
                foreach (CatalogItem wrappedCatalogItem in clientCatalogItems) {
                    if (onlyUpdate) {
                        catalogItems.Add(wrappedCatalogItem);
                    }
                    else {
                        CatalogItem catalogItem = new CatalogItem {
                            Name = wrappedCatalogItem.Name,
                            Description = wrappedCatalogItem.Description,
                            Price = wrappedCatalogItem.Price,
                            PictureFileName = wrappedCatalogItem.PictureFileName,
                            CatalogTypeId = wrappedCatalogItem.CatalogTypeId,
                            CatalogBrandId = wrappedCatalogItem.CatalogBrandId,
                            AvailableStock = wrappedCatalogItem.AvailableStock,
                            RestockThreshold = wrappedCatalogItem.RestockThreshold,
                            MaxStockThreshold = wrappedCatalogItem.MaxStockThreshold,
                            OnReorder = wrappedCatalogItem.OnReorder,
                            Id = wrappedCatalogItem.Id
                        };
                        // If we pass the original catalog item, with the set ID, will it work? Test it.
                        catalogItems.Add(catalogItem);
                    }
                }
                return catalogItems;
            }
            return null;
        }

        public List<(string, EventMonitor)> RemoveFromManualResetEvents(List<CatalogItem> wrappedItems, string clientID) {
            // Remove the MREs associated with the wrapped items, so that no other client will wait on them (they have been committed)

            // Remove the MREs associated with the wrapped items
            foreach (CatalogItem wrappedItem in wrappedItems) {
                ProposedCatalogItem proposedItem = new ProposedCatalogItem {
                    Name = wrappedItem.Name,
                    CatalogBrandId = wrappedItem.CatalogBrandId,
                    CatalogTypeId = wrappedItem.CatalogTypeId,
                    Id = wrappedItem.Id
                };

                if (catalog_items_manual_reset_events.TryGetValue(proposedItem, out var EM_dict)) {
                    var EMsToRemove = EM_dict.Where(kvp => kvp.Value.ClientID == clientID).ToList();
                    var EMPairs = new List<(string, EventMonitor)>();
                    foreach(var kvp in EMsToRemove) {
                        EMPairs.Add((kvp.Key, kvp.Value));
                        if(EM_dict.TryRemove(kvp.Key, out var EM)) {
                            _logger.LogInformation($"ClientID: {clientID} - Removed EM for catalog item with ID {proposedItem.Name}, from Catalog Items Manual Reset Events, by client {EM.ClientID}, GUID={kvp.Key}");
                        } else {
                            _logger.LogError($"ClientID: {clientID} - Could not remove EM for catalog item with ID {proposedItem.Name}, from Catalog Items Manual Reset Events, by client {EM.ClientID}, GUID={kvp.Key}");
                        }
                    }
                    return EMPairs;
                } else {
                    _logger.LogError($"ClientID: {clientID} - Could not find any ManualResetEvent associated with catalog item with ID {proposedItem.Name}, to remove from Catalog Items Manual Reset Events");
                }

            }
            return new List<(string, EventMonitor)>();
        }

        public void AddToCommittedDataMREs(List<(string,EventMonitor)> guid_MREs, string clientID) {
            // Add the MREs to the committed data MREs, so that the garbage collection service can dispose them
            dictionaryLock.EnterWriteLock();
            try {
                foreach (var guid_MRE in guid_MREs) {
                    committed_Data_MREs.AddOrUpdate(guid_MRE.Item1, (guid_MRE.Item2.Event, clientID), (_, value) => {
                        value = (guid_MRE.Item2.Event, clientID);
                        _logger.LogError($"ClientID: {clientID} - The GUID={guid_MRE.Item1}, was found to be duplicate.");
                        return value;
                    });
                }
            } finally {
                dictionaryLock.ExitWriteLock();
            }
        }

        public void DisposeCommittedDataMREs() {
            // Dispose the MREs associated with the committed data only if there is currently no other client waiting on them
            dictionaryLock.EnterWriteLock();
            try {
                int numberOfActiveMREs = 0;
                foreach (var guid_MRE in committed_Data_MREs) {
                    var dependentClientIDs = dependent_client_ids.GetValueOrDefault(guid_MRE.Key, (null, null));
                    if(dependentClientIDs.Item1 == null || dependentClientIDs.Item2.Count == 0)  {
                        _logger.LogInformation($"Garbage Collector: \t Disposing MRE for committed data with client ID {guid_MRE.Value.Item2}...");
                        // There are no other clients waiting on this MRE, so we can dispose it
                        guid_MRE.Value.Item1.Dispose();
                        if(committed_Data_MREs.TryRemove(guid_MRE.Key, out var kvp)) {
                            _logger.LogInformation($"Garbage Collector: \t Removed MRE for committed data with client ID {guid_MRE.Value.Item2}");
                        }
                        else {
                            _logger.LogInformation($"Garbage Collector: \t Could not remove MRE for committed data with client ID {guid_MRE.Value.Item2}");
                        }
                    } else {
                        _logger.LogInformation($"Garbage Collector: \t Not disposing MRE for committed data with client ID {guid_MRE.Value.Item2} because there are {dependentClientIDs.Item2.Count} clients waiting on it:");
                        var dependentClients = dependentClientIDs.Item2.ToList();
                        foreach(string clientID in dependentClients) {
                            _logger.LogInformation($"Garbage Collector: \t \t Client ID {clientID} is waiting on this MRE from client ID {guid_MRE.Value.Item2}");
                        }
                        numberOfActiveMREs++;
                    }
                }
                _logger.LogInformation($"Garbage Collector: Number of active MREs: {numberOfActiveMREs}");
            } finally {
                dictionaryLock.ExitWriteLock();
            }
        }

        //[Trace]
        public void CleanWrappedObjects(string clientID) {
            // _logger.LogInformation($"Cleaning wrapped objects for client {clientID} ...");
            // Clean up the proposed items;
            SingletonRemoveWrappedItemsFromProposedSet(clientID);

            // Start garbage collection process
            var wrappedItems = SingletonGetWrappedCatalogItemsToFlush(clientID, false);
            var guid_MREs = RemoveFromManualResetEvents(wrappedItems, clientID);
            AddToCommittedDataMREs(guid_MREs, clientID);

            // Clean up wrapped objects;
            SingletonRemoveFunctionalityObjects(clientID);

            // Clean up the proposed functionality state
            SingletonRemoveProposedFunctionality(clientID);

            
        }

        //[Trace]
        public void NotifyReaderThreads(string clientID, List<CatalogItem> committedItems) {
            _logger.LogInformation($"ClientID: {clientID} - Notifying reader threads waiting on committed data ({committedItems.Count} items) ...");
            foreach(CatalogItem item in committedItems) {
                ProposedCatalogItem proposedItem = new ProposedCatalogItem {
                    CatalogBrandId = item.CatalogBrandId,
                    CatalogTypeId = item.CatalogTypeId,
                    Name = item.Name,
                    Id = item.Id
                };
                // Get proposed timestamp for the wrapped item for client with ID clientID
                var proposedTS = proposed_functionalities.GetValueOrDefault(clientID, 0);

                if(proposedTS == 0) {
                    _logger.LogError($"ClientID: {clientID} - DateTime 0: Could not find proposed timestamp for client {clientID}");
                }

                _logger.LogInformation($"ClientID: {clientID} - Searching for EMs associated with catalog item with parameters: {proposedItem.Name} - {proposedItem.CatalogBrandId} - {proposedItem.CatalogTypeId} - {proposedItem.Id}");
                var EMs_PropItem_dict = catalog_items_manual_reset_events.GetValueOrDefault(proposedItem, null);
                if(EMs_PropItem_dict == null || EMs_PropItem_dict.Keys.Count == 0) {
                    _logger.LogError($"ClientID: {clientID} - A: Could not find any ManualResetEvent for catalog item with ID {proposedItem.Name} with proposed TS {proposedTS}");
                }

                // Find the MRE associated with the notifier ClientID
                var guid_EM = EMs_PropItem_dict.Where(kvp => kvp.Value.ClientID == clientID).Single();
                _logger.LogInformation($"ClientID: {clientID} - Found MRE for Proposed item with ID {proposedItem.Name} with proposed TS {proposedTS}. Notifying Event from client {guid_EM.Value.ClientID}, GUID={guid_EM.Key}");
                guid_EM.Value.Event.Set(); // Set the first ManualResetEvent that is not set yet                         
            }
        }

        public void RemoveFromDependencyList((string, EventMonitor) guid_EM, string clientID) {
            string guid = guid_EM.Item1;
            _logger.LogInformation($"ClientID: {clientID} - Removing clientID: {clientID} from the list of dependent client IDs for the MRE with GUID: {guid}.");
            // Remove the clientID from the list of dependent client IDs for the MRE
            if(dependent_client_ids.TryGetValue(guid, out var MRE_depends)) {
                _logger.LogInformation($"ClientID: {clientID} - Got inner dict for MRE");
                int numberOfDependentClients = MRE_depends.Item2.Count;
                MRE_depends.Item2.Remove(clientID);
                int numberOfDependentClientsAfterRemoval = MRE_depends.Item2.Count;
            } 
            else {
                _logger.LogInformation($"ClientID: {clientID} - Unable to get MRE dependencies");
            }
        }
    }

    public struct ProposedCatalogItem : IEquatable<ProposedCatalogItem> {
        public string Name { get; set; }
        public int CatalogTypeId { get; set; }
        public int CatalogBrandId { get; set; }
        public int Id { get; set; }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ProposedCatalogItem && Equals((ProposedCatalogItem)obj);
        }

        public bool Equals(ProposedCatalogItem other) {
            return Name == other.Name && CatalogTypeId == other.CatalogTypeId && CatalogBrandId == other.CatalogBrandId && Id == other.Id;
        }

        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 23 + Name.GetHashCode();
                hash = hash * 23 + CatalogTypeId.GetHashCode();
                hash = hash * 23 + CatalogBrandId.GetHashCode();
                hash = hash * 23 + Id.GetHashCode();
                // Console.WriteLine($"Hash code for object - {Name} - {CatalogTypeId} - {CatalogBrandId} - {Id} := {hash}");
                return hash;
            }
        }
    }

    public struct ProposedCatalogBrand {
        public string BrandName { get; set; }
        public int Id { get; set; }
    }

    public struct ProposedCatalogType {
        public string TypeName { get; set; }
        public int Id { get; set; }
    }
}
