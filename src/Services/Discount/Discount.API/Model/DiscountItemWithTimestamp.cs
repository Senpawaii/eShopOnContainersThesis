namespace Microsoft.eShopOnContainers.Services.Discount.API.Model {
    public class DiscountItemWithTimestamp {
        public int Id { get; set; }
        
        public string ItemName { get; set; }
        public string ItemBrand { get; set; }
        public string ItemType { get; set; }

        public int DiscountValue { get; set; }

        public DateTime? Timestamp { get; set; }

        public DiscountItemWithTimestamp() { }

    }

}
