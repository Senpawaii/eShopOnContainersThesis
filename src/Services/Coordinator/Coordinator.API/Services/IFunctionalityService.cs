using System.Collections.Concurrent;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public interface IFunctionalityService {
    ConcurrentDictionary<string, List<(string, long)>> Proposals { get; }
    ConcurrentDictionary<string, double> Tokens { get; }

    public void AddNewProposalGivenService(string funcID, string service, long proposalTicks);
    public void IncreaseTokens(string funcID, double tokens);
    public Boolean HasCollectedAllTokens(string funcID);

}
