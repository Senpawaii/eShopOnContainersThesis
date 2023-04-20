using Catalog.API.DependencyServices;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using System.Threading;

namespace Catalog.API.Infrastructure.Interceptors {
    public class CatalogDBInterceptor : DbCommandInterceptor {
        const int INSERT_TYPE_OP = 1;
        const int INSERT_BRAND_OP = 2;
        const int INSERT_ITEM_OP = 3;

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

        /// <summary>
        /// Method where the different types of queries are sorted and attributed an operation identifier based on the type of query to be performed.
        /// </summary>
        /// <param name="command"></param>
        private void ModifyCommand(DbCommand command) {
            switch(command.CommandText) {
                case string s when s.Contains("INSERT INTO [CatalogType]"):
                    // Update commands that insert Data for the Catalog DB
                    UpdateInsertCommand(command, INSERT_TYPE_OP);
                    break;
                case string s when s.Contains("INSERT INTO [CatalogBrand]"):
                    UpdateInsertCommand(command, INSERT_BRAND_OP);
                    break;
                case string s when s.Contains("INSERT INTO [Catalog]"):
                    UpdateInsertCommand(command, INSERT_ITEM_OP);
                    break;
                case var s when new[] { "SELECT COUNT_BIG(*)\nFROM [Catalog] AS [c]", 
                                        "SELECT [c].[Id], [c].[Brand]\nFROM [CatalogBrand] AS [c]", 
                                        "SELECT [c].[Id], [c].[Type]\nFROM [CatalogType] AS [c]",
                                        "SELECT [c].[Id], [c].[AvailableStock], [c].[CatalogBrandId], [c].[CatalogTypeId], [c].[Description], [c].[MaxStockThreshold], [c].[Name], [c].[OnReorder], [c].[PictureFileName], [c].[Price], [c].[RestockThreshold]"
                                      }.Any(q => s.StartsWith(q)):
                    // Update Read Queries
                    UpdateReadQueriesCommand(command);
                    break;
            }
            Console.WriteLine($"==Command==\n{command.CommandText}====\n");
        }


        /* ========== UPDATE READ QUERIES ==========*/
        /// <summary>
        /// Updates the Read queries for Item Count, Brands and Types command, adding a filter for the functionality Timestamp (DateTime). 
        /// Applicable to queries that contain either:
        /// "SELECT COUNT_BIG(*) ..."; "SELECT ... FROM [CatalogBrand] ..."; "SELECT ... FROM [CatalogType] ..."
        /// </summary>
        /// <param name="command"></param>
        private void UpdateReadQueriesCommand(DbCommand command) {
            // Get the current functionality-set timeestamp
            DateTime functionalityTimestamp = _scopedMetadata.ScopedMetadataTimestamp;

            // Replace Command Text to account for new filter
            string commandTextWithFilter = command.CommandText.Replace("AS [c]", $"AS [c] WHERE [c].[Timestamp] <= '{functionalityTimestamp}'");
            command.CommandText = commandTextWithFilter;
        }


        /* ========== UPDATE WRITE QUERIES ==========*/

        private void UpdateInsertCommand(DbCommand command, int operation) {
            List<DbParameter> generatedParameters = new List<DbParameter>();

            // Get the current timestamp
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            Console.WriteLine($"Writing item with Timestamp:{timestamp}");
            // Replace Command Text to account for new parameter
            string commandWithTimestamp;
            commandWithTimestamp = UpdateInsertCommandWithTimestamp(command, operation);
            var stringParts = commandWithTimestamp.Split("VALUES", StringSplitOptions.RemoveEmptyEntries);
            string newCommandText = stringParts[0] + "VALUES";

            // Add timestamp Parameter to the Command Parameters
            newCommandText = UpdateCommandParameters(command, generatedParameters, timestamp, newCommandText, operation);

            // Clear the existing parameters and add the new parameters to the command
            command.Parameters.Clear();
            command.Parameters.AddRange(generatedParameters.ToArray());
            command.CommandText = newCommandText;
        }

        private static string UpdateCommandParameters(DbCommand command, List<DbParameter> generatedParameters, string timestamp, string newCommandText, int operation) {
            if (operation == INSERT_BRAND_OP || operation == INSERT_TYPE_OP) {
                return UpdateBrandOrTypeOp(command, generatedParameters, timestamp, newCommandText);
            }
            else {
                return UpdateItemOp(command, generatedParameters, timestamp, newCommandText);
            }
        }

