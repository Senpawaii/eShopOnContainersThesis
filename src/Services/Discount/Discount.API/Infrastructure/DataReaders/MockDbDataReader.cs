using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.eShopOnContainers.Services.Discount.API.Infrastructure.DataReaders;
public class MockDbDataReader : DbDataReader {
    private readonly List<object[]> _rows;
    private readonly int _recordsAffected;
    private readonly string _targetTable;
    private readonly bool _executedOnDatabase = false;
    private int _rowIndex = -1;
    private bool _hasNextResult = false;
    private readonly bool _hasRows;


    public MockDbDataReader(List<object[]> rows, int recordsAffected, string targetTable) {
        _rows = rows;
        _recordsAffected = recordsAffected;
        _targetTable = targetTable;
        _hasRows = _rows != null && _rows.Count > 0;

    }

    public override bool Read() {
        _rowIndex++;
        return (_rowIndex < _rows.Count);
    }

    public bool ExecutedRequestOnDatabase => _executedOnDatabase;

    public override int FieldCount => _rows[0].Length;

    public override int Depth => throw new NotImplementedException();

    public override bool HasRows => _hasRows;

    public override bool IsClosed => throw new NotImplementedException();

    public override int RecordsAffected => _recordsAffected;

    public override object this[string name] => throw new NotImplementedException();

    public override object this[int ordinal] => throw new NotImplementedException();

    public override bool IsDBNull(int ordinal) {
        return (_rows[_rowIndex][ordinal] == DBNull.Value);
    }

    public override object GetValue(int ordinal) {
        return _rows[_rowIndex];
    }

    public override bool GetBoolean(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _rows[_rowIndex][ordinal];
        return Convert.ToBoolean(value);
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

    public override DateTime GetDateTime(int ordinal) {
        throw new NotImplementedException();
    }

    public override decimal GetDecimal(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }
        var value = _rows[_rowIndex][ordinal];
        return Convert.ToDecimal(value);
    }

    public override double GetDouble(int ordinal) {
        throw new NotImplementedException();
    }

    public override IEnumerator GetEnumerator() {
        throw new NotImplementedException();
    }

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new IndexOutOfRangeException($"Invalid column index {ordinal}. There are {FieldCount} columns in the result set.");
        }

        if(_targetTable == "Discount") {
            switch (ordinal) {
                case 0:
                    return typeof(int);
                case 1:
                    return typeof(string);
                case 2:
                    return typeof(string);
                case 3:
                    return typeof(string);
                case 4:
                    return typeof(int);
            }
        }

        throw new NotImplementedException();
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

        var value = _rows[_rowIndex][ordinal];
        return Convert.ToInt16(value);
    }

    public override int GetInt32(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _rows[_rowIndex][ordinal];
        return Convert.ToInt32(value);
    }

    public override long GetInt64(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new ArgumentOutOfRangeException(nameof(ordinal));
        }

        var value = _rows[_rowIndex][ordinal];
        return Convert.ToInt64(value);
    }

    public override string GetName(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new IndexOutOfRangeException($"Invalid column index {ordinal}. There are {FieldCount} columns in the result set.");
        }

        if (_targetTable == "Discount") {
            switch (ordinal) {
                case 0:
                    return "Id";
                case 1:
                    return "ItemName";
                case 2:
                    return "ItemBrand";
                case 3:
                    return "ItemType";
                case 4:
                    return "DiscountValue";
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

        var value = _rows[_rowIndex][ordinal];
        return Convert.ToString(value);
    }

    public override int GetValues(object[] values) {
        throw new NotImplementedException();
    }

    public override bool NextResult() {
        _hasNextResult = false; // Assume no next result

        // Check if there's another result set
        // (in this example, we assume there's only one result set)
        if (_rowIndex >= _rows.Count - 1) {
            return false;
        }

        _rowIndex = -1; // Reset row index for new result set
        _hasNextResult = true;
        return true;
    }
}
