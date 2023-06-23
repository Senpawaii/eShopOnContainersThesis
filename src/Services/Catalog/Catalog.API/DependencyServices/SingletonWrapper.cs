using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;

namespace Catalog.API.DependencyServices {
    public class SingletonWrapper : ISingletonWrapper {
        public ILogger<SingletonWrapper> _logger;

        //ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_items = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        //ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_types = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        //ConcurrentDictionary<string, ConcurrentBag<object[]>> wrapped_catalog_brands = new ConcurrentDictionary<string, ConcurrentBag<object[]>>();
        ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogItem>> wrapped_catalog_items2 = new ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogItem>>();
        ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogType>> wrapped_catalog_types2 = new ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogType>>();
        ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogBrand>> wrapped_catalog_brands2 = new ConcurrentDictionary<string, ConcurrentBag<WrappedCatalogBrand>>();


        //ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> proposed_catalog_items = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>>();
        //ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> proposed_catalog_types = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>>();
        //ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> proposed_catalog_brands = new ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>>();
        ConcurrentDictionary<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>> proposed_catalog_items2 = new ConcurrentDictionary<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>>();
        ConcurrentDictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>> proposed_catalog_brands2 = new ConcurrentDictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>>();
        ConcurrentDictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>> proposed_catalog_types2 = new ConcurrentDictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>>();

        // Store the proposed Timestamp for each functionality in Proposed State
        ConcurrentDictionary<string, long> proposed_functionalities = new ConcurrentDictionary<string, long>();

        // Dictionary that hold the state of a transaction: PREPARE or COMMITTED
        ConcurrentDictionary<string, bool> transaction_state = new ConcurrentDictionary<string, bool>();

        public SingletonWrapper(ILogger<SingletonWrapper> logger) {
            _logger = logger;
        }

        //public ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogItems {
        //    get { return wrapped_catalog_items; }
        //}
        //public ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogTypes {
        //    get { return wrapped_catalog_types; }
        //}
        //public ConcurrentDictionary<string, ConcurrentBag<object[]>> SingletonWrappedCatalogBrands {
        //    get { return wrapped_catalog_brands; }
        //}

        public ConcurrentDictionary<string, bool> SingletonTransactionState {
            get { return transaction_state; }
        }

        //public ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_items {
        //    get { return proposed_catalog_items; } 
        //}
        //public ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_types {
        //    get { return proposed_catalog_types; }
        //}
        //public ConcurrentDictionary<string, ConcurrentDictionary<DateTime, int>> Proposed_catalog_brands {
        //    get { return proposed_catalog_brands; }
        //}

        public ConcurrentDictionary<string, long> Proposed_functionalities {
            get { return proposed_functionalities; }
        }

        //public ConcurrentBag<object[]> SingletonGetCatalogITems(string key) {
        //    // Get wrapped catalog items associated with client ID
        //    return wrapped_catalog_items.GetValueOrDefault(key, new ConcurrentBag<object[]>());
        //}
        //public ConcurrentBag<object[]> SingletonGetCatalogTypes(string key) {
        //    return wrapped_catalog_types.GetValueOrDefault(key, new ConcurrentBag<object[]>());
        //}
        //public ConcurrentBag<object[]> SingletonGetCatalogBrands(string key) {
        //    return wrapped_catalog_brands.GetValueOrDefault(key, new ConcurrentBag<object[]>());
        //}

