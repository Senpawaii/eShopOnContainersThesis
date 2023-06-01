using WebMVC.ViewModels;

namespace WebMVC.Infrastructure;

public static class API
{

    public static class Purchase
    {
        public static string AddItemToBasket(string baseUri) => $"{baseUri}/basket/items";
        public static string UpdateBasketItem(string baseUri) => $"{baseUri}/basket/items";

        public static string GetOrderDraft(string baseUri, string basketId) => $"{baseUri}/order/draft/{basketId}";
    }

    public static class Basket
    {
        public static string GetBasket(string baseUri, string basketId) => $"{baseUri}/{basketId}";
        public static string UpdateBasket(string baseUri) => baseUri;
        public static string CheckoutBasket(string baseUri) => $"{baseUri}/checkout";
        public static string CleanBasket(string baseUri, string basketId) => $"{baseUri}/{basketId}";
    }

    public static class Order
    {
        public static string GetOrder(string baseUri, string orderId)
        {
            return $"{baseUri}/{orderId}";
        }

        public static string GetAllMyOrders(string baseUri)
        {
            return baseUri;
        }

        public static string AddNewOrder(string baseUri)
        {
            return $"{baseUri}/new";
        }

        public static string CancelOrder(string baseUri)
        {
            return $"{baseUri}/cancel";
        }

        public static string ShipOrder(string baseUri)
        {
            return $"{baseUri}/ship";
        }
    }

    public static class Catalog
    {
        public static string GetAllCatalogItems(string baseUri, int page, int take, int? brand, int? type, TCCMetadata metadata)
        {
            var filterQs = "";

            if (type.HasValue)
            {
                var brandQs = (brand.HasValue) ? brand.Value.ToString() : string.Empty;
                filterQs = $"/type/{type.Value}/brand/{brandQs}";

            }
            else if (brand.HasValue)
            {
                var brandQs = (brand.HasValue) ? brand.Value.ToString() : string.Empty;
                filterQs = $"/type/all/brand/{brandQs}";
            }
            else
            {
                filterQs = string.Empty;
            }
            if(metadata == null) {
                return $"{baseUri}items{filterQs}?pageIndex={page}&pageSize={take}";
            }
            return $"{baseUri}items{filterQs}?pageIndex={page}&pageSize={take}&clientID={metadata.ClientID}&timestamp={metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}";
        }

        public static string GetAllBrands(string baseUri, TCCMetadata metadata)
        {
            if(metadata == null) {
                return $"{baseUri}catalogBrands";
            }
            return $"{baseUri}catalogBrands?clientID={metadata.ClientID}&timestamp={metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}";
        }

        public static string GetAllTypes(string baseUri, TCCMetadata metadata)
        {
            if(metadata == null) {
                return $"{baseUri}catalogTypes";
            }
            return $"{baseUri}catalogTypes?clientID={metadata.ClientID}&timestamp={metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}";
        }
    }

    public static class Discount {
        public static string GetDiscountsById(string baseUri, List<int> ids, TCCMetadata metadata) {
            if(metadata == null) {
                return $"{baseUri}discounts";
            }
            var idsString = string.Join("&", ids.Select(n => $"ids={n}"));
            return $"{baseUri}discounts?{idsString}&clientID={metadata.ClientID}&timestamp={metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}";
        }
    }
}
