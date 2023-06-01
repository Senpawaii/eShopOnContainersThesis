namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Model {
    public class DiscountItem {
        public int Id { get; set; }
        
        public string ItemName { get; set; }
        public string ItemBrand { get; set; }
        public string ItemType { get; set; }

        public int DiscountValue { get; set; }

        public DiscountItem() { }

    }

}
