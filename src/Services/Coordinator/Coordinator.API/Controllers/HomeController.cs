namespace Microsoft.eShopOnContainers.Services.Coordinator.API.Controllers;

public class HomeController : Controller {
    // GET: /<controller>/
    public IActionResult Index() {
        return new RedirectResult("~/swagger");
    }
}
