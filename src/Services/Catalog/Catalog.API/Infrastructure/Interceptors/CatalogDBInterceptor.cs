using Catalog.API.DependencyServices;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.DataReaders;
using Microsoft.Extensions.Azure;
using Microsoft.IdentityModel.Tokens;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Data.Sql;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Autofac.Core;
using System.Runtime.CompilerServices;
using Microsoft.eShopOnContainers.Services.Catalog.API.DependencyServices;
using NewRelic.Api.Agent;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using Microsoft.Extensions.ObjectPool;
using SqlParameter = Microsoft.Data.SqlClient.SqlParameter;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.Interceptors;
public class CatalogDBInterceptor : DbCommandInterceptor {
    const int INSERT_COMMAND = 1;
    const int SELECT_COMMAND = 2;
    const int UPDATE_COMMAND = 3;
    const int DELETE_COMMAND = 4;
    const int UNKNOWN_COMMAND = -1;

    private static readonly Regex StoreInWrapperV2InsertRegex = new Regex(@"(?:, |\()\[(\w+)\]", RegexOptions.Compiled);
    private static readonly Regex StoreInWrapperV2UpdateRegex = new Regex(@"\[(\w+)\] = (@\w+)", RegexOptions.Compiled);
    private static readonly Regex GetTargetTableRegex = new Regex(@"(?i)(?:INSERT INTO|FROM|UPDATE)\s*\[(?<Target>[_a-zA-Z]*)\]", RegexOptions.Compiled);
    private static readonly Regex GetSelectedColumnsRegex = new Regex(@"SELECT\s+(.*?)\s+FROM", RegexOptions.Compiled);
    private static readonly Regex GetWhereConditionsRegex = new Regex(@"WHERE\s+(.*?)(?:\bGROUP\b|\bORDER\b|\bHAVING\b|\bLIMIT\b|\bUNION\b|$)", RegexOptions.Compiled);
    private static readonly Regex GetWhereConditionsColumnAndValueRegex = new Regex(@"\[\w+\]\.\[(?<columnName>\w+)\]\s*=\s*(?:N?'(?<paramValue1>[^']*?)'|(?<paramValue2>\@\w+))", RegexOptions.Compiled);

    private static readonly Dictionary<string, int> columnIndexesBrand = new Dictionary<string, int> {
        { "Id", 0 },
        { "Brand", 1 }
    };

    private static readonly Dictionary<string, int> columnIndexesItem = new Dictionary<string, int> {
        { "Id", 0 },
        { "CatalogBrandId", 1 },
        { "CatalogTypeId", 2 },
        { "Description", 3 },
        { "Name", 4 },
        { "PictureFileName", 5 },
        { "Price", 6 },
        { "AvailableStock", 7 },
        { "MaxStockThreshold", 8 },
        { "OnReorder", 9 },
        { "RestockThreshold", 10 }
    };

    private static readonly Dictionary<string, int> columnIndexesType = new Dictionary<string, int> {
          { "Id", 0 },
          { "Type", 1 }
    };

    // Benchmarking stuff
    public static ConcurrentBag<TimeSpan> Timespans = new ConcurrentBag<TimeSpan>();
    public static ConcurrentBag<TimeSpan> Timespans2 = new ConcurrentBag<TimeSpan>();
    public static ConcurrentBag<TimeSpan> Timespans3 = new ConcurrentBag<TimeSpan>();
    public static ConcurrentBag<TimeSpan> Timespans4 = new ConcurrentBag<TimeSpan>();
    public static ConcurrentBag<TimeSpan> Timespans5 = new ConcurrentBag<TimeSpan>();
    public static ConcurrentBag<TimeSpan> Timespans6 = new ConcurrentBag<TimeSpan>();

    public static TimeSpan Average(IEnumerable<TimeSpan> spans) {
        return TimeSpan.FromSeconds(spans.Select(s => s.TotalSeconds).Average());
    }

    public CatalogDBInterceptor(IScopedMetadata requestMetadata, ISingletonWrapper wrapper, ILogger<CatalogContext> logger, IOptions<CatalogSettings> settings) {
        _request_metadata = requestMetadata;
        _wrapper = wrapper;
        _logger = logger;
        _settings = settings;
    }

    public IScopedMetadata _request_metadata;
    public ISingletonWrapper _wrapper;
    private string _originalCommandText;
    public ILogger<CatalogContext> _logger;
    public IOptions<CatalogSettings> _settings;

    [Trace]
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {

        _originalCommandText = new string(command.CommandText);

        (var commandType, var targetTable) = GetCommandInfo(command);

        var clientID = _request_metadata.ClientID;

        if (clientID == null) {
            // This is a system query
            return result;
        }

        switch (commandType) {
            case UNKNOWN_COMMAND:
                return result;
            case SELECT_COMMAND:
                string clientTimestamp =  _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
                if(_settings.Value.Limit1Version) {
                    UpdateSelectCommandV2(command, targetTable, clientTimestamp);
                } else {
                    UpdateSelectCommand(command, targetTable);
                }
                WaitForProposedItemsIfNecessary(command, clientID, clientTimestamp, targetTable);
                break;
            case INSERT_COMMAND:
                // Set the request readOnly flag to false
                _request_metadata.ReadOnly = false;

                bool funcStateIns = _wrapper.SingletonGetTransactionState(clientID);
                if (!funcStateIns) {
                    // If the Transaction is not in commit state, store data in wrapper
                    var mockReader = StoreDataInWrapperV2(command, INSERT_COMMAND, targetTable);
                    result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
                }
                else {
                    // _logger.LogInformation($"ClientID: {clientID} is in commit state. Updating the command text.");
                    // Transaction is in commit state, update the command to store in the database
                    UpdateInsertCommand(command, targetTable);
                }
                break;
            case UPDATE_COMMAND:
                // Set the request readOnly flag to false
                _request_metadata.ReadOnly = false;

                bool transactionState = _wrapper.SingletonGetTransactionState(clientID);
                if(_settings.Value.Limit1Version) {
                    if (!transactionState) {
                        // Transaction is not in commit state, add to the wrapper
                        // _logger.LogInformation(command.CommandText);
                        Dictionary<string, SqlParameter> columnsToInsert = UpdateToInsert(command); // Get the columns and parameter names
                        var mockReader = StoreDataInWrapper1RowVersion(command, columnsToInsert, targetTable);
                        result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
                        break;
                    } 
                    else {
                        // _logger.LogInformation($"ClientID {clientID} is in commit state");
                        // Transaction is in commit state, update the row in the database
                        UpdateUpdateCommand(command, targetTable, clientID);
                        // _logger.LogInformation($"ClientID {clientID} updated the command text to: {command.CommandText}");
                        // _logger.LogInformation("Checkpoint command before DB commit: {0}", command.CommandText);
                        break;
                    }
                } 
                else {
                    // Convert the Update Command into an INSERT command
                    Dictionary<string, SqlParameter> columnsToInsert = UpdateToInsert(command);
                    // _logger.LogInformation("Checkpoint 1");
                    // Create a new INSERT command
                    var insertCommand = new StringBuilder("SET IMPLICIT_TRANSACTIONS OFF; SET NOCOUNT ON; INSERT INTO [")
                        .Append(targetTable)
                        .Append("] (");
                    // _logger.LogInformation("Checkpoint 2");
                    // Add the columns incased in squared brackets
                    var columnNames = columnsToInsert.Keys.Select(x => $"[{x}]");
                    insertCommand.Append(string.Join(", ", columnNames));
                    // _logger.LogInformation("Checkpoint 3");
                    // Add the values as parameters

                    //var parameterNames = columnsToInsert.Keys.Select(x => $"@{x}");
                    //var parameters = columnsToInsert.Select(x => new Microsoft.Data.SqlClient.SqlParameter($"@{x.Key}", x.Value)).ToArray();

                    var parameterNames = columnsToInsert.Select(x => x.Value.ParameterName).ToArray();
                    var parameters = columnsToInsert.Select(x => x.Value).ToArray();

                    insertCommand.Append(") VALUES (")
                        .Append(string.Join(", ", parameterNames))
                        .Append(")");
                    // _logger.LogInformation("Checkpoint 4");
                    // _logger.LogInformation("Parameters Names: {0}", string.Join(", ", parameters.Select(x => $"{x.ParameterName}: {x.Value}")));
                    
                    // Set the parameters on the command
                    command.Parameters.Clear();
                    command.Parameters.AddRange(parameters);
                    // _logger.LogInformation("Checkpoint 5");
                    // Set the command text to the INSERT command
                    command.CommandText = insertCommand.ToString();
                    // _logger.LogInformation($"ClientID: {clientID}, async UPODATE to INSERT new insertCommand: {insertCommand.ToString()}");
                    // _logger.LogInformation($"ClientID: {clientID}, Async UPDATE to INSERT command text: {command.CommandText}");
                    // If the Transaction is not in commit state, store data in wrapper
                    var updateToInsertReader = StoreDataInWrapperV2(command, INSERT_COMMAND, targetTable);
                    // _logger.LogInformation("Checkpoint 6");
                    result = InterceptionResult<DbDataReader>.SuppressWithResult(updateToInsertReader);
                }
                break;
        }

        //_logger.LogInformation($"Checkpoint 2_b_sync: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
        return result;
    }

