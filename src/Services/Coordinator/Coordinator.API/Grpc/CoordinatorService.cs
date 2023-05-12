namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Grpc;

using CoordinatorAPI;
using global::Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using static CoordinatorAPI.Coordinator;

public class CoordinatorService : CoordinatorBase {
    private readonly CoordinatorSettings _coordinatorSettings;
    private readonly ILogger _logger;

    public CoordinatorService(IOptions<CoordinatorSettings> settings, ILogger<CoordinatorService> logger) { 
        _coordinatorSettings = settings.Value;
        _logger = logger;
    }

    //public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context) {
    //    _logger.LogInformation($"Begin grpc call CoordinatorService: Hello <>", request.Name);
    //    return new HelloReply() {
    //        Message = $"Hello {request.Name}",
    //    };
    //}
}
