namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public interface ICatalogService {
    public Task IssueCommit(string maxTS, string clientID);
    public Task<long> GetProposal(string clientID);

}
