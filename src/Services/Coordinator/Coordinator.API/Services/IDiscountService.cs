﻿namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
public interface IDiscountService {
    public Task IssueCommit(string maxTS, string clientID);
    public ValueTask<long> GetProposal(string clientID);

}
