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
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.Interceptors;
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
                var mockReader = StoreDataInWrapper(command);
                result = InterceptionResult<DbDataReader>.SuppressWithResult(mockReader);
            } 
            else {
                UpdateInsertCommand(command, targetTable);
            }
        }

        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    private MockDbDataReader StoreDataInWrapper(DbCommand command) {
        var funcId = _scopedMetadata.ScopedMetadataFunctionalityID;
        var targetTable = GetInsertTargetTable(command.CommandText);

        // Get the number of columns in each row to be inserted
        var regex = new Regex(@"\((.*?)\)");
        var matches = regex.Matches(command.CommandText);
        var values = matches[matches.Count - 1].Groups[1].Value.Split(',');
        var numberColumnsPerRow = values.Length;

        var rows = new List<object[]>();

        var rowsAffected = 0;
        for (int i = 0; i < command.Parameters.Count; i += numberColumnsPerRow) {
            var row = new List<object>();
            for (int j = 0; j < i + numberColumnsPerRow; j++) {
                var columnValue = command.Parameters[$"@p{j}"];
                row.Add(columnValue.Value == DBNull.Value ? null : columnValue.Value);
            }
            rows.Add(row.ToArray());
            rowsAffected++;
        }

        var mockReader = new MockDbDataReader(rows, rowsAffected, targetTable);

        switch (targetTable) {
            case "CatalogBrand":
                _wrapper.SingletonAddCatalogBrand(funcId, rows.ToArray());
                break;
            case "CatalogType":
                _wrapper.SingletonAddCatalogType(funcId, rows.ToArray());
                break;
            case "Catalog":
                _wrapper.SingletonAddCatalogItem(funcId, rows.ToArray());
                break;
        }
        return mockReader;
    }

    private string GetInsertTargetTable(string commandText) {
        // Extract the name of the table being inserted into, from the command text.
        var match = Regex.Match(commandText, @"INSERT INTO\s*\[(?<Target>[a-zA-Z]*)\]",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups["Target"].Value : null;
    }
    private string GetSelectTargetTable(string commandText) {
        // Extract the name of the table being selected from, from the command text.
        var match = Regex.Match(commandText, @"FROM\s*\[(?<Target>[a-zA-Z]*)\]",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return match.Success ? match.Groups["Target"].Value : null;
    }

    public (int, string) GetCommandInfo(DbCommand command) {
        var commandType = GetCommandType(command);

        // Check if the command is an INSERT command.
        if (commandType == INSERT_COMMAND) {
            string targetTable = GetInsertTargetTable(command.CommandText);
            return (INSERT_COMMAND, targetTable);
        }
        else if (commandType == SELECT_COMMAND) {
            string targetTable = GetSelectTargetTable(command.CommandText);
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

        string commandTextWithFilter = string.Empty;
        // Replace Command Text to account for new filter
        if (command.CommandText.Contains("WHERE")) {
            // Command already has at least 1 filter
            command.CommandText += $" AND [c].[Timestamp] <= '{functionalityTimestamp}'"; 
        }
        else {
            // Command has no filters yet
            command.CommandText = command.CommandText.Replace("AS [c]", $"AS [c] WHERE [c].[Timestamp] <= '{functionalityTimestamp}'");
        }
    }


    /* ========== UPDATE WRITE QUERIES ==========*/

    private void UpdateInsertCommand(DbCommand command, string targetTable) {
        // Get the current timestamp - Should use the value received from the Coordinator
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");

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
        var funcId = _scopedMetadata.ScopedMetadataFunctionalityID;

        // Check if the command is an INSERT command and has yet to be committed
        if (command.CommandText.Contains("INSERT") && !_wrapper.SingletonGetTransactionState(funcId)) {
            return result;
        }

        // Note: It is important that the wrapper data is cleared before saving it to the database, when the commit happens.

        string targetTable = string.Empty;
        if (command.CommandText.Contains("INSERT")) {
            targetTable = GetInsertTargetTable(command.CommandText);
        }
        else {
            targetTable = GetSelectTargetTable(command.CommandText);
            if(targetTable.IsNullOrEmpty()) {
                // Unsupported Table (Migration for example)
                return result;
            }
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
                    // add additional cases for other data types as needed
                    default:
                        rowValues.Add(null);
                        break;
                }
            }
            newData.Add(rowValues.ToArray());
        }

        // Read the data from the Wrapper structures if inside a functionality
        if (funcId != null && command.CommandText.Contains("SELECT")) {
            targetTable = GetSelectTargetTable(command.CommandText);
            ConcurrentBag<object[]> wrapperData = null;

            switch (targetTable) {
                case "CatalogBrand":
                    wrapperData = _wrapper.SingletonGetCatalogBrands(funcId);
                    break;
                case "CatalogType":
                    wrapperData = _wrapper.SingletonGetCatalogTypes(funcId);
                    break;
                case "Catalog":
                    wrapperData = _wrapper.SingletonGetCatalogITems(funcId);
                    break;
            }
            if (wrapperData != null) {
                if (IsCountSelect(command.CommandText)) {
                    var countObj = newData[0][0];
                    Console.WriteLine(newData[0][0]);

                    // Check if the Command query has a Filter applied
                    if (HasFilterCondition(command.CommandText)) {
                        var filteredData = FilterData(wrapperData, command);
                        newData[0][0] = Convert.ToInt64(countObj) + filteredData.Count;
                    }
                }
                else {
                    if (HasFilterCondition(command.CommandText)) {
                        var filteredData = FilterData(wrapperData, command);
                        foreach (object[] row in filteredData) {
                            newData.Add(row);
                        }
                    }
                    else {
                        foreach (object[] row in wrapperData) {
                            newData.Add(row);
                        }
                    }
                }
            }
        }

        var recordsAffected = result.RecordsAffected;
        var newReader = new WrapperDbDataReader(newData, result, targetTable, recordsAffected);
        return newReader;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default) {
        var funcId = _scopedMetadata.ScopedMetadataFunctionalityID;
        
        // Check if the command is an INSERT command and has yet to be committed
        if(command.CommandText.Contains("INSERT") && !_wrapper.SingletonGetTransactionState(funcId)) {
            return result;
        }

        // Note: It is important that the wrapper data is cleared before saving it to the database, when the commit happens.

        string targetTable = string.Empty;
        if (command.CommandText.Contains("INSERT")) {
            targetTable = GetInsertTargetTable(command.CommandText);
        }
        else {
            targetTable = GetSelectTargetTable(command.CommandText);
            if (targetTable.IsNullOrEmpty()) {
                // Unsupported Table (Migration for example)
                return result;
            }
        }
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

        // Read the data from the Wrapper structures
        if (command.CommandText.Contains("SELECT")) {
            targetTable = GetSelectTargetTable(command.CommandText);
            ConcurrentBag<object[]> wrapperData = null;

            switch (targetTable) {
                case "CatalogBrand":
                    wrapperData = _wrapper.SingletonGetCatalogBrands(funcId);
                    break;
                case "CatalogType":
                    wrapperData = _wrapper.SingletonGetCatalogTypes(funcId);
                    break;
                case "Catalog":
                    wrapperData = _wrapper.SingletonGetCatalogITems(funcId);
                    break;
            }
            if (wrapperData != null) {
                if (IsCountSelect(command.CommandText)) {
                    var countObj = newData[0][0];
                    Console.WriteLine(newData[0][0]);

                    // Check if the Command query has a Filter applied
                    if (HasFilterCondition(command.CommandText)) {
                        var filteredData = FilterData(wrapperData, command);
                        newData[0][0] = Convert.ToInt64(countObj) + filteredData.Count;
                    }
                }
                else {
                    if (HasFilterCondition(command.CommandText)) {
                        var filteredData = FilterData(wrapperData, command);
                        foreach (object[] row in filteredData) {
                            newData.Add(row);
                        }
                    }
                    else {
                        foreach (object[] row in wrapperData) {
                            newData.Add(row);
                        }
                    }
                }
            }
        } else {
            targetTable = GetInsertTargetTable(command.CommandText);
        }
            
        var recordsAffected = result.RecordsAffected;
        var newReader = new WrapperDbDataReader(newData, result, targetTable, recordsAffected);
        return newReader;
    }

    private List<object[]> FilterData(ConcurrentBag<object[]> wrapperData, DbCommand command) {
        string commandText = command.CommandText;
        var targetTable = GetSelectTargetTable(commandText);

        // Store the column name that are selected
        List<string> columnSelect = new List<string>();

        var filteredData = ApplyWhereFilterIfExist(wrapperData, command).ToList();

        // Extract SELECT clause parameters
        string subCommandText = commandText.Replace(commandText.Substring(commandText.IndexOf("FROM")), "");
        Regex regex = new Regex(@"\[(?<targetTable>[a-zA-Z]+)\]\.*\[(?<columnName>[a-zA-Z]+)\]");
        MatchCollection matches = regex.Matches(subCommandText);

        if (matches.Count == 0) {
            // No Where filters exist
            return filteredData;
        }

        var columnIndexes = new Dictionary<string, int>();
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

        // Extract parameter names from the matches
        for (int i = 0; i < matches.Count; i++) {
            columnSelect.Add(matches[i].Groups[2].Value);
        }

        filteredData = filteredData.Select(row => {
            var filteredRow = new object[columnSelect.Count];
            for (int i = 0; i < columnSelect.Count; i++) {
                filteredRow[i] = row[columnIndexes[columnSelect[i]]];
            }
            return filteredRow;
        }).ToList();

        // Check if command includes TOP(n) clause
        (bool hasTopClause, int nElem) = HasTopClause(commandText);
        if( hasTopClause ) {
            // Reduce the number of items in the filteredData list according to TOP clause
            filteredData = filteredData.Take(nElem).ToList();
        }


        // -----------------


        //// Regex pattern to match the SELECT clause of the SQL command
        //string pattern = @"SELECT\s+(.*?)\s+FROM";
        //Match match = Regex.Match(commandText, pattern, RegexOptions.IgnoreCase);
        //if (match.Success) {
        //    bool hasTOPclause = false;
        //    int topClauseItems = 0;

        //    List<string> columns = new List<string>();

        //    // Extract the list of columns (with table alias) from the SELECT clause
        //    string fields = match.Groups[1].Value;

        //    // Split the list by the commas
        //    string[] fieldList = fields.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        //    // Regex pattern to match only the column name
        //    string columnNamePattern = @"\[[a-zA-Z]+\].\[(?<columnName>.+?)\]$";
        //    foreach (string field in fieldList) {
        //        if (field.Contains("COUNT")) {
        //            // Ignore the COUNT clause
        //            continue;
        //        }

        //        if (field.Contains("TOP")) {
        //            // There is a TOP clause present
        //            hasTOPclause = true;

        //            // Extract the number of elements to display
        //            string numberElementsPattern = @"TOP\((?<items>.\d*?)\).*$";
        //            Match numberElementsMatch = Regex.Match(field, numberElementsPattern);
        //            if (numberElementsMatch.Success) {

        //                string numberElements = numberElementsMatch.Groups["items"].Value.Trim();
        //                topClauseItems = Convert.ToInt32(numberElements);
        //            }
        //        }

        //        // Match each column Name in the SELECT clause (without tables alias)
        //        Match columnMatch = Regex.Match(field, columnNamePattern);
        //        if (columnMatch.Success) {

        //            string columnName = columnMatch.Groups["columnName"].Value;
        //            columns.Add(columnName.Trim());
        //        }
        //    }

        //    if (columns.Count <= 0) {
        //        return filteredData;
        //    }

        //    Dictionary<string, int> fieldNamesToReturn = new Dictionary<string, int>();
        //    switch (GetSelectTargetTable(command.CommandText)) {
        //        case "CatalogBrand":
        //            foreach (string column in columns) {
        //                switch (column) {
        //                    case "Id":
        //                        fieldNamesToReturn.Add(column, 0);
        //                        break;
        //                    case "Brand":
        //                        fieldNamesToReturn.Add(column, 1);
        //                        break;
        //                }
        //            }
        //            filteredData = filteredData
        //                .Select(row => fieldNamesToReturn.Select(fieldName => row[fieldName.Value]).ToArray())
        //                .ToList();
        //            break;
        //        case "CatalogType":
        //            foreach (string column in columns) {
        //                switch (column) {
        //                    case "Id":
        //                        fieldNamesToReturn.Add(column, 0);
        //                        break;
        //                    case "Type":
        //                        fieldNamesToReturn.Add(column, 1);
        //                        break;
        //                }
        //            }
        //            filteredData = filteredData
        //                .Select(row => fieldNamesToReturn.Select(fieldName => row[fieldName.Value]).ToArray())
        //                .ToList();
        //            break;
        //    }

        //    if (hasTOPclause) {
        //        // Reduce the number of items in the filteredData list according to TOP clause
        //        filteredData = filteredData.Take(topClauseItems).ToList();
        //    }

        //}

        return filteredData;
    }

    private IEnumerable<object[]> ApplyWhereFilterIfExist(ConcurrentBag<object[]> wrapperData, DbCommand command) {
        List<object[]> filteredData = new List<object[]>();

        // Store the column name associated with the respective value for comparison
        Dictionary<string, object> parametersFilter = new Dictionary<string, object>();
        Regex regex = new Regex(@"\.*\[[a-zA-Z]\].\[(?<columnName>\S*)\]\s*=\s*(?<paramValue>\S*)");
        MatchCollection matches = regex.Matches(command.CommandText);

        if(matches.Count == 0) {
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

        var columnIndexes = new Dictionary<string, int>();
        // Apply the filters according to the Target Table
        switch (GetSelectTargetTable(command.CommandText)) {
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

        foreach (KeyValuePair<string, object> parameter in parametersFilter) {
            if (columnIndexes.TryGetValue(parameter.Key, out int columnIndex)) {
                filteredData = wrapperData
                        .Where(row => row[columnIndex] != null && row[columnIndex].ToString() == parameter.Value.ToString())
                        .ToList();
            }
        }

        return filteredData;
    }

    private Boolean HasFilterCondition(string commandText) {
        return commandText.IndexOf("WHERE") != -1;
    }

    private (Boolean, int) HasTopClause(string commandText) {
        Regex regex = new Regex(@"\.*TOP\((?<nElem>\d*)\)");
        MatchCollection matches = regex.Matches(commandText);
        if(matches.Count > 0) { 
            return (true, Convert.ToInt32(matches[0].Groups[1].Value));
        }
        return (false, 0);
    }

    private Boolean IsSelectQuery(string commandText) {
        return commandText.Contains("SELECT");
    }

    private Boolean IsCountSelect(string commandText) {
        return commandText.Contains("COUNT");
    }
}
