﻿using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.eShopOnContainers.Services.Discount.API.DependencyServices;
using Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.DataReaders;
using Microsoft.eShopOnContainers.Services.Discount.API.Model;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.Interceptors; 
public class DiscountDBInterceptor : DbCommandInterceptor {
    const int INSERT_COMMAND = 1;
    const int SELECT_COMMAND = 2;
    const int UPDATE_COMMAND = 3;
    const int DELETE_COMMAND = 4;
    const int UNKNOWN_COMMAND = -1;

    public DiscountDBInterceptor(DiscountContext discountContext, IOptions<DiscountSettings> settings) {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables();
        var config = builder.Build();
        var connectionString = config["ConnectionString"];
        var contextOptions = new DbContextOptionsBuilder<DiscountContext>()
            .UseSqlServer(connectionString)
            .Options;

        _scopedMetadata = discountContext._scopedMetadata;
        _wrapper = discountContext._wrapper;
        _discountContext = new DiscountContext(contextOptions, _scopedMetadata, _wrapper, settings);
    }

    public IScopedMetadata _scopedMetadata;
    public ISingletonWrapper _wrapper;
    public DiscountContext _discountContext;
    private string _originalCommandText;

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {

        _originalCommandText = new string(command.CommandText);

        (var commandType, var targetTable) = GetCommandInfo(command);

        if (commandType == UNKNOWN_COMMAND) {
            return result;
        }

        // Check if the Transaction ID
        var funcID = _scopedMetadata.ScopedMetadataFunctionalityID;

        // Check if is this a functionality context (ID differs from null)
        if (funcID == null) {
            // This is a system query
            return result;
        }

        switch (commandType) {
            case UNKNOWN_COMMAND:
                return result;
            case SELECT_COMMAND:
                UpdateSelectCommand(command);
                break;
            case INSERT_COMMAND:
                bool funcStateIns = _wrapper.SingletonGetTransactionState(funcID);
                if (!funcStateIns) {
                    // If the Transaction is not in commit state, store data in wrapper
                    var mockReader = StoreDataInWrapper(command, INSERT_COMMAND, targetTable);
                    result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
                }
                else {
                    // Transaction is in commit state, update the command to store in the database
                    UpdateInsertCommand(command, targetTable);
                }
                break;
            case UPDATE_COMMAND:
                // Convert the Update Command into an INSERT command
                Dictionary<string, object> columnsToInsert = UpdateToInsert(command, targetTable);

                // Add the new object to the catalog context
                switch (targetTable) {
                    case "Discount":
                        var newDiscount = new DiscountItem() {
                            ItemName = (string)columnsToInsert["ItemName"],
                            ItemBrand = (string)columnsToInsert["ItemBrand"],
                            ItemType = (string)columnsToInsert["ItemType"],
                            DiscountValue = (int)columnsToInsert["DiscountValue"]
                        };
                        _discountContext.Discount.Add(newDiscount);
                        break;
                }
                _discountContext.SaveChanges();
                var testMock = new List<object[]> {
                    new object[] { 1 }
                };
                result = InterceptionResult<DbDataReader>.SuppressWithResult(new MockDbDataReader(testMock, 1, targetTable));
                break;
        }

        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) {

        _originalCommandText = new string(command.CommandText);

        (var commandType, var targetTable) = GetCommandInfo(command);

        // Check if the Transaction ID
        var funcID = _scopedMetadata.ScopedMetadataFunctionalityID;

        if (funcID == null) {
            // This is a system query
            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }

        switch (commandType) {
            case UNKNOWN_COMMAND:
                return new ValueTask<InterceptionResult<DbDataReader>>(result);
            case SELECT_COMMAND:
                UpdateSelectCommand(command);
                break;
            case INSERT_COMMAND:
                bool funcStateIns = _wrapper.SingletonGetTransactionState(funcID);
                if (!funcStateIns) {
                    // If the Transaction is not in commit state, store data in wrapper
                    var mockReader = StoreDataInWrapper(command, INSERT_COMMAND, targetTable);
                    result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
                }
                else {
                    // Transaction is in commit state, update the command to store in the database
                    UpdateInsertCommand(command, targetTable);
                }
                break;
            case UPDATE_COMMAND:
                // Convert the Update Command into an INSERT command
                Dictionary<string, object> columnsToInsert = UpdateToInsert(command, targetTable);
                var mockRows = new List<object[]>();
                // Add the new object to the catalog context
                switch (targetTable) {
                    case "Discount":
                        var newDiscount = new DiscountItem() {
                            ItemName = (string)columnsToInsert["ItemName"],
                            ItemBrand = (string)columnsToInsert["ItemBrand"],
                            ItemType = (string)columnsToInsert["ItemType"],
                            DiscountValue = (int)columnsToInsert["DiscountValue"]
                        };
                        _discountContext.Discount.Add(newDiscount);
                        break;
                }
                _discountContext.SaveChanges();
                var testMock = new List<object[]> {
                    new object[] { 1 }
                };
                result = InterceptionResult<DbDataReader>.SuppressWithResult(new MockDbDataReader(testMock, 1, targetTable));
                break;
        }

        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    private MockDbDataReader StoreDataInWrapper(DbCommand command, int operation, string targetTable) {
        var funcId = _scopedMetadata.ScopedMetadataFunctionalityID;

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
            rowsAffected++;
        }

        var mockReader = new MockDbDataReader(rows, rowsAffected, targetTable);

        switch (targetTable) {
            case "Discount":
                _wrapper.SingletonAddDiscountItem(funcId, rows.ToArray());
                break;
        }
        return mockReader;
    }

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
    /// Updates the Read queries for Item Count, Brands and Types command, adding a filter for the functionality Timestamp (DateTime). 
    /// Applicable to queries that contain either:
    /// "SELECT COUNT_BIG(*) ..."; "SELECT ... FROM [CatalogBrand] ..."; "SELECT ... FROM [CatalogType] ..."
    /// </summary>
    /// <param name="command"></param>
    private void UpdateSelectCommand(DbCommand command) {
        // Get the current functionality-set timeestamp
        DateTime functionalityTimestamp = _scopedMetadata.ScopedMetadataTimestamp;

        (bool hasPartialRowSelection, List<string> _) = HasPartialRowSelection(command.CommandText);
        if (hasPartialRowSelection) {
            // Remove the partial Row Selection
            command.CommandText = RemovePartialRowSelection(command.CommandText);
        }

        bool hasCount = HasCountClause(command.CommandText);
        if (hasCount) {
            command.CommandText = RemoveCountSelection(command.CommandText);
        }

        // Replace Command Text to account for new filter
        if (command.CommandText.Contains("WHERE")) {
            // Command already has at least 1 filter
            command.CommandText += $" AND [d].[Timestamp] <= '{functionalityTimestamp}'";
        }
        else {
            // Command has no filters yet
            command.CommandText = command.CommandText.Replace("AS [d]", $"AS [d] WHERE [d].[Timestamp] <= '{functionalityTimestamp}'");
        }
    }

