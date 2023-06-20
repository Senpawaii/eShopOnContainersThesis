using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.eShopOnContainers.Services.Coordinator.API;
using Microsoft.eShopOnContainers.Services.Coordinator.API.Controllers;
using Microsoft.eShopOnContainers.Services.Coordinator.API.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Threading.Tasks;

[MemoryDiagnoser]
public class CoordinatorControllerBenchmark {
    private readonly CoordinatorController _controller;

    public CoordinatorControllerBenchmark() {
        var mockFunctionalityService = new Mock<IFunctionalityService>();
        var mockCatalogService = new Mock<ICatalogService>();
        
        // Mock the CoordinatorSettings object
        CoordinatorSettings settings = new CoordinatorSettings();
        var mockSettings = new Mock<IOptions<CoordinatorSettings>>();
        mockSettings.Setup(ap => ap.Value).Returns(settings);


        _controller = new CoordinatorController(mockSettings.Object, null, mockFunctionalityService.Object, mockCatalogService.Object, null, null);
    }

    //[Benchmark]
    //public async Task ReceiveTokensBenchmark() {
    //    await _controller.ReceiveTokens("10", "1", "aaa", true);
    //}
    
    [Benchmark]
    public async Task ReceiveTokensV2Benchmark() {
        await _controller.ReceiveTokens("10", "1", "aaa", true);
    }
}

[MemoryDiagnoser]
public class CatalogServiceBenchmark {
    private readonly CatalogService _catalogService = new CatalogService(new HttpClient(), new Mock<ILogger<CatalogService>>().Object, Options.Create(new CoordinatorSettings { CatalogUrl = "http://localhost/api/v1/catalog/" }));

    [Benchmark]
    public async Task Task_IssueCommitBenchmark() {
        await _catalogService.IssueCommit(maxTS: "0", clientID: "user1");
    }

    [Benchmark]
    public async ValueTask ValueTask_IssueCommitBenchmark() {
        await _catalogService.IssueCommit(maxTS: "0", clientID: "user1");
    }
}

class Program {
    static void Main(string[] args) {
        //var results = BenchmarkRunner.Run<CoordinatorControllerBenchmark>();
        var results2 = BenchmarkRunner.Run<CatalogServiceBenchmark>();
    }
}
