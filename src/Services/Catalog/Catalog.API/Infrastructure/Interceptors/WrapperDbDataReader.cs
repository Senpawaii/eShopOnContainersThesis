using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.Interceptors;
public class WrapperDbDataReader : DbDataReader {
    private readonly List<object[]> _data;
    private readonly int _fieldCount;
    private readonly string _tableRead;
    private int _rowIndex = -1;
    private bool _hasNextResult = false;
    private readonly int _recordsAffected = 0;
    private readonly DbDataReader _dataReader;

    public WrapperDbDataReader(List<object[]> data, DbDataReader dataReader, string tableRead, int recordsAffected) {
        _data = data;
        _fieldCount = (_data != null && _data.Count > 0) ? _data[0].Length : 0;
        _tableRead = tableRead;
        _recordsAffected = recordsAffected;
        _dataReader = dataReader;
    }

    public override bool Read() {
        _rowIndex++;
        return _rowIndex < _data.Count;
    }

    // Number of columns
    public override int FieldCount => _fieldCount;

    public override int Depth => throw new NotImplementedException();

    public override bool HasRows => throw new NotImplementedException();

    public override bool IsClosed => throw new NotImplementedException();

    public override int RecordsAffected => _recordsAffected;

    public override object this[string name] => throw new NotImplementedException();

    public override object this[int ordinal] => throw new NotImplementedException();

    public override object GetValue(int ordinal) {
        // Changed this
        return _data[_rowIndex];
    }

    public override bool GetBoolean(int ordinal) {
        throw new NotImplementedException();
    }

    public override byte GetByte(int ordinal) {
        throw new NotImplementedException();
    }

    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) {
        throw new NotImplementedException();
    }

    public override char GetChar(int ordinal) {
        throw new NotImplementedException();
    }

    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) {
        throw new NotImplementedException();
    }

    public override string GetDataTypeName(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }
        
        if(_tableRead == "CatalogBrand" || _tableRead == "CatalogType") {
            switch(ordinal) {
                case 0:
                    return "int";
                case 1:
                    return "nvarchar";
            }
        }
        throw new NotImplementedException();
    }

    public override DateTime GetDateTime(int ordinal) {
        throw new NotImplementedException();
    }

    public override decimal GetDecimal(int ordinal) {
        throw new NotImplementedException();
    }

    public override double GetDouble(int ordinal) {
        throw new NotImplementedException();
    }

    public override IEnumerator<int> GetEnumerator() {
        throw new NotImplementedException();
    }

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        //if (_tableRead == "CatalogBrand") {
        //    switch (ordinal) {
        //        case 0:
        //            return typeof(int);
        //        case 1:
        //            return typeof(string);
        //        default:
        //            throw new InvalidOperationException($"Invalid ordinal {ordinal}");
        //    }
        //}

        var schemaTable = _dataReader.GetSchemaTable();
        var row = schemaTable.Rows[ordinal];
        var columnType = (Type)row["DataType"];
        return columnType;

        throw new InvalidOperationException($"Invalid ordinal {ordinal}");
    }

    public override float GetFloat(int ordinal) {
        throw new NotImplementedException();
    }

    public override Guid GetGuid(int ordinal) {
        throw new NotImplementedException();
    }

    public override short GetInt16(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToInt16(value);
    }

    public override int GetInt32(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToInt32(value);
    }

    public override long GetInt64(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToInt64(value);
    }

    public override string GetName(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        if (_tableRead == "CatalogBrand") {
            switch (ordinal) {
                case 0:
                    return "Id";
                case 1:
                    return "Brand";
                default:
                    throw new InvalidOperationException($"Invalid ordinal {ordinal}");
            }
        }

        throw new NotImplementedException();
    }

    public override int GetOrdinal(string name) {
        throw new NotImplementedException();
    }

    public override string GetString(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _data[_rowIndex][ordinal];
        return Convert.ToString(value);
    }

    public override int GetValues(object[] values) {
        throw new NotImplementedException();
    }

    public override bool IsDBNull(int ordinal) {
        throw new NotImplementedException();
    }

    public override bool NextResult() {
        _hasNextResult = false; // Assume no next result

        // Check if there's another result set
        // (in this example, we assume there's only one result set)
        if (_rowIndex >= _data.Count - 1) {
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