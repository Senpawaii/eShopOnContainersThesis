﻿using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
public interface ITokensContextSingleton {
    ConcurrentDictionary<string, int> RemainingTokens { get; }
    public int GetRemainingTokens(string key);
    public void AddRemainingTokens(string clientID, int tokens);
    public void DecrementRemainingTokens(string clientID, int amount);
    public void RemoveRemainingTokens(string clientID);
}
