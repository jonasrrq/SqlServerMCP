using FluentAssertions;
using SqlServerMCP;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SqlServerMCP.Tests
{
    public class CacheAdminTests
    {
        [Fact]
        public async Task ClearCache_RemovesEntries()
        {
            var provider = new SqlServerMetadataProvider(() => new FakeDbConnection(new[] { "dbo.Table1" }), metadataCacheTtlSeconds: 60);

            var tables = await provider.GetTablesAsync();
            tables.Should().ContainSingle("dbo.Table1");

            var statusBefore = provider.GetCacheStatus();
            statusBefore.Should().ContainKey("metadata:tables");

            provider.ClearCache();

            var statusAfter = provider.GetCacheStatus();
            statusAfter.Should().NotContainKey("metadata:tables");
        }
    }

    // Minimal fakes for this test file
    internal sealed class FakeDbConnection : System.Data.Common.DbConnection
    {
        private readonly string[] _values;
        public FakeDbConnection(string[] values) => _values = values;
        public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "FakeDb";
        public override string DataSource => "FakeSource";
        public override string ServerVersion => "1.0";
        public override System.Data.ConnectionState State => System.Data.ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        public override Task OpenAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected override System.Data.Common.DbTransaction BeginDbTransaction(System.Data.IsolationLevel isolationLevel) => throw new NotImplementedException();
        protected override System.Data.Common.DbCommand CreateDbCommand() => new FakeDbCommand(_values);
    }

    internal sealed class FakeDbCommand : System.Data.Common.DbCommand
    {
        private readonly string[] _values;
        public FakeDbCommand(string[] values) => _values = values;
        public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override System.Data.CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override System.Data.UpdateRowSource UpdatedRowSource { get; set; }
        protected override System.Data.Common.DbConnection DbConnection { get; set; }
        protected override System.Data.Common.DbParameterCollection DbParameterCollection { get; } = new SqlServerMCP.Tests.SqlParameterCollectionMock();
        protected override System.Data.Common.DbTransaction DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery() => 0;
        public override object ExecuteScalar() => 0;
        public override void Prepare() { }
        protected override System.Data.Common.DbParameter CreateDbParameter() => throw new NotImplementedException();
        protected override System.Data.Common.DbDataReader ExecuteDbDataReader(System.Data.CommandBehavior behavior) => new FakeDbDataReader(_values);
    }

    internal sealed class FakeDbDataReader : System.Data.Common.DbDataReader
    {
        private int _i = -1;
        private readonly string[] _values;
        public FakeDbDataReader(string[] values) => _values = values;
        public override int FieldCount => 1;
        public override bool HasRows => _values.Length > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override object this[int ordinal] => _values[_i];
        public override object this[string name] => _values[_i];
        public override bool Read() => ++_i < _values.Length;
        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());
        public override bool NextResult() => false;
        public override Task<bool> NextResultAsync(CancellationToken cancellationToken) => Task.FromResult(false);
        public override string GetString(int ordinal) => _values[_i];
        public override string GetName(int ordinal) => "Name";
        public override object GetValue(int ordinal) => _values[_i];
        public override bool IsDBNull(int ordinal) => false;
        public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) => Task.FromResult(false);
        public override int GetValues(object?[] valuesArray) { valuesArray[0] = _values[_i]; return 1; }
        public override System.Collections.IEnumerator GetEnumerator() => _values.GetEnumerator();
        public override System.Data.DataTable GetSchemaTable() => throw new NotImplementedException();
        public override bool GetBoolean(int ordinal) => throw new NotImplementedException();
        public override byte GetByte(int ordinal) => throw new NotImplementedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();
        public override char GetChar(int ordinal) => throw new NotImplementedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotImplementedException();
        public override string GetDataTypeName(int ordinal) => "nvarchar";
        public override System.DateTime GetDateTime(int ordinal) => throw new NotImplementedException();
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
}
