namespace Discount.API.IntegrationEvents.Events.Factories {
    public interface IFactoryClientIDWrappedProductDiscountChangedIntegrationEvent {
        public ClientIDWrappedProductDiscountChangedIntegrationEvent getClientIDWrappedProductDiscountChangedIntegrationEvent(int productId, decimal newDiscount, decimal oldDiscount);
    }
}