    private string RemovePartialRowSelection(string commandText) {
        string pattern = @"SELECT\s+(.*?)\s+FROM";
        string replacement = "SELECT * FROM";
        string result = Regex.Replace(commandText, pattern, replacement);
        return result;
    }

    private string RemoveCountSelection(string commandText) {
        string pattern = @"SELECT\s+(.*?)\s+FROM";
        string replacement = "SELECT * FROM";
        string result = Regex.Replace(commandText, pattern, replacement);
        return result;
    }

    /* ========== UPDATE WRITE QUERIES ==========*/

    private void UpdateInsertCommand(DbCommand command, string targetTable) {
        // Get the timestamp received from the Coordinator
        string timestamp = _scopedMetadata.ScopedMetadataTimestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

        // Replace Command Text to account for new parameter
        string commandWithTimestamp;
        commandWithTimestamp = UpdateInsertCommandText(command, targetTable);

        // Generate new list of parameters
        List<DbParameter> newParameters = new List<DbParameter>();
        if (targetTable == "Discount") {
            UpdateDiscountOp(command, newParameters, timestamp);
        }

        // Assign new Parameters and Command text to database command
        command.Parameters.Clear();
        command.Parameters.AddRange(newParameters.ToArray());
        command.CommandText = commandWithTimestamp;
    }

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