    [Trace]
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) {
        
        _originalCommandText = new string(command.CommandText);

        (var commandType, var targetTable) = GetCommandInfo(command);

        // Check if the Transaction ID
        var clientID = _request_metadata.ClientID;
        if (clientID == null) {
            // This is a system query
            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }

        switch (commandType) {
            case UNKNOWN_COMMAND:
                return  new ValueTask<InterceptionResult<DbDataReader>>(result);
            case SELECT_COMMAND:
                string clientTimestamp =  _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
                // _logger.LogInformation($"ClientID: {clientID}, The original received SELECT command text: {command.CommandText}");
                if(_settings.Value.Limit1Version) {
                    UpdateSelectCommandV2(command, targetTable, clientTimestamp);
                } else {
                    UpdateSelectCommand(command, targetTable);
                }
                WaitForProposedItemsIfNecessary(command, clientID, clientTimestamp, targetTable);
                break;
            case INSERT_COMMAND:
                // Set the request readOnly flag to false
                _request_metadata.ReadOnly = false;

                bool funcStateIns = _wrapper.SingletonGetTransactionState(clientID);
                if (!funcStateIns) {
                    // If the Transaction is not in commit state, store data in wrapper
                    var mockReader = StoreDataInWrapperV2(command, INSERT_COMMAND, targetTable);
                    result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
                }
                else {
                    // _logger.LogInformation($"ClientID: {clientID} is in commit state. Updating the command text.");
                    // Transaction is in commit state, update the command to store in the database
                    UpdateInsertCommand(command, targetTable);
                }
                break;
            case UPDATE_COMMAND: // Performance-tested
                // Set the request readOnly flag to false
                _request_metadata.ReadOnly = false;

                bool transactionState = _wrapper.SingletonGetTransactionState(clientID);
                if(_settings.Value.Limit1Version) {
                    if (!transactionState) {
                        // Transaction is not in commit state, add to the wrapper
                        Dictionary<string, SqlParameter> columnsToInsert = UpdateToInsert(command); // Get the columns and parameter names
                        var mockReader = StoreDataInWrapper1RowVersion(command, columnsToInsert, targetTable);
                        result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
                        break;
                    } 
                    else {
                        // This is only reached for the 1 version system. 
                        // Transaction is in commit state, update the row in the database
                        UpdateUpdateCommand(command, targetTable, clientID);
                        // _logger.LogInformation($"ClientID {clientID} updated the command text to: {command.CommandText}");
                        // _logger.LogInformation("Checkpoint command before DB commit: {0}", command.CommandText);
                        break;
                    }
                } 
                else { // TODO: Technically we don't need to build a new INSERT command as we only store the data in the wrapper and supress the DB access. Point of improvement.
                    // Get the columns and parameter names that are being updated
                    Dictionary<string, SqlParameter> columnsToInsert = UpdateToInsert(command);

                    // Create a new INSERT command
                    var insertCommand = new StringBuilder("SET IMPLICIT_TRANSACTIONS OFF; SET NOCOUNT ON; INSERT INTO [")
                        .Append(targetTable)
                        .Append("] (");
                    
                    // Add the columns incased in squared brackets
                    var columnNames = columnsToInsert.Keys.Select(x => $"[{x}]");
                    insertCommand.Append(string.Join(", ", columnNames));
                    
                    // Add the parameters
                    var parameterNames = columnsToInsert.Select(x => x.Value.ParameterName).ToArray();
                    var parameters = columnsToInsert.Select(x => x.Value).ToArray();
                    insertCommand.Append(") VALUES (")
                        .Append(string.Join(", ", parameterNames))
                        .Append(")");
                    
                    // Add the parameters to the command
                    command.Parameters.Clear();
                    command.Parameters.AddRange(parameters);
                    command.CommandText = insertCommand.ToString();
                    
                    // Store data in wrapper
                    var updateToInsertReader = StoreDataInWrapperV2(command, INSERT_COMMAND, targetTable);
                    // Supress the command and return the mock reader
                    result = InterceptionResult<DbDataReader>.SuppressWithResult(updateToInsertReader);
                }
                break;
        }
        return new ValueTask<InterceptionResult<DbDataReader>>(result);;
    }

    // Not performance-tested
    private MockDbDataReader StoreDataInWrapper1RowVersion(DbCommand command, Dictionary<string, SqlParameter> columnNamesAndParameters, string targetTable) {
        var clientID = _request_metadata.ClientID;

        Dictionary<string, int> standardColumnIndexes = GetDefaultColumIndexesForUpdate(targetTable);  // Get the expected order of the columns
        // Get the number of rows being inserted
        var rowsAffected = 0;
        
        var rows = new List<object[]>();
        var row = new object[columnNamesAndParameters.Keys.Count + 1]; // Added Timestamp at the end

        foreach(var columnName in columnNamesAndParameters.Keys) {
            var sqlParameter = columnNamesAndParameters[columnName];
            var correctIndexToStore = standardColumnIndexes[columnName];
            row[correctIndexToStore] = sqlParameter.Value;
        }
        // Define the uncommitted timestamp as the current time
        row[^1] = DateTime.UtcNow;
        rowsAffected++;
        rows.Add(row);
        // Log the rows
        // foreach (object[] roww in rows) {
        //     _logger.LogInformation($"ClientID: {clientID} adding to the wrapper row: {string.Join(", ", roww)}");
        // }
        var mockReader = new MockDbDataReader(rows, rowsAffected, targetTable);

        switch (targetTable) {
            case "CatalogBrand":
                _wrapper.SingletonAddCatalogBrand(clientID, rows.ToArray());
                break;
            case "CatalogType":
                _wrapper.SingletonAddCatalogType(clientID, rows.ToArray());
                break;
            case "Catalog":
                _wrapper.SingletonAddCatalogItem(clientID, rows.ToArray());
                break;
        }
        return mockReader;
    }

    // Performance-tested
    [Trace]
    private MockDbDataReader StoreDataInWrapperV2(DbCommand command, int operation, string targetTable) {
        var clientID = _request_metadata.ClientID;
        var regex = operation == UPDATE_COMMAND ? StoreInWrapperV2UpdateRegex : StoreInWrapperV2InsertRegex; // Compiled Regex patterns to get the column names
        var matches = regex.Matches(command.CommandText); // Each match includes a column name
        
        var columns = new List<string>(Enumerable.Range(0, matches.Count)
                        .Select(i => matches[i].Groups[1].Value));
        int numberRows = command.Parameters.Count / columns.Count; // Number of rows being inserted
        var rows = new object[numberRows][];
        Dictionary<string, int> columnIndexes = null;

        int columnCount = 0;
        switch(targetTable) {
            case "CatalogBrand":
                columnIndexes = columnIndexesBrand;
                columnCount = columnIndexesBrand.Count;
                break;
            case "CatalogType":
                columnIndexes = columnIndexesType;
                columnCount = columnIndexesType.Count;
                break;
            case "Catalog":
                columnIndexes = columnIndexesItem;
                columnCount = columnIndexesItem.Count;
                break;
        }

        for (int i = 0; i < numberRows; i+=1) {
            var row = new object[columnCount + 1]; // Added Timestamp at the end
            for (int j = 0; j < columns.Count; j++) {
                var columnName = columns[j];
                var paramValue = command.Parameters["@" + columnName].Value;
                var correctIndexToStore = columnIndexes[columnName];
                row[correctIndexToStore] = paramValue;
            }
            // Define the uncommitted timestamp as the current time
            row[^1] = DateTime.UtcNow;
            rows[i] = row;
        }

        var rowsAffected = rows.GetLength(0);
        var mockReader = new MockDbDataReader(rows, rowsAffected, targetTable);

        switch (targetTable) {
            case "CatalogBrand":
                _wrapper.SingletonAddCatalogBrand(clientID, rows);
                break;
            case "CatalogType":
                _wrapper.SingletonAddCatalogType(clientID, rows);
                break;
            case "Catalog":
                _wrapper.SingletonAddCatalogItem(clientID, rows);
                break;
        }
        return mockReader;
    }


    // Performance-tested
    [Trace]
    public (int, string) GetCommandInfo(DbCommand command) {
        int commandType;
        var commandText = command.CommandText;
        if (commandText.Contains("SELECT ")) {
            commandType = SELECT_COMMAND;
        }
        else if (commandText.Contains("INSERT ")) {
            commandType = INSERT_COMMAND;
        }
        else if (commandText.Contains("UPDATE ")) {
            commandType = UPDATE_COMMAND;
        }
        else if (commandText.Contains("DELETE ")) {
            commandType = DELETE_COMMAND;
        }
        else {
            commandType = UNKNOWN_COMMAND;
        }

        var match = GetTargetTableRegex.Match(command.CommandText);
        string targetTable = match.Success ? match.Groups["Target"].Value : null;
        switch (commandType) {
            case INSERT_COMMAND:
                return (INSERT_COMMAND, targetTable);
            case SELECT_COMMAND:
                if (targetTable != null && targetTable != "__EFMigrationsHistory") {
                    return (SELECT_COMMAND, targetTable);
                }
                break;
            case UPDATE_COMMAND:
                return (UPDATE_COMMAND, targetTable);
        }
        return (UNKNOWN_COMMAND, null);
    }

    /* ========== UPDATE READ QUERIES ==========*/
    /// <summary>
    /// Updates the Read queries for Item Count, Brands and Types command, adding a filter for the client session Timestamp (DateTime). 
    /// Applicable to queries that contain either:
    /// "SELECT COUNT_BIG(*) ..."; "SELECT ... FROM [CatalogBrand] ..."; "SELECT ... FROM [CatalogType] ..."
    /// </summary>
    /// <param name="command"></param>
    
    // Not performance-tested
    [Trace]
    public void UpdateSelectCommandV2(DbCommand command, string targetTable, string clientTimestamp) {
        // Check if the SELECT includes a "*" or a column list
        (bool hasPartialRowSelection, List<string> _) = HasPartialRowSelection(command.CommandText);

        // Add the Timestamp column parameter, if the command has a partial row selection
        // if (hasPartialRowSelection) {
        //     AddTimestampToColumnList(command);
        // }
        AddTimestampToWhereList(command, targetTable, clientTimestamp);
        // Log the resulting command text
        // _logger.LogInformation($"Updated Command Text: {command.CommandText}");
    }

    // Not performance-tested
    public void AddTimestampToWhereList(DbCommand command, string targetTable, string clientTimestamp) {
        // Note: If we create a parameter of type DbType.DateTime2, the query will fail: "Failed executing DbCommand...", and I can't find a good explanation for this. 
        // The exception thrown does not show the actual error. This topic is being followed on: https://github.com/dotnet/efcore/issues/24530 
        // Create new SQL Parameter for clientTimestamp
        var clientTimestampParameter = new Microsoft.Data.SqlClient.SqlParameter("@clientTimestamp", SqlDbType.DateTime2);
        clientTimestampParameter.Value = clientTimestamp;
        command.Parameters.Add(clientTimestampParameter);

        if(command.CommandText.Contains("WHERE")) {
            string pattern = @"WHERE\s+(.*)$";
            command.CommandText = Regex.Replace(command.CommandText, pattern, "WHERE $1 AND [c].[Timestamp] <= @clientTimestamp");
        }
        else {
            command.CommandText += " WHERE [c].[Timestamp] <= @clientTimestamp";
        }
    }
    
    //public void AddTimestampToColumnList(DbCommand command) {
    //    string pattern = @"SELECT\s+(.*?)\s+FROM";
    //    command.CommandText = Regex.Replace(command.CommandText, pattern, "SELECT $1, [c].[Timestamp] FROM");
    //}


    // Performance tested
    [Trace]
    private void UpdateSelectCommand(DbCommand command, string targetTable) {
        // Get the current client session timeestamp
        DateTime clientTimestamp = _request_metadata.Timestamp;

        if (command.CommandText.Contains("COUNT")) { // We need to remove to be able to group with wrapper data after recceiving the data from the database
            //string pattern = @"SELECT\s+(.*?)\s+FROM";
            string replacement = "SELECT * FROM";
            //command.CommandText = Regex.Replace(command.CommandText, pattern, replacement);
            command.CommandText = GetSelectedColumnsRegex.Replace(command.CommandText, replacement);
        }

        // Create new SQL Parameter for clientTimestamp
        var clientTimestampParameter = new Microsoft.Data.SqlClient.SqlParameter("@clientTimestamp", SqlDbType.DateTime2);
        clientTimestampParameter.Value = clientTimestamp;
        command.Parameters.Add(clientTimestampParameter);

        string whereCondition;
        if (command.CommandText.Contains("WHERE")) {
            // Extract where condition
            Match match = GetWhereConditionsRegex.Match(command.CommandText);
            whereCondition = match.Groups[1].Value;
            // Remove the where condition from the command
            command.CommandText = command.CommandText.Replace(whereCondition, "");

            if(!_settings.Value.Limit1Version) {
                whereCondition = whereCondition.Replace("[c]", $"[{targetTable}]");
                whereCondition += $" AND [{targetTable}].[Timestamp] <= @clientTimestamp ";
            } 
            else {
                whereCondition += $" AND [c].[Timestamp] <= @clientTimestamp ";
            }
        } else {
            whereCondition = $" WHERE [{targetTable}].[Timestamp] <= @clientTimestamp ";
        }
        if (_settings.Value.Limit1Version) { 
            // There is only 1 version, so we don't need to join with the max timestamp, as the timestamp is already the max
            command.CommandText += whereCondition;
            return;
        }
        else {
            // There are multiple versions, so we need to join with the max timestamp, to get the latest version that respects the client timestamp
            switch (targetTable) {
                case "CatalogBrand":
                    command.CommandText = command.CommandText.Replace("AS [c]", $"AS [c] JOIN (SELECT CatalogBrand.Brand, max(CatalogBrand.Timestamp) as max_timestamp FROM CatalogBrand");
                    command.CommandText += whereCondition;
                    command.CommandText += "GROUP BY CatalogBrand.Brand) d on c.Brand = d.Brand AND c.Timestamp = d.max_timestamp";
                    break;
                case "CatalogType":
                    command.CommandText = command.CommandText.Replace("AS [c]", $"AS [c] JOIN (SELECT CatalogType.Type, max(CatalogType.Timestamp) as max_timestamp FROM CatalogType");
                    command.CommandText += whereCondition;
                    command.CommandText += "GROUP BY CatalogType.Type) d on c.Type = d.Type AND c.Timestamp = d.max_timestamp";
                    break;
                case "Catalog":
                    command.CommandText = command.CommandText.Replace("AS [c]", $"AS [c] JOIN (SELECT Catalog.Name, Catalog.CatalogBrandId, Catalog.CatalogTypeId, max(Catalog.Timestamp) as max_timestamp FROM Catalog");
                    command.CommandText += whereCondition;
                    command.CommandText += "GROUP BY Catalog.Name, Catalog.CatalogBrandId, Catalog.CatalogTypeId ) d on c.Name = d.Name AND c.CatalogBrandId = d.CatalogBrandId AND c.CatalogTypeId = d.CatalogTypeId and c.Timestamp = d.max_timestamp";
                    break;
            }
        }
    }

    // Not performance-tested
    [Trace]
    private void UpdateUpdateCommand(DbCommand command, string targetTable, string clientID) {
        Stopwatch sw;

        sw = Stopwatch.StartNew();
        // Get the timestamp received from the Coordinator
        string timestamp = _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        // Replace Command Text to account for new parameter: timestamp
        string commandWithTimestamp;
        commandWithTimestamp = UpdateUpdateCommandText(command, targetTable);

        sw.Stop();
        Console.WriteLine("Elapsed time 1: {0}", sw.Elapsed);
        Timespans.Add(sw.Elapsed);
        sw.Restart();

        // Add new Parameter and Command text to database command
        command.CommandText = commandWithTimestamp;
        // _logger.LogInformation($"ClientID: {clientID}, Updated command text: {command.CommandText}");
        
        // Generate timestamp parameter
        var clientTimestampParameter = new Microsoft.Data.SqlClient.SqlParameter("@clientTimestamp", SqlDbType.DateTime2);
        clientTimestampParameter.Value = timestamp;
        command.Parameters.Add(clientTimestampParameter);
        // _logger.LogInformation($"Number of parameters: {command.Parameters.Count}");
        sw.Stop();
        Console.WriteLine("Elapsed time 2: {0}", sw.Elapsed);
        Timespans2.Add(sw.Elapsed);
        _logger.LogInformation($"Average time 1: {Average(Timespans)}");
        _logger.LogInformation($"Average time 2: {Average(Timespans2)}");
    }

    // Not performance-tested
    [Trace]
    private static string UpdateUpdateCommandText(DbCommand command, string targetTable) {
        string updatedCommandText;

        // Update the CommandText to include the Timestamp column and parameter for each entry
        if (targetTable == "CatalogType") {
            updatedCommandText = Regex.Replace(command.CommandText, @"(?<lastParam>\[Type\] = @p(\d+))", "${lastParam}, [Timestamp] = @clientTimestamp");
        }
        else if(targetTable == "CatalogBrand") {
            updatedCommandText = Regex.Replace(command.CommandText, @"(?<lastParam>\[Brand\] = @p(\d+))", "${lastParam}, [Timestamp] = @clientTimestamp");
        }
        else {
            // The @p10 is the Id
            updatedCommandText = Regex.Replace(command.CommandText, @"(?<lastParam>\[RestockThreshold\] = @p(\d+))", "${lastParam}, [Timestamp] = @clientTimestamp");
        }

        return updatedCommandText;
    }

    /* ========== UPDATE WRITE QUERIES ==========*/
    // Performance-tested
    [Trace]
    private void UpdateInsertCommand(DbCommand command, string targetTable) {
        // Get the timestamp received from the Coordinator
        string timestamp = _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

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

    // Performance-Tested
    [Trace]
    private Dictionary<string, SqlParameter> UpdateToInsert(DbCommand command) {
        var matches = StoreInWrapperV2UpdateRegex.Matches(command.CommandText);
        var columns = new Dictionary<string, SqlParameter>();
        
        foreach (Match match in matches) {
            string parameterName = match.Groups[2].Value;
            string columnName = match.Groups[1].Value;
            SqlParameter sqlParameter = (SqlParameter)command.Parameters[parameterName];
            sqlParameter.ParameterName = $"@{columnName}";
            columns[columnName] = sqlParameter;
        }
        return columns;
    }

    // Performance-tested
    [Trace]
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

    // Performance-tested
    [Trace]
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

    // Performance-tested
    private static string UpdateInsertCommandText(DbCommand command, string targetTable) {
        StringBuilder newCommandTextBuilder = new StringBuilder($"SET IMPLICIT_TRANSACTIONS OFF; SET NOCOUNT ON; INSERT INTO [{targetTable}] (");
        int numberRows;
        switch(targetTable) {
            case "Catalog":
                newCommandTextBuilder.Append("[Id], [AvailableStock], [CatalogBrandId], [CatalogTypeId], [Description], [MaxStockThreshold], [Name], [OnReorder], [PictureFileName], [Price], [RestockThreshold], [Timestamp]) VALUES ");
                numberRows = command.Parameters.Count / 11; // Do not include Timestamp column in count
                for(int i = 0; i < numberRows; i++) {
                    newCommandTextBuilder.Append($"(@p{12 * i}, @p{12 * i + 1}, @p{12 * i + 2}, @p{12 * i + 3}, @p{12 * i + 4}, @p{12 * i + 5}, @p{12 * i + 6}, @p{12 * i + 7}, @p{12 * i + 8}, @p{12 * i + 9}, @p{12 * i + 10}, @p{12 * i + 11})");
                    if (i != numberRows - 1) {
                        newCommandTextBuilder.Append(", ");
                    }
                    else {
                        newCommandTextBuilder.Append(";");
                    }
                }
                break;
            case "CatalogBrand":
                newCommandTextBuilder.Append("[Id], [Brand], [Timestamp]) VALUES ");
                numberRows = command.Parameters.Count / 2; // Do not include Timestamp column in count
                for (int i = 0; i < numberRows; i++) {
                    newCommandTextBuilder.Append($"(@p{12 * i}, @p{12 * i + 1}, @p{12 * i + 2})");
                    if (i != numberRows - 1) {
                        newCommandTextBuilder.Append(", ");
                    }
                    else {
                        newCommandTextBuilder.Append(";");
                    }
                }
                break;
            case "CatalogType":
                newCommandTextBuilder.Append("[Id], [Type], [Timestamp]) VALUES ");
                numberRows = command.Parameters.Count / 2; // Do not include Timestamp column in count
                for (int i = 0; i < numberRows; i++) {
                    newCommandTextBuilder.Append($"(@p{12 * i}, @p{12 * i + 1}, @p{12 * i + 2})");
                    if (i != numberRows - 1) {
                        newCommandTextBuilder.Append(", ");
                    }
                    else {
                        newCommandTextBuilder.Append(';');
                    }
                }
                break;
            default:
                throw new Exception("Invalid target table");
        }
        return newCommandTextBuilder.ToString();
    }

    [Trace]
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) {
        var clientID = _request_metadata.ClientID;

        if(clientID == null) {
            // This is a system transaction
            return result;
        }

        string targetTable = GetTargetTable(command.CommandText);
        if (targetTable.IsNullOrEmpty()) {
            // Unsupported Table (Migration for example)
            return result;
        }

        // Check if the command was originally an update command
        if (_originalCommandText.Contains("UPDATE")) {
            var newUpdatedData = new List<object[]>();
            newUpdatedData.Add(new object[] { 1 });
            // _logger.LogInformation("ClientID: {0}", clientID);
            return new WrapperDbDataReader(newUpdatedData, result, targetTable);
        }

        // Check if the command is an INSERT command and has yet to be committed
        if (command.CommandText.Contains("INSERT") && !_wrapper.SingletonGetTransactionState(clientID)) {
            // _logger.LogInformation($"ClientID: {clientID}, insert commandText= {_originalCommandText}");
            return result;
        }

        // Note: It is important that the wrapper data is cleared before saving it to the database, when the commit happens.

        var newData = new List<object[]>();

        while (result.Read()) {
            var rowValues = new List<object>();
            for (int i = 0; i < result.FieldCount; i++) {
                var fieldName = result.GetName(i);
                var columnIndex = result.GetOrdinal(fieldName);
                var fieldType = result.GetFieldType(columnIndex);
                switch(fieldType.Name) {
                    case "Int32":
                        rowValues.Add(result.GetInt32(columnIndex));
                        break;
                    case "Int64":
                        rowValues.Add(result.GetInt64(columnIndex));
                        break;
                    case "String":
                        rowValues.Add(result.GetString(columnIndex));
                        break;
                    case "Decimal":
                        rowValues.Add(result.GetDecimal(columnIndex));
                        break;
                    case "Boolean":
                        rowValues.Add(result.GetBoolean(columnIndex));
                        break;
                    case "DateTime":
                        rowValues.Add(result.GetDateTime(columnIndex));
                        break;
                    // add additional cases for other data types as needed
                    default:
                        rowValues.Add(null);
                        break;
                }
            }
            newData.Add(rowValues.ToArray());
        }

        // Read the data from the Wrapper structures
        if (command.CommandText.Contains("SELECT")) {
            List<object[]> wrapperData = null;

            switch (targetTable) {
                case "CatalogBrand":
                    wrapperData = _wrapper.SingletonGetCatalogBrands(clientID).ToList();
                    break;
                case "CatalogType":
                    wrapperData = _wrapper.SingletonGetCatalogTypes(clientID).ToList();
                    break;
                case "Catalog":
                    wrapperData = _wrapper.SingletonGetCatalogItems(clientID).ToList();
                    break;
            }

            // Filter the results to display only 1 version of data for both the Wrapper Data as well as the DB data
            //newData = GroupVersionedObjects(newData, targetTable);
            wrapperData = GroupVersionedObjects(wrapperData, targetTable);
            //_logger.LogInformation($"Checkpoint 2_c_4: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

            if (wrapperData.Count > 0) {
                if (HasFilterCondition(command.CommandText)) {
                    // The select query contains a WHERE clause
                    wrapperData = FilterData(wrapperData, command);

                    if(!HasCountClause(command.CommandText)) {
                        // Only add the rows if the data from the DB is not a single count product
                        foreach(var wrapperRow in wrapperData) {
                            // TODO: Replace the existing row with the same identifier in the newData for the one present in the wrapper
                            newData.Add(wrapperRow);
                        }
                    }
                }
                //_logger.LogInformation($"Checkpoint 2_c_4: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");


                // We group the data again to ensure that the data is grouped by the same version in the union of the DB and Wrapper data
                newData = GroupVersionedObjects(newData, targetTable);
                //_logger.LogInformation($"Checkpoint 2_c_5: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

                (bool hasOrderBy, string orderByColumn, string typeOrder) = HasOrderByCondition(command.CommandText);
                if(hasOrderBy) {
                    // The select query has an ORDER BY clause
                    newData = OrderBy(newData, orderByColumn, typeOrder, targetTable);
                }

                (bool hasTopClause, int nElem) = HasTopClause(command.CommandText);
                if (hasTopClause) {
                    // The select query has a take clause
                    newData = newData.Take(nElem).ToList();
                }

                // Special Cases: offset and fetch next TODO: fix them, can only be executed after the result are merged
                (bool hasOffset, string offsetParam) = HasOffsetCondition(command.CommandText);
                if (hasOffset) {
                    // the select query has an offset
                    newData = OffsetData(command, newData, offsetParam);
                }

                (bool hasFetchNext, string fetchRowsParam) = HasFetchNextCondition(command.CommandText);
                if (hasFetchNext) {
                    // The select query has a fetch next limit
                    newData = FetchNext(command, newData, fetchRowsParam);
                }
                //_logger.LogInformation($"Checkpoint 2_c_6: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

            }

            if (HasCountClause(_originalCommandText)) {
                List<object[]> countedData = new List<object[]>();
                object[] data = new object[] { newData.Count + wrapperData.Count };
                countedData.Add(data);

                //_logger.LogInformation($"Checkpoint 2_d: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
                return new WrapperDbDataReader(countedData, result, targetTable);
            }
            //_logger.LogInformation($"Checkpoint 2_c_7: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

            // Search for partial SELECTION on the original unaltered commandText
            (bool hasPartialRowSelection, List<string> selectedColumns) = HasPartialRowSelection(_originalCommandText);
            if (hasPartialRowSelection) {
                // foreach (string column in selectedColumns) {
                //     _logger.LogInformation("The column {0} was selected", column);
                // }

                // The select query has a partial row selection
                var originalNumColumns = newData[0].Length;
                for(int i = 0; i < newData.Count; i++) {
                    newData[i] = PartialRowSelectionV2(command.CommandText, newData[i], selectedColumns, result.GetSchemaTable(), clientID);
                }
                // _logger.LogInformation($"ClientID {clientID}: Applied the partial row selection. The original data had {originalNumColumns} columns. The new data has {newData[0].Length} columns. CommandText was {_originalCommandText}");
                // foreach (object value in newData[0]) {
                //     _logger.LogInformation($"ClientID {clientID} The value {value.ToString()} was selected");
                // }
            }
        }
        //_logger.LogInformation($"Checkpoint 2_e: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

        if(_settings.Value.Limit1Version) {
            if (HasCountClause(_originalCommandText) && newData[0][0].Equals(0)) {
                // _logger.LogInformation("The database count returned 0. Adding 1 to the count for default.");
                newData.Add(new object[] { 1 });
            }
            if(newData.IsNullOrEmpty()) {
                // _logger.LogInformation("The database + wrapper returned no data");
                // If newData is empty return a reader with a single default row
                switch(targetTable) {
                    case "Catalog":
                        newData.Add(new object[] {
                            1, // Id
                            ".NET Bot Black Hoodie", // Name
                            ".NET Bot Black Hoodie, and more", // Description
                            11000, // Price
                            "1.png", // PictureFileName
                            2, // CatalogTypeId
                            1, // CatakigBrandId
                            100, // AvailableStock
                            0, // RestockThreshold
                            0, // MaxStockThreshold
                            false // OnReorder
                        });
                        break;
                    case "CatalogType":
                        newData.Add(new object[] { 0, "" });
                        break;
                    case "CatalogBrand":
                        newData.Add(new object[] { 0, "" });
                        break;
                }
                (bool hasPartialRowSelection, List<string> selectedColumns) = HasPartialRowSelection(_originalCommandText);
                if (hasPartialRowSelection) {
                    // foreach (string column in selectedColumns) {
                    //     _logger.LogInformation("The column {0} was selected", column);
                    // }

                    // The select query has a partial row selection
                    var originalNumColumns = newData[0].Length;
                    for(int i = 0; i < newData.Count; i++) {
                        newData[i] = PartialRowSelection(command.CommandText, newData[i], selectedColumns);
                    }
                    // _logger.LogInformation($"ClientID {clientID}: Applied the partial row selection. The original data had {originalNumColumns} columns. The new data has {newData[0].Length} columns. CommandText was {_originalCommandText}");
                    // foreach (object value in newData[0]) {
                    //     _logger.LogInformation($"ClientID {clientID} The value {value.ToString()} was selected");
                    // }
                }
            }
        }

        return new WrapperDbDataReader(newData, result, targetTable);
    }

    [Trace]
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) {
        //_logger.LogInformation($"Checkpoint 2_c: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

        var clientID = _request_metadata.ClientID;
        
        if(clientID == null) {
            // This is a system transaction or a database initial population
            return result;
        }
        // _logger.LogInformation($"ClientID {clientID}, executing ReaderExecutedAsync.");
        string targetTable = GetTargetTable(command.CommandText);
        if (targetTable.IsNullOrEmpty()) {
            // Unsupported Table (Migration for example)
            return result;
        }

        // Check if the command was originally an update command
        if (_originalCommandText.Contains("UPDATE")) {
            // _logger.LogInformation($"ClientID {clientID}, update commandText= {_originalCommandText}");
            var newUpdatedData = new List<object[]>();
            newUpdatedData.Add(new object[] { 1 });
            // _logger.LogInformation("ClientID: {0}", clientID);
            return new WrapperDbDataReader(newUpdatedData, result, targetTable);
        }

        // Check if the command is an INSERT command and has yet to be committed
        if (command.CommandText.Contains("INSERT") && !_wrapper.SingletonGetTransactionState(clientID)) {
            // _logger.LogInformation($"ClientID: {clientID}, insert commandText= {_originalCommandText}");
            return result;
        }

        // Note: It is important that the wrapper data is cleared before saving it to the database, when the commit happens.
        
        var newData = new List<object[]>();
        // _logger.LogInformation($"ClientID {clientID}, Before readasync.");

        while(await result.ReadAsync(cancellationToken)) {
            // _logger.LogInformation($"ClientID: {clientID}, starting to read the result of command text: {command.CommandText}");
            var rowValues = new List<object>();
            for (int i = 0; i < result.FieldCount; i++) {
                var fieldName = result.GetName(i);
                var columnIndex = result.GetOrdinal(fieldName);
                var fieldType = result.GetFieldType(columnIndex);
                switch(fieldType.Name) {
                    case "Int32":
                        rowValues.Add(result.GetInt32(columnIndex));
                        break;
                    case "Int64":
                        rowValues.Add(result.GetInt64(columnIndex));
                        break;
                    case "String":
                        rowValues.Add(result.GetString(columnIndex));
                        break;
                    case "Decimal":
                        rowValues.Add(result.GetDecimal(columnIndex));
                        break;
                    case "Boolean":
                        rowValues.Add(result.GetBoolean(columnIndex));
                        break;
                    case "DateTime":
                        rowValues.Add(result.GetDateTime(columnIndex));
                        break;
                    // add additional cases for other data types as needed
                    default:
                        rowValues.Add(null);
                        break;
                }
                // _logger.LogInformation($"ClientID: {clientID}, FieldName: {fieldName}, FieldType: {fieldType.Name}, FieldValue: {rowValues[i]}, ColumnIndex: {columnIndex}");
            }

            newData.Add(rowValues.ToArray());
        }

        // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count}");

        // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - A");
        // Read the data from the Wrapper structures
        if (command.CommandText.Contains("SELECT")) {
            List<object[]> wrapperData = null;

            switch (targetTable) {
                case "CatalogBrand":
                    wrapperData = _wrapper.SingletonGetCatalogBrands(clientID).ToList();
                    break;
                case "CatalogType":
                    wrapperData = _wrapper.SingletonGetCatalogTypes(clientID).ToList();
                    break;
                case "Catalog":
                    wrapperData = _wrapper.SingletonGetCatalogItems(clientID).ToList();
                    break;
            }
            // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - B");

            // Filter the results to display only 1 version of data for both the Wrapper Data as well as the DB data
            //newData = GroupVersionedObjects(newData, targetTable);

            wrapperData = GroupVersionedObjects(wrapperData, targetTable);
            //_logger.LogInformation($"Checkpoint 2_c_4: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

            if (wrapperData.Count > 0) {
                if (HasFilterCondition(command.CommandText)) {
                    // The select query contains a WHERE clause
                    wrapperData = FilterData(wrapperData, command);

                    if(!HasCountClause(command.CommandText)) {
                        // Only add the rows if the data from the DB is not a single count product
                        foreach(var wrapperRow in wrapperData) {
                            // TODO: Replace the existing row with the same identifier in the newData for the one present in the wrapper
                            newData.Add(wrapperRow);
                        }
                    }
                }
                //_logger.LogInformation($"Checkpoint 2_c_4: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");


                // We group the data again to ensure that the data is grouped by the same version in the union of the DB and Wrapper data
                newData = GroupVersionedObjects(newData, targetTable);
                //_logger.LogInformation($"Checkpoint 2_c_5: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

                (bool hasOrderBy, string orderByColumn, string typeOrder) = HasOrderByCondition(command.CommandText);
                if(hasOrderBy) {
                    // The select query has an ORDER BY clause
                    newData = OrderBy(newData, orderByColumn, typeOrder, targetTable);
                }

                (bool hasTopClause, int nElem) = HasTopClause(command.CommandText);
                if (hasTopClause) {
                    // The select query has a take clause
                    newData = newData.Take(nElem).ToList();
                }

                // Special Cases: offset and fetch next TODO: fix them, can only be executed after the result are merged
                (bool hasOffset, string offsetParam) = HasOffsetCondition(command.CommandText);
                if (hasOffset) {
                    // the select query has an offset
                    newData = OffsetData(command, newData, offsetParam);
                }

                (bool hasFetchNext, string fetchRowsParam) = HasFetchNextCondition(command.CommandText);
                if (hasFetchNext) {
                    // The select query has a fetch next limit
                    newData = FetchNext(command, newData, fetchRowsParam);
                }
                //_logger.LogInformation($"Checkpoint 2_c_6: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

            }
            // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - C");

            if (HasCountClause(_originalCommandText)) {
                List<object[]> countedData = new List<object[]>();
                object[] data = new object[] { newData.Count + wrapperData.Count };
                countedData.Add(data);

                //_logger.LogInformation($"Checkpoint 2_d: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
                return new WrapperDbDataReader(countedData, result, targetTable);
            }
            //_logger.LogInformation($"Checkpoint 2_c_7: : {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
            // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - D");

            if(!newData.IsNullOrEmpty()) {
                // Search for partial SELECTION on the original unaltered commandText
                (bool hasPartialRowSelection, List<string> selectedColumns) = HasPartialRowSelection(_originalCommandText);
                if (hasPartialRowSelection) {
                    // foreach (string column in selectedColumns) {
                    //     _logger.LogInformation("The column {0} was selected", column);
                    // }
                    // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - E");

                    // The select query has a partial row selection
                    var originalNumColumns = newData[0].Length;
                    for(int i = 0; i < newData.Count; i++) {
                        newData[i] = PartialRowSelectionV2(command.CommandText, newData[i], selectedColumns, result.GetSchemaTable(), clientID);
                    }
                    // _logger.LogInformation($"ClientID {clientID}: Applied the partial row selection. The original data had {originalNumColumns} columns. The new data has {newData[0].Length} columns. CommandText was {_originalCommandText}");
                    // foreach (object value in newData[0]) {
                    //     _logger.LogInformation($"ClientID {clientID} The value {value.ToString()} was selected");
                    // }
                }
            }
        }
        //_logger.LogInformation($"Checkpoint 2_e: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

        if(_settings.Value.Limit1Version) {
            // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - E2");
            // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - E2.1");

            var nullOrEmpty = newData.IsNullOrEmpty();
            // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - E2.2");

            // _logger.LogInformation($"ClientID {clientID}: Limiting the number of versions to 1. Newdata is empty?: <{nullOrEmpty}>");

            if (HasCountClause(_originalCommandText) && newData[0][0].Equals(0)) {
                // _logger.LogInformation("The database count returned 0. Adding 1 to the count for default.");
                // _logger.LogInformation($"ClientID {clientID}: The database count returned 0. Adding 1 to the count for default.");
                newData.Add(new object[] { 1 });
            }
            // _logger.LogInformation($"ClientID {clientID}: newData size: {newData.Count}");
            if(newData.IsNullOrEmpty()) {
                // _logger.LogInformation("The database + wrapper returned no data");
                // If newData is empty return a reader with a single default row
                // _logger.LogInformation($"ClientID {clientID}: The database + wrapper returned no data");

                // We need to add the new object[] according to the schema of the table in the result
                switch(targetTable) {
                    case "Catalog":
                        // var defaultValues = new Dictionary<string, object> {
                        //     { "Id", 1 },
                        //     { "Name", ".NET Bot Black Hoodie" },
                        //     { "Description", ".NET Bot Black Hoodie, and more" },
                        //     { "Price", 11000 },
                        //     { "PictureFileName", "1.png" },
                        //     { "CatalogTypeId", 2 },
                        //     { "CatalogBrandId", 1 },
                        //     { "AvailableStock", 100 },
                        //     { "RestockThreshold", 0 },
                        //     { "MaxStockThreshold", 0 },
                        //     { "OnReorder", false }
                        // };    

                        // var schemaTable = result.GetSchemaTable();
                        // var schemaColumns = schemaTable.Columns;
                        // var newRow = new object[schemaColumns.Count];
                        // _logger.LogInformation($"ClientID: {clientID} Number of columns in the schema table: {schemaColumns.Count}");
                        // foreach(DataColumn column in schemaColumns) {
                        //     _logger.LogInformation($"ClientID: {clientID} Column name: {column.ColumnName}, column type: {column.DataType}");
                        // }

                        // foreach(DataColumn column in schemaColumns) {
                        //     var columnName = column.ColumnName;
                        //     _logger.LogInformation($"ClientID: {clientID} Column name: {columnName}");
                        //     var columnIndex = schemaColumns.IndexOf(columnName);
                        //     _logger.LogInformation($"ClientID: {clientID} ColumnName: {columnName} Column index: {columnIndex}");
                        //     newRow[columnIndex] = defaultValues[columnName];
                        // }

                         newData.Add(new object[] {
                            1, // Id
                            100, // AvailableStock
                            1, // CatakigBrandId
                            2, // CatalogTypeId
                            ".NET Bot Black Hoodie, and more", // Description
                            0, // MaxStockThreshold
                            ".NET Bot Black Hoodie", // Name
                            false, // OnReorder
                            "1.png", // PictureFileName
                            0, // RestockThreshold
                            11000 // Price
                        });
//  SELECT TOP(2) [c].[Id], [c].[AvailableStock], [c].[CatalogBrandId], [c].[CatalogTypeId], [c].[Description], [c].[MaxStockThreshold], [c].[Name], [c].[OnReorder], [c].[PictureFileName], [c].[Price], [c].[RestockThreshold]
                        break;
                    case "CatalogType":
                        newData.Add(new object[] { 0, "" });
                        break;
                    case "CatalogBrand":
                        newData.Add(new object[] { 0, "" });
                        break;
                }
            }
            // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - F");

            (bool hasPartialRowSelection, List<string> selectedColumns) = HasPartialRowSelection(_originalCommandText);
            if (hasPartialRowSelection) {
                // _logger.LogInformation($"ClientID: {clientID}, has partial row selection");
                // foreach (string column in selectedColumns) {
                //     _logger.LogInformation("The column {0} was selected", column);
                // }
                // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - G");

                // The select query has a partial row selection
                var originalNumColumns = newData[0].Length;
                for(int i = 0; i < newData.Count; i++) {
                    newData[i] = PartialRowSelectionV2(command.CommandText, newData[i], selectedColumns, result.GetSchemaTable(), clientID);
                }
                // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - H");

                // _logger.LogInformation($"ClientID {clientID}: Applied the partial row selection. The original data had {originalNumColumns} columns. Is newData empty?: <{nullOrEmpty}> CommandText was {_originalCommandText}");
            }
            else {
                // _logger.LogInformation($"ClientID: {clientID}, has no partial row selection");
            }
        }
        // _logger.LogInformation($"ClientID {clientID}, newData size: {newData.Count} - I");

        return new WrapperDbDataReader(newData, result, targetTable);
    }

    [Trace]
    private List<object[]> OrderBy(List<object[]> newData, string orderByColumn, string typeOrder, string targetTable) {
        Dictionary <string, int> columnIndexes = GetDefaultColumIndexes(targetTable);
        int sortByIndex = columnIndexes[orderByColumn];

        // Determine the type of the objects to compare
        Type sortColumnType = newData.First()[sortByIndex].GetType();

        newData = newData.OrderBy(arr => Convert.ChangeType(arr[sortByIndex], sortColumnType)).ToList();
        return newData;
    }

    [Trace]
    private List<object[]> OffsetData(DbCommand command, List<object[]> newData, string offsetParam) {
        DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == offsetParam);
        int offsetValue = Convert.ToInt32(parameter.Value);
        newData = newData.Skip(offsetValue).ToList();
        return newData;
    }

    [Trace]
    private List<object[]> FetchNext(DbCommand command, List<object[]> newData, string fetchRowsParam) {
        DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == fetchRowsParam);
        int fetchRows = Convert.ToInt32(parameter.Value);
        newData = newData.Take(fetchRows).ToList();
        return newData;
    }

    [Trace]
    private List<object[]> GroupVersionedObjects(List<object[]> newData, string targetTable) {
        var dateTimeComparer = Comparer<object>.Create((x, y) => {
            DateTime xValue = (DateTime)x;
            DateTime yValue = (DateTime)y;
            return xValue.CompareTo(yValue);
        });

        var groupByColumns = GetUniqueIndentifierColumns(targetTable);
        
        switch(targetTable) {
            case "CatalogBrand":
                var tempData = newData.GroupBy(row => row[1]);

                newData = tempData
                    .Select(group => group
                        .OrderByDescending(row => row[^1], dateTimeComparer).First())
                    .ToList();
                break;
            case "CatalogType":
                newData = newData
                    .GroupBy(row => row[1])
                    .Select(group => group
                        .OrderByDescending(row => row[^1], dateTimeComparer).First())
                    .ToList();
                break;
            case "Catalog":
                var tempData2 = newData.Select(row => new { CatalogBrandId = row[1], CatalogTypeId = row[2], Name = row[4] });
                var newDataGrouped = newData
                    .GroupBy(row => new { CatalogBrandId = row[1], CatalogTypeId = row[2], Name = row[4] });

                newData = newDataGrouped
                    .Select(group => group
                        .OrderByDescending(row => row[^1], dateTimeComparer).First())
                    .ToList();
                break;
        }

        return newData;
    }

    [Trace]
    private object[] PartialRowSelection(string commandText, object[] row, List<string> selectedColumns) {
        string targetTable = GetTargetTable(commandText);
        object[] newRow = new object[selectedColumns.Count];

        // Get the default indexes for the columns of the Catalog Database
        Dictionary<string, int> columnIndexes = GetDefaultColumIndexes(targetTable);

        // _logger.LogInformation($"Selected {columnIndexes.Count} columns. The commandText was {commandText}");

        for (int i = 0; i < selectedColumns.Count; i++) {
            switch(selectedColumns[i]) {
                case "Id":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[0]}");
                    newRow[i] = row[0];
                    break;
                case "Name":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[1]}");
                    newRow[i] = row[1];
                    break;
                case "Description":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[2]}");
                    newRow[i] = row[2];
                    break;
                case "Price":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[3]}");
                    newRow[i] = row[3];
                    break;
                case "PictureFileName":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[4]}");
                    newRow[i] = row[4];
                    break;
                case "CatalogTypeId":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[5]}");
                    newRow[i] = row[5];
                    break;
                case "CatalogBrandId":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[6]}");
                    newRow[i] = row[6];
                    break;
                case "AvailableStock":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[7]}");
                    newRow[i] = row[7];
                    break;
                case "RestockThreshold":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[8]}");
                    newRow[i] = row[8];
                    break;
                case "MaxStockThreshold":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[9]}");
                    newRow[i] = row[9];
                    break;
                case "OnReorder":   
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[10]}");
                    newRow[i] = row[10];
                    break;
            }
        }   
        return newRow;
    }

    [Trace]
    private object[] PartialRowSelectionV2(string commandText, object[] row, List<string> selectedColumns, DataTable schemaTable, string clientID) {
        // Build a new row with the selected columns only, as defined in the original commandText
        var columnIndexes = new Dictionary<string, int>();
        // _logger.LogInformation($"ClientID: {clientID} Starting to apply the partial row selection. CommandText: {commandText}");
        try {
            for (int i = 0; i < schemaTable.Rows.Count; i++) {
                var columnName = schemaTable.Rows[i]["ColumnName"].ToString();
                var columnIndex = (int)schemaTable.Rows[i]["ColumnOrdinal"];
                columnIndexes[columnName] = columnIndex;
                // _logger.LogInformation($"ClientID: {clientID} Added column {columnName} with index {columnIndex} to the columnIndexes dictionary");            
            }

            var newRow = new object[selectedColumns.Count];
            for (int i = 0; i < selectedColumns.Count; i++) {
                var columnName = selectedColumns[i];
                var columnIndex = columnIndexes[columnName];
                newRow[i] = row[columnIndex];
                // _logger.LogInformation($"ClientID: {clientID} Added column {columnName} with value {newRow[i]} to the new row");
            }

            return newRow;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error while applying the partial row selection");
            return row;
        }
    }

    [Trace]
    private List<object[]> FilterData(List<object[]> wrapperData, DbCommand command) {
        string commandText = command.CommandText;
        var targetTable = GetTargetTable(commandText);

        var filteredData = ApplyWhereFilterIfExist(wrapperData, command).ToList();

        return filteredData;
    }

    [Trace]
    private IEnumerable<object[]> ApplyWhereFilterIfExist(List<object[]> wrapperData, DbCommand command) {
        List<object[]> filteredData = new List<object[]>();
        string targetTable = GetTargetTable(command.CommandText);

        // Store the column name associated with the respective value for comparison
        Dictionary<string, object> parametersFilter = new Dictionary<string, object>();
        //Regex regex = new Regex(@"\.*\[[a-zA-Z]\].\[(?<columnName>\S*)\]\s*=\s*(?<paramValue>\S*)");
        Regex regex = new Regex(@"\.*\[[a-zA-Z]\].\[(?<columnName>\S*)\]\s*=\s*N?'(?<paramValue>.*?)'");
        MatchCollection matches = regex.Matches(command.CommandText);

        if(matches.Count == 0) {
            // No Where filters exist
            return wrapperData;
        }

        // Extract parameter names from the matches
        for (int i = 0; i < matches.Count; i++) {
            string columnName = matches[i].Groups[1].Value;
            string parameterParametrization = matches[i].Groups[2].Value;

            // Check if the first index of the string is a "@" symbol
            if (parameterParametrization[0] == '@') {
                DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == parameterParametrization);
                parametersFilter[columnName] = parameter.Value;
            }
            else {
                parametersFilter[columnName] = parameterParametrization;
            }
        }

        // Get the default indexes for the columns of the Catalog Database
        var columnIndexes = GetDefaultColumIndexes(targetTable);

        foreach (KeyValuePair<string, object> parameter in parametersFilter) {
            if (columnIndexes.TryGetValue(parameter.Key, out int columnIndex)) {
                filteredData = wrapperData
                        .Where(row => row[columnIndex] != null && row[columnIndex].ToString() == parameter.Value.ToString())
                        .ToList();
            }
        }
        return filteredData;
    }

    [Trace]
    private static Dictionary<string, int> GetDefaultColumIndexesForUpdate(string targetTable) {
        Dictionary<string, int> columnIndexes = new Dictionary<string, int>();

        // Apply the filters according to the Target Table
        switch (targetTable) {
            case "CatalogBrand":
                columnIndexes.Add("Id", 0);
                columnIndexes.Add("Brand", 1);
                break;
            case "Catalog":
                columnIndexes.Add("Id", 0);
                columnIndexes.Add("CatalogBrandId", 1);
                columnIndexes.Add("CatalogTypeId", 2);
                columnIndexes.Add("Description", 3);
                columnIndexes.Add("Name", 4);
                columnIndexes.Add("PictureFileName", 5);
                columnIndexes.Add("Price", 6);
                columnIndexes.Add("AvailableStock", 7);
                columnIndexes.Add("MaxStockThreshold", 8);
                columnIndexes.Add("OnReorder", 9);
                columnIndexes.Add("RestockThreshold", 10);
                break;
            case "CatalogType":
                columnIndexes.Add("Id", 0);
                columnIndexes.Add("Type", 1);
                break;
        }
        return columnIndexes;
    }

    [Trace]
    private static Dictionary<string, int> GetDefaultColumIndexes(string targetTable) {
        Dictionary<string, int> columnIndexes = new Dictionary<string, int>();

        // Apply the filters according to the Target Table
        switch (targetTable) {
            case "CatalogBrand":
                columnIndexes.Add("Id", 0);
                columnIndexes.Add("Brand", 1);
                break;
            case "Catalog":
                columnIndexes.Add("Id", 0);
                columnIndexes.Add("CatalogBrandId", 1);
                columnIndexes.Add("CatalogTypeId", 2);
                columnIndexes.Add("Description", 3);
                columnIndexes.Add("Name", 4);
                columnIndexes.Add("PictureFileName", 5);
                columnIndexes.Add("Price", 6);
                columnIndexes.Add("AvailableStock", 7);
                columnIndexes.Add("MaxStockThreshold", 8);
                columnIndexes.Add("OnReorder", 9);
                columnIndexes.Add("RestockThreshold", 10);
                break;
            case "CatalogType":
                columnIndexes.Add("Id", 0);
                columnIndexes.Add("Type", 1);
                break;
        }
        return columnIndexes;
    }

    [Trace]
    private static Dictionary<string, int> GetUniqueIndentifierColumns(string targetTable) {
        Dictionary<string, int> columnUniqueIdentifiers = new Dictionary<string, int>();

        switch(targetTable) {
            case "Catalog":
                columnUniqueIdentifiers.Add("CatalogBrandId", 1);
                columnUniqueIdentifiers.Add("CatalogTypeId", 2);
                columnUniqueIdentifiers.Add("Name", 4);
                break;
            case "CatalogBrand":
                columnUniqueIdentifiers.Add("Brand", 1);
                break;
            case "CatalogType":
                columnUniqueIdentifiers.Add("Type", 1);
                break;
        }

        return columnUniqueIdentifiers;
    }

    [Trace]
    private string GetTargetTable(string commandText) {
        // Extract the name of the table target by the SQL query
        var match = Regex.Match(commandText, @"(?i)(?:INSERT INTO|FROM|UPDATE)\s*\[(?<Target>[_a-zA-Z]*)\]",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups["Target"].Value : null;
    }

    [Trace]
    private (Boolean, string, string) HasOrderByCondition(string commandText) {
        // Searches for either a ASC or DESC pattern. Will not match in the default case.
        var match = Regex.Match(commandText, @"ORDER BY\s+\[[_a-zA-Z]+\]\.\[(?<Column>[_a-zA-Z]+)\]\s*(?<Type>ASC|DESC|)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        string typeOrdering = match.Success ? match.Groups["Type"].Value : null;
        if(typeOrdering == "") {
            // ASC/DESC is missing. Assume default=ASC
            typeOrdering = "ASC";
        }
        return match.Success ? (true, match.Groups["Column"].Value, typeOrdering) : (false, null, null);

    }

    [Trace]
    private (Boolean, string) HasOffsetCondition(string commandText) {
        var match = Regex.Match(commandText, @"OFFSET\s+(?<OffsetValue>[@_a-zA-Z\d]+)\s+ROWS",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? (true, match.Groups["OffsetValue"].Value) : (false, null);
    }

    [Trace]
    private (Boolean, string) HasFetchNextCondition(string commandText) {
        var match = Regex.Match(commandText, @"FETCH NEXT\s+(?<FetchRows>[@_a-zA-Z\d]+)\s+ROWS",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? (true, match.Groups["FetchRows"].Value) : (false, null);
    }

    [Trace]
    private (Boolean, List<string>) HasPartialRowSelection(string commandText) {
        // Extract SELECT clause parameters
        string subCommandText = commandText.Replace(commandText.Substring(commandText.IndexOf("FROM")), "");
        Regex regex = new Regex(@"\[(?<targetTable>[a-zA-Z]+)\]\.*\[(?<columnName>[a-zA-Z]+)\]");
        MatchCollection matches = regex.Matches(subCommandText);

        if (matches.Count == 0) {
            // The entire row is being selected
            return (false, null);
        }

        // Store the column name that are selected (the order matters)
        List<string> columnSelect = new List<string>();

        // Extract parameter names from the matches
        for (int i = 0; i < matches.Count; i++) {
            columnSelect.Add(matches[i].Groups[2].Value);
        }

        return (true, columnSelect);
    }

    [Trace]
    private Boolean HasFilterCondition(string commandText) {
        return commandText.IndexOf("WHERE") != -1;
    }

    [Trace]
    private (Boolean, int) HasTopClause(string commandText) {
        Regex regex = new Regex(@"\.*TOP\((?<nElem>\d*)\)");
        MatchCollection matches = regex.Matches(commandText);
        if(matches.Count > 0) { 
            return (true, Convert.ToInt32(matches[0].Groups[1].Value));
        }
        return (false, 0);
    }

    [Trace]
    private Boolean HasCountClause(string commandText) {
        return commandText.Contains("COUNT");
    }

    [Trace]
    private Boolean IsSelectQuery(string commandText) {
        return commandText.Contains("SELECT");
    }

    [Trace]
    private Boolean IsCountSelect(string commandText) {
        return commandText.Contains("COUNT");
    }

    [Trace]
    private void WaitForProposedItemsIfNecessary(DbCommand command, string clientID, string clientTimestamp, string targetTable) {
        // The cases where this applies are:
        // 1. The command is a SELECT query and all rows are being selected and the proposed items are not empty
        // 2. The command is a SELECT query and some rows that are being selected are present in the proposed items

        DateTime readerTimestamp = DateTime.Parse(clientTimestamp);

        List<Tuple<string, string>> conditions = (command.CommandText.IndexOf("WHERE") != -1) ? GetWhereConditions(command) : null;
        //if (HasFilterCondition(command.CommandText)) {
        //    // Get the conditions of the WHERE clause, for now we only support equality conditions. Conditions are in the format: <columnName, value>
        //    conditions = GetWhereConditions(command);
        //}
        //else {
        //    // The reader is trying to read all items. Wait for all proposed items with lower proposed Timestamp than client Timestamp to be committed.
        //    conditions = null;
        //}

        var mre = _wrapper.AnyProposalWithLowerTimestamp(conditions, targetTable, readerTimestamp, clientID);
        while(mre != null) {
            // There is at least one proposed item with lower timestamp than the client timestamp. Wait for it to be committed.
            mre.WaitOne();
            mre = _wrapper.AnyProposalWithLowerTimestamp(conditions, targetTable, readerTimestamp, clientID);
        }
    }

    
    // Performance-tested
    [Trace]
    private List<Tuple<string, string>> GetWhereConditions(DbCommand command) {
        List<Tuple<string, string>> conditions = new List<Tuple<string, string>>();
        
        // Get all equality conditions in the format: [table].[column] = @param (or) [table].[column] = N'param'
        MatchCollection matches = GetWhereConditionsColumnAndValueRegex.Matches(command.CommandText);
        foreach (Match match in matches) {
            string columnName = match.Groups["columnName"].Value;
            if(columnName == "Timestamp") { // Timestamp is not considered as a condition
                continue;
            }
            string parameterName = match.Groups["paramValue2"].Value;
            var parameterValue = command.Parameters[parameterName].Value;
            Tuple<string, string> condition = new Tuple<string, string>(columnName, parameterValue.ToString());
            conditions.Add(condition);
        }
        return conditions;
    }

    [Trace]
    private List<object[]> ApplyFilterToProposedSet(List<object[]> proposedSet, DbCommand command) {
        List<object[]> filteredData = new List<object[]>();
        string targetTable = GetTargetTable(command.CommandText);

        // Store the column name associated with the respective value for comparison
        Dictionary<string, object> parametersFilter = new Dictionary<string, object>();
        Regex regex = new Regex(@"\[\w+\]\.\[(?<columnName>\w+)\]\s*=\s*(?:N?'(?<paramValue1>[^']*?)'|(?<paramValue2>\@\w+))");
        MatchCollection matches = regex.Matches(command.CommandText);

        if (matches.Count == 0) {
            // No Where filters exist
            return new List<object[]>();
        }

        // Extract parameter names from the matches
        for (int i = 0; i < matches.Count; i++) {
            string columnName = matches[i].Groups[1].Value;

            // Get the parameter value: either matched again paramValue1 or paramValue2
            string parameterParametrization = matches[i].Groups[2].Value.IsNullOrEmpty() ? matches[i].Groups[3].Value : matches[i].Groups[2].Value;

            // Check if the first index of the string is a "@" symbol
            if (parameterParametrization[0] == '@') {
                DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == parameterParametrization);
                parametersFilter[columnName] = parameter.Value;
            }
            else {
                parametersFilter[columnName] = parameterParametrization;
            }
        }

        // Get the default indexes for the columns identifiers of the Target Catalog Database
        Dictionary<string, int> columnIndexes = new Dictionary<string, int>();
        switch (targetTable) {
            case "Catalog":
                columnIndexes.Add("CatalogBrandId", 0);
                columnIndexes.Add("CatalogTypeId", 1);
                columnIndexes.Add("Name", 2);
                break;
            case "CatalogBrand":
                columnIndexes.Add("Brand", 0);
                break;
            case "CatalogType":
                columnIndexes.Add("Type", 0);
                break;
        }
        
        foreach (KeyValuePair<string, object> parameter in parametersFilter) {
            if (columnIndexes.TryGetValue(parameter.Key, out int columnIndex)) {
                // The column exists in the proposed set. Filter the proposed set by the parameter value
                filteredData = proposedSet
                        .Where(row => row[columnIndex] != null && row[columnIndex].ToString() == parameter.Value.ToString())
                        .ToList();
            }
        }
        return filteredData;
    }
}
