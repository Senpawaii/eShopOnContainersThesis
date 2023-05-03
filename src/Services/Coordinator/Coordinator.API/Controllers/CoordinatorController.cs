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

    public CoordinatorController(IOptions<CoordinatorSettings> settings, ILogger<CoordinatorController> logger, IFunctionalityService functionalityService, ICatalogService catalogSvc) {
        _settings = settings.Value;
        _logger = logger;
        _functionalityService = functionalityService;
        _catalogService = catalogSvc;
    }

    [HttpGet]
    [Route("propose")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<int>> ProposeTimestamp([FromQuery] string ticks = "", [FromQuery] string tokens = "", [FromQuery] string funcID = "", [FromQuery] string serviceName = "") {
        long.TryParse(ticks, out var numTicks);
        double.TryParse(tokens, out var numTokens);

        // Call async condition checker
        await AsyncCheck(numTicks, numTokens, funcID, serviceName);

        return Ok(tokens);
    }

    private Task AsyncCheck(long ticks, double tokens, string funcID, string service) {
        return Task.Factory.StartNew(() => {
            // Store the Timestamp proposal
            _functionalityService.AddNewProposalGivenService(funcID, service, ticks);

            // Incremement the Tokens
            _functionalityService.IncreaseTokens(funcID, tokens);

            if(_functionalityService.HasCollectedAllTokens(funcID)) {
                long maxTS = _functionalityService.Proposals[funcID].Max(t => t.Item2);

                // Call Services to commit with the MAX TS
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
                    break;
            }
        }
    }
}