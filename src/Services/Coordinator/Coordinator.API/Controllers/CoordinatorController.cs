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

    public CoordinatorController(IOptions<CoordinatorSettings> settings, ILogger<CoordinatorController> logger, IFunctionalityService functionalityService) {
        _settings = settings.Value;
        _logger = logger;
        _functionalityService = functionalityService;
    }

    [HttpGet]
    [Route("propose")]
    [ProducesResponseType(typeof(int), (int)HttpStatusCode.OK)]
    public async Task<ActionResult<int>> ProposeTimestamp(string ticks, string tokens, string funcID, string serviceName) {
        Int32.TryParse(ticks, out var numTicks);
        Int32.TryParse(tokens, out var numTokens);

        // Call async condition checker
        await AsyncCheck(numTicks, numTokens, funcID, serviceName);

        return Ok(tokens);
    }

    private Task AsyncCheck(int ticks, int tokens, string funcID, string service) {
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

    private void BeginCommitProcess(string funcID, long maxTS) {
        // Get all services' addresses involved in the functionality
        List<string> addresses = _functionalityService.Proposals[funcID].Select(t => t.Item1).ToList();
        foreach (string address in addresses) {
            var baseUrl = _settings.CatalogUrl;
            string uri = $"{baseUrl}/commit?timestamp={maxTS}";


        }
    }
}


//  // Generate the URI string
//var uri = API.Catalog.GetAllCatalogItems(_remoteServiceBaseUrl, page, take, brand, type, metadata);

//// Obtain the header parameters (metadata)
//HttpResponseMessage response = await _httpClient.GetAsync(uri);
//response.EnsureSuccessStatusCode();

//        var responseString = await response.Content.ReadAsStringAsync();

//        if(metadata != null) {
//            ExtractMetadataFromResponseHeaders(metadata, response);
//        }

//        // Decompose the responseString view in a Catalog object
//        var catalog = JsonSerializer.Deserialize<Catalog>(responseString, new JsonSerializerOptions {
//            PropertyNameCaseInsensitive = true
//        });

//return (catalog, metadata);