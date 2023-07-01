using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public class FunctionalityService : IFunctionalityService {
    ConcurrentDictionary<string, List<(string, long)>> _proposals = new ConcurrentDictionary<string, List<(string, long)>>(); 
    ConcurrentDictionary<string, double> _tokens = new ConcurrentDictionary<string, double>();
    ConcurrentDictionary<string, List<string>> _servicesSentTokens = new ConcurrentDictionary<string, List<string>>();

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

    public void AddNewServiceSentTokens(string clientID, string service) {
        if(_servicesSentTokens.ContainsKey(clientID)) {
            _servicesSentTokens[clientID].Add(service);
        }
        else {
            // First proposal in the functionality
            _servicesSentTokens[clientID] = new List<string> {
                { service }
            };
        }
    }

    public void AddNewProposalGivenService(string clientID, string service, long proposalTicks) {
        // Console.WriteLine($"Adding proposal from {service} for client {clientID}");
        if(_proposals.ContainsKey(clientID)) {
            _proposals[clientID].Add((service, proposalTicks));
        }
        else {
            // First proposal in the functionality
            _proposals[clientID] = new List<(string, long)> {
                { (service, proposalTicks) }
            };
        }
    }

    public void IncreaseTokens(string clientID, double tokens) {
        if(_tokens.ContainsKey(clientID)) {
            // Add the number of tokens
            _tokens[clientID] += tokens;
        } 
        else {
            // First proposal in the functionality
            _tokens[clientID] = tokens;
        }
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
    }
}
