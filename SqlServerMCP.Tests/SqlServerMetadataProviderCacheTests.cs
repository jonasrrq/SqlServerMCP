using FluentAssertions;
using SqlServerMCP;
using System.Collections;
using System.Data;
using System.Data.Common;
using Xunit;

namespace SqlServerMCP.Tests;

public class SqlServerMetadataProviderCacheTests
{
    [Fact]
    public async Task GetTablesAsync_UsesCache_WithinTtl()
    {
        var calls = 0;
        var provider = new SqlServerMetadataProvider(() =>
        {
            calls++;
            return new FakeDbConnection(new[] { "dbo.Table1" });
        }, metadataCacheTtlSeconds: 60);

        var first = await provider.GetTablesAsync();
        var second = await provider.GetTablesAsync();

        first.Should().ContainSingle(x => x == "dbo.Table1");
        second.Should().ContainSingle(x => x == "dbo.Table1");
        calls.Should().Be(1);
    }

    private sealed class FakeDbConnection(string[] values) : DbConnection
    {
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "FakeDb";
        public override string DataSource => "FakeSource";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => ConnectionState.Open;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        public override Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
            => throw new NotImplementedException();

        protected override DbCommand CreateDbCommand() => new FakeDbCommand(values);
    }

    private sealed class FakeDbCommand(string[] values) : DbCommand
    {
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection DbConnection { get; set; } = null!;
        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();
        protected override DbTransaction DbTransaction { get; set; } = null!;

        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object ExecuteScalar() => 0;
        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new FakeDbDataReader(values);
    }

    private sealed class FakeDbDataReader(string[] values) : DbDataReader
    {
        private int _index = -1;

        public override int FieldCount => 1;
        public override bool HasRows => values.Length > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;

        public override object this[int ordinal] => values[_index];
        public override object this[string name] => values[_index];

        public override bool Read() => ++_index < values.Length;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override bool NextResult() => false;
        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public override string GetString(int ordinal) => values[_index];
        public override string GetName(int ordinal) => "Name";
        public override object GetValue(int ordinal) => values[_index];
        public override bool IsDBNull(int ordinal) => false;
        public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => Task.FromResult(false);
        public override int GetValues(object?[] valuesArray)
        {
            valuesArray[0] = values[_index];
            return 1;
        }

        public override System.Collections.IEnumerator GetEnumerator() => values.GetEnumerator();
        public override DataTable GetSchemaTable() => throw new NotImplementedException();
        public override bool GetBoolean(int ordinal) => throw new NotImplementedException();
        public override byte GetByte(int ordinal) => throw new NotImplementedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();
        public override char GetChar(int ordinal) => throw new NotImplementedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();
        public override string GetDataTypeName(int ordinal) => "nvarchar";
        public override DateTime GetDateTime(int ordinal) => throw new NotImplementedException();
        public override decimal GetDecimal(int ordinal) => throw new NotImplementedException();
        public override double GetDouble(int ordinal) => throw new NotImplementedException();
        public override Type GetFieldType(int ordinal) => typeof(string);
        public override float GetFloat(int ordinal) => throw new NotImplementedException();
        public override Guid GetGuid(int ordinal) => throw new NotImplementedException();
        public override short GetInt16(int ordinal) => throw new NotImplementedException();
        public override int GetInt32(int ordinal) => throw new NotImplementedException();
        public override long GetInt64(int ordinal) => throw new NotImplementedException();
        public override int GetOrdinal(string name) => 0;
    }

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        public override int Count => 0;
        public override object SyncRoot => this;
        public override int Add(object? value) => 0;
        public override void AddRange(Array values) { }
        public override void Clear() { }
        public override bool Contains(string value) => false;
        public override bool Contains(object? value) => false;
        public override void CopyTo(Array array, int index) { }
        public override IEnumerator GetEnumerator() => Array.Empty<object>().GetEnumerator();
        public override int IndexOf(string parameterName) => -1;
        public override int IndexOf(object? value) => -1;
        public override void Insert(int index, object value) { }
        public override void Remove(object? value) { }
        public override void RemoveAt(string parameterName) { }
        public override void RemoveAt(int index) { }
        protected override DbParameter GetParameter(string parameterName) => throw new NotImplementedException();
        protected override DbParameter GetParameter(int index) => throw new NotImplementedException();
        protected override void SetParameter(string parameterName, DbParameter value) { }
        protected override void SetParameter(int index, DbParameter value) { }
        public override bool IsFixedSize => false;
        public override bool IsReadOnly => false;
        public override bool IsSynchronized => false;
    }
}
