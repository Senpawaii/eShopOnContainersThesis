using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
public class TokensContextSingleton : ITokensContextSingleton {
    ConcurrentDictionary<string, int> remaining_tokens = new ConcurrentDictionary<string, int>();
    ConcurrentDictionary<string, string> transactions_state = new ConcurrentDictionary<string, string>();
    ConcurrentDictionary<string, ManualResetEvent> transactions_state_events = new ConcurrentDictionary<string, ManualResetEvent>();
    public TokensContextSingleton() {
    }

    public ConcurrentDictionary<string, int> RemainingTokens {
        get { return remaining_tokens; }
    }
    public ConcurrentDictionary<string, string> TransactionsState {
        get { return transactions_state; }
    }

    public int GetRemainingTokens(string clientID) {
        return remaining_tokens.GetValueOrDefault(clientID, -1);
    }

    public string GetTransactionState(string clientID) {
        return transactions_state.GetValueOrDefault(clientID, null);
    }

    public void AddRemainingTokens(string clientID, int tokens) {
        // Add new entry with default value of tokens
        remaining_tokens.AddOrUpdate(clientID, tokens, (key, value) => {
            value = tokens;
            return value;
        });
    }

    public void DecrementRemainingTokens(string clientID, int amount) {
        // Reduce the number of tokens by the amount
        remaining_tokens.AddOrUpdate(clientID, amount, (key, value) => value - amount);
    }

    public void ChangeTransactionState(string clientID, string state) {
        // Change the state of the transaction associated with the clientID
        transactions_state.AddOrUpdate(clientID, state, (key, value) => {
            value = state;
            return value;
        });
    }

    public void RemoveRemainingTokens(string clientID) {
        // Remove the entry with the given clientID
        remaining_tokens.TryRemove(clientID, out int _);
    }

    public void RemoveTransactionState(string clientID) {
        // Remove the entry with the given clientID
        transactions_state.TryRemove(clientID, out string _);
    }

    public void AddManualResetEvent(string clientID) {
        // Add a new ManualResetEvent associated with the clientID
        transactions_state_events.AddOrUpdate(clientID, new ManualResetEvent(false), (key, value) => value);
    }

    public ManualResetEvent GetManualResetEvent(string clientID) {
        // Return the ManualResetEvent associated with the clientID
        return transactions_state_events.GetValueOrDefault(clientID, null);
    }

    public void SignalManualResetEvent(string clientID) {
        // Signal the ManualResetEvent associated with the clientID
        try {
            transactions_state_events.GetValueOrDefault(clientID, null).Set();
        } catch (NullReferenceException) {
            // The ManualResetEvent was already removed
        }
    }

    public void RemoveManualResetEvent(string clientID) {
        // Remove the ManualResetEvent associated with the clientID
        transactions_state_events.TryRemove(clientID, out ManualResetEvent _);
    }
}
