using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;
using NewRelic.Api.Agent;
using System.Threading;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        public ILogger<SingletonWrapper> _logger;

        ConcurrentDictionary<string, ConcurrentBag<CatalogItem>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<CatalogItem>>();
        ConcurrentDictionary<string, ConcurrentBag<CatalogType>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<CatalogType>>();
        ConcurrentDictionary<string, ConcurrentBag<CatalogBrand>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<CatalogBrand>>();

        ConcurrentDictionary<ProposedCatalogItem, ConcurrentDictionary<DateTime, string>> proposed_catalog_items = new ConcurrentDictionary<ProposedCatalogItem, ConcurrentDictionary<DateTime, string>>();
        ConcurrentDictionary<ProposedCatalogBrand, ConcurrentDictionary<DateTime, string>> proposed_catalog_brands = new ConcurrentDictionary<ProposedCatalogBrand, ConcurrentDictionary<DateTime, string>>();
        ConcurrentDictionary<ProposedCatalogType, ConcurrentDictionary<DateTime, string>> proposed_catalog_types = new ConcurrentDictionary<ProposedCatalogType, ConcurrentDictionary<DateTime, string>>();

        ConcurrentDictionary<(ProposedCatalogItem, long), ManualResetEvent> catalog_items_manual_reset_events = new ConcurrentDictionary<(ProposedCatalogItem, long), ManualResetEvent>(); 
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

        [Trace]
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

        [Trace]
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

        [Trace]
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

        [Trace]
        public bool SingletonGetTransactionState(string clientID) {
            if(!transaction_state.ContainsKey(clientID)) {
                // Initialize a new state: uncommitted = false; to be commit = true;
                transaction_state[clientID] = false;
            }
            return transaction_state.GetValueOrDefault(clientID);
        }

        [Trace]
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
                wrapped_catalog_items.AddOrUpdate(clientID, new ConcurrentBag<CatalogItem> { wrapped_item }, (key, bag) => {
                    bag.Add(wrapped_item);
                    return bag;
                });
            }
        }

        [Trace]
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

        [Trace]
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

        [Trace]
        public bool SingletonSetTransactionState(string clientID, bool state) {
            return transaction_state[clientID] = state;
        }


        // Remove the objects associated with the clientID
        [Trace]
        public void SingletonRemoveFunctionalityObjects(string clientID) {
            wrapped_catalog_brands.TryRemove(clientID, out _);
            wrapped_catalog_types.TryRemove(clientID, out _);
            wrapped_catalog_items.TryRemove(clientID, out _);
            transaction_state.TryRemove(clientID, out bool _);
            // _logger.LogInformation($"Number of elements in the wrapped catalog items 2: {wrapped_catalog_items2.Count}, and transaction states: {transaction_state.Count}");
        }

        [Trace]
        public void SingletonAddProposedFunctionality(string clientID, long proposedTS) {
            proposed_functionalities.AddOrUpdate(clientID, proposedTS, (key, value) => {
                value = proposedTS;
                return value;
            });
        }

        [Trace]
        public void SingletonRemoveProposedFunctionality(string clientID) {
            if(proposed_functionalities.ContainsKey(clientID))
                proposed_functionalities.TryRemove(clientID, out long _);
        }

        [Trace]
        public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS) {
            // Gather the items inside the wrapper with given clientID
            ConcurrentBag<CatalogItem> catalog_items_to_propose = wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<CatalogItem>());
            ConcurrentBag<CatalogType> catalog_types_to_propose = wrapped_catalog_types.GetValueOrDefault(clientID, new ConcurrentBag<CatalogType>());
            ConcurrentBag<CatalogBrand> catalog_brands_to_propose = wrapped_catalog_brands.GetValueOrDefault(clientID, new ConcurrentBag<CatalogBrand>());

            // For each item, add it to the proposed set, adding the proposed timestamp associated
            foreach (CatalogItem catalog_item_to_propose in catalog_items_to_propose) {
                DateTime proposedTSDate = new DateTime(proposedTS);
                ProposedCatalogItem proposedItem = new ProposedCatalogItem {
                    Name = catalog_item_to_propose.Name,
                    CatalogBrandId = catalog_item_to_propose.CatalogBrandId,
                    CatalogTypeId = catalog_item_to_propose.CatalogTypeId
                };

                proposed_catalog_items.AddOrUpdate(proposedItem, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, string>();
                    innerDict.TryAdd(proposedTSDate, clientID);
                    _logger.LogInformation($"ClientID: {clientID}, inner dict did not exist yet. Adding a new one.");
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(proposedTSDate, clientID);
                    _logger.LogInformation($"ClientID: {clientID}, number of items in inner dict: {value.Count}");
                    return value;
                });

                // Create ManualEvent for catalog Item
                if(!catalog_items_manual_reset_events.TryAdd((proposedItem, proposedTS), new ManualResetEvent(false))) {
                    _logger.LogError($"ClientID: {clientID} - Could not add ManualResetEvent for catalog item with ID {catalog_item_to_propose.Id}");
                }
            }

            foreach (CatalogType catalog_type_to_propose in catalog_types_to_propose) {
                DateTime proposedTSDate = new DateTime(proposedTS);
                ProposedCatalogType proposedCatalogType = new ProposedCatalogType {
                    TypeName = catalog_type_to_propose.Type
                };
                
                proposed_catalog_types.AddOrUpdate(proposedCatalogType, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, string>();
                    innerDict.TryAdd(proposedTSDate, clientID);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(proposedTSDate, clientID);
                    return value;
                });
            }

            foreach (CatalogBrand catalog_brand_to_propose in catalog_brands_to_propose) {
                DateTime proposedTSDate = new DateTime(proposedTS);
                ProposedCatalogBrand proposedCatalogType = new ProposedCatalogBrand {
                    BrandName = catalog_brand_to_propose.Brand
                };
                //ProposedBrand newBrand = new ProposedBrand(catalog_brand_to_propose.BrandName);
                proposed_catalog_brands.AddOrUpdate(proposedCatalogType, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, string>();
                    innerDict.TryAdd(proposedTSDate, clientID);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(proposedTSDate, clientID);
                    return value;
                });
            }
        }

        [Trace]
        public void SingletonRemoveWrappedItemsFromProposedSet(string clientID) {
            // TODO: Possible optimization: add structure that indexes proposed objects by the client ID that proposed them

            // Get the proposed objects from the wrapper with given clientID
            ConcurrentBag<CatalogItem> catalog_items_to_remove = wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<CatalogItem>());
            ConcurrentBag<CatalogBrand> catalog_brands_to_remove = wrapped_catalog_brands.GetValueOrDefault(clientID, new ConcurrentBag<CatalogBrand>());
            ConcurrentBag<CatalogType> catalog_types_to_remove = wrapped_catalog_types.GetValueOrDefault(clientID, new ConcurrentBag<CatalogType>());

            // Remove entries from the proposed set. These are identified by their key table identifiers.
            foreach(CatalogItem item in catalog_items_to_remove) {
                ProposedCatalogItem proposedItem = new ProposedCatalogItem {
                    Name = item.Name,
                    CatalogBrandId = item.CatalogBrandId,
                    CatalogTypeId = item.CatalogTypeId
                };
                // Remove each item from the proposed set
                proposed_catalog_items.TryRemove(proposedItem, out _);
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

        [Trace]
        public ManualResetEvent AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp, string clientID) {
            switch (targetTable) {
                case "Catalog":
                    // Apply all the conditions to the set of proposed catalog items 2, this set will keep shrinking as we apply more conditions.
                    // Note that the original set of proposed catalog items 2 is not modified, we are just creating a new set with the results of the conditions.
                    var filtered_proposed_catalog_items2 = new Dictionary<ProposedCatalogItem, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_items);
                    if (conditions != null) {
                        filtered_proposed_catalog_items2 = ApplyFiltersToCatalogItems(conditions, filtered_proposed_catalog_items2);
                        // _logger.LogInformation($"There are left {filtered_proposed_catalog_items2.Count} items after applying the filters, namely items with ID {string.Join(",", filtered_proposed_catalog_items2.Keys.Select(x => x.Id))}");
                    }
                    // Check if the there any proposed catalog items 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<ProposedCatalogItem, ConcurrentDictionary<DateTime, string>> proposed_catalog_item in filtered_proposed_catalog_items2) {
                        foreach (DateTime proposedTS in proposed_catalog_item.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                // There is at least one proposed catalog item 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                // Return the Manual reset event to wait for
                                ManualResetEvent manualResetEvent = catalog_items_manual_reset_events.GetValueOrDefault((proposed_catalog_item.Key, proposedTS.Ticks), null);
                                if(manualResetEvent != null) {
                                    return manualResetEvent;
                                }
                                else {
                                    _logger.LogError($"ClientID: {clientID} - Could not find ManualResetEvent for catalog item with ID {proposed_catalog_item.Key.Name} although proposedTS < readerTimestamp.");
                                }
                                return null;
                            }
                        }
                    }
                    break;
                case "CatalogBrand":
                    var filtered_proposed_catalog_brands2 = new Dictionary<ProposedCatalogBrand, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_brands);
                    if (conditions != null) {
                        filtered_proposed_catalog_brands2 = ApplyFiltersToCatalogBrands(conditions, filtered_proposed_catalog_brands2);
                    }
                    // Check if the there any proposed catalog brands 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<ProposedCatalogBrand, ConcurrentDictionary<DateTime, string>> proposed_catalog_brand in filtered_proposed_catalog_brands2) {
                        foreach (DateTime proposedTS in proposed_catalog_brand.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                // There is at least one proposed catalog brand 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                // TODO: Complete this method
                                return null;
                            }
                        }
                    }

                    break;
                case "CatalogType":
                    var filtered_proposed_catalog_types2 = new Dictionary<ProposedCatalogType, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_types);
                    if (conditions != null) {
                        filtered_proposed_catalog_types2 = ApplyFiltersToCatalogTypes(conditions, filtered_proposed_catalog_types2);
                    }
                    // Check if the there any proposed catalog types 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<ProposedCatalogType, ConcurrentDictionary<DateTime, string>> proposed_catalog_type in filtered_proposed_catalog_types2) {
                        foreach (DateTime proposedTS in proposed_catalog_type.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
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

        [Trace]
        private static Dictionary<ProposedCatalogType, ConcurrentDictionary<DateTime, string>> ApplyFiltersToCatalogTypes(List<Tuple<string, string>> conditions, Dictionary<ProposedCatalogType, ConcurrentDictionary<DateTime, string>> filtered_proposed_catalog_types2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_types2 = filtered_proposed_catalog_types2.Where(x => x.Key.TypeName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_types2;
        }

        [Trace]
        private static Dictionary<ProposedCatalogBrand, ConcurrentDictionary<DateTime, string>> ApplyFiltersToCatalogBrands(List<Tuple<string, string>> conditions, Dictionary<ProposedCatalogBrand, ConcurrentDictionary<DateTime, string>> filtered_proposed_catalog_brands2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_brands2 = filtered_proposed_catalog_brands2.Where(x => x.Key.BrandName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_brands2;
        }

        [Trace]
        private static Dictionary<ProposedCatalogItem, ConcurrentDictionary<DateTime, string>> ApplyFiltersToCatalogItems(List<Tuple<string, string>> conditions, Dictionary<ProposedCatalogItem, ConcurrentDictionary<DateTime, string>> filtered_proposed_catalog_items2) {
            foreach (Tuple<string, string> condition in conditions) {
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
                    default:
                        break;
                }
            }

            return filtered_proposed_catalog_items2;
        }

        [Trace]
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
                            OnReorder = wrappedCatalogItem.OnReorder
                        };
                        // If we pass the original catalog item, with the set ID, will it work? Test it.
                        catalogItems.Add(catalogItem);
                    }
                }
                return catalogItems;
            }
            return null;
        }

        [Trace]
        public void CleanWrappedObjects(string clientID) {
            // _logger.LogInformation($"Cleaning wrapped objects for client {clientID} ...");
            // Clean up the proposed items;
            SingletonRemoveWrappedItemsFromProposedSet(clientID);

            // Clean up wrapped objects;
            SingletonRemoveFunctionalityObjects(clientID);

            // Clean up the proposed functionality state
            SingletonRemoveProposedFunctionality(clientID);
        }

        [Trace]
        public void NotifyReaderThreads(string clientID, List<CatalogItem> committedItems) {
            foreach(CatalogItem item in committedItems) {
                // Locate the WrappedCatalogItem associated with the committed item
                ProposedCatalogItem proposedItem = new ProposedCatalogItem {
                    CatalogBrandId = item.CatalogBrandId,
                    CatalogTypeId = item.CatalogTypeId,
                    Name = item.Name,
                };
                // Get proposed timestamp for the wrapped item for client with ID clientID
                var proposedTS = proposed_functionalities.GetValueOrDefault(clientID, 0);

                if(proposedTS == 0) {
                    _logger.LogError($"ClientID: {clientID} - DateTime 0: Could not find proposed timestamp for client {clientID}");
                }

                ManualResetEvent manualResetEvent = catalog_items_manual_reset_events.GetValueOrDefault((proposedItem, proposedTS), null);
                if(manualResetEvent != null) {
                    _logger.LogInformation($"ClientID: {clientID} - Notifying ManualResetEvent for catalog item with ID {proposedItem.Name} and proposed TS {proposedTS}");
                    manualResetEvent.Set();
                }
                else {
                    _logger.LogError($"ClientID: {clientID} - Could not find ManualResetEvent for catalog item with ID {proposedItem.Name} with proposed TS {proposedTS}");
                }
            }
            // TODO: a thread should clear the manual reset events dictionary from time to time to avoid unnecessary memory consumption
        }
    }

    public struct ProposedCatalogItem {
        public string Name { get; set; }
        public int CatalogTypeId { get; set; }
        public int CatalogBrandId { get; set; }
    }

    public struct ProposedCatalogBrand {
        public string BrandName { get; set; }
    }

    public struct ProposedCatalogType {
        public string TypeName { get; set; }
    }
}
