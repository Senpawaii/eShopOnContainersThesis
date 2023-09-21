using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public class FunctionalityService : IFunctionalityService {
    ConcurrentDictionary<string, List<(string, long)>> _proposals = new ConcurrentDictionary<string, List<(string, long)>>(); 
    ConcurrentDictionary<string, double> _tokens = new ConcurrentDictionary<string, double>();
    ConcurrentDictionary<string, List<string>> _servicesSentTokens = new ConcurrentDictionary<string, List<string>>();
    ConcurrentDictionary<string, List<string>> _servicesSentEventTokens = new ConcurrentDictionary<string, List<string>>();

    public FunctionalityService() {
    }

    public ConcurrentDictionary<string, List<(string, long)>> Proposals {
        get { return _proposals; }
    }
    public ConcurrentDictionary<string, double> Tokens {
        get { return Tokens; }
    }

    public ConcurrentDictionary<string, List<string>> ServicesTokensProposed { 
        get { return _servicesSentTokens; } 
    }

    public ConcurrentDictionary<string, List<string>> ServicesEventTokensProposed {
        get { return _servicesSentEventTokens; }
    }

    public void AddNewServiceSentTokens(string clientID, string service) {
        _servicesSentTokens.AddOrUpdate(clientID,
            new List<string> { service },
            (_, list) =>
            {
                list.Add(service);
                return list;
            });
    }

    public void AddNewServiceSentEventTokens(string clientID, string service) {
        _servicesSentEventTokens.AddOrUpdate(clientID,
                       new List<string> { service },
                                  (_, list) => {
                list.Add(service);
                return list;
            });
    }

    public void AddNewProposalGivenService(string clientID, string service, long proposalTicks) {
        _proposals.AddOrUpdate(clientID,
            new List<(string, long)> { (service, proposalTicks) },
            (_, list) =>
            {
                list.Add((service, proposalTicks));
                return list;
            });
    }


    public void IncreaseTokens(string clientID, double tokens) {
        _tokens.AddOrUpdate(clientID, tokens, (_, value) => value + tokens);
    }

    public Boolean HasCollectedAllTokens(string clientID) {
        // Return true if the functionality has ended => the max number of tokens have been reached
        return _tokens[clientID] == 1000000000;
    }

    public void ClearFunctionality(string clientID) {
        // Remove the functionality from the proposals
        _proposals.TryRemove(clientID, out _);
        // Remove the functionality from the tokens
        _tokens.TryRemove(clientID, out _);
        // Remove the functionality from the services sent tokens
        _servicesSentTokens.TryRemove(clientID, out _);
        // Remove the functionality from the services sent event tokens
        _servicesSentEventTokens.TryRemove(clientID, out _);
    }

    public bool HasConfirmedFunctionality(string clientID) {
        // Return true if the functionality has ended => the max number of tokens have been reached
        return _tokens[clientID] == 1000000000;
    }
}
