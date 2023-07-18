using Azure.Messaging.EventHubs;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.DataReaders;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using Microsoft.IdentityModel.Tokens;
// using NewRelic.Api.Agent;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.Interceptors; 
public class DiscountDBInterceptor : DbCommandInterceptor {
    const int INSERT_COMMAND = 1;
    const int SELECT_COMMAND = 2;
    const int UPDATE_COMMAND = 3;
    const int DELETE_COMMAND = 4;
    const int UNKNOWN_COMMAND = -1;

    private static readonly Regex GetWhereConditionsColumnAndValueRegex = new Regex(@"\[\w+\]\.\[(?<columnName>\w+)\]\s*=\s*(?:N?'(?<paramValue1>[^']*?)'|(?<paramValue2>\@\w+))", RegexOptions.Compiled);

    public DiscountDBInterceptor(IScopedMetadata requestMetadata, ISingletonWrapper wrapper, ILogger<DiscountContext> logger, IOptions<DiscountSettings> settings) {
        _request_metadata = requestMetadata;
        _wrapper = wrapper;
        _logger = logger;
        _settings = settings;
    }

    public IScopedMetadata _request_metadata;
    public ISingletonWrapper _wrapper;
    private string _originalCommandText;
    public ILogger<DiscountContext> _logger;
    public IOptions<DiscountSettings> _settings;

   //[Trace]
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {

        _originalCommandText = new string(command.CommandText);

        (var commandType, var targetTable) = GetCommandInfo(command);

        // Check if the Transaction ID
        var clientID = _request_metadata.ClientID;

        if (clientID == null) {
            // This is a system query
            return result;
        }

        switch (commandType) {
            case UNKNOWN_COMMAND:
                return result;
            case SELECT_COMMAND:
                string clientTimestamp = _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
                // _logger.LogInformation("The original received SELECT command text: {0}", command.CommandText);
                if (_settings.Value.Limit1Version) {
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
                    var mockReader = StoreDataInWrapper(command, INSERT_COMMAND, targetTable);
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
                    if(!transactionState) {
                        // The transaction is not in commit state, add to the wrapper
                        // _logger.LogInformation($"ClientID: {clientID}, transactionState: {transactionState}, commandText= {command.CommandText}");
                        Dictionary<string, object> columnsToInsert = UpdateToInsert(command, targetTable); // Get the columns and parameter names
                        var mockReader = StoreDataInWrapper1RowVersion(command, columnsToInsert, targetTable);
                        result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
                        break;
                    } 
                    else {
                        // Transaction is in commit state, update the command to store in the database
                        UpdateUpdateCommand(command, targetTable);
                        // _logger.LogInformation("Checkpoint command before DB commit: {0}", command.CommandText);
                        break;
                    }
                }
                else {
                    // Convert the Update Command into an INSERT command
                    Dictionary<string, object> columnsToInsert = UpdateToInsert(command, targetTable);

                    // Create a new INSERT COMMAND
                    var insertCommand = new StringBuilder("SET IMPLICIT_TRANSACTIONS OFF; SET NOCOUNT ON; INSERT INTO [")
                        .Append(targetTable)
                        .Append("] (");

                    // Add the columns incased in squared brackets
                    var columnNames = columnsToInsert.Keys.Select(x => $"[{x}]");
                    insertCommand.Append(string.Join(", ", columnNames));

                    // Add the values as parameters
                    var parameterNames = columnsToInsert.Keys.Select(x => $"@{x}");
                    var parameters = columnsToInsert.Select(x => new Microsoft.Data.SqlClient.SqlParameter($"@{x.Key}", x.Value)).ToArray();
                    insertCommand.Append(") VALUES (")
                        .Append(string.Join(", ", parameterNames))
                        .AppendLine(")");
                    
                    // Set the parameters on the command
                    command.Parameters.Clear();
                    command.Parameters.AddRange(parameters);
                    
                    // Set the command text to the INSERT command
                    command.CommandText = insertCommand.ToString();

                    // If the Transaction is not in commit state, store data in wrapper
                    var updateToInsertReader = StoreDataInWrapperV2(command, INSERT_COMMAND, targetTable);
                    result = InterceptionResult<DbDataReader>.SuppressWithResult(updateToInsertReader);
                }
                break;
        }

        // _logger.LogInformation($"Checkpoint 2_b_sync: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");
        return result;
    }

