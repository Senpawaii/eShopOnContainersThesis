﻿using System.Collections.Concurrent;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public interface IFunctionalityService {
    ConcurrentDictionary<string, List<(string, long)>> Proposals { get; }
    ConcurrentDictionary<string, List<string>> ServicesTokensProposed { get; }
    ConcurrentDictionary<string, List<string>> ServicesEventTokensProposed { get; }
    ConcurrentDictionary<string, double> Tokens { get; }

    public void AddNewProposalGivenService(string clientID, string service, long proposalTicks);
    public void IncreaseTokens(string clientID, double tokens);
    public Boolean HasCollectedAllTokens(string clientID);
    public void AddNewServiceSentTokens(string clientID, string service);
    public void AddNewServiceSentEventTokens(string clientID, string service);
    public void ClearFunctionality(string clientID);
    public bool HasConfirmedFunctionality(string clientID);

}
