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

        ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogItem>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogItem>>();
        ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogType>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogType>>();
        ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogBrand>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogBrand>>();

        ConcurrentDictionary<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>> proposed_catalog_items = new ConcurrentDictionary<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>>();
        ConcurrentDictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>> proposed_catalog_brands = new ConcurrentDictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>>();
        ConcurrentDictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>> proposed_catalog_types = new ConcurrentDictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>>();

        ConcurrentDictionary<(WrappedCatalogItem, long), ManualResetEvent> catalog_items_manual_reset_events = new ConcurrentDictionary<(WrappedCatalogItem, long), ManualResetEvent>(); 
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
            var bag = wrapped_catalog_items.GetValueOrDefault(key, new ConcurrentBag<WrappedCatalogItem>());

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
            var bag = wrapped_catalog_types.GetValueOrDefault(key, new ConcurrentBag<WrappedCatalogType>());
            
            // Convert the wrapped catalog types to a list of object[]
            var list = new ConcurrentBag<object[]>();
            foreach (var item in bag) {
                list.Add(new object[] {
                    item.Id,
                    item.TypeName
                });
            }
            return list;
        }

        [Trace]
        public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key) {
            // Get the wrapped catalog brands associated with the clientID
            var bag = wrapped_catalog_brands.GetValueOrDefault(key, new ConcurrentBag<WrappedCatalogBrand>());

            var list = new ConcurrentBag<object[]>();
            foreach (var item in bag) {
                list.Add(new object[] {
                    item.Id,
                    item.BrandName
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
                var wrapped_item = new WrappedCatalogItem {
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
                wrapped_catalog_items.AddOrUpdate(clientID, new ConcurrentBag<WrappedCatalogItem> { wrapped_item }, (key, bag) => {
                    bag.Add(wrapped_item);
                    return bag;
                });
            }
            // _logger.LogInformation($"CatalogItems for clientID: {wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogItem>()).Count}");
        }

        [Trace]
        public void SingletonAddCatalogType(string clientID, IEnumerable<object[]> values) {
            foreach (object[] type in values) {
                // Create a new Wrapped Catalog Type
                var wrapped_type = new WrappedCatalogType {
                    Id = (int)type[0],
                    TypeName = (string)type[1]
                };
                wrapped_catalog_types.AddOrUpdate(clientID, new ConcurrentBag<WrappedCatalogType> { wrapped_type }, (key, bag) => {
                    bag.Add(wrapped_type);
                    return bag;
                });
            }
        }

        [Trace]
        public void SingletonAddCatalogBrand(string clientID, IEnumerable<object[]> values) {
            foreach (object[] brand in values) {
                // Create a new Wrapped Catalog Brand
                var wrapped_brand = new WrappedCatalogBrand {
                    Id = (int)brand[0],
                    BrandName = (string)brand[1]
                };
                wrapped_catalog_brands.AddOrUpdate(clientID, new ConcurrentBag<WrappedCatalogBrand> { wrapped_brand }, (key, bag) => {
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
            ConcurrentBag<WrappedCatalogItem> catalog_items_to_propose = wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogItem>());
            ConcurrentBag<WrappedCatalogType> catalog_types_to_propose = wrapped_catalog_types.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogType>());
            ConcurrentBag<WrappedCatalogBrand> catalog_brands_to_propose = wrapped_catalog_brands.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogBrand>());

            // For each item, add it to the proposed set, adding the proposed timestamp associated
            foreach (WrappedCatalogItem catalog_item_to_propose in catalog_items_to_propose) {
                // Name: catalog_item_to_propose.Name, BrandId: catalog_item_to_propose.BrandId, TypeId: catalog_item_to_propose.TypeId
                DateTime proposedTSDate = new DateTime(proposedTS);                
                proposed_catalog_items.AddOrUpdate(catalog_item_to_propose, _ => {
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
                if(!catalog_items_manual_reset_events.TryAdd((catalog_item_to_propose, proposedTS), new ManualResetEvent(false))) {
                    _logger.LogError($"ClientID: {clientID} - Could not add ManualResetEvent for catalog item with ID {catalog_item_to_propose.Id}");
                }
            }

            foreach (WrappedCatalogType catalog_type_to_propose in catalog_types_to_propose) {
                // Name: catalog_type_to_propose.Name
                //ProposedType newType = new ProposedType(catalog_type_to_propose.TypeName);
                proposed_catalog_types.AddOrUpdate(catalog_type_to_propose, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, string>();
                    innerDict.TryAdd(new DateTime(proposedTS), clientID);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(new DateTime(proposedTS), clientID);
                    return value;
                });
            }

            foreach (WrappedCatalogBrand catalog_brand_to_propose in catalog_brands_to_propose) {
                // Name: catalog_brand_to_propose.Name
                //ProposedBrand newBrand = new ProposedBrand(catalog_brand_to_propose.BrandName);
                proposed_catalog_brands.AddOrUpdate(catalog_brand_to_propose, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, string>();
                    innerDict.TryAdd(new DateTime(proposedTS), clientID);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(new DateTime(proposedTS), clientID);
                    return value;
                });
            }
        }

        [Trace]
        public void SingletonRemoveWrappedItemsFromProposedSet(string clientID) {
            // TODO: Possible optimization: add structure that indexes proposed objects by the client ID that proposed them

            // Get the proposed objects from the wrapper with given clientID
            ConcurrentBag<WrappedCatalogItem> catalog_items_to_remove = wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogItem>());
            ConcurrentBag<WrappedCatalogBrand> catalog_brands_to_remove = wrapped_catalog_brands.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogBrand>());
            ConcurrentBag<WrappedCatalogType> catalog_types_to_remove = wrapped_catalog_types.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogType>());
            // _logger.LogInformation($"ClientID: {clientID}, the wrapper contains only 1 item, that is: {string.Join(",", catalog_items_to_remove.Select(x => x.Id))}");
            // _logger.LogInformation($"ClientID: {clientID}, the proposed item set contains {proposed_catalog_items2.Count} items, that is: {string.Join(",", proposed_catalog_items2.Keys.Select(x => x.Id))}");


            // _logger.LogInformation($"Client {clientID} is removing {catalog_items_to_remove.Count} catalog items, {catalog_brands_to_remove.Count} catalog brands, and {catalog_types_to_remove.Count} catalog types from the proposed set");
            // Remove entries from the proposed set. These are identified by their key table identifiers.
            foreach(WrappedCatalogItem item in catalog_items_to_remove) {
                // Remove each item from the proposed set
                proposed_catalog_items.TryRemove(item, out _);
            }
            foreach(WrappedCatalogBrand brand in catalog_brands_to_remove) {
                // Remove each brand from the proposed set
                proposed_catalog_brands.TryRemove(brand, out _);
            }
            foreach(WrappedCatalogType type in catalog_types_to_remove) {
                // Remove each type from the proposed set
                proposed_catalog_types.TryRemove(type, out _);
            }

            // Check if there any missing objects
            // _logger.LogInformation($"At the end, ClientID: {clientID}, the proposed item set contains {proposed_catalog_items2.Count} items, that is: {string.Join(",", proposed_catalog_items2.Keys.Select(x => x.Id))}");

        }

        [Trace]
        public ManualResetEvent AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp, string clientID) {
            switch (targetTable) {
                case "Catalog":
                    // Apply all the conditions to the set of proposed catalog items 2, this set will keep shrinking as we apply more conditions.
                    // Note that the original set of proposed catalog items 2 is not modified, we are just creating a new set with the results of the conditions.
                    var filtered_proposed_catalog_items2 = new Dictionary<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_items);
                    if (conditions != null) {
                        filtered_proposed_catalog_items2 = ApplyFiltersToCatalogItems(conditions, filtered_proposed_catalog_items2);
                        // _logger.LogInformation($"There are left {filtered_proposed_catalog_items2.Count} items after applying the filters, namely items with ID {string.Join(",", filtered_proposed_catalog_items2.Keys.Select(x => x.Id))}");
                    }
                    // Check if the there any proposed catalog items 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>> proposed_catalog_item in filtered_proposed_catalog_items2) {
                        foreach (DateTime proposedTS in proposed_catalog_item.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                // There is at least one proposed catalog item 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                // Return the Manual reset event to wait for
                                ManualResetEvent manualResetEvent = catalog_items_manual_reset_events.GetValueOrDefault((proposed_catalog_item.Key, proposedTS.Ticks), null);
                                if(manualResetEvent != null) {
                                    return manualResetEvent;
                                }
                                else {
                                    _logger.LogError($"ClientID: {clientID} - Could not find ManualResetEvent for catalog item with ID {proposed_catalog_item.Key.Id} although proposedTS < readerTimestamp.");
                                }
                                return null;
                            }
                        }
                    }
                    break;
                case "CatalogBrand":
                    var filtered_proposed_catalog_brands2 = new Dictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_brands);
                    if (conditions != null) {
                        filtered_proposed_catalog_brands2 = ApplyFiltersToCatalogBrands(conditions, filtered_proposed_catalog_brands2);
                    }
                    // Check if the there any proposed catalog brands 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>> proposed_catalog_brand in filtered_proposed_catalog_brands2) {
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
                    var filtered_proposed_catalog_types2 = new Dictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_types);
                    if (conditions != null) {
                        filtered_proposed_catalog_types2 = ApplyFiltersToCatalogTypes(conditions, filtered_proposed_catalog_types2);
                    }
                    // Check if the there any proposed catalog types 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<WrappedCatalogType, ConcurrentDictionary<DateTime, string>> proposed_catalog_type in filtered_proposed_catalog_types2) {
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
        private static Dictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>> ApplyFiltersToCatalogTypes(List<Tuple<string, string>> conditions, Dictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>> filtered_proposed_catalog_types2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_types2 = filtered_proposed_catalog_types2.Where(x => x.Key.TypeName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_types2;
        }

        [Trace]
        private static Dictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>> ApplyFiltersToCatalogBrands(List<Tuple<string, string>> conditions, Dictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>> filtered_proposed_catalog_brands2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_brands2 = filtered_proposed_catalog_brands2.Where(x => x.Key.BrandName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_brands2;
        }

        [Trace]
        private static Dictionary<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>> ApplyFiltersToCatalogItems(List<Tuple<string, string>> conditions, Dictionary<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>> filtered_proposed_catalog_items2) {
            foreach (Tuple<string, string> condition in conditions) {
                switch (condition.Item1) {
                    case "Id":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.Id == int.Parse(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "Name":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.Name == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "Description":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.Description == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "Price":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.Price == Convert.ToDecimal(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "PictureFileName":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.PictureFileName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "CatalogTypeId":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.CatalogTypeId == int.Parse(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "CatalogBrandId":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.CatalogBrandId == int.Parse(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "AvailableStock":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.AvailableStock == Convert.ToInt32(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "RestockThreshold":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.RestockThreshold == Convert.ToInt32(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "MaxStockThreshold":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.MaxStockThreshold == Convert.ToInt32(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
                        break;
                    case "OnReorder":
                        filtered_proposed_catalog_items2 = filtered_proposed_catalog_items2.Where(x => x.Key.OnReorder == Convert.ToBoolean(condition.Item2)).ToDictionary(x => x.Key, x => x.Value);
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
            if (wrapped_catalog_items.TryGetValue(clientID, out ConcurrentBag<WrappedCatalogItem> clientCatalogItems)) {
                foreach (WrappedCatalogItem wrappedCatalogItem in clientCatalogItems) {
                    if (onlyUpdate) {
                        // _logger.LogInformation($"Client {clientID}, updating catalog item with ID {wrappedCatalogItem.Id}");
                        CatalogItem catalogItem = new CatalogItem {
                            Id = wrappedCatalogItem.Id,
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
                        catalogItems.Add(catalogItem);
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
                        catalogItems.Add(catalogItem);
                    }
                }
                return catalogItems;
            }
            else {
                // _logger.LogInformation($"ClientID {clientID} does not have any catalog items to flush");
            }
            // _logger.LogInformation("No items were registered in the wrapper for clientID " + clientID);
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
                WrappedCatalogItem wrappedItem = new WrappedCatalogItem {
                    Id = item.Id,
                    CatalogBrandId = item.CatalogBrandId,
                    CatalogTypeId = item.CatalogTypeId,
                    Description = item.Description,
                    Name = item.Name,
                    PictureFileName = item.PictureFileName,
                    Price = item.Price,
                    AvailableStock = item.AvailableStock,
                    MaxStockThreshold = item.MaxStockThreshold,
                    OnReorder = item.OnReorder,
                    RestockThreshold = item.RestockThreshold
                };
                // Get proposed timestamp for the wrapped item for client with ID clientID
                var proposedTS = proposed_functionalities.GetValueOrDefault(clientID, 0);

                if(proposedTS == 0) {
                    _logger.LogError($"ClientID: {clientID} - DateTime 0: Could not find proposed timestamp for client {clientID}");
                }

                ManualResetEvent manualResetEvent = catalog_items_manual_reset_events.GetValueOrDefault((wrappedItem, proposedTS), null);
                if(manualResetEvent != null) {
                    _logger.LogInformation($"ClientID: {clientID} - Notifying ManualResetEvent for catalog item with ID {wrappedItem.Id} and proposed TS {proposedTS}");
                    manualResetEvent.Set();
                }
                else {
                    _logger.LogError($"ClientID: {clientID} - Could not find ManualResetEvent for catalog item with ID {wrappedItem.Id} with proposed TS {proposedTS}");
                }
            }
            // TODO: a thread should clear the manual reset events dictionary from time to time to avoid unnecessary memory consumption
        }
    }

    
    public struct WrappedCatalogItem {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string PictureFileName { get; set; }
        public int CatalogTypeId { get; set; }
        public int CatalogBrandId { get; set; }
        public int AvailableStock { get; set; }
        public int RestockThreshold { get; set; }
        public int MaxStockThreshold { get; set; }
        public bool OnReorder { get; set; }

        public WrappedCatalogItem(int id, string name, string description, decimal price, string pictureFileName, int catalogTypeId, int catalogBrandId, int availableStock, int restockThreshold, int maxStockThreshold, bool onReorder) {
            Id = id;
            Name = name;
            Description = description;
            Price = price;
            PictureFileName = pictureFileName;
            CatalogTypeId = catalogTypeId;
            CatalogBrandId = catalogBrandId;
            AvailableStock = availableStock;
            RestockThreshold = restockThreshold;
            MaxStockThreshold = maxStockThreshold;
            OnReorder = onReorder;
        }
        
    }

    public struct WrappedCatalogBrand {
        public int Id { get; set; }
        public string BrandName { get; set; }
    }

    public struct WrappedCatalogType {
        public int Id { get; set; }
        public string TypeName { get; set; }
    }
}