   //[Trace]
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) {
        command.CommandTimeout = 100;
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
                return new ValueTask<InterceptionResult<DbDataReader>>(result);
            case SELECT_COMMAND:
                string clientTimestamp = _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
                // _logger.LogInformation("The original received SELECT command text: {0}", command.CommandText);
                if (_settings.Value.Limit1Version) {
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
                    var mockReader = StoreDataInWrapper(command, INSERT_COMMAND, targetTable);
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
                    if(!transactionState) {
                        // The transaction is not in commit state, add to the wrapper
                        // _logger.LogInformation($"ClientID: {clientID}, transactionState: {transactionState}, commandText= {command.CommandText}");
                        Dictionary<string, object> columnsToInsert = UpdateToInsert(command, targetTable);
                        var mockReader = StoreDataInWrapper1RowVersion(command, columnsToInsert, targetTable);
                        result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
                        break;
                    } 
                    else {
                        // Transaction is in commit state, update the command to store in the database
                        UpdateUpdateCommand(command, targetTable);
                        // _logger.LogInformation("Checkpoint command before DB commit: {0}", command.CommandText);
                        break;
                    }
                }
                else {
                    // Convert the Update Command into an INSERT command
                    Dictionary<string, object> columnsToInsert = UpdateToInsert(command, targetTable);

                    // Create a new INSERT COMMAND
                    var insertCommand = new StringBuilder("SET IMPLICIT_TRANSACTIONS OFF; SET NOCOUNT ON; INSERT INTO [")
                        .Append(targetTable)
                        .Append("] (");

                    // Add the columns incased in squared brackets
                    var columnNames = columnsToInsert.Keys.Select(x => $"[{x}]");
                    insertCommand.Append(string.Join(", ", columnNames));

                    // Add the values as parameters
                    var parameterNames = columnsToInsert.Keys.Select(x => $"@{x}");
                    var parameters = columnsToInsert.Select(x => new Microsoft.Data.SqlClient.SqlParameter($"@{x.Key}", x.Value)).ToArray();
                    insertCommand.Append(") VALUES (")
                        .Append(string.Join(", ", parameterNames))
                        .AppendLine(")");
                    
                    // Set the parameters on the command
                    command.Parameters.Clear();
                    command.Parameters.AddRange(parameters);
                    
                    // Set the command text to the INSERT command
                    command.CommandText = insertCommand.ToString();

                    // If the Transaction is not in commit state, store data in wrapper
                    var updateToInsertReader = StoreDataInWrapperV2(command, INSERT_COMMAND, targetTable);
                    result = InterceptionResult<DbDataReader>.SuppressWithResult(updateToInsertReader);
                }
                break;
        }
        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    private MockDbDataReader StoreDataInWrapper1RowVersion(DbCommand command, Dictionary<string, object> columnNamesAndValues, string targetTable) {
        var clientID = _request_metadata.ClientID;

        Dictionary<string, int> standardColumnIndexes = GetDefaultColumIndexes(targetTable);  // Get the expected order of the columns
        // Get the number of rows being inserted
        var rowsAffected = 0;
        
        var rows = new List<object[]>();
        var row = new object[columnNamesAndValues.Keys.Count + 1]; // Added Timestamp at the end

        foreach(var columnName in columnNamesAndValues.Keys) {
            var paramValue = columnNamesAndValues[columnName];
            var correctIndexToStore = standardColumnIndexes[columnName];
            row[correctIndexToStore] = paramValue;
        }
        // Define the uncommitted timestamp as the current time
        row[^1] = DateTime.UtcNow;
        rowsAffected++;
        rows.Append(row);
        // Log the rows
        // foreach (object[] row in rows) {
        //     _logger.LogInformation($"ClientID: {clientID} adding to the wrapper row: {string.Join(", ", row)}");
        // }
        var mockReader = new MockDbDataReader(rows, rowsAffected, targetTable);
        _wrapper.SingletonAddDiscountItem(clientID, rows.ToArray());
        return mockReader;
    }

   //[Trace]
    private MockDbDataReader StoreDataInWrapperV2(DbCommand command, int operation, string targetTable) {
        var clientID = _request_metadata.ClientID;
        // _logger.LogInformation($"Storing data in WrapperV2... ClientID: {clientID}, commandText= {command.CommandText}");
        string regexPattern;
        if( operation == UPDATE_COMMAND) {
            regexPattern = @"\[(\w+)\] = (@\w+)";
        }
        else if(operation == INSERT_COMMAND) {
            regexPattern = @"(?:, |\()\[(\w+)\]";
        }
        else {
            _logger.LogError($"Operation not supported, command text: {command.CommandText}");
            regexPattern = "";
        }

        // Get the number of columns in each row to be inserted
        var regex = new Regex(regexPattern);
        var matches = regex.Matches(command.CommandText); // Each match includes a column name

        var columns = new List<string>(); // List of column names

        for(int i = 0; i < matches.Count; i++) {
            columns.Add(matches[i].Groups[1].Value);
        }

        Dictionary<string, int> standardColumnIndexes = GetDefaultColumIndexes(targetTable);
        // Get the number of rows being inserted
        int numberRows = command.Parameters.Count / columns.Count;
        var rowsAffected = 0;

        var rows = new List<object[]>();
        for(int i = 0; i < numberRows; i += 1) {
            var row = new object[columns.Count + 1]; // Added Timestamp at the end

            for(int j = 0; j < columns.Count; j++) {
                var columnName = columns[j];
                var paramValue = command.Parameters["@"+columnName].Value;
                var correctIndexToStore = standardColumnIndexes[columnName];
                row[correctIndexToStore] = paramValue;
                // _logger.LogInformation($"Row: {columnName}: {paramValue}");

            }
            // Define the uncommitted timestamp as the current time
            row[^1] = DateTime.UtcNow;
            rows.Add(row);
            rowsAffected++;
        }

        // Log the rows
        // foreach (object[] row in rows) {
        //     // _logger.LogInformation($"Row: {string.Join(", ", row)}");
        // }
        var mockReader = new MockDbDataReader(rows, rowsAffected, targetTable);
        _wrapper.SingletonAddDiscountItem(clientID, rows.ToArray());
        return mockReader;
    }

   //[Trace]
    private MockDbDataReader StoreDataInWrapper(DbCommand command, int operation, string targetTable) {
        var clientID = _request_metadata.ClientID;

        string regexPattern;
        if (operation == INSERT_COMMAND) {
            regexPattern = @"\[(\w+)\]";
        }
        else {
            regexPattern = @"\[(\w+)\] = (@\w+)";
        }

        // Get the number of columns in each row to be inserted
        var regex = new Regex(regexPattern);
        var matches = regex.Matches(command.CommandText);

        var columns = new List<string>();

        // Discard the first match, it is the table name
        for (int i = 1; i < matches.Count; i++) {
            columns.Add(matches[i].Groups[1].Value);
        }

        Dictionary<string, int> standardColumnIndexes = GetDefaultColumIndexes(targetTable);

        // Get the number of rows being inserted
        int numberRows = command.Parameters.Count / columns.Count;
        var rowsAffected = 0;

        // Log the paramters values being inserted
        // string values = "";
        // for(int i = 0; i < command.Parameters.Count; i++) {
        //     var param = command.Parameters[i].Value;
        //     values += param.ToString() + ", ";
        // }
        //_logger.LogInformation("Values being inserted: {0}", values);

        var rows = new List<object[]>();
        for (int i = 0; i < numberRows; i += 1) {
            var row = new object[columns.Count + 1]; // Added Timestamp at the end
            for (int j = 0; j < columns.Count; j++) {
                var columnName = columns[j];
                var paramValue = command.Parameters[(i * columns.Count) + j].Value;
                var correctIndexToStore = standardColumnIndexes[columnName];
                row[correctIndexToStore] = paramValue;
            }
            // Define the uncommitted timestamp as the current time
            row[^1] = DateTime.UtcNow;
            rows.Add(row);
            // Log each element of the row being added to the wrapper
            //_logger.LogInformation("Adding row to wrapper: {0}", string.Join(", ", row));

            rowsAffected++;
        }

        var mockReader = new MockDbDataReader(rows, rowsAffected, targetTable);

        switch (targetTable) {
            case "Discount":
                _wrapper.SingletonAddDiscountItem(clientID, rows.ToArray());
                break;
        }
        return mockReader;
    }

   //[Trace]
    public (int, string) GetCommandInfo(DbCommand command) {
        var commandType = GetCommandType(command);

        switch (commandType) {
            case INSERT_COMMAND:
                string targetTable = GetTargetTable(command.CommandText);
                return (INSERT_COMMAND, targetTable);
            case SELECT_COMMAND:
                targetTable = GetTargetTable(command.CommandText);
                List<string> exceptionTables = new List<string>() {
                    "__EFMigrationsHistory"
                };
                if (!exceptionTables.Contains(targetTable)) {
                    return (SELECT_COMMAND, targetTable);
                }
                break;
            case UPDATE_COMMAND:
                targetTable = GetTargetTable(command.CommandText);
                return (UPDATE_COMMAND, targetTable);
        }
        return (UNKNOWN_COMMAND, null);
    }

   //[Trace]
    private int GetCommandType(DbCommand command) {
        var commandText = command.CommandText.ToUpperInvariant();

        if (commandText.Contains("INSERT ")) {
            return INSERT_COMMAND;
        }
        else if (commandText.Contains("UPDATE ")) {
            return UPDATE_COMMAND;
        }
        else if (commandText.Contains("DELETE ")) {
            return DELETE_COMMAND;
        }
        else if (commandText.Contains("SELECT ")) {
            return SELECT_COMMAND;
        }
        else {
            return UNKNOWN_COMMAND;
        }
    }

    /* ========== UPDATE READ QUERIES ==========*/
    /// <summary>
    /// Updates the Read queries for Item Count, Brands and Types command, adding a filter for the client Timestamp (DateTime). 
    /// Applicable to queries that contain either:
    /// </summary>
    /// <param name="command"></param>
    /// 

   //[Trace]
    public void UpdateSelectCommandV2(DbCommand command, string targetTable, string clientTimestamp) {
        // Check if the SELECT includes a "*" or a column list
        (bool hasPartialRowSelection, List<string> _) = HasPartialRowSelection(command.CommandText);

        // Add the Timestamp column parameter, if the command has a partial row selection
        if (hasPartialRowSelection) {
            AddTimestampToColumnList(command);
        }
        AddTimestampToWhereList(command, targetTable, clientTimestamp);
        // Log the resulting command text
        // _logger.LogInformation($"Updated Command Text: {command.CommandText}");
    }

   //[Trace]
    public void AddTimestampToWhereList(DbCommand command, string targetTable, string clientTimestamp) {
        var clientTimestampParameter = new Microsoft.Data.SqlClient.SqlParameter("@clientTimestamp", SqlDbType.DateTime2);
        clientTimestampParameter.Value = clientTimestamp;
        command.Parameters.Add(clientTimestampParameter);
        if (command.CommandText.Contains("WHERE")) {
            string pattern = @"WHERE\s+(.*)$";
            command.CommandText = Regex.Replace(command.CommandText, pattern, "WHERE $1 AND [d].[Timestamp] <= @clientTimestamp");
        }
        else {
            command.CommandText += " WHERE [d].[Timestamp] <= @clientTimestamp";
        }
    }

   //[Trace]
    public void AddTimestampToColumnList(DbCommand command) {
        string pattern = @"SELECT\s+(.*?)\s+FROM";
        command.CommandText = Regex.Replace(command.CommandText, pattern, "SELECT $1, [d].[Timestamp] FROM");
    }

   //[Trace]
    private void UpdateSelectCommand(DbCommand command, string targetTable) {
        // Log the command text
        //_logger.LogInformation($"Command Text: {command.CommandText}");

        // Get the current client seesion timeestamp
        DateTime clientTimestamp = _request_metadata.Timestamp;

        // (bool hasPartialRowSelection, List<string> _) = HasPartialRowSelection(command.CommandText);
        // if (hasPartialRowSelection) {
        //     // Remove the partial Row Selection
        //     command.CommandText = RemovePartialRowSelection(command.CommandText);
        // }

        bool hasCount = HasCountClause(command.CommandText);
        if (hasCount) {
            command.CommandText = RemoveCountSelection(command.CommandText);
        }

        // (bool hasOrderBy, string orderByColumn, string typeOrder) = HasOrderByCondition(command.CommandText);

        // (bool hasOffset, string offsetParam) = HasOffsetCondition(command.CommandText);

        // // Remove the ORDER BY clause and everything after it
        // if (hasOrderBy) {
        //     string orderByPattern = @"ORDER\s+BY(.|\n)*";
        //     command.CommandText = Regex.Replace(command.CommandText, orderByPattern, "");
        // }

        string whereCondition = "";

        // Create new SQL Parameter for clientTimestamp
        var clientTimestampParameter = new Microsoft.Data.SqlClient.SqlParameter("@clientTimestamp", SqlDbType.DateTime2);
        clientTimestampParameter.Value = clientTimestamp;
        command.Parameters.Add(clientTimestampParameter);

        if (command.CommandText.Contains("WHERE")) {
            // Extract where condition
            string regex_pattern = @"WHERE\s+(.*?)(?:\bGROUP\b|\bORDER\b|\bHAVING\b|\bLIMIT\b|\bUNION\b|$)";
            Match match = Regex.Match(command.CommandText, regex_pattern);

            whereCondition = match.Groups[1].Value;
            // Remove the where condition from the command
            command.CommandText = command.CommandText.Replace(whereCondition, "");
            if(!_settings.Value.Limit1Version) {
                whereCondition = whereCondition.Replace("[d]", $"[{targetTable}]");
                whereCondition += $" AND [{targetTable}].[Timestamp] <= @clientTimestamp ";    
            } 
            else {
                whereCondition += $" AND [d].[Timestamp] <= '{clientTimestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}' ";
            }
        }
        else {
            whereCondition = $" WHERE [{targetTable}].[Timestamp] <= @clientTimestamp ";
        }

        if(_settings.Value.Limit1Version) {
            command.CommandText += whereCondition;
            // _logger.LogInformation($"Updated Command Text: {command.CommandText}");
            return;
        }

        command.CommandText = command.CommandText.Replace("AS [d]", $"AS [d] JOIN (SELECT Discount.ItemName, Discount.ItemBrand, Discount.ItemType, max(Discount.Timestamp) as max_timestamp FROM Discount");
        command.CommandText += whereCondition;
        command.CommandText += "GROUP BY Discount.ItemName, Discount.ItemBrand, Discount.ItemType) e on d.ItemName = e.ItemName AND d.ItemBrand = e.ItemBrand AND d.ItemType = e.ItemType AND d.Timestamp = e.max_timestamp"; //WHERE d.ID = (SELECT TOP 1 ID FROM Discount WHERE ItemName = d.ItemName AND ItemBrand = d.ItemBrand AND ItemType = d.ItemType AND Timestamp = d.Timestamp ORDER BY ID DESC)
    }

   //[Trace]
    private string RemovePartialRowSelection(string commandText) {
        string pattern = @"SELECT\s+(.*?)\s+FROM";
        string replacement = "SELECT * FROM";
        string result = Regex.Replace(commandText, pattern, replacement);
        return result;
    }

   //[Trace]
    private string RemoveCountSelection(string commandText) {
        string pattern = @"SELECT\s+(.*?)\s+FROM";
        string replacement = "SELECT * FROM";
        string result = Regex.Replace(commandText, pattern, replacement);
        return result;
    }

   //[Trace]
    private void UpdateUpdateCommand(DbCommand command, string targetTable) {
        // Get the Timestamp received from the Coordinator
        string timestamp = _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        // Replace Command Text to account for new parameter: Timestamp
        string commandWithTimestamp;
        commandWithTimestamp = UpdateUpdateCommandText(command, targetTable);

        // Add new Parameter and Command Text to database command
        command.CommandText = commandWithTimestamp;
        var timestampParam = command.CreateParameter();
        timestampParam.ParameterName = "@p5";
        timestampParam.Value = timestamp;
        command.Parameters.Add(timestampParam);
        
        // command.Parameters.Clear();
        // var parameter = command.CreateParameter();
        // parameter.ParameterName = "@p0";
        // parameter.Value = 100;
        // command.Parameters.Add(parameter);

        // parameter = command.CreateParameter();
        // parameter.ParameterName = "@p1";
        // parameter.Value = ".NET";
        // command.Parameters.Add(parameter);

        // parameter = command.CreateParameter();
        // parameter.ParameterName = "@p2";
        // parameter.Value = ".NET Bot Black Hoodie";
        // command.Parameters.Add(parameter);

        // parameter = command.CreateParameter();
        // parameter.ParameterName = "@p3";
        // parameter.Value = "T-Shirt";
        // command.Parameters.Add(parameter);

        // parameter = command.CreateParameter();
        // parameter.ParameterName = "@p4";
        // parameter.Value = 1;
        // command.Parameters.Add(parameter);

        // parameter = command.CreateParameter();
        // parameter.ParameterName = "@p5";
        // parameter.Value = timestamp;
        // command.Parameters.Add(parameter);

        // command.CommandText = "SET IMPLICIT_TRANSACTIONS OFF; SET NOCOUNT ON; UPDATE [Discount] SET [DiscountValue] = @p0, [ItemBrand] = @p1, [ItemName] = @p2, [ItemType] = @p3, [Timestamp] = @p5 OUTPUT 1 WHERE [Id] = @p4;";
    }

   //[Trace]
    private static string UpdateUpdateCommandText(DbCommand command, string targetTable) {
        string updatedCommandText = Regex.Replace(command.CommandText, @"(?<lastParam>\[ItemType\] = @p(\d+))", "${lastParam}, [Timestamp] = @p5");
    
        return updatedCommandText;
    }

    /* ========== UPDATE WRITE QUERIES ==========*/

   //[Trace]
    private void UpdateInsertCommand(DbCommand command, string targetTable) {
        // Get the timestamp received from the Coordinator
        string timestamp = _request_metadata.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        // Replace Command Text to account for new parameter
        string commandWithTimestamp;
        commandWithTimestamp = UpdateInsertCommandText(command, targetTable);

        // Generate new list of parameters
        List<DbParameter> newParameters = new List<DbParameter>();
        if (targetTable == "Discount") {
            UpdateDiscountOp(command, newParameters, timestamp);
            // _logger.LogInformation($"Updated insert command text.");
        }

        // Assign new Parameters and Command text to database command
        command.Parameters.Clear();
        command.Parameters.AddRange(newParameters.ToArray());
        command.CommandText = commandWithTimestamp;
    }

   //[Trace]
    private Dictionary<string, object> UpdateToInsert(DbCommand command, string targetTable) {
        var regex = new Regex(@"\[(\w+)\] = (@\w+)");
        var matches = regex.Matches(command.CommandText);

        var columns = new Dictionary<string, object>();

        for (int i = 0; i < matches.Count; i++) {
            string parameterName = matches[i].Groups[2].Value;
            var paramterValue = command.Parameters[parameterName].Value;
            columns[matches[i].Groups[1].Value] = paramterValue;
        }
        return columns;
    }

   //[Trace]
    private static void UpdateDiscountOp(DbCommand command, List<DbParameter> generatedParameters, string timestamp) {
        int numObjectsToInsert = command.Parameters.Count / 5;

        for (int i = 0; i < numObjectsToInsert; i++) {
            // Get the ID param
            var idParam = command.CreateParameter();
            idParam.ParameterName = $"@p{i * 6}";
            idParam.Value = command.Parameters[i * 5].Value;

            // Get the ItemName param
            var itemNameParam = command.CreateParameter();
            itemNameParam.ParameterName = $"@p{i * 6 + 1}";
            itemNameParam.Value = command.Parameters[i * 5 + 1].Value;

            // Get the ItemBrand param
            var itemBrandParam = command.CreateParameter();
            itemBrandParam.ParameterName = $"@p{i * 6 + 2}";
            itemBrandParam.Value = command.Parameters[i * 5 + 2].Value;

            // Get the ItemType param
            var itemTypeParam = command.CreateParameter();
            itemTypeParam.ParameterName = $"@p{i * 6 + 3}";
            itemTypeParam.Value = command.Parameters[i * 5 + 3].Value;

            // Get the DiscountValue param
            var discountValueParam = command.CreateParameter();
            discountValueParam.ParameterName = $"@p{i * 6 + 4}";
            discountValueParam.Value = command.Parameters[i * 5 + 4].Value;

            // Create new entry Value with the Timestamp parameter
            var timeStampParam = command.CreateParameter();
            timeStampParam.ParameterName = $"@p{i * 6 + 5}";
            timeStampParam.Value = timestamp;

            generatedParameters.Add(idParam);
            generatedParameters.Add(itemNameParam);
            generatedParameters.Add(itemBrandParam);
            generatedParameters.Add(itemTypeParam);
            generatedParameters.Add(discountValueParam);
            generatedParameters.Add(timeStampParam);
        }
    }

   //[Trace]
    private static string UpdateInsertCommandText(DbCommand command, string targetTable) {
        string updatedCommandText = "";
        int numberColumns = 0;

        // Update the CommandText to include the Timestamp column and parameter for each entry
        if (targetTable == "Discount") {
            numberColumns = 6;
            updatedCommandText = Regex.Replace(command.CommandText, @"INSERT INTO\s+\[Discount\]\s+\(\s*\[Id\],\s*\[DiscountValue\],\s*\[ItemBrand\],\s*\[ItemName\],\s*\[ItemType\]\s*", "$0, [Timestamp]");
        }
        
        var regex = new Regex(@"\(@p\d+[,\s@p\d]*@p(?<LastIndexOfRow>\d+)\)");
        var matches = regex.Matches(command.CommandText);
        int numRows = matches.Count;
        updatedCommandText = updatedCommandText.Replace(updatedCommandText.Substring(updatedCommandText.IndexOf("VALUES ")), "VALUES ");

        for (int i = 0; i < numRows; i++) {
            switch (numberColumns) {
                case 6:
                    updatedCommandText += $"(@p{numberColumns * i}, @p{numberColumns * i + 1}, @p{numberColumns * i + 2}, @p{numberColumns * i + 3}, @p{numberColumns * i + 4}, @p{numberColumns * i + 5})";
                    break;
            }
            if (i != numRows - 1) {
                updatedCommandText += ", ";
            }
            else {
                updatedCommandText += ";";
            }
        }
        return updatedCommandText;
    }

   //[Trace]
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) {
        // _logger.LogInformation("Command executed: " + command.CommandText);

        var clientID = _request_metadata.ClientID;

        if (clientID == null) {
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
        if(_originalCommandText.Contains("UPDATE")) {
            // _logger.LogInformation($"ClientID: {clientID}, update commandText= {_originalCommandText}");
            var newUpdatedData = new List<object[]>();
            newUpdatedData.Add(new object[] { 1 });
            return new WrapperDbDataReader(newUpdatedData, result, targetTable);
        }

        // Check if the command is an INSERT command and has yet to be committed
        if (command.CommandText.Contains("INSERT") && !_wrapper.SingletonGetTransactionState(clientID)) {
            // _logger.LogInformation($"ClientID: {clientID}, insert commandText= {_originalCommandText}");
            return result;
        }
        // _logger.LogInformation($"ClientID: {clientID}, transaction state: {_wrapper.SingletonGetTransactionState(clientID)}, commandText: {command.CommandText}");
        // Note: It is important that the wrapper data is cleared before saving it to the database, when the commit happens.

        var newData = new List<object[]>();
        // _logger.LogInformation($"ClientID: {clientID}, starting to read the result of command text: {command.CommandText}");
        while (result.Read()) {
            var rowValues = new List<object>();
            // _logger.LogInformation($"ClientID: {clientID}, Number of fields in the result: {result.FieldCount}");
            for (int i = 0; i < result.FieldCount; i++) {
                var fieldName = result.GetName(i);
                var columnIndex = result.GetOrdinal(fieldName);
                var fieldType = result.GetFieldType(columnIndex);
                switch (fieldType.Name) {
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
        // if(newData.Count > 0) {
        //     _logger.LogInformation($"ClientID: {clientID}, Number of fields in the newData: {newData[0].Length}");
        // }
        // Read the data from the Wrapper structures
        if (command.CommandText.Contains("SELECT")) {
            List<object[]> wrapperData = null;

            wrapperData = _wrapper.SingletonGetDiscountItems(clientID).ToList();

            // Filter the results to display only 1 version of data for both the Wrapper Data as well as the DB data
            //newData = GroupVersionedObjects(newData, targetTable);
            wrapperData = GroupVersionedObjects(wrapperData, targetTable);

            if (wrapperData.Count > 0) {
                if (HasFilterCondition(command.CommandText)) {
                    // The select query contains a WHERE clause
                    wrapperData = FilterData(wrapperData, command);

                    if (!HasCountClause(command.CommandText)) {
                        // Only add the rows if the data from the DB is not a single count product
                        foreach (var wrapperRow in wrapperData) {
                            newData.Add(wrapperRow);
                        }
                    }
                }
                // _logger.LogInformation($"9B: ClientID: {_request_metadata.ClientID}, request readOnly flag: {_request_metadata.ReadOnly}");

                // We group the data again to ensure that the data is grouped by the same version in the union of the DB and Wrapper data
                newData = GroupVersionedObjects(newData, targetTable);

                (bool hasOrderBy, string orderByColumn, string typeOrder) = HasOrderByCondition(command.CommandText);
                if (hasOrderBy) {
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
            }
            // _logger.LogInformation($"10B: ClientID: {_request_metadata.ClientID}, request readOnly flag: {_request_metadata.ReadOnly}");

            if (HasCountClause(_originalCommandText)) {
                List<object[]> countedData = new List<object[]>();
                object[] data = new object[] { newData.Count + wrapperData.Count };
                countedData.Add(data);

                return new WrapperDbDataReader(countedData, result, targetTable);
            }

            // Search for partial SELECTION on the original unaltered commandText
            (bool hasPartialRowSelection, List<string> selectedColumns) = HasPartialRowSelection(_originalCommandText);
            if (hasPartialRowSelection) {
                // foreach (string column in selectedColumns) {
                //     _logger.LogInformation("The column {0} was selected", column);
                // }

                // The select query has a partial row selection
                var originalNumColumns = newData[0].Length;
                for(int i = 0; i < newData.Count; i++) {
                    newData[i] = PartialRowSelectionV2(command.CommandText, newData[i], selectedColumns, result.GetSchemaTable());
                }
                // _logger.LogInformation($"ClientID {clientID}: Applied the partial row selection. The original data had {originalNumColumns} columns. The new data has {newData[0].Length} columns. CommandText was {_originalCommandText}");
                // foreach (object value in newData[0]) {
                //     _logger.LogInformation($"ClientID {clientID} The value {value.ToString()} was selected");
                // }
            }
        }
        if(_settings.Value.Limit1Version) {
            if (HasCountClause(_originalCommandText) && newData[0][0].Equals(0)) {
                // _logger.LogInformation("The database count returned 0. Adding 1 to the count for default.");
                newData.Add(new object[] { 1 });
            }
            if(newData.IsNullOrEmpty()) {
                // _logger.LogInformation("The database + wrapper returned no data");
                // If newData is empty return a reader with a single default row
                switch(targetTable) {
                    case "Discount":
                        newData.Add(new object[] {
                            1, // Id
                            ".NET Bot Black Hoodie", // ItemName
                            ".NET", // ItemBrand
                            "Mug", // ItemType
                            2, // DiscountValue
                        });
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

   //[Trace]
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) {
        // _logger.LogInformation($"Checkpoint 2_c_async: {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}");

        var clientID = _request_metadata.ClientID;

        if (clientID == null) {
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
        if(_originalCommandText.Contains("UPDATE")) {
            // _logger.LogInformation($"ClientID: {clientID}, update commandText= {_originalCommandText}");
            var newUpdatedData = new List<object[]>();
            newUpdatedData.Add(new object[] { 1 });
            return new WrapperDbDataReader(newUpdatedData, result, targetTable);
        }

        // Check if the command is an INSERT command and has yet to be committed
        if (command.CommandText.Contains("INSERT") && !_wrapper.SingletonGetTransactionState(clientID)) {
            // _logger.LogInformation($"ClientID: {clientID}, insert commandText= {_originalCommandText}");
            return result;
        }
        // _logger.LogInformation($"ClientID: {clientID}, transaction state: {_wrapper.SingletonGetTransactionState(clientID)}, commandText: {command.CommandText}");
        // Note: It is important that the wrapper data is cleared before saving it to the database, when the commit happens.

        var newData = new List<object[]>();
        // _logger.LogInformation($"ClientID: {clientID}, starting to read the result of command text: {command.CommandText}");
        while (await result.ReadAsync(cancellationToken)) {
            var rowValues = new List<object>();
            // _logger.LogInformation($"ClientID: {clientID}, Number of fields in the result: {result.FieldCount}");
            for (int i = 0; i < result.FieldCount; i++) {
                var fieldName = result.GetName(i);
                var columnIndex = result.GetOrdinal(fieldName);
                var fieldType = result.GetFieldType(columnIndex);
                switch (fieldType.Name) {
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
        // if(newData.Count > 0) {
        //     _logger.LogInformation($"ClientID: {clientID}, Number of fields in the newData: {newData[0].Length}");
        // }
        // Read the data from the Wrapper structures
        if (command.CommandText.Contains("SELECT")) {
            List<object[]> wrapperData = null;

            wrapperData = _wrapper.SingletonGetDiscountItems(clientID).ToList();

            // Filter the results to display only 1 version of data for both the Wrapper Data as well as the DB data
            //newData = GroupVersionedObjects(newData, targetTable);
            wrapperData = GroupVersionedObjects(wrapperData, targetTable);

            if (wrapperData.Count > 0) {
                if (HasFilterCondition(command.CommandText)) {
                    // The select query contains a WHERE clause
                    wrapperData = FilterData(wrapperData, command);

                    if (!HasCountClause(command.CommandText)) {
                        // Only add the rows if the data from the DB is not a single count product
                        foreach (var wrapperRow in wrapperData) {
                            newData.Add(wrapperRow);
                        }
                    }
                }
                // _logger.LogInformation($"9B: ClientID: {_request_metadata.ClientID}, request readOnly flag: {_request_metadata.ReadOnly}");

                // We group the data again to ensure that the data is grouped by the same version in the union of the DB and Wrapper data
                newData = GroupVersionedObjects(newData, targetTable);

                (bool hasOrderBy, string orderByColumn, string typeOrder) = HasOrderByCondition(command.CommandText);
                if (hasOrderBy) {
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
            }
            // _logger.LogInformation($"10B: ClientID: {_request_metadata.ClientID}, request readOnly flag: {_request_metadata.ReadOnly}");

            if (HasCountClause(_originalCommandText)) {
                List<object[]> countedData = new List<object[]>();
                object[] data = new object[] { newData.Count + wrapperData.Count };
                countedData.Add(data);

                return new WrapperDbDataReader(countedData, result, targetTable);
            }

            if(!newData.IsNullOrEmpty()) {
                // Search for partial SELECTION on the original unaltered commandText
                (bool hasPartialRowSelection, List<string> selectedColumns) = HasPartialRowSelection(_originalCommandText);
                if (hasPartialRowSelection) {
                    // foreach (string column in selectedColumns) {
                    //     _logger.LogInformation("The column {0} was selected", column);
                    // }

                    // The select query has a partial row selection
                    var originalNumColumns = newData[0].Length;
                    for(int i = 0; i < newData.Count; i++) {
                        newData[i] = PartialRowSelectionV2(command.CommandText, newData[i], selectedColumns, result.GetSchemaTable());
                    }
                    // _logger.LogInformation($"ClientID {clientID}: Applied the partial row selection. The original data had {originalNumColumns} columns. The new data has {newData[0].Length} columns. CommandText was {_originalCommandText}");
                    // foreach (object value in newData[0]) {
                    //     _logger.LogInformation($"ClientID {clientID} The value {value.ToString()} was selected");
                    // }
                }
            }
            
        }
        if(_settings.Value.Limit1Version) {
            if (HasCountClause(_originalCommandText) && newData[0][0].Equals(0)) {
                // _logger.LogInformation("The database count returned 0. Adding 1 to the count for default.");
                newData.Add(new object[] { 1 });
            }
            if(newData.IsNullOrEmpty()) {
                // _logger.LogInformation("The database + wrapper returned no data");
                // If newData is empty return a reader with a single default row
                switch(targetTable) {
                    case "Discount":
                        newData.Add(new object[] {
                            1, // Id
                            ".NET Bot Black Hoodie", // ItemName
                            ".NET", // ItemBrand
                            "Mug", // ItemType
                            2, // DiscountValue
                        });
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

   //[Trace]
    private List<object[]> OrderBy(List<object[]> newData, string orderByColumn, string typeOrder, string targetTable) {
        Dictionary<string, int> columnIndexes = GetDefaultColumIndexes(targetTable);
        int sortByIndex = columnIndexes[orderByColumn];

        // Determine the type of the objects to compare
        Type sortColumnType = newData.First()[sortByIndex].GetType();

        newData = newData.OrderBy(arr => Convert.ChangeType(arr[sortByIndex], sortColumnType)).ToList();
        return newData;
    }

   //[Trace]
    private List<object[]> OffsetData(DbCommand command, List<object[]> newData, string offsetParam) {
        DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == offsetParam);
        int offsetValue = Convert.ToInt32(parameter.Value);
        newData = newData.Skip(offsetValue).ToList();
        return newData;
    }

   //[Trace]
    private List<object[]> FetchNext(DbCommand command, List<object[]> newData, string fetchRowsParam) {
        DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == fetchRowsParam);
        int fetchRows = Convert.ToInt32(parameter.Value);
        newData = newData.Take(fetchRows).ToList();
        return newData;
    }

   //[Trace]
    private List<object[]> GroupVersionedObjects(List<object[]> newData, string targetTable) {
        var dateTimeComparer = Comparer<object>.Create((x, y) => {
            DateTime xValue = (DateTime)x;
            DateTime yValue = (DateTime)y;
            return xValue.CompareTo(yValue);
        });

        var groupByColumns = GetUniqueIndentifierColumns(targetTable);

        switch (targetTable) {
            case "Discount":
                var newDataGrouped = newData
                    .GroupBy(row => new { ItemName = row[1], ItemBrand = row[2], ItemType = row[3] });

                newData = newDataGrouped
                    .Select(group => group
                        .OrderByDescending(row => row[^1], dateTimeComparer).First())
                    .ToList();
                break;
        }

        return newData;
    }

   //[Trace]
    private object[] PartialRowSelection(string commandText, object[] row, List<string> selectedColumns) {
        string targetTable = GetTargetTable(commandText);
        object[] newRow = new object[selectedColumns.Count];

        // Get the default indexes for the columns of the Discount Database
        Dictionary<string, int> columnIndexes = GetDefaultColumIndexes(targetTable);

        for (int i = 0; i < selectedColumns.Count; i++) {
            switch(selectedColumns[i]) {
                case "Id":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[0]}");
                    newRow[i] = row[0];
                    break;
                case "ItemName":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[1]}");
                    newRow[i] = row[1];
                    break;
                case "ItemBrand":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[2]}");
                    newRow[i] = row[2];
                    break;
                case "ItemType":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[3]}");
                    newRow[i] = row[3];
                    break;
                case "DiscountValue":
                    // _logger.LogInformation($"Selected column: {i} Name: {selectedColumns[i]}, value={row[4]}");
                    newRow[i] = row[4];
                    break;
            }
        }
        return newRow;
    }

   //[Trace]
    private object[] PartialRowSelectionV2(string commandText, object[] row, List<string> selectedColumns, DataTable schemaTable) {
        // Build a new row with the selected columns only, as defined in the original commandText
        var columnIndexes = new Dictionary<string, int>();

        for (int i = 0; i < schemaTable.Rows.Count; i++) {
            var columnName = schemaTable.Rows[i]["ColumnName"].ToString();
            var columnIndex = (int)schemaTable.Rows[i]["ColumnOrdinal"];
            columnIndexes[columnName] = columnIndex;            
        }

        var newRow = new object[selectedColumns.Count];
        for (int i = 0; i < selectedColumns.Count; i++) {
            var columnName = selectedColumns[i];
            var columnIndex = columnIndexes[columnName];
            newRow[i] = row[columnIndex];
            // _logger.LogInformation($"Added column {columnName} with value {newRow[i]} to the new row");
        }

        return newRow;
    }

   //[Trace]
    private List<object[]> FilterData(List<object[]> wrapperData, DbCommand command) {
        string commandText = command.CommandText;
        var targetTable = GetTargetTable(commandText);

        var filteredData = ApplyWhereFilterIfExist(wrapperData, command).ToList();

        return filteredData;
    }

   //[Trace]
    private IEnumerable<object[]> ApplyWhereFilterIfExist(List<object[]> wrapperData, DbCommand command) {
        List<object[]> filteredData = new List<object[]>();
        string targetTable = GetTargetTable(command.CommandText);

        // Store the column name associated with the respective value for comparison
        Dictionary<string, object> parametersFilter = new Dictionary<string, object>();
        Regex regex = new Regex(@"\.*\[[a-zA-Z]\].\[(?<columnName>\S*)\]\s*=\s*N?'(?<paramValue>.*?)'");
        MatchCollection matches = regex.Matches(command.CommandText);

        if (matches.Count == 0) {
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

        // Get the default indexes for the columns of the Discount Database
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

   //[Trace]
    private static Dictionary<string, int> GetDefaultColumIndexes(string targetTable) {
        Dictionary<string, int> columnIndexes = new Dictionary<string, int>();

        // Apply the filters according to the Target Table
        switch (targetTable) {
            case "Discount":
                columnIndexes.Add("Id", 0);
                columnIndexes.Add("ItemName", 1);
                columnIndexes.Add("ItemBrand", 2);
                columnIndexes.Add("ItemType", 3);
                columnIndexes.Add("DiscountValue", 4);
                break;
        }
        return columnIndexes;
    }

   //[Trace]
    private static Dictionary<string, int> GetUniqueIndentifierColumns(string targetTable) {
        Dictionary<string, int> columnUniqueIdentifiers = new Dictionary<string, int>();

        switch (targetTable) {
            case "Discount":
                columnUniqueIdentifiers.Add("ItemName", 1);
                columnUniqueIdentifiers.Add("ItemBrand", 2);
                columnUniqueIdentifiers.Add("ItemType", 3);
                break;
        }

        return columnUniqueIdentifiers;
    }

   //[Trace]
    private string GetTargetTable(string commandText) {
        // Extract the name of the table target by the SQL query
        var match = Regex.Match(commandText, @"(?i)(?:INSERT INTO|FROM|UPDATE)\s*\[(?<Target>[_a-zA-Z]*)\]",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups["Target"].Value : null;
    }

   //[Trace]
    private (Boolean, string, string) HasOrderByCondition(string commandText) {
        // Searches for either a ASC or DESC pattern. Will not match in the default case.
        var match = Regex.Match(commandText, @"ORDER BY\s+\[[_a-zA-Z]+\]\.\[(?<Column>[_a-zA-Z]+)\]\s*(?<Type>ASC|DESC|)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        string typeOrdering = match.Success ? match.Groups["Type"].Value : null;
        if (typeOrdering == "") {
            // ASC/DESC is missing. Assume default=ASC
            typeOrdering = "ASC";
        }
        return match.Success ? (true, match.Groups["Column"].Value, typeOrdering) : (false, null, null);

    }

   //[Trace]
    private (Boolean, string) HasOffsetCondition(string commandText) {
        var match = Regex.Match(commandText, @"OFFSET\s+(?<OffsetValue>[@_a-zA-Z\d]+)\s+ROWS",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? (true, match.Groups["OffsetValue"].Value) : (false, null);
    }

   //[Trace]
    private (Boolean, string) HasFetchNextCondition(string commandText) {
        var match = Regex.Match(commandText, @"FETCH NEXT\s+(?<FetchRows>[@_a-zA-Z\d]+)\s+ROWS",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? (true, match.Groups["FetchRows"].Value) : (false, null);
    }

   //[Trace]
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

    private Boolean HasFilterCondition(string commandText) {
        return commandText.IndexOf("WHERE") != -1;
    }

    private (Boolean, int) HasTopClause(string commandText) {
        Regex regex = new Regex(@"\.*TOP\((?<nElem>\d*)\)");
        MatchCollection matches = regex.Matches(commandText);
        if (matches.Count > 0) {
            return (true, Convert.ToInt32(matches[0].Groups[1].Value));
        }
        return (false, 0);
    }

    private Boolean HasCountClause(string commandText) {
        return commandText.Contains("COUNT");
    }

   //[Trace]
    private void WaitForProposedItemsIfNecessary(DbCommand command, string clientID, string clientTimestamp, string targetTable) {
        // The cases where this applies are:
        // 1. The command is a SELECT query and all rows are being selected and the proposed items are not empty
        // 2. The command is a SELECT query and some rows that are being selected are present in the proposed items

        DateTime readerTimestamp = DateTime.Parse(clientTimestamp);
        List<Tuple<string, string>> conditions = (command.CommandText.IndexOf("WHERE") != -1) ? GetWhereConditions(command) : null;

        var mres = _wrapper.AnyProposalWithLowerTimestamp(conditions, targetTable, readerTimestamp, clientID);
        if(mres == null) {
            // There are no proposed items with lower timestamp than the client timestamp
            return;
        }

        for (int i = 0; i < mres.Count; i++) {
            _logger.LogInformation($"ClientID {clientID}: There is at least one proposed item with lower timestamp than the client timestamp.");
            mres[i].WaitOne(); // Since this is a Manual Reset Event, once the object is notified by the writer, all threads can proceed without stopping
            _logger.LogInformation($"ClientID {clientID}: The proposed item was committed. Checking if there are more proposed items with lower timestamp than the client timestamp.");
        }
        _logger.LogInformation($"ClientID {clientID}: There are no more proposed items with lower timestamp than the client timestamp.");
    }

   //[Trace]
    private List<Tuple<string, string>> GetWhereConditions(DbCommand command) {
        List<Tuple<string, string>> conditions = new List<Tuple<string, string>>();
        
        // Get all equality conditions in the format: [table].[column] = @param (or) [table].[column] = N'param'
        MatchCollection matches = GetWhereConditionsColumnAndValueRegex.Matches(command.CommandText);
        foreach (Match match in matches) {
            // Get the column name and the parameter name
            string columnName = match.Groups["columnName"].Value;
            if (columnName == "Timestamp") { // Timestamp is not considered as a condition
                continue;
            }

            string parameterName = match.Groups["paramValue2"].Value;
            object parameterValue;
            if(!parameterName.IsNullOrEmpty()) {
                parameterValue = command.Parameters[parameterName].Value;
            }
            else {
                parameterValue = match.Groups["paramValue1"].Value;
            }

            // Add the condition to the list
            Tuple<string, string> condition = new Tuple<string, string>(columnName, parameterValue.ToString());
            conditions.Add(condition);
        }
        return conditions;
    }

   //[Trace]
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

        // Get the default indexes for the columns identifiers of the Discount Database
        Dictionary<string, int> columnIndexes = new Dictionary<string, int> {
            // Apply the filters according to the Target Table
            { "ItemName", 0 },
            { "ItemBrand", 1 },
            { "ItemType", 2 }
        };

        foreach (KeyValuePair<string, object> parameter in parametersFilter) {
            if (columnIndexes.TryGetValue(parameter.Key, out int columnIndex)) {
                filteredData = proposedSet
                        .Where(row => row[columnIndex] != null && row[columnIndex].ToString() == parameter.Value.ToString())
                        .ToList();
            }
        }
        return filteredData;
    }
}


