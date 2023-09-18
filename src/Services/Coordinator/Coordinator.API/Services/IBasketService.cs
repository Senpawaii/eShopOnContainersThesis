namespace Coordinator.API.Services {
    public interface IBasketService {
        public Task IssueCommitEventBased(string clientID);
    }
}
