using Grpc.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.eShopOnContainers.Services.Discount.API.Extensions;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using Polly;
using Polly.Retry;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure;

public class DiscountContextSeed
{
    ILogger<DiscountContextSeed> _logger;
    public async Task SeedAsync(DiscountContext context, IWebHostEnvironment env, IOptions<DiscountSettings> settings, ILogger<DiscountContextSeed> logger)
    {
        var policy = CreatePolicy(logger, nameof(DiscountContextSeed));
        _logger = logger;

        await policy.ExecuteAsync(async () => {
            var useCustomizationData = settings.Value.UseCustomizationData;
            var catalogURL = settings.Value.CatalogUrl;
            var contentRootPath = env.ContentRootPath;

            if (!context.Discount.Any()) {
                await context.Discount.AddRangeAsync(useCustomizationData
                    ? GetDiscountItemsFromFile(contentRootPath, logger, catalogURL)
                    : GetPreconfiguredDiscountItems());

                await context.SaveChangesAsync();
            }
        });
    }

    private IEnumerable<DiscountItem> GetDiscountItemsFromFile(string contentRootPath, ILogger<DiscountContextSeed> logger, string catalogURL) {
        string csvFileDiscountItems = Path.Combine(contentRootPath, "Setup", "DiscountItems.csv");

        if (!File.Exists(csvFileDiscountItems)) {
            Console.WriteLine($"File '{csvFileDiscountItems}' does not exist.");
            return GetPreconfiguredDiscountItems();
        }

        string[] csvheaders;
        try {
            string[] requiredHeaders = { "discount", "itemname", "itembrand", "itemtype" };
            csvheaders = GetHeaders(csvFileDiscountItems, requiredHeaders);
        } catch (Exception ex) {
            logger.LogError(ex, $"EXCEPTION ERROR: {ex.Message}");
            return GetPreconfiguredDiscountItems();
        }

        List<DiscountItem> discountItems = new List<DiscountItem>();
        IEnumerable<string[]> rows = File.ReadAllLines(csvFileDiscountItems)
            .Skip(1) // Skip header row
            .Select(row => Regex.Split(row, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"));

        foreach (string[] row in rows) {
            var discountItem = CreateDiscountItem(row, csvheaders, catalogURL);
            discountItems.Add(discountItem);
        }
        return discountItems;
    }

    private string[] GetHeaders(string csvFile, string[] requiredHeaders) {
        string[] csvheaders = File.ReadLines(csvFile).First().ToLowerInvariant().Split(',');

        if (csvheaders.Count() < requiredHeaders.Count()) {
            throw new Exception($"requiredHeader count '{requiredHeaders.Count()}' is bigger then csv header count '{csvheaders.Count()}' ");
        }

        foreach (var requiredHeader in requiredHeaders) {
            if (!csvheaders.Contains(requiredHeader)) {
                throw new Exception($"does not contain required header '{requiredHeader}'");
            }
        }
        return csvheaders;
    }

    private IEnumerable<DiscountItem> GetPreconfiguredDiscountItems() {
        return new List<DiscountItem>() {
            new() { DiscountValue = 10, ItemName = ".NET Bot Black Hoodie", ItemBrand = ".NET", ItemType = "T-Shirt" },
        };
    }

    private DiscountItem CreateDiscountItem(string[] column, string[] headers, string catalogURL) {
        if (column.Length != headers.Length) {
            throw new Exception($"column count '{column.Count()}' not the same as headers count'{headers.Count()}'");
        }

        if (!int.TryParse(column[Array.IndexOf(headers, "discount")].Trim('"').Trim(), out var discount)) {
            _logger.LogError("Failed to Parse Discount from .csv file.");
        }

        string itemName = column[Array.IndexOf(headers, "itemname")].Trim('"').Trim();
        string itemBrand = column[Array.IndexOf(headers, "itembrand")].Trim('"').Trim();
        string itemType = column[Array.IndexOf(headers, "itemtype")].Trim('"').Trim();

        var discountItem = new DiscountItem() {
            DiscountValue = discount,
            ItemName = itemName,
            ItemBrand = itemBrand,
            ItemType = itemType
        };

        return discountItem;
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
