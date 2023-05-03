using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public class FunctionalityService : IFunctionalityService {
    ConcurrentDictionary<string, List<(string, long)>> _proposals = new ConcurrentDictionary<string, List<(string, long)>>(); 
    ConcurrentDictionary<string, double> _tokens = new ConcurrentDictionary<string, double>();

    public FunctionalityService() {
    }

    public ConcurrentDictionary<string, List<(string, long)>> Proposals {
        get { return _proposals; }
    }
    public ConcurrentDictionary<string, double> Tokens {
        get { return Tokens; }
    }

    public void AddNewProposalGivenService(string funcID, string service, long proposalTicks) {
        if(_proposals.ContainsKey(funcID)) {
            _proposals[funcID].Add((service, proposalTicks));
        }
        else {
            // First proposal in the functionality
            _proposals[funcID] = new List<(string, long)> {
                { (service, proposalTicks) }
            };
        }
    }

    public void IncreaseTokens(string funcID, double tokens) {
        if(_tokens.ContainsKey(funcID)) {
            // Add the number of tokens
            _tokens[funcID] += tokens;
        } 
        else {
            // First proposal in the functionality
            _tokens[funcID] = tokens;
        }
    }

    public Boolean HasCollectedAllTokens(string funcID) {
        // Return true if the functionality has ended => the max number of tokens have been reached
        return _tokens[funcID] == 100;
    }
}
