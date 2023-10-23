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
using Coordinator.API.Services;

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
    private readonly IBasketService _basketService;

    public CoordinatorController(IOptions<CoordinatorSettings> settings, ILogger<CoordinatorController> logger, IFunctionalityService functionalityService, ICatalogService catalogSvc, IDiscountService discountSvc, IThesisFrontendService thesisfrontendSvc, IBasketService basketService) {
        _settings = settings.Value;
        _logger = logger;
        _functionalityService = functionalityService;
        _catalogService = catalogSvc;
        _discountService = discountSvc;
        _thesisFrontendService = thesisfrontendSvc;
        _basketService = basketService;
    }

    [HttpGet]
    [Route("eventtokens")]
    [ProducesResponseType(typeof(bool), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<int>> ReceiveEventTokens([FromQuery] string tokens = "", [FromQuery] string clientID = "", [FromQuery] string serviceName = "") {
        double.TryParse(tokens, out var numTokens);

        // _logger.LogInformation($"Event Service: Received {numTokens} tokens from {serviceName} for client {clientID}");

        _functionalityService.IncreaseTokens(clientID, numTokens);
        _functionalityService.AddNewServiceSentEventTokens(clientID, serviceName);

        if (_functionalityService.HasCollectedAllTokens(clientID)) {
            _logger.LogInformation("Received all tokens for client {clientID}", clientID);
            // If no services are registered (read-only functionality), and no Event-based services sent tokens do not ask for proposals / commit
            if (!_functionalityService.ServicesTokensProposed.ContainsKey(clientID) || allServicesReadOnly(clientID)) {
                // Clear all the data structures from the functionality
                _functionalityService.ClearFunctionality(clientID);
                return Ok(tokens);
            }

            // Receive proposals only from Non-Event-based services
            await ReceiveProposals(clientID);

            long maxTS = _functionalityService.Proposals[clientID].Max(t => t.Item2);

            // Call Services to commit with the MAX TS
            await BeginCommitProcess(clientID, maxTS);

            await IssueCommitEventBasedServices(clientID);
        }

        return Ok(tokens);
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
        _logger.LogInformation($"Received {numTokens} tokens from {serviceName} for client {clientID}");
        if (_functionalityService.HasCollectedAllTokens(clientID)) {
            // _logger.LogInformation("Received all tokens for client {clientID}", clientID);
            // If no services are registered (read-only functionality), do not ask for proposals / commit
            if (!_functionalityService.ServicesTokensProposed.ContainsKey(clientID) || _functionalityService.ServicesTokensProposed[clientID].Count == 0) {
                // Clear all the data structures from the functionality
                _functionalityService.ClearFunctionality(clientID);
                return Ok(tokens);
            }
            _logger.LogInformation($" {clientID} has collected all tokens, asking for proposals");
            await ReceiveProposals(clientID);

            long maxTS = _functionalityService.Proposals[clientID].Max(t => t.Item2);

            // Call Services to commit with the MAX TS
            await BeginCommitProcess(clientID, maxTS);

            await IssueCommitEventBasedServices(clientID);
        }

        return Ok(tokens);
    }

    private bool allServicesReadOnly(string clientID) {
        return _functionalityService.ServicesTokensProposed[clientID].Count == 0 && _functionalityService.ServicesEventTokensProposed[clientID].Count == 0;
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
            switch (address) {
                case "CatalogService":
                    task = _catalogService.IssueCommit(maxTS.ToString(), clientID);
                    break;
                case "DiscountService":
                    task = _discountService.IssueCommit(maxTS.ToString(), clientID);
                    break;
            }
            taskList.Add(task);
        }

        // Issue commit to the ThesisFrontendService: this allows the service to return the functionality results to the client 
        taskList.Add(_thesisFrontendService.IssueCommit(clientID));

        // Wait for all the services to commit
        await Task.WhenAll(taskList);
    }

    private async ValueTask IssueCommitEventBasedServices(string clientID) {
        if (_functionalityService.ServicesEventTokensProposed.ContainsKey(clientID) && _functionalityService.ServicesEventTokensProposed[clientID].Count > 0) {
            // Get all services' addresses involved in the functionality
            List<string> addresses = _functionalityService.ServicesEventTokensProposed[clientID]
                                        .Distinct()
                                        .ToList();

            // Parallelize the commit process
            List<Task> taskList = new List<Task>();
            foreach (string address in addresses) {
                // _logger.LogInformation($"Issuing event confirmation to {address} for client {clientID}");
                Task task = null;
                switch (address) {
                    case "BasketService":
                        // Implement BasketService, IBasketService, and declare the IBasketService object in this class
                        task = _basketService.IssueCommitEventBased(clientID);
                        break;
                }
                taskList.Add(task);
            }

            // Issue commit to the ThesisFrontendService: this allows the service to return the functionality results to the client 
            taskList.Add(_thesisFrontendService.IssueCommit(clientID));

            // Wait for all the services to commit
            await Task.WhenAll(taskList);
        }

        // Clear all the data structures from the functionality
        _functionalityService.ClearFunctionality(clientID);
    }

    private async Task ReceiveProposals(string clientID) {
        // Get all services' addresses involved in the functionality
        List<string> services = _functionalityService.ServicesTokensProposed[clientID];
        _logger.LogInformation($"Issuing proposals to {string.Join(", ", services)} for client {clientID}");
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

        for (int i = 0; i < tasks.Count; i++) {
            // Note that services might include the ThesisFrontendService, which does not have a proposal, thus we use tasks.Count instead of services.Count
            _functionalityService.AddNewProposalGivenService(clientID, services[i], results[i]);
        }
    }

    [HttpGet]
    [Route("ping")]
    [ProducesResponseType(typeof(string), (int)HttpStatusCode.OK)]
    public ActionResult<string> Ping() {
        return Ok("Coordinator is alive");
    }
}
