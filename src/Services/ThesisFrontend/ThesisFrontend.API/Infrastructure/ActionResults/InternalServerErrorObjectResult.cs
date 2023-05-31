namespace Microsoft.eShopOnContainers.Services.ThesisFrontend.API.Infrastructure.ActionResults;

public class InternalServerErrorObjectResult : ObjectResult {
    public InternalServerErrorObjectResult(object error)
        : base(error) {
        StatusCode = StatusCodes.Status500InternalServerError;
    }
}