    private static void UpdateDiscountOp(DbCommand command, List<DbParameter> generatedParameters, string timestamp) {
        int numObjectsToInsert = command.Parameters.Count / 11;

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

    private static string UpdateInsertCommandText(DbCommand command, string targetTable) {
        string updatedCommandText = "";
        int numberColumns = 0;

        // Update the CommandText to include the Timestamp column and parameter for each entry
        if (targetTable == "Discount") {
            numberColumns = 6;
            updatedCommandText = Regex.Replace(command.CommandText, @"INSERT INTO\s+\[Discount\]\s+\(\s*\[Id\],\s*\[ItemName\],\s*\[ItemBrand\],\s*\[ItemType\],\s*\[DiscountValue\]\s*", "$0, [Timestamp]");
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


    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) {
        var funcId = _scopedMetadata.ScopedMetadataFunctionalityID;

        if (funcId == null) {
            // This is a system transaction
            return result;
        }

        // Check if the command is an INSERT command and has yet to be committed
        if (command.CommandText.Contains("INSERT") && !_wrapper.SingletonGetTransactionState(funcId)) {
            return result;
        }

        // Note: It is important that the wrapper data is cleared before saving it to the database, when the commit happens.
        string targetTable = GetTargetTable(command.CommandText);
        if (targetTable.IsNullOrEmpty()) {
            // Unsupported Table (Migration for example)
            return result;
        }

        var newData = new List<object[]>();

        while (result.Read()) {
            var rowValues = new List<object>();
            for (int i = 0; i < result.FieldCount; i++) {
                var fieldType = result.GetFieldType(i);
                switch (fieldType.Name) {
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
                    case "DateTime":
                        rowValues.Add(result.GetDateTime(i));
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
            targetTable = GetTargetTable(command.CommandText);
            List<object[]> wrapperData = null;

            switch (targetTable) {
                case "Discount":
                    wrapperData = _wrapper.SingletonGetDiscountItems(funcId).ToList();
                    break;
            }

            // Filter the results to display only 1 version of data for both the Wrapper Data as well as the DB data
            newData = GroupVersionedObjects(newData, targetTable);
            wrapperData = GroupVersionedObjects(wrapperData, targetTable);

            if (wrapperData != null) {
                if (HasFilterCondition(command.CommandText)) {
                    // The select query contains a WHERE clause
                    wrapperData = FilterData(wrapperData, command);

                    if (!HasCountClause(command.CommandText)) {
                        // Only add the rows if the data from the DB is not a single count product
                        foreach (var wrapperRow in wrapperData) {
                            newData.Add(wrapperRow);
                        }
                    }
                    else {
                        // The select query contains a COUNT clause, so we need to update the count
                        newData[0][0] = Convert.ToInt32(newData[0][0]) + wrapperData.Count;
                    }
                }

                // We group the data again to ensure that the data is grouped by the same version in the union of the DB and Wrapper data
                newData = GroupVersionedObjects(newData, targetTable);

                (bool hasOrderBy, string orderByColumn, string typeOrder) = HasOrderByCondition(command.CommandText);
                if (hasOrderBy) {
                    // The select query has an ORDER BY clause
                    newData = OrderBy(newData, orderByColumn, typeOrder, targetTable);
                }

                if (HasCountClause(command.CommandText)) {
                    var wrapperDataCount = wrapperData.Count;
                    newData[0][0] = Convert.ToInt64(newData[0][0]) + wrapperDataCount;
                    return new WrapperDbDataReader(newData, result, targetTable);
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

                (bool hasPartialRowSelection, List<string> selectedColumns) = HasPartialRowSelection(_originalCommandText);
                if (hasPartialRowSelection) {
                    newData = PartialRowSelection(command.CommandText, newData, selectedColumns);
                }

            }
        }

        return new WrapperDbDataReader(newData, result, targetTable);
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) {
        var funcId = _scopedMetadata.ScopedMetadataFunctionalityID;

        if (funcId == null) {
            // This is a system transaction or a database initial population
            return result;
        }

        // Check if the command is an INSERT command and has yet to be committed
        if (command.CommandText.Contains("INSERT") && !_wrapper.SingletonGetTransactionState(funcId)) {
            return result;
        }

        // Note: It is important that the wrapper data is cleared before saving it to the database, when the commit happens.

        string targetTable = GetTargetTable(command.CommandText);
        if (targetTable.IsNullOrEmpty()) {
            // Unsupported Table (Migration for example)
            return result;
        }

        var newData = new List<object[]>();

        while (await result.ReadAsync(cancellationToken)) {
            var rowValues = new List<object>();
            for (int i = 0; i < result.FieldCount; i++) {
                var fieldType = result.GetFieldType(i);
                switch (fieldType.Name) {
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
                    case "DateTime":
                        rowValues.Add(result.GetDateTime(i));
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
            targetTable = GetTargetTable(command.CommandText);
            List<object[]> wrapperData = null;

            switch (targetTable) {
                case "Discount":
                    wrapperData = _wrapper.SingletonGetDiscountItems(funcId).ToList();
                    break;
            }

            // Filter the results to display only 1 version of data for both the Wrapper Data as well as the DB data
            newData = GroupVersionedObjects(newData, targetTable);
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

            if (HasCountClause(_originalCommandText)) {
                List<object[]> countedData = new List<object[]>();
                object[] data = new object[] { newData.Count + wrapperData.Count };
                countedData.Add(data);

                return new WrapperDbDataReader(countedData, result, targetTable);
            }

            // Search for partial SELECTION on the original unaltered commandText
            (bool hasPartialRowSelection, List<string> selectedColumns) = HasPartialRowSelection(_originalCommandText);
            if (hasPartialRowSelection) {
                newData = PartialRowSelection(command.CommandText, newData, selectedColumns);
            }

        }
        return new WrapperDbDataReader(newData, result, targetTable);
    }

    private List<object[]> OrderBy(List<object[]> newData, string orderByColumn, string typeOrder, string targetTable) {
        Dictionary<string, int> columnIndexes = GetDefaultColumIndexes(targetTable);
        int sortByIndex = columnIndexes[orderByColumn];

        // Determine the type of the objects to compare
        Type sortColumnType = newData.First()[sortByIndex].GetType();

        newData = newData.OrderBy(arr => Convert.ChangeType(arr[sortByIndex], sortColumnType)).ToList();
        return newData;
    }

    private List<object[]> OffsetData(DbCommand command, List<object[]> newData, string offsetParam) {
        DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == offsetParam);
        int offsetValue = Convert.ToInt32(parameter.Value);
        newData = newData.Skip(offsetValue).ToList();
        return newData;
    }

    private List<object[]> FetchNext(DbCommand command, List<object[]> newData, string fetchRowsParam) {
        DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == fetchRowsParam);
        int fetchRows = Convert.ToInt32(parameter.Value);
        newData = newData.Take(fetchRows).ToList();
        return newData;
    }

    private List<object[]> GroupVersionedObjects(List<object[]> newData, string targetTable) {
        var dateTimeComparer = Comparer<object>.Create((x, y) => {
            DateTime xValue = (DateTime)x;
            DateTime yValue = (DateTime)y;
            return xValue.CompareTo(yValue);
        });

        var groupByColumns = GetUniqueIndentifierColumns(targetTable);

        switch (targetTable) {
            case "Catalog":
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

    private List<object[]> PartialRowSelection(string commandText, List<object[]> newData, List<string> selectedColumns) {
        string targetTable = GetTargetTable(commandText);

        // Get the default indexes for the columns of the Catalog Database
        Dictionary<string, int> columnIndexes = GetDefaultColumIndexes(targetTable);

        newData = newData.Select(row => {
            var newRow = new object[selectedColumns.Count];
            for (int i = 0; i < selectedColumns.Count; i++) {
                string columnName = selectedColumns[i];
                int columnIndex = columnIndexes[columnName];
                newRow[i] = row[columnIndex];
            }
            return newRow;
        }).ToList();

        return newData;
    }

    private List<object[]> FilterData(List<object[]> wrapperData, DbCommand command) {
        string commandText = command.CommandText;
        var targetTable = GetTargetTable(commandText);

        var filteredData = ApplyWhereFilterIfExist(wrapperData, command).ToList();

        return filteredData;
    }

    private IEnumerable<object[]> ApplyWhereFilterIfExist(List<object[]> wrapperData, DbCommand command) {
        List<object[]> filteredData = new List<object[]>();
        string commandText = GetTargetTable(command.CommandText);

        // Store the column name associated with the respective value for comparison
        Dictionary<string, object> parametersFilter = new Dictionary<string, object>();
        Regex regex = new Regex(@"\.*\[[a-zA-Z]\].\[(?<columnName>\S*)\]\s*=\s*(?<paramValue>\S*)");
        MatchCollection matches = regex.Matches(command.CommandText);

        if (matches.Count == 0) {
            // No Where filters exist
            return wrapperData;
        }

        // Extract parameter names from the matches
        for (int i = 0; i < matches.Count; i++) {
            string columnName = matches[i].Groups[1].Value;
            string parameterParametrization = matches[i].Groups[2].Value;
            DbParameter parameter = command.Parameters.Cast<DbParameter>().SingleOrDefault(p => p.ParameterName == parameterParametrization);
            parametersFilter[columnName] = parameter.Value;
        }

        // Get the default indexes for the columns of the Catalog Database
        var columnIndexes = GetDefaultColumIndexes(commandText);

        foreach (KeyValuePair<string, object> parameter in parametersFilter) {
            if (columnIndexes.TryGetValue(parameter.Key, out int columnIndex)) {
                filteredData = wrapperData
                        .Where(row => row[columnIndex] != null && row[columnIndex].ToString() == parameter.Value.ToString())
                        .ToList();
            }
        }
        return filteredData;
    }

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

    private string GetTargetTable(string commandText) {
        // Extract the name of the table target by the SQL query
        var match = Regex.Match(commandText, @"(?i)(?:INSERT INTO|FROM|UPDATE)\s*\[(?<Target>[_a-zA-Z]*)\]",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups["Target"].Value : null;
    }

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

    private (Boolean, string) HasOffsetCondition(string commandText) {
        var match = Regex.Match(commandText, @"OFFSET\s+(?<OffsetValue>[@_a-zA-Z\d]+)\s+ROWS",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? (true, match.Groups["OffsetValue"].Value) : (false, null);
    }

    private (Boolean, string) HasFetchNextCondition(string commandText) {
        var match = Regex.Match(commandText, @"FETCH NEXT\s+(?<FetchRows>[@_a-zA-Z\d]+)\s+ROWS",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? (true, match.Groups["FetchRows"].Value) : (false, null);
    }

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
}


