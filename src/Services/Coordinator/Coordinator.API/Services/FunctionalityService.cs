using Google.Protobuf.WellKnownTypes;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public class FunctionalityService : IFunctionalityService {
    ConcurrentDictionary<string, List<(string, long)>> _proposals = new ConcurrentDictionary<string, List<(string, long)>>(); 
    ConcurrentDictionary<string, int> _tokens = new ConcurrentDictionary<string, int>();

    public FunctionalityService() {
    }

    public ConcurrentDictionary<string, List<(string, long)>> Proposals {
        get { return _proposals; }
    }
    public ConcurrentDictionary<string, int> Tokens {
        get { return Tokens; }
    }

    public void AddNewProposalGivenService(string funcID, string service, long proposalTicks) {
        _proposals[funcID].Add((service, proposalTicks));
    }

    public void IncreaseTokens(string funcID, int tokens) {
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
