﻿namespace Microsoft.eShopOnContainers.Web.Shopping.HttpAggregator.Models;

public class AddBasketItemRequest
{
    public int CatalogItemId { get; set; }

    public string BasketId { get; set; }

    public int Quantity { get; set; }

    public string CatalogItemName { get; set; }
    public string CatalogItemBrandName { get; set; }
    public string CatalogItemTypeName { get; set; }

    public AddBasketItemRequest()
    {
        Quantity = 1;
    }
}

