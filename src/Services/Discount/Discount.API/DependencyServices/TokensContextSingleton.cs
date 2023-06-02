using System.Collections.Concurrent;

namespace Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
public class TokensContextSingleton : ITokensContextSingleton {
    ConcurrentDictionary<string, int> remaining_tokens = new ConcurrentDictionary<string, int>();
    public TokensContextSingleton() {
    }

    public ConcurrentDictionary<string, int> RemainingTokens {
        get { return remaining_tokens; }
    }
    public int GetRemainingTokens(string clientID) {
        return remaining_tokens.GetValueOrDefault(clientID, -1);
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
    public void RemoveRemainingTokens(string clientID) {
        // Remove the entry with the given clientID
        remaining_tokens.TryRemove(clientID, out int _);
    }
}
