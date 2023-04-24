using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.eShopOnContainers.Services.Catalog.API.Infrastructure.DataReaders;
public class MockDbDataReader : DbDataReader {
    private readonly List<object[]> _rows;
    private int _currentRow;
    private readonly int _recordsAffected;
    private readonly string _targetTable;
    private readonly bool _executedOnDatabase = false;

    public MockDbDataReader(List<object[]> rows, int recordsAffected, string targetTable) {
        _rows = rows;
        _currentRow = -1;
        _recordsAffected = recordsAffected;
        _targetTable = targetTable;
    }

    public override bool Read() {
        _currentRow++;
        return (_currentRow < _rows.Count);
    }

    public bool ExecutedRequestOnDatabase => _executedOnDatabase;

    public override int FieldCount => _rows[0].Length;

    public override int Depth => throw new NotImplementedException();

    public override bool HasRows => throw new NotImplementedException();

    public override bool IsClosed => throw new NotImplementedException();

    public override int RecordsAffected => _recordsAffected;

    public override object this[string name] => throw new NotImplementedException();

    public override object this[int ordinal] => throw new NotImplementedException();

    public override bool IsDBNull(int ordinal) {
        return (_rows[_currentRow][ordinal] == null);
    }

    public override object GetValue(int ordinal) {
        return _rows[_currentRow][ordinal];
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

    public override IEnumerator GetEnumerator() {
        throw new NotImplementedException();
    }

    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal) {
        if (ordinal < 0 || ordinal >= FieldCount) {
            throw new IndexOutOfRangeException($"Invalid column index {ordinal}. There are {FieldCount} columns in the result set.");
        }

        if(_targetTable == "CatalogType") {
            switch (ordinal) {
                case 0:
                    return typeof(int);
                case 1:
                    return typeof(string);
            }
        }

        if(_targetTable == "CatalogBrand") {
            switch (ordinal) {
                case 0:
                    return typeof(int);
                case 1:
                    return typeof(string);
            }
        }

        if(_targetTable == "Catalog") {
            switch (ordinal) {
                case 0:
                    return typeof(int);
                case 1:
                    return typeof(int);
                case 2:
                    return typeof(int);
                case 3:
                    return typeof(string);
                case 4:
                    return typeof(string);
                case 5:
                    return typeof(string);
                case 6:
                    return typeof(decimal);
                case 7:
                    return typeof(int);
                case 8:
                    return typeof(int);
                case 9:
                    return typeof(bool);
                case 10:
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
        throw new NotImplementedException();
    }

    public override int GetInt32(int ordinal) {
        throw new NotImplementedException();
    }

    public override long GetInt64(int ordinal) {
        throw new NotImplementedException();
    }

    public override string GetName(int ordinal) {
        throw new NotImplementedException();
    }

    public override int GetOrdinal(string name) {
        throw new NotImplementedException();
    }

    public override string GetString(int ordinal) {
        throw new NotImplementedException();
    }

    public override int GetValues(object[] values) {
        throw new NotImplementedException();
    }

    public override bool NextResult() {
        throw new NotImplementedException();
    }
}
