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
using Autofac.Core;

namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class CoordinatorController : ControllerBase {
    private readonly CoordinatorSettings _settings;
    private readonly ILogger<CoordinatorController> _logger;
    private readonly IFunctionalityService _functionalityService;
    private readonly ICatalogService _catalogService;
    private readonly IDiscountService _discountService;
    private readonly IThesisFrontendService _thesisFrontendService;

    public CoordinatorController(IOptions<CoordinatorSettings> settings, ILogger<CoordinatorController> logger, IFunctionalityService functionalityService, ICatalogService catalogSvc, IDiscountService discountSvc, IThesisFrontendService thesisfrontendSvc) {
        _settings = settings.Value;
        _logger = logger;
        _functionalityService = functionalityService;
        _catalogService = catalogSvc;
        _discountService = discountSvc;
        _thesisFrontendService = thesisfrontendSvc;
    }

    [HttpGet]
    [Route("tokens")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<int>> ReceiveTokens([FromQuery] string tokens = "", [FromQuery] string clientID = "", [FromQuery] string serviceName = "", [FromQuery] bool readOnly = false) {
        double.TryParse(tokens, out var numTokens);

        // Incremement the Tokens
        _functionalityService.IncreaseTokens(clientID, numTokens);

        if (!readOnly) {
            // Register the service that sent the tokens and executed at least 1 write operation
            _functionalityService.AddNewServiceSentTokens(clientID, serviceName);
        }

        if (_functionalityService.HasCollectedAllTokens(clientID)) {

            // If no services are registered (read-only functionality), do not ask for proposals / commit
            if (!_functionalityService.ServicesTokensProposed.ContainsKey(clientID) || _functionalityService.ServicesTokensProposed[clientID].Count == 0) {
                // Clear all the data structures from the functionality
                _functionalityService.ClearFunctionality(clientID);
                return Ok(tokens);
            }

            await ReceiveProposals(clientID);

            long maxTS = _functionalityService.Proposals[clientID].Max(t => t.Item2);

            // Call Services to commit with the MAX TS
            await BeginCommitProcess(clientID, maxTS);
        }

        return Ok(tokens);
    }

    private async ValueTask BeginCommitProcess(string clientID, long maxTS) {
        // Get all services' addresses involved in the functionality
        List<string> addresses = _functionalityService.Proposals[clientID]
                                    .Select(t => t.Item1)
                                    .Distinct()
                                    .ToList();
        // Parallelize the commit process
        List<Task> taskList = new List<Task>();
        foreach (string address in addresses) {
            Task task = null;
            switch(address) {
                case "CatalogService":
                    task = _catalogService.IssueCommit(maxTS.ToString(), clientID);
                    break;
                case "DiscountService":
                    task = _discountService.IssueCommit(maxTS.ToString(), clientID);
                    break;
                case "ThesisFrontendService":
                    task = _thesisFrontendService.IssueCommit(clientID);
                    break;
            }
            taskList.Add(task);
        }

        // Wait for all the services to commit
        await Task.WhenAll(taskList);

        // Clear all the data structures from the functionality
        _functionalityService.ClearFunctionality(clientID);
    }

    private async Task ReceiveProposals(string clientID) {
        // Get all services' addresses involved in the functionality
        List<string> services = _functionalityService.ServicesTokensProposed[clientID];

        var tasks = new List<Task<long>>();
        foreach (string service in services) {
            switch (service) {
                case "CatalogService":
                    tasks.Add(_catalogService.GetProposal(clientID));
                    break;
                case "DiscountService":
                    tasks.Add(_discountService.GetProposal(clientID));
                    break;
            }
        }
        var results = await Task.WhenAll(tasks);

        for (int i = 0; i < services.Count; i++) {
            _functionalityService.AddNewProposalGivenService(clientID, services[i], results[i]);
        }
    }
}
