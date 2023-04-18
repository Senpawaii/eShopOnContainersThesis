namespace Microsoft.eShopOnContainers.Services.Discount.API.Model {
    public class DiscountItem {
        public int Id { get; set; }
        
        public int CatalogItemId { get; set; }

        public string CatalogItemName { get; set; }

        // An integer percentage between 0 and 100.
        public int DiscountValue { get; set; }

        public DiscountItem() { }

    }

}
