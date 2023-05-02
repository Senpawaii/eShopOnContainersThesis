namespace Microsoft.eShopOnContainers.Services.Catalog.API.Config;

public class UrlsConfig
{

    public class CoordinatorOperations
    {
        // grpc call under REST must go trough port 80
        public static string SayHello(string message) => $"/api/v1/coordinator/{message}";

    }

    public string Coordinator { get; set; }

    public string GrpcCoordinator { get; set; }

}

