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
    public async Task<ActionResult<int>> ReceiveTokens([FromQuery] string tokens = "", [FromQuery] string funcID = "", [FromQuery] string serviceName = "") {
        double.TryParse(tokens, out var numTokens);

        // Call async condition checker
        await AsyncCheck(numTokens, funcID, serviceName);

        return Ok(tokens);
    }

    private Task<Task> AsyncCheck(double tokens, string funcID, string service) {
        return Task.Factory.StartNew(async () => {
            // Store the Timestamp proposal
            //_functionalityService.AddNewProposalGivenService(funcID, service, ticks);

            // Incremement the Tokens
            _functionalityService.IncreaseTokens(funcID, tokens);

            // Register the service that sent the tokens
            _functionalityService.AddNewServiceSentTokens(funcID, service);

            if (_functionalityService.HasCollectedAllTokens(funcID)) {
                await ReceiveProposals(funcID);

                long maxTS = _functionalityService.Proposals[funcID].Max(t => t.Item2);

                // Call Services to commit with the MAX TS
                _logger.LogInformation($"Func:<{funcID}> - TS:<{new DateTime(maxTS)}>");
                BeginCommitProcess(funcID, maxTS);
            }
        });
    }

    private async void BeginCommitProcess(string funcID, long maxTS) {
        // Get all services' addresses involved in the functionality
        List<string> addresses = _functionalityService.Proposals[funcID]
                                    .Select(t => t.Item1)
                                    .Distinct()
                                    .ToList();
        
        foreach (string address in addresses) {
            switch(address) {
                case "CatalogService":
                    await _catalogService.IssueCommit(maxTS.ToString(), funcID);
                    break;
                case "DiscountService":
                    await _discountService.IssueCommit(maxTS.ToString(), funcID);
                    break;
            }
        }
    }

    private async Task ReceiveProposals(string funcID) {
        // Get all services' addresses involved in the functionality
        List<string> services = _functionalityService.ServicesTokensProposed[funcID];

        foreach (string service in services) {
            long proposedTS = -1;
            switch (service) {
                case "CatalogService":
                    proposedTS = await _catalogService.GetProposal(funcID);
                    // Add proposed TS to the list of proposals
                    break;
                case "DiscountService":
                    proposedTS = await _discountService.GetProposal(funcID);
                    break;
            }
            _functionalityService.AddNewProposalGivenService(funcID, service, proposedTS);
        }
    }
}