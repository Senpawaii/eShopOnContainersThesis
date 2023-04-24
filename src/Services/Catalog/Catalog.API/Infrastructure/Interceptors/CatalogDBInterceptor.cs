using Catalog.API.DependencyServices;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.Interceptors;
using Microsoft.Extensions.Azure;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.PortableExecutable;
using System.Threading;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Catalog.API.Infrastructure.Interceptors {
    public class CatalogDBInterceptor : DbCommandInterceptor {
        const int INSERT_COMMAND = 1;
        const int SELECT_COMMAND = 2;
        const int UPDATE_COMMAND = 3;
        const int DELETE_COMMAND = 4;
        const int UNKNOWN_COMMAND = -1;

        public CatalogDBInterceptor(IScopedMetadata svc, ISingletonWrapper wrapper) {
            _scopedMetadata = svc;
            _wrapper = wrapper;
        }

        public IScopedMetadata _scopedMetadata;
        public ISingletonWrapper _wrapper;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {

            (var commandType, var targetTable) = GetCommandInfo(command);

            if (commandType == UNKNOWN_COMMAND) {
                return result;
            }

            // Check if the Transaction ID
            var funcID = _scopedMetadata.ScopedMetadataFunctionalityID;

            // Check if is this a functionality context (ID differs from null)
            if (funcID == null) {
                return result;
            }

            if (commandType == SELECT_COMMAND) {
                UpdateSelectCommand(command);

            }
            else if (commandType == INSERT_COMMAND) {
                bool funcState = _wrapper.SingletonGetTransactionState(funcID);

                // If the Transaction is not in commit state, store data in wrapper
                if (!funcState) {
                    // TODO, the updateInsertCommand command should not be executed in this case, perhaps store the original command?
                }
                UpdateInsertCommand(command, targetTable);
            }
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) {

            (var commandType, var targetTable) = GetCommandInfo(command);

            if(commandType == UNKNOWN_COMMAND) {
                return new ValueTask<InterceptionResult<DbDataReader>>(result);
            }

            // Check if the Transaction ID
            var funcID = _scopedMetadata.ScopedMetadataFunctionalityID;

            // Check if is this a functionality context (ID differs from null)
            if (funcID == null) {
                return new ValueTask<InterceptionResult<DbDataReader>>(result);
            }

            if (commandType == SELECT_COMMAND) {
                UpdateSelectCommand(command);

            }
            else if(commandType == INSERT_COMMAND) {
                bool funcState = _wrapper.SingletonGetTransactionState(funcID);

                // If the Transaction is not in commit state, store data in wrapper
                if (!funcState) {
                    // TODO, the updateInsertCommand command should not be executed in this case, perhaps store the original command?
                }
                UpdateInsertCommand(command, targetTable);
            }

            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }

        public (int, string) GetCommandInfo(DbCommand command) {
            var commandType = GetCommandType(command);

            // Check if the command is an INSERT command.
            if (commandType == INSERT_COMMAND) {

                // Extract the name of the table being inserted into from the command text.
                var match = Regex.Match(command.CommandText, @"INSERT INTO\s*\[(?<Target>[a-zA-Z]*)\]",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

                // If regex matches, return the command type and table name
                if (match.Success) {
                    var targetTable = match.Groups["Target"].Value;
                    return (INSERT_COMMAND, targetTable);
                }
            }

            // Check if the command is an SELECT command.
            else if (commandType == SELECT_COMMAND) {
                
                // Extract the name of the table being selected from the command text.
                var match = Regex.Match(command.CommandText, @"FROM\s+\[?([\w-]+)\]?\s*",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

                // Check if the target Table is not part of the exception list of Tables
                var targetTable = match.Groups[1].Value;
                List<string> exceptionTables = new List<string>() {
                    "__EFMigrationsHistory"
                };
                if (!exceptionTables.Contains(targetTable)) {
                    return (SELECT_COMMAND, targetTable);
                }
            }

            return (UNKNOWN_COMMAND, null);
        }

        private int GetCommandType(DbCommand command) {
            var commandText = command.CommandText.ToUpperInvariant();

            if (commandText.Contains("INSERT")) {
                return INSERT_COMMAND;
            }
            else if (commandText.Contains("UPDATE")) {
                return UPDATE_COMMAND;
            }
            else if (commandText.Contains("DELETE")) {
                return DELETE_COMMAND;
            }
            else if (commandText.Contains("SELECT")) {
                return SELECT_COMMAND;
            }
            else {
                return UNKNOWN_COMMAND;
            }
        }

        /* ========== UPDATE READ QUERIES ==========*/
        /// <summary>
        /// Updates the Read queries for Item Count, Brands and Types command, adding a filter for the functionality Timestamp (DateTime). 
        /// Applicable to queries that contain either:
        /// "SELECT COUNT_BIG(*) ..."; "SELECT ... FROM [CatalogBrand] ..."; "SELECT ... FROM [CatalogType] ..."
        /// </summary>
        /// <param name="command"></param>
        private void UpdateSelectCommand(DbCommand command) {
            // Get the current functionality-set timeestamp
            DateTime functionalityTimestamp = _scopedMetadata.ScopedMetadataTimestamp;

            // Replace Command Text to account for new filter
            string commandTextWithFilter = command.CommandText.Replace("AS [c]", $"AS [c] WHERE [c].[Timestamp] <= '{functionalityTimestamp}'");
            command.CommandText = commandTextWithFilter;
        }


        /* ========== UPDATE WRITE QUERIES ==========*/

        private void UpdateInsertCommand(DbCommand command, string targetTable) {
            // Get the current timestamp - Should use the value received from the Coordinator
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            Console.WriteLine($"Writing item with Timestamp:{timestamp}");

            // Replace Command Text to account for new parameter
            string commandWithTimestamp;
            commandWithTimestamp = UpdateInsertCommandText(command, targetTable);

            // Generate new list of parameters
            List<DbParameter> newParameters = new List<DbParameter>();
            if(targetTable == "Catalog") {
                UpdateItemOp(command, newParameters, timestamp);
            } 
            else {
                UpdateBrandOrTypeOp(command, newParameters, timestamp);
            }

            // Assign new Parameters and Command text to database command
            command.Parameters.Clear();
            command.Parameters.AddRange(newParameters.ToArray());
            command.CommandText = commandWithTimestamp;

        }

        private static void UpdateItemOp(DbCommand command, List<DbParameter> generatedParameters, string timestamp) {
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

            }
        }

        private static void UpdateBrandOrTypeOp(DbCommand command, List<DbParameter> generatedParameters, string timestamp) {
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

            }
        }

        private static string UpdateInsertCommandText(DbCommand command, string targetTable) {
            string updatedCommandText;

            if (targetTable == "CatalogType") {
                updatedCommandText = Regex.Replace(command.CommandText, @"INSERT INTO\s+\[CatalogType\]\s+\(\s*\[Id\],\s*\[Type\]\s*", "$0, [Timestamp]");
                updatedCommandText = Regex.Replace(updatedCommandText, @"VALUES\s\(@p0, @p1", "$0, @p2");
            }
            else if(targetTable == "CatalogBrand") {
                updatedCommandText = Regex.Replace(command.CommandText, @"INSERT INTO\s+\[CatalogBrand\]\s+\(\s*\[Id\],\s*\[Brand\]\s*", "$0, [Timestamp]");
                updatedCommandText = Regex.Replace(updatedCommandText, @"VALUES\s\(@p0, @p1", "$0, @p2");
            }
            else {
                updatedCommandText = Regex.Replace(command.CommandText, @"INSERT INTO\s+\[Catalog\]\s+\(\s*\[Id\],\s*\[AvailableStock\],\s*\[CatalogBrandId\],\s*\[CatalogTypeId\],\s*\[Description\],\s*\[MaxStockThreshold\],\s*\[Name\],\s*\[OnReorder\],\s*\[PictureFileName\],\s*\[Price\],\s*\[RestockThreshold\]\s*", "$0, [Timestamp]");
                updatedCommandText = Regex.Replace(updatedCommandText, @"VALUES\s\(@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10", "$0, @p11");
            }
            return updatedCommandText;
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) {
            Console.WriteLine("Hello there.");
            
            // After reading data from the Database, union the data with the wrapper existing data for the current functionality.
            return base.ReaderExecuted(command, eventData, result);
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) {
            // Get the target table
            var match = Regex.Match(command.CommandText, @"FROM\s+\[?([\w-]+)\]?\s*",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);

            // Check if the target Table is not part of the exception list of Tables
            var targetTable = match.Groups[1].Value;
            var newData = new List<object[]>();

            while(await result.ReadAsync(cancellationToken)) {
                var rowValues = new List<object>();
                for (int i = 0; i < result.FieldCount; i++) {
                    var fieldType = result.GetFieldType(i);
                    switch(fieldType.Name) {
                        case "Int32":
                            rowValues.Add(result.GetInt32(i));
                            break;
                        case "Int64":
                            rowValues.Add(result.GetInt64(i));
                            break;
                        case "String":
                            rowValues.Add(result.GetString(i));
                            break;
                        case "Decimal":
                            rowValues.Add(result.GetDecimal(i));
                            break;
                        case "Boolean":
                            rowValues.Add(result.GetBoolean(i));
                            break;
                        // add additional cases for other data types as needed
                        default:
                            rowValues.Add(null);
                            break;
                    }
                }
                newData.Add(rowValues.ToArray());
            }

            var recordsAffected = result.RecordsAffected;
            var newReader = new WrapperDbDataReader(newData, result, targetTable, recordsAffected);
            return newReader;
        }
    }
}
