using Microsoft.Data.SqlClient;

namespace SqlServerMCP;

public class SqlServerMetadataProvider : IMetadataProvider
{
    private readonly string _connectionString;

    public SqlServerMetadataProvider(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IEnumerable<string>> GetTablesAsync()
    {
        var tables = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        return tables;
    }

    public async Task<IEnumerable<string>> GetViewsAsync()
    {
        var views = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            views.Add(reader.GetString(0));
        return views;
    }

    public async Task<IEnumerable<string>> GetStoredProceduresAsync()
    {
        var procs = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand("SELECT SPECIFIC_SCHEMA + '.' + SPECIFIC_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'PROCEDURE'", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            procs.Add(reader.GetString(0));
        return procs;
    }

    public async Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync()
    {
        var fks = new List<ForeignKeyInfo>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"SELECT 
            fk.name AS ForeignKey,
            tp.name AS TableName,
            cp.name AS ColumnName,
            tr.name AS ReferencedTable,
            cr.name AS ReferencedColumn
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        INNER JOIN sys.tables tp ON fkc.parent_object_id = tp.object_id
        INNER JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id
        INNER JOIN sys.tables tr ON fkc.referenced_object_id = tr.object_id
        INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            fks.Add(new ForeignKeyInfo
            {
                ForeignKey = reader.GetString(0),
                Table = reader.GetString(1),
                Column = reader.GetString(2),
                ReferencedTable = reader.GetString(3),
                ReferencedColumn = reader.GetString(4)
            });
        }
        return fks;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string query)
    {
        var results = new List<Dictionary<string, object?>>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(query, conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i) ?? $"col{i}";
                row[colName] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }
        return results;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteStoredProcedureAsync(string procedureName, Dictionary<string, object>? parameters)
    {
        var results = new List<Dictionary<string, object?>>();
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(procedureName, conn) { CommandType = System.Data.CommandType.StoredProcedure };
        if (parameters != null)
        {
            foreach (var kv in parameters)
            {
                cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
            }
        }
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i) ?? $"col{i}";
                row[colName] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }
        return results;
    }

    public async Task<IEnumerable<ColumnInfo>> GetColumnsAsync(string tableOrView)
    {
        var columns = new List<ColumnInfo>();
        string schema = "dbo";
        string name = tableOrView;
        if (tableOrView.Contains('.'))
        {
            var parts = tableOrView.Split('.', 2);
            schema = parts[0];
            name = parts[1];
        }
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = new SqlCommand(@"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @name", conn);
        cmd.Parameters.AddWithValue("@schema", schema);
        cmd.Parameters.AddWithValue("@name", name);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = await reader.IsDBNullAsync(3) ? null : reader.GetInt32(3)
            });
        }
        return columns;
    }
}
