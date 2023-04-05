using Catalog.API.DependencyServices;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using System.Threading;

namespace Catalog.API.Infrastructure.Interceptors {
    public class CatalogDBInterceptor : DbCommandInterceptor {
        public CatalogDBInterceptor(IScopedMetadata svc) {
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

        private void ModifyCommand(DbCommand command) {
            if(command.CommandText.Contains("INSERT INTO [CatalogType]")) {
                // Update commands that insert Data for the Catalog DB
                updateInsertTypeCommand(command);
            } 
            else if(command.CommandText.Contains("SELECT COUNT_BIG(*)\nFROM [Catalog] AS [c]")) {
                // Update commands that count the number of existing CatalogItems
                updateCountItemsCommand(command);
            }
            Console.WriteLine($"Command:{command.CommandText}");

        }

        private void updateCountItemsCommand(DbCommand command) {
            // Get the current functionality-set timeestamp
            DateTime functionalityTimestamp = _scopedMetadata.ScopedMetadataTimestamp.DateTime;

            // Replace Command Text to account for new filter
            string commandTextWithFilter = command.CommandText.Replace("AS [c]", $"AS [c] WHERE [c].[Timestamp] <= '{functionalityTimestamp}'");
            command.CommandText = commandTextWithFilter;
        }

        private void updateInsertTypeCommand(DbCommand command) {
            int numObjectsToInsert = command.Parameters.Count / 2;
            List<DbParameter> generatedParameters = new List<DbParameter>();

            // Get the current timestamp
            var timestamp = DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss");

            // Replace Command Text to account for new parameter
            string commandTextWithBrand = command.CommandText.Replace("INSERT INTO [CatalogType] ([Id], [Type])", "INSERT INTO [CatalogType] ([Id], [Type], [Timestamp])");
            var stringParts = commandTextWithBrand.Split("VALUES", StringSplitOptions.RemoveEmptyEntries);
            string newCommandText = stringParts[0] + "VALUES";

            for (int i = 0; i < numObjectsToInsert; i++) {
                // Get the ID param
                var idParam = command.CreateParameter();
                idParam.ParameterName = $"@p{i * 3}";
                idParam.Value = command.Parameters[i * 2].Value;

                // Get the Type param
                var typeParam = command.CreateParameter();
                typeParam.ParameterName = $"@p{i * 3 + 1}";
                typeParam.Value = command.Parameters[i * 2 + 1].Value;

                // Create new entry Value with the Timestamp parameter
                var timeStampParam = command.CreateParameter();
                timeStampParam.ParameterName = $"@p{i * 3 + 2}";
                timeStampParam.Value = timestamp;

                generatedParameters.Add(idParam);
                generatedParameters.Add(typeParam);
                generatedParameters.Add(timeStampParam);

                newCommandText += $"(@p{i * 3}, @p{i * 3 + 1}, @p{i * 3 + 2})";
                if(i < numObjectsToInsert - 1) {
                    newCommandText += ", ";
                } else {
                    newCommandText += ";";
                }
            }

            // Clear the eexisting parameters and add the new parameters to the command
            command.Parameters.Clear();
            command.Parameters.AddRange(generatedParameters.ToArray());
            command.CommandText = newCommandText;
        }
    }
}
