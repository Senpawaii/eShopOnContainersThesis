using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.Interceptors; 
public class DiscountDBInterceptor : DbCommandInterceptor {

    public DiscountDBInterceptor(IScopedMetadata svc) {
        _scopedMetadata = svc;
    }

    public IScopedMetadata _scopedMetadata;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {

        ModifyCommand(command);

        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) {

        ModifyCommand(command);

        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    /// <summary>
    /// Method where the different types of queries are sorted based on the type of query to be performed (READ / WRITE).
    /// </summary>
    /// <param name="command"></param>
    private void ModifyCommand(DbCommand command) {
        switch(command.CommandText) {
            case string s when s.Contains("INSERT INTO [Discount]"):
                // Update insertion command
                UpdateInsertCommand(command);
                break;
            case var s when new[] { "SELECT [d].[Id], [d].[CatalogItemId], [d].[CatalogItemName], [d].[DiscountValue]", 
                                  }.Any(q => s.StartsWith(q)):
                // Update Read Queries
                UpdateReadQueriesCommand(command);
                break;
        }
        Console.WriteLine($"==Command==\n{command.CommandText}====\n");
    }

    /* ========== UPDATE READ QUERIES ==========*/
    /// <summary>
    /// Updates the Read queries, adding a filter for the functionality Timestamp (DateTime). 
    /// </summary>
    /// <param name="command"></param>
    private void UpdateReadQueriesCommand(DbCommand command) {
        // Get the current functionality-set timeestamp
        DateTime functionalityTimestamp = _scopedMetadata.ScopedMetadataTimestamp;

        // Replace Command Text to account for new filter
        string commandTextWithFilter = command.CommandText.Replace("WHERE", $"WHERE [d].[Timestamp] <= '{functionalityTimestamp}' AND");
        Console.WriteLine($"Reading rows with timestamp <= {functionalityTimestamp}");
        command.CommandText = commandTextWithFilter;
    }


    /* ========== UPDATE WRITE QUERIES ==========*/

    private void UpdateInsertCommand(DbCommand command) {
        List<DbParameter> generatedParameters = new List<DbParameter>();

        // Get the current timestamp, this will not work like this in the future
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
        Console.WriteLine($"Writing item with Timestamp:{timestamp}");
        // Replace Command Text to account for new parameter
        string commandWithTimestamp;
        commandWithTimestamp = UpdateInsertCommandWithTimestamp(command);
        var stringParts = commandWithTimestamp.Split("VALUES", StringSplitOptions.RemoveEmptyEntries);
        string newCommandText = stringParts[0] + "VALUES";

        // Add timestamp Parameter to the Command Parameters
        newCommandText = UpdateDiscountParams(command, generatedParameters, timestamp, newCommandText);

        // Clear the existing parameters and add the new parameters to the command
        command.Parameters.Clear();
        command.Parameters.AddRange(generatedParameters.ToArray());
        command.CommandText = newCommandText;
    }

    private static string UpdateDiscountParams(DbCommand command, List<DbParameter> generatedParameters, string timestamp, string newCommandText) {
        int numObjectsToInsert = command.Parameters.Count / 4;
        //([Id], [CatalogItemId], [CatalogItemName], [DiscountValue])
        for (int i = 0; i < numObjectsToInsert; i++) {
            // Get the ID param
            var idParam = command.CreateParameter();
            idParam.ParameterName = $"@p{i * 5}";
            idParam.Value = command.Parameters[i * 4].Value;

            // Get the Catalog Item ID param
            var catalogItemIdParam = command.CreateParameter();
            catalogItemIdParam.ParameterName = $"@p{i * 5 + 1}";
            catalogItemIdParam.Value = command.Parameters[i * 4 + 1].Value;

            // Get the Catalog Item Name param
            var catalogItemNameParam = command.CreateParameter();
            catalogItemNameParam.ParameterName = $"@p{i * 5 + 2}";
            catalogItemNameParam.Value = command.Parameters[i * 4 + 2].Value;

            // Get the Discount Value param
            var discountValueParam = command.CreateParameter();
            discountValueParam.ParameterName = $"@p{i * 5 + 3}";
            discountValueParam.Value = command.Parameters[i * 4 + 3].Value;

            // Create new entry Value with the Timestamp parameter
            var timeStampParam = command.CreateParameter();
            timeStampParam.ParameterName = $"@p{i * 5 + 4}";
            timeStampParam.Value = timestamp;

            // Add the params to the List of Generated Parameters
            generatedParameters.Add(idParam);
            generatedParameters.Add(catalogItemIdParam);
            generatedParameters.Add(catalogItemNameParam);
            generatedParameters.Add(discountValueParam);
            generatedParameters.Add(timeStampParam);

            // Generate parameter string
            newCommandText += $"(@p{i * 5}, @p{i * 5 + 1}, @p{i * 5 + 2}, @p{i * 5 + 3}, @p{i * 5 + 4})";
            if (i < numObjectsToInsert - 1) {
                newCommandText += ", ";
            }
            else {
                newCommandText += ";";
            }
        }
        return newCommandText;
    }

    private static string UpdateInsertCommandWithTimestamp(DbCommand command) {
        return command.CommandText.Replace("INSERT INTO [Discount] ([Id], [CatalogItemId], [CatalogItemName], [DiscountValue])",
            "INSERT INTO [Discount] ([Id], [CatalogItemId], [CatalogItemName], [DiscountValue], [Timestamp])");
    }
}