        public ConcurrentBag<object[]> SingletonGetCatalogItemsV2(string key) {
            // Get the wrapped catalog items associated with the clientID
            var bag = wrapped_catalog_items2.GetValueOrDefault(key, new ConcurrentBag<WrappedCatalogItem>());

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
        public ConcurrentBag<object[]> SingletonGetCatalogTypesV2(string key) {
            // Get the wrapped catalog types associated with the clientID
            var bag = wrapped_catalog_types2.GetValueOrDefault(key, new ConcurrentBag<WrappedCatalogType>());
            
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
        public ConcurrentBag<object[]> SingletonGetCatalogBrandsV2(string key) {
            // Get the wrapped catalog brands associated with the clientID
            var bag = wrapped_catalog_brands2.GetValueOrDefault(key, new ConcurrentBag<WrappedCatalogBrand>());

            var list = new ConcurrentBag<object[]>();
            foreach (var item in bag) {
                list.Add(new object[] {
                    item.Id,
                    item.BrandName
                });
            }
            return list;
        }

        public bool SingletonGetTransactionState(string clientID) {
            if(!transaction_state.ContainsKey(clientID)) {
                // Initialize a new state: uncommitted
                transaction_state[clientID] = false;
            }
            return transaction_state.GetValueOrDefault(clientID);
        }

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
                wrapped_catalog_items2.AddOrUpdate(clientID, new ConcurrentBag<WrappedCatalogItem> { wrapped_item }, (key, bag) => {
                    bag.Add(wrapped_item);
                    return bag;
                });
            }
        }
        public void SingletonAddCatalogType(string clientID, IEnumerable<object[]> values) {
            foreach (object[] type in values) {
                // Create a new Wrapped Catalog Type
                var wrapped_type = new WrappedCatalogType {
                    Id = (int)type[0],
                    TypeName = (string)type[1]
                };
                wrapped_catalog_types2.AddOrUpdate(clientID, new ConcurrentBag<WrappedCatalogType> { wrapped_type }, (key, bag) => {
                    bag.Add(wrapped_type);
                    return bag;
                });
            }
        }
        public void SingletonAddCatalogBrand(string clientID, IEnumerable<object[]> values) {
            foreach (object[] brand in values) {
                // Create a new Wrapped Catalog Brand
                var wrapped_brand = new WrappedCatalogBrand {
                    Id = (int)brand[0],
                    BrandName = (string)brand[1]
                };
                wrapped_catalog_brands2.AddOrUpdate(clientID, new ConcurrentBag<WrappedCatalogBrand> { wrapped_brand }, (key, bag) => {
                    bag.Add(wrapped_brand);
                    return bag;
                });
            }
        }

        public bool SingletonSetTransactionState(string clientID, bool state) {
            return transaction_state[clientID] = state;
        }

        // Remove the objects associated with the clientID
        public void SingletonRemoveFunctionalityObjects(string clientID) {
            if(wrapped_catalog_brands2.ContainsKey(clientID))
                wrapped_catalog_brands2.TryRemove(clientID, out _);
            if(wrapped_catalog_types2.ContainsKey(clientID))
                wrapped_catalog_types2.TryRemove(clientID, out _);
            if(wrapped_catalog_items2.ContainsKey(clientID))
                wrapped_catalog_items2.TryRemove(clientID, out _);
            if(transaction_state.ContainsKey(clientID))
                transaction_state.TryRemove(clientID, out bool _);
        }

        public void SingletonAddProposedFunctionality(string clientID, long proposedTS) {
            proposed_functionalities.AddOrUpdate(clientID, proposedTS, (key, value) => {
                value = proposedTS;
                return value;
            });
        }

        public void SingletonRemoveProposedFunctionality(string clientID) {
            if(proposed_functionalities.ContainsKey(clientID))
                proposed_functionalities.TryRemove(clientID, out long _);
        }

