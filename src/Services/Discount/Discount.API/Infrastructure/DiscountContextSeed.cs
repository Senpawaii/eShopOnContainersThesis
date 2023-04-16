using Microsoft.Data.SqlClient;
using Microsoft.eShopOnContainers.Services.Catalog.API;
using Microsoft.eShopOnContainers.Services.Discount.API.Extensions;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using Polly;
using Polly.Retry;
using System.Text.RegularExpressions;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;

public class DiscountContextSeed
{
    public async Task SeedAsync(DiscountContext context, IWebHostEnvironment env, IOptions<DiscountSettings> settings, ILogger<DiscountContextSeed> logger)
    {
        var policy = CreatePolicy(logger, nameof(DiscountContextSeed));

        await policy.ExecuteAsync(async () => {
            var useCustomizationData = settings.Value.UseCustomizationData;
            var contentRootPath = env.ContentRootPath;

            if (!context.DiscountItems.Any()) {
                await context.DiscountItems.AddRangeAsync(useCustomizationData
                    ? GetDiscountItemsFromFile(contentRootPath, logger)
                    : GetPreconfiguredDiscountItems());

                await context.SaveChangesAsync();
            }

        });
    }

    private IEnumerable<DiscountItem> GetDiscountItemsFromFile(string contentRootPath, ILogger<DiscountContextSeed> logger) {
        string csvFileDiscountItems = Path.Combine(contentRootPath, "Setup", "DiscountItems.csv");

        if(!File.Exists(csvFileDiscountItems)) {
            return GetPreconfiguredDiscountItems();
        }

        string[] csvheaders;
        try {
            string[] requiredHeaders = { "discount", "itemname", "itembrand", "itemtype" };
            csvheaders = GetHeaders(csvFileDiscountItems, requiredHeaders);
        } catch(Exception ex) {
            logger.LogError(ex, $"EXCEPTION ERROR: {ex.Message}");
            return GetPreconfiguredDiscountItems();
        }

        return File.ReadAllLines(csvFileDiscountItems)
            .Skip(1) // Skip header row
            .Select(row => Regex.Split(row, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"))
            .SelectTry(column => CreateDiscountItem(column, csvheaders))
            .OnCaughtException(ex => { logger.LogError(ex, $"EXCEPTION ERROR: {ex.Message}"); return null; })
            .Where(x => x != null);
    }

    private DiscountItem CreateDiscountItem(string[] column, string[] headers) {
        // Query the Catalog microservice and register the new Discount with the catalog ID and the Item name
        return null;
    }

    private AsyncRetryPolicy CreatePolicy(ILogger<DiscountContextSeed> logger, string prefix, int retries = 3) {
        return Policy.Handle<SqlException>().
            WaitAndRetryAsync(
                retryCount: retries,
                sleepDurationProvider: retry => TimeSpan.FromSeconds(5),
                onRetry: (exception, timeSpan, retry, ctx) => {
                    logger.LogWarning(exception, "[{prefix}] Exception {ExceptionType} with message {Message} detected on attempt {retry} of {retries}", prefix, exception.GetType().Name, exception.Message, retry, retries);
                }
            );
    }
}
