namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public interface ICatalogService {
    public Task IssueCommit(string maxTS, string funcID);
    public Task<long> GetProposal(string funcID);

}