        public void SingletonAddWrappedItemsToProposedSetV2(string clientID, long proposedTS) {
            // Gather the items inside the wrapper with given clientID
            ConcurrentBag<WrappedCatalogItem> catalog_items_to_propose = wrapped_catalog_items2.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogItem>());
            ConcurrentBag<WrappedCatalogType> catalog_types_to_propose = wrapped_catalog_types2.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogType>());
            ConcurrentBag<WrappedCatalogBrand> catalog_brands_to_propose = wrapped_catalog_brands2.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogBrand>());

            // For each item, add it to the proposed set, adding the proposed timestamp associated
            foreach (WrappedCatalogItem catalog_item_to_propose in catalog_items_to_propose) {
                // Name: catalog_item_to_propose.Name, BrandId: catalog_item_to_propose.BrandId, TypeId: catalog_item_to_propose.TypeId
                //WrappedCatalogItem newItem = new WrappedCatalogItem(catalog_item_to_propose.Id, catalog_item_to_propose.Name, catalog_item_to_propose.Description, catalog_item_to_propose.Price, catalog_item_to_propose.PictureFileName, catalog_item_to_propose.CatalogTypeId, catalog_item_to_propose.CatalogBrandId, catalog_item_to_propose.AvailableStock, catalog_item_to_propose.RestockThreshold, catalog_item_to_propose.MaxStockThreshold, catalog_item_to_propose.OnReorder);
                proposed_catalog_items2.AddOrUpdate(catalog_item_to_propose, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, string>();
                    innerDict.TryAdd(new DateTime(proposedTS), clientID);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(new DateTime(proposedTS), clientID);
                    return value;
                });
            }

            foreach (WrappedCatalogType catalog_type_to_propose in catalog_types_to_propose) {
                // Name: catalog_type_to_propose.Name
                //ProposedType newType = new ProposedType(catalog_type_to_propose.TypeName);
                proposed_catalog_types2.AddOrUpdate(catalog_type_to_propose, _ => {
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
                proposed_catalog_brands2.AddOrUpdate(catalog_brand_to_propose, _ => {
                    var innerDict = new ConcurrentDictionary<DateTime, string>();
                    innerDict.TryAdd(new DateTime(proposedTS), clientID);
                    return innerDict;
                }, (_, value) => {
                    value.TryAdd(new DateTime(proposedTS), clientID);
                    return value;
                });
            }
        }

        //public void SingletonAddWrappedItemsToProposedSet(string clientID, long proposedTS) {
        //    // Gather the objects inside the wrapper with given clientID
        //    ConcurrentBag<object[]> catalog_items_to_propose = wrapped_catalog_items.GetValueOrDefault(clientID, new ConcurrentBag<object[]>());
        //    ConcurrentBag<object[]> catalog_types_to_propose = wrapped_catalog_types.GetValueOrDefault(clientID, new ConcurrentBag<object[]>());
        //    ConcurrentBag<object[]> catalog_brands_to_propose = wrapped_catalog_brands.GetValueOrDefault(clientID, new ConcurrentBag<object[]>());

        //    // For each object, add it to the proposed set using its identifiers, adding the proposed timestamp associated
        //    foreach (object[] catalog_item_to_propose in catalog_items_to_propose) {
        //        // Name: catalog_item_to_propose[1], BrandId: catalog_item_to_propose[2], TypeId: catalog_item_to_propose[4]
        //        object[] identifiers = new object[] { catalog_item_to_propose[1], catalog_item_to_propose[2], catalog_item_to_propose[4] };
        //        string identifiersStr = catalog_item_to_propose[1].ToString() + "_" + catalog_item_to_propose[2].ToString() + "_" + catalog_item_to_propose[4].ToString();

        //        proposed_catalog_items.AddOrUpdate(identifiersStr, _ => {
        //            var innerDict = new ConcurrentDictionary<DateTime, int>();
        //            innerDict.TryAdd(new DateTime(proposedTS), 1);
        //            return innerDict;
        //        }, (_, value) => {
        //            value.TryAdd(new DateTime(proposedTS), 1);
        //            return value;
        //        });
        //    }

        //    foreach (object[] catalog_brand_to_propose in catalog_brands_to_propose) {
        //        // Brand: catalog_brand_to_propose[1]
        //        object[] identifiers = new object[] { catalog_brand_to_propose[1] };
        //        string identifiersStr = catalog_brand_to_propose[1].ToString();
        //        proposed_catalog_brands.AddOrUpdate(identifiersStr, _ => {
        //            var innerDict = new ConcurrentDictionary<DateTime, int>();
        //            innerDict.TryAdd(new DateTime(proposedTS), 1);
        //            return innerDict;
        //        }, (_, value) => {
        //            value.TryAdd(new DateTime(proposedTS), 1);
        //            return value;
        //        });
        //    }

        //    foreach (object[] catalog_type_to_propose in catalog_types_to_propose) {
        //        // Type: catalog_type_to_propose[1]
        //        object[] identifiers = new object[] { catalog_type_to_propose[1] };
        //        string identifiersStr = catalog_type_to_propose[1].ToString();
        //        proposed_catalog_types.AddOrUpdate(identifiersStr, _ => {
        //            var innerDict = new ConcurrentDictionary<DateTime, int>();
        //            innerDict.TryAdd(new DateTime(proposedTS), 1);
        //            return innerDict;
        //        }, (_, value) => {
        //            value.TryAdd(new DateTime(proposedTS), 1);
        //            return value;
        //        });
        //    }
        //}
        

        public void SingletonRemoveWrappedItemsFromProposedSetV2(string clientID) {
            // TODO: Possible optimization: add structure that indexes proposed objects by the client ID that proposed them

            // Get the proposed objects from the wrapper with given clientID
            ConcurrentBag<WrappedCatalogItem> catalog_items_to_remove = wrapped_catalog_items2.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogItem>());
            ConcurrentBag<WrappedCatalogBrand> catalog_brands_to_remove = wrapped_catalog_brands2.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogBrand>());
            ConcurrentBag<WrappedCatalogType> catalog_types_to_remove = wrapped_catalog_types2.GetValueOrDefault(clientID, new ConcurrentBag<WrappedCatalogType>());
            _logger.LogInformation($"ClientID: {clientID}, the wrapper contains only 1 item, that is: {string.Join(",", catalog_items_to_remove.Select(x => x.Id))}");
            _logger.LogInformation($"ClientID: {clientID}, the proposed item set contains {proposed_catalog_items2.Count} items, that is: {string.Join(",", proposed_catalog_items2.Keys.Select(x => x.Id))}");


            _logger.LogInformation($"Client {clientID} is removing {catalog_items_to_remove.Count} catalog items, {catalog_brands_to_remove.Count} catalog brands, and {catalog_types_to_remove.Count} catalog types from the proposed set");
            // Remove entries from the proposed set. These are identified by their key table identifiers.
            foreach(WrappedCatalogItem item in catalog_items_to_remove) {
                // Remove each item from the proposed set
                proposed_catalog_items2.TryRemove(item, out _);
            }
            foreach(WrappedCatalogBrand brand in catalog_brands_to_remove) {
                // Remove each brand from the proposed set
                proposed_catalog_brands2.TryRemove(brand, out _);
            }
            foreach(WrappedCatalogType type in catalog_types_to_remove) {
                // Remove each type from the proposed set
                proposed_catalog_types2.TryRemove(type, out _);
            }

            // Check if there any missing objects
            _logger.LogInformation($"At the end, ClientID: {clientID}, the proposed item set contains {proposed_catalog_items2.Count} items, that is: {string.Join(",", proposed_catalog_items2.Keys.Select(x => x.Id))}");

        }

        //public void SingletonRemoveWrappedItemsFromProposedSet(string clientID, ConcurrentBag<object[]> wrapped_objects, string targetTable) {
        //    // Remove entries in the proposed set, identified by their table identifiers and the timestamp proposed in the functionality
        //    switch(targetTable) {
        //        case "Catalog":
        //            foreach (object[] object_to_remove in wrapped_objects) {
        //                // Name: wrapped_object[1], BrandId: wrapped_object[2], TypeId: wrapped_object[4]
        //                object[] identifiers = new object[] { object_to_remove[1], object_to_remove[2], object_to_remove[4] };
        //                string identifiersStr = object_to_remove[1].ToString() + "_" + object_to_remove[2].ToString() + "_" + object_to_remove[4].ToString();
        //                var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
        //                proposed_catalog_items[identifiersStr].TryRemove(original_proposedTS, out _);
        //            }
        //            break;
        //        case "CatalogBrand":
        //            foreach (object[] object_to_remove in wrapped_objects) {
        //                // Brand: wrapped_object[1]
        //                object[] identifiers = new object[] { object_to_remove[1] };
        //                string identifiersStr = object_to_remove[1].ToString();
        //                var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
        //                proposed_catalog_brands[identifiersStr].TryRemove(original_proposedTS, out _);
        //            }
        //            break;
        //        case "CatalogType":
        //            foreach (object[] object_to_remove in wrapped_objects) {
        //                // Type: wrapped_object[1]
        //                object[] identifiers = new object[] { object_to_remove[1] };
        //                string identifiersStr = object_to_remove[1].ToString();
        //                var original_proposedTS = new DateTime(proposed_functionalities[clientID]);
        //                proposed_catalog_types[identifiersStr].TryRemove(original_proposedTS, out _);
        //            }
        //            break;
        //    }
        //}

        public bool AnyProposalWithLowerTimestamp(List<Tuple<string, string>> conditions, string targetTable, DateTime readerTimestamp) {
            switch (targetTable) {
                case "Catalog":
                    // Apply all the conditions to the set of proposed catalog items 2, this set will keep shrinking as we apply more conditions.
                    // Note that the original set of proposed catalog items 2 is not modified, we are just creating a new set with the results of the conditions.
                    var filtered_proposed_catalog_items2 = new Dictionary<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_items2);
                    if (conditions != null) {
                        filtered_proposed_catalog_items2 = ApplyFiltersToCatalogItems(conditions, filtered_proposed_catalog_items2);
                        _logger.LogInformation($"There are left {filtered_proposed_catalog_items2.Count} items after applying the filters, namely items with ID {string.Join(",", filtered_proposed_catalog_items2.Keys.Select(x => x.Id))}");
                    }
                    // Check if the there any proposed catalog items 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<WrappedCatalogItem, ConcurrentDictionary<DateTime, string>> proposed_catalog_item in filtered_proposed_catalog_items2) {
                        foreach (DateTime proposedTS in proposed_catalog_item.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                Console.WriteLine($"The item {proposed_catalog_item.Key.Name} has a lower timestamp than the reader's timestamp: {proposedTS.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")} < {readerTimestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}. Proposed ticks: {proposedTS.Ticks}, proposed by client: {proposed_catalog_item.Value[proposedTS]}");
                                // There is at least one proposed catalog item 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                return true;
                            }
                        }
                    }
                    break;
                case "CatalogBrand":
                    var filtered_proposed_catalog_brands2 = new Dictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_brands2);
                    if (conditions != null) {
                        filtered_proposed_catalog_brands2 = ApplyFiltersToCatalogBrands(conditions, filtered_proposed_catalog_brands2);
                    }
                    // Check if the there any proposed catalog brands 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>> proposed_catalog_brand in filtered_proposed_catalog_brands2) {
                        foreach (DateTime proposedTS in proposed_catalog_brand.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                // There is at least one proposed catalog brand 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                return true;
                            }
                        }
                    }

                    break;
                case "CatalogType":
                    var filtered_proposed_catalog_types2 = new Dictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>>(this.proposed_catalog_types2);
                    if (conditions != null) {
                        filtered_proposed_catalog_types2 = ApplyFiltersToCatalogTypes(conditions, filtered_proposed_catalog_types2);
                    }
                    // Check if the there any proposed catalog types 2 with a lower timestamp than the reader's timestamp
                    foreach (KeyValuePair<WrappedCatalogType, ConcurrentDictionary<DateTime, string>> proposed_catalog_type in filtered_proposed_catalog_types2) {
                        foreach (DateTime proposedTS in proposed_catalog_type.Value.Keys) {
                            if (proposedTS < readerTimestamp) {
                                // There is at least one proposed catalog type 2 with a lower timestamp than the reader's timestamp that might be committed before the reader's timestamp
                                return true;
                            }
                        }
                    }
                    break;
            }

            return false;
        }

        private static Dictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>> ApplyFiltersToCatalogTypes(List<Tuple<string, string>> conditions, Dictionary<WrappedCatalogType, ConcurrentDictionary<DateTime, string>> filtered_proposed_catalog_types2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_types2 = filtered_proposed_catalog_types2.Where(x => x.Key.TypeName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_types2;
        }

        private static Dictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>> ApplyFiltersToCatalogBrands(List<Tuple<string, string>> conditions, Dictionary<WrappedCatalogBrand, ConcurrentDictionary<DateTime, string>> filtered_proposed_catalog_brands2) {
            foreach (Tuple<string, string> condition in conditions) {
                filtered_proposed_catalog_brands2 = filtered_proposed_catalog_brands2.Where(x => x.Key.BrandName == condition.Item2).ToDictionary(x => x.Key, x => x.Value);
            }

            return filtered_proposed_catalog_brands2;
        }

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

                //Id = id;
                //Name = name;
                //Description = description;
                //Price = price;
                //PictureFileName = pictureFileName;
                //PictureUri = pictureUri;
                //CatalogTypeId = catalogTypeId;
                //CatalogBrandId = catalogBrandId;
                //AvailableStock = availableStock;
                //RestockThreshold = restockThreshold;
                //MaxStockThreshold = maxStockThreshold;
                //OnReorder = onReorder;
            }

            return filtered_proposed_catalog_items2;
        }

        public List<CatalogItem> SingletonGetWrappedCatalogItemsToFlush(string clientID, bool onlyUpdate) {
            // Check the Catalog Items, Brands, and Types associated with the clientID and flush them to the database
            
            var catalogItems = new List<CatalogItem>();
            
            Console.WriteLine("Getting proposed data to flush from client: " + clientID + " ...");
            if (wrapped_catalog_items2.TryGetValue(clientID, out ConcurrentBag<WrappedCatalogItem> clientCatalogItems)) {
                foreach (WrappedCatalogItem wrappedCatalogItem in clientCatalogItems) {
                    if (onlyUpdate) {
                        _logger.LogInformation($"Client {clientID}, updating catalog item with ID {wrappedCatalogItem.Id}");
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
            _logger.LogInformation("No items were registered in the wrapper for clientID " + clientID);
            return null;

            // if (wrapped_catalog_brands2.TryGetValue(clientID, out ConcurrentBag<WrappedCatalogBrand> clientCatalogBrands)) {
            //     foreach (WrappedCatalogBrand wrappedCatalogBrand in clientCatalogBrands) {
            //         if (onlyUpdate) {
            //             CatalogBrand catalogBrand = new CatalogBrand {
            //                 Id = wrappedCatalogBrand.Id,
            //                 Brand = wrappedCatalogBrand.BrandName
            //             };

            //             dbcontext.CatalogBrands.Update(catalogBrand);
            //         }
            //         else {
            //             CatalogBrand catalogBrand = new CatalogBrand {
            //                 Brand = wrappedCatalogBrand.BrandName
            //             };

            //             dbcontext.CatalogBrands.Add(catalogBrand);
            //         }
            //         await dbcontext.SaveChangesAsync();
            //     }
            // }

            // if (wrapped_catalog_types2.TryGetValue(clientID, out ConcurrentBag<WrappedCatalogType> clientCatalogTypes)) {
            //     foreach (WrappedCatalogType wrappedCatalogType in clientCatalogTypes) {
            //         if (onlyUpdate) {
            //             CatalogType catalogType = new CatalogType {
            //                 Id = wrappedCatalogType.Id,
            //                 Type = wrappedCatalogType.TypeName
            //             };

            //             dbcontext.CatalogTypes.Update(catalogType);
            //         }
            //         else {
            //             CatalogType catalogType = new CatalogType {
            //                 Type = wrappedCatalogType.TypeName
            //             };

            //             dbcontext.CatalogTypes.Add(catalogType);
            //         }
            //         await dbcontext.SaveChangesAsync();
            //     }
            // }
        }

        public void CleanWrappedObjects(string clientID) {
            _logger.LogInformation($"Cleaning wrapped objects for client {clientID} ...");
            // Clean up the proposed items;
            SingletonRemoveWrappedItemsFromProposedSetV2(clientID);

            // Clean up wrapped objects;
            SingletonRemoveFunctionalityObjects(clientID);

            // Clean up the proposed functionality state
            SingletonRemoveProposedFunctionality(clientID);
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