        private static string UpdateItemOp(DbCommand command, List<DbParameter> generatedParameters, string timestamp, string newCommandText) {
            int numObjectsToInsert = command.Parameters.Count / 11;

            for (int i = 0; i < numObjectsToInsert; i++) {
                // Get the ID param
                var idParam = command.CreateParameter();
                idParam.ParameterName = $"@p{i * 12}";
                idParam.Value = command.Parameters[i * 11].Value;

                // Get the Foreign Key params -- Brand and Type
                var foreignBrandParam = command.CreateParameter();
                foreignBrandParam.ParameterName = $"@p{i * 12 + 1}";
                foreignBrandParam.Value = command.Parameters[i * 11 + 1].Value;
                
                var foreignTypeParam = command.CreateParameter();
                foreignTypeParam.ParameterName = $"@p{i * 12 + 2}";
                foreignTypeParam.Value = command.Parameters[i * 11 + 2].Value;

                // Get the Description param
                var descriptionParam = command.CreateParameter();
                descriptionParam.ParameterName = $"@p{i * 12 + 3}";
                descriptionParam.Value = command.Parameters[i * 11 + 3].Value;

                // Get the Name param
                var nameParam = command.CreateParameter();
                nameParam.ParameterName = $"@p{i * 12 + 4}";
                nameParam.Value = command.Parameters[i * 11 + 4].Value;

                // Get the PictureNameFile param
                var pictureNameFileParam = command.CreateParameter();
                pictureNameFileParam.ParameterName = $"@p{i * 12 + 5}";
                pictureNameFileParam.Value = command.Parameters[i * 11 + 5].Value;

                // Get the Price param
                var priceParam = command.CreateParameter();
                priceParam.ParameterName = $"@p{i * 12 + 6}";
                priceParam.Value = command.Parameters[i * 11 + 6].Value;

                // Get the Available Stock param
                var availableStockParam = command.CreateParameter();
                availableStockParam.ParameterName = $"@p{i * 12 + 7}";
                availableStockParam.Value = command.Parameters[i * 11 + 7].Value;

                // Get the Max Stock param
                var maxStockParam = command.CreateParameter();
                maxStockParam.ParameterName = $"@p{i * 12 + 8}";
                maxStockParam.Value = command.Parameters[i * 11 + 8].Value;

                // Get the onReorder param
                var onReorderParam = command.CreateParameter();
                onReorderParam.ParameterName = $"@p{i * 12 + 9}";
                onReorderParam.Value = command.Parameters[i * 11 + 9].Value;

                // Get the Restock Threshold param
                var restockThresholdParam = command.CreateParameter();
                restockThresholdParam.ParameterName = $"@p{i * 12 + 10}";
                restockThresholdParam.Value = command.Parameters[i * 11 + 10].Value;

                // Create new entry Value with the Timestamp parameter
                var timeStampParam = command.CreateParameter();
                timeStampParam.ParameterName = $"@p{i * 12 + 11}";
                timeStampParam.Value = timestamp;

                generatedParameters.Add(idParam);
                generatedParameters.Add(foreignBrandParam);
                generatedParameters.Add(foreignTypeParam);
                generatedParameters.Add(descriptionParam);
                generatedParameters.Add(nameParam);
                generatedParameters.Add(pictureNameFileParam);
                generatedParameters.Add(priceParam);
                generatedParameters.Add(availableStockParam);
                generatedParameters.Add(maxStockParam);
                generatedParameters.Add(onReorderParam);
                generatedParameters.Add(restockThresholdParam);
                generatedParameters.Add(timeStampParam);

                newCommandText += $"(@p{i * 12}, @p{i * 12 + 1}, @p{i * 12 + 2}, @p{i * 12 + 3}, @p{i * 12 + 4}, @p{i * 12 + 5}, @p{i * 12 + 6}, @p{i * 12 + 7}, @p{i * 12 + 8}, @p{i * 12 + 9}, @p{i * 12 + 10}, @p{i * 12 + 11})";
                if (i < numObjectsToInsert - 1) {
                    newCommandText += ", ";
                }
                else {
                    newCommandText += ";";
                }
            }
            return newCommandText;
        }

        private static string UpdateBrandOrTypeOp(DbCommand command, List<DbParameter> generatedParameters, string timestamp, string newCommandText) {
            int numObjectsToInsert = command.Parameters.Count / 2;

            for (int i = 0; i < numObjectsToInsert; i++) {
                // Get the ID param
                var idParam = command.CreateParameter();
                idParam.ParameterName = $"@p{i * 3}";
                idParam.Value = command.Parameters[i * 2].Value;

                // Get the Variable Operation param -- either Brand or Type parameter
                var variableOpParam = command.CreateParameter();
                variableOpParam.ParameterName = $"@p{i * 3 + 1}";
                variableOpParam.Value = command.Parameters[i * 2 + 1].Value;

                // Create new entry Value with the Timestamp parameter
                var timeStampParam = command.CreateParameter();
                timeStampParam.ParameterName = $"@p{i * 3 + 2}";
                timeStampParam.Value = timestamp;

                generatedParameters.Add(idParam);
                generatedParameters.Add(variableOpParam);
                generatedParameters.Add(timeStampParam);

                newCommandText += $"(@p{i * 3}, @p{i * 3 + 1}, @p{i * 3 + 2})";
                if (i < numObjectsToInsert - 1) {
                    newCommandText += ", ";
                }
                else {
                    newCommandText += ";";
                }
            }
            return newCommandText;
        }

        private static string UpdateInsertCommandWithTimestamp(DbCommand command, int operation) {
            if (operation == INSERT_TYPE_OP) {
                return command.CommandText.Replace("INSERT INTO [CatalogType] ([Id], [Type])", "INSERT INTO [CatalogType] ([Id], [Type], [Timestamp])");
            }
            else if(operation == INSERT_BRAND_OP) {
                return command.CommandText.Replace("INSERT INTO [CatalogBrand] ([Id], [Brand])", "INSERT INTO [CatalogBrand] ([Id], [Brand], [Timestamp])");
            } 
            else {
                return command.CommandText.Replace("INSERT INTO [Catalog] ([Id], [AvailableStock], [CatalogBrandId], [CatalogTypeId], [Description], [MaxStockThreshold], [Name], [OnReorder], [PictureFileName], [Price], [RestockThreshold])",
                    "INSERT INTO [Catalog] ([Id], [AvailableStock], [CatalogBrandId], [CatalogTypeId], [Description], [MaxStockThreshold], [Name], [OnReorder], [PictureFileName], [Price], [RestockThreshold], [Timestamp])");
            }
        }
    }
}
