namespace Basket.API.DependencyServices {
    public class PairedEvents {
        public ProductPriceChangedIntegrationEvent PriceEvent { get; set; }
        public ProductDiscountChangedIntegrationEvent DiscountEvent { get; set; }
        public bool ConfirmedFunctionality { get; set; }
    }
}
