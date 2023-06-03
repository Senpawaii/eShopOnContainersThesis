using Azure;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text.Json;
using System;
using Microsoft.AspNetCore.Mvc.RazorPages;
using YamlDotNet.Serialization;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class CoordinatorController : ControllerBase {
    private readonly CoordinatorSettings _settings;
    private readonly ILogger<CoordinatorController> _logger;
    private readonly IFunctionalityService _functionalityService;
    private readonly ICatalogService _catalogService;
    private readonly IDiscountService _discountService;

    public CoordinatorController(IOptions<CoordinatorSettings> settings, ILogger<CoordinatorController> logger, IFunctionalityService functionalityService, ICatalogService catalogSvc, IDiscountService discountSvc) {
        _settings = settings.Value;
        _logger = logger;
        _functionalityService = functionalityService;
        _catalogService = catalogSvc;
        _discountService = discountSvc;
    }

    [HttpGet]
    [Route("tokens")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<int>> ReceiveTokens([FromQuery] string tokens = "", [FromQuery] string clientID = "", [FromQuery] string serviceName = "", [FromQuery] bool readOnly = false) {
        double.TryParse(tokens, out var numTokens);

        // Call async condition checker
        await AsyncCheck(numTokens, clientID, serviceName, readOnly);

        return Ok(tokens);
    }

    private Task<Task> AsyncCheck(double tokens, string clientID, string service, bool readOnly) {
        return Task.Factory.StartNew(async () => {
            // Store the Timestamp proposal
            //_functionalityService.AddNewProposalGivenService(clientID, service, ticks);

            // Incremement the Tokens
            _functionalityService.IncreaseTokens(clientID, tokens);

            if(!readOnly) {
                // Register the service that sent the tokens and executed at least 1 write operation
                _logger.LogInformation($"Func:<{clientID}> - Service:<{service}> - Tokens:<{tokens}> - Write");
                _functionalityService.AddNewServiceSentTokens(clientID, service);
            }
            else {
                _logger.LogInformation($"Func:<{clientID}> - Service:<{service}> - Tokens:<{tokens}> - Read Only");
            }

            if (_functionalityService.HasCollectedAllTokens(clientID)) {
                
                // If no services are registered (read-only functionality), do not ask for proposals / commit
                if (!_functionalityService.ServicesTokensProposed.ContainsKey(clientID) || _functionalityService.ServicesTokensProposed[clientID].Count == 0) {
                    _logger.LogInformation($"Func:<{clientID}> - Read-only Functionality");
                    // Clear all the data structures from the functionality
                    _functionalityService.ClearFunctionality(clientID);
                    return;
                }
                
                await ReceiveProposals(clientID);

                long maxTS = _functionalityService.Proposals[clientID].Max(t => t.Item2);

                // Call Services to commit with the MAX TS
                // _logger.LogInformation($"Func:<{clientID}> - TS:<{new DateTime(maxTS)}>");
                BeginCommitProcess(clientID, maxTS);
            }
        });
    }

    private async void BeginCommitProcess(string clientID, long maxTS) {
        // Log the number of functionalities received up to this point
        // _logger.LogInformation("Number of functionalities received: " + _functionalityService.Proposals.Count);

        // Get all services' addresses involved in the functionality
        List<string> addresses = _functionalityService.Proposals[clientID]
                                    .Select(t => t.Item1)
                                    .Distinct()
                                    .ToList();
        _logger.LogInformation($"Commit Func:<{clientID}> - Addresses:<{string.Join(",", addresses)}>");
        foreach (string address in addresses) {
            switch(address) {
                case "CatalogService":
                    await _catalogService.IssueCommit(maxTS.ToString(), clientID);
                    break;
                case "DiscountService":
                    await _discountService.IssueCommit(maxTS.ToString(), clientID);
                    break;
            }
        }

        // Clear all the data structures from the functionality
        _functionalityService.ClearFunctionality(clientID);
    }

    private async Task ReceiveProposals(string clientID) {
        // Get all services' addresses involved in the functionality
        List<string> services = _functionalityService.ServicesTokensProposed[clientID];

        foreach (string service in services) {
            long proposedTS = -1;
            switch (service) {
                case "CatalogService":
                    proposedTS = await _catalogService.GetProposal(clientID);
                    // Add proposed TS to the list of proposals
                    break;
                case "DiscountService":
                    proposedTS = await _discountService.GetProposal(clientID);
                    break;
            }
            _functionalityService.AddNewProposalGivenService(clientID, service, proposedTS);
        }
    }
}
