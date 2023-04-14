namespace Microsoft.eShopOnContainers.Services.Discount.API.Model {
    public class DiscountItem {
        public int Id { get; set; }
        
        public int CatalogItemId { get; set; }

        // An integer percentage between 0 and 100.
        public int Discount { get; set; }

        public DiscountItem() { }

    }

}
