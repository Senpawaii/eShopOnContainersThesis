using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.DependencyServices;
public interface ITokensContextSingleton {
    ConcurrentDictionary<string, int> RemainingTokens { get; }
    public int GetRemainingTokens(string key);
    public void AddRemainingTokens(string clientID, int tokens);
    public void DecrementRemainingTokens(string clientID, int amount);
    public void RemoveRemainingTokens(string clientID);

    ConcurrentDictionary<string, string> TransactionsState { get; }
    public string GetTransactionState(string clientID);
    public void ChangeTransactionState(string clientID, string state);
    public void RemoveTransactionState(string clientID);

    public ManualResetEvent GetManualResetEvent(string clientID);
    public void SignalManualResetEvent(string clientID);
    public void RemoveManualResetEvent(string clientID);
    public void AddManualResetEvent(string clientID);
}
