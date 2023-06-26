using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.DataReaders;
public class WrapperDbDataReader : DbDataReader
{
    private readonly List<object[]> _data;
    private readonly int _fieldCount;
    private readonly bool _hasRows;
    private readonly string _tableRead;
    private int _rowIndex = -1;
    private bool _hasNextResult = false;
    private readonly int _recordsAffected = 0;
    private readonly DbDataReader _dataReader;

    public WrapperDbDataReader(List<object[]> data, DbDataReader dataReader, string tableRead)
    {
        _data = data;
        _fieldCount = _data != null && _data.Count > 0 ? _data[0].Length : 0;
        _hasRows = _data != null && _data.Count > 0;
        _tableRead = tableRead;
        _dataReader = dataReader;
        _recordsAffected = dataReader.RecordsAffected;
    }

    public override bool Read()
    {
        _rowIndex++;
        return _rowIndex < _data.Count;
    }

    // Number of columns
    public override int FieldCount => _fieldCount;

    public override int Depth => throw new NotImplementedException();

    public override bool HasRows => _hasRows;

    public override bool IsClosed => throw new NotImplementedException();

    public override int RecordsAffected => _recordsAffected;

    public override object this[string name] => throw new NotImplementedException();

    public override object this[int ordinal] => throw new NotImplementedException();

    public override object GetValue(int ordinal)
    {
        // Changed this
        return _data[_rowIndex];
    }

    public override bool GetBoolean(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToBoolean(value);
    }

    public override byte GetByte(int ordinal)
    {
        throw new NotImplementedException();
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
    {
        throw new NotImplementedException();
    }

    public override char GetChar(int ordinal)
    {
        throw new NotImplementedException();
    }

    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
    {
        throw new NotImplementedException();
    }

    public override string GetDataTypeName(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var fieldType = GetFieldType(ordinal);

        switch (fieldType.Name) {
            case "Int32":
                return "int";
            case "String":
                return "nvarchar";
            case "Int64":
                return "bigint";
            case "Boolean":
                return "bit";
            default:
                return fieldType.Name;
        }
    }

    public override DateTime GetDateTime(int ordinal)
    {
        throw new NotImplementedException();
    }

    public override decimal GetDecimal(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }
        var value = _data[_rowIndex][ordinal];
        return Convert.ToDecimal(value);
    }

    public override double GetDouble(int ordinal)
    {
        throw new NotImplementedException();
    }

    public override IEnumerator<int> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var schemaTable = _dataReader.GetSchemaTable();
        if (schemaTable.Rows.Count <= ordinal) {
            // Log the schema rows
            var row1 = schemaTable.Rows[0];
            var row2 = schemaTable.Rows[1];
            throw new InvalidOperationException($"There are fewer schema rows ({schemaTable.Rows.Count}) than the ordinal ({ordinal}) requested. Row 1: {row1["ColumnName"]} - {row1["DataType"]}. Row 2: {row2["ColumnName"]} - {row2["DataType"]}");
        }
        var row = schemaTable.Rows[ordinal];
        var columnType = (Type)row["DataType"];
        return columnType;

        throw new InvalidOperationException($"Invalid ordinal {ordinal}");
    }

    public override float GetFloat(int ordinal)
    {
        throw new NotImplementedException();
    }

    public override Guid GetGuid(int ordinal)
    {
        throw new NotImplementedException();
    }

    public override short GetInt16(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToInt16(value);
    }

    public override int GetInt32(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToInt32(value);
    }

    public override long GetInt64(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToInt64(value);
    }

    public override string GetName(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var schemaTable = _dataReader.GetSchemaTable();
        var row = schemaTable.Rows[ordinal];
        var columnName = (string)row["ColumnName"];
        return columnName;
    }

    public override int GetOrdinal(string name)
    {
        throw new NotImplementedException();
    }

    public override string GetString(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToString(value);
    }

    public override int GetValues(object[] values)
    {
        throw new NotImplementedException();
    }

    public override bool IsDBNull(int ordinal)
    {
        if (ordinal < 0 || ordinal >= FieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        return _data[_rowIndex][ordinal] == DBNull.Value;
    }

    public override bool NextResult()
    {
        _hasNextResult = false; // Assume no next result

        // Check if there's another result set
        // (in this example, we assume there's only one result set)
        if (_rowIndex >= _data.Count - 1)
        {
            return false;
        }

        _rowIndex = -1; // Reset row index for new result set
        _hasNextResult = true;
        return true;
    }

    // Implement the required DbDataReader methods
    // ...

    // Implement the IDisposable interface
    // ...
}