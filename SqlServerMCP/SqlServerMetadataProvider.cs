using System.Data;
using System.Data.Common;

namespace SqlServerMCP;

public class SqlServerMetadataProvider : IMetadataProvider, IMetadataCache
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly int _defaultTimeoutSeconds;
    private readonly int _defaultMaxRows;
    private readonly TimeSpan _metadataCacheTtl;
    private readonly Dictionary<string, CacheEntry> _metadataCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _cacheLock = new();

    public SqlServerMetadataProvider(
        Func<DbConnection> connectionFactory,
        int defaultTimeoutSeconds = 30,
        int defaultMaxRows = 1000,
        int metadataCacheTtlSeconds = 60)
    {
        _connectionFactory = connectionFactory;
        _defaultTimeoutSeconds = defaultTimeoutSeconds > 0 ? defaultTimeoutSeconds : 30;
        _defaultMaxRows = defaultMaxRows > 0 ? defaultMaxRows : 1000;
        _metadataCacheTtl = TimeSpan.FromSeconds(metadataCacheTtlSeconds > 0 ? metadataCacheTtlSeconds : 60);
    }

    private async Task<(DbConnection Conn, bool ShouldDispose)> GetOpenConnectionAsync()
    {
        DbConnection conn;
        try
        {
            conn = _connectionFactory() ?? throw new InvalidOperationException("La fábrica de conexión devolvió null.");
        }
        catch (Exception ex)
        {
            // Best-effort audit log for connection creation failure
            try { (ServiceProviderAccessor.Current?.GetService(typeof(IAuditLogger)) as IAuditLogger)?.LogToolCall("-", "GetOpenConnectionAsync", ex.Message, false, "DB-CONN-ERR"); } catch { }
            throw new InvalidOperationException("No se pudo crear la conexión de base de datos.", ex);
        }

        bool shouldDispose = conn.State == ConnectionState.Closed || conn.State == ConnectionState.Broken;
        if (shouldDispose)
        {
            try
            {
                await conn.OpenAsync();
            }
            catch (Exception ex)
            {
                // Best-effort audit log for open failure
                try { (ServiceProviderAccessor.Current?.GetService(typeof(IAuditLogger)) as IAuditLogger)?.LogToolCall("-", "GetOpenConnectionAsync", ex.Message, false, "DB-OPEN-ERR"); } catch { }
                throw new InvalidOperationException("No se pudo abrir la conexión a la base de datos.", ex);
            }
        }

        return (conn, shouldDispose);
    }

    public async Task<IEnumerable<string>> GetTablesAsync()
    {
        if (TryGetCachedValue("metadata:tables", out IEnumerable<string>? cachedTables))
            return cachedTables!;

        var tables = new List<string>();
        var (conn, shouldDispose) = await GetOpenConnectionAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = _defaultTimeoutSeconds;
            cmd.CommandText = "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }
        finally
        {
            if (shouldDispose) conn.Dispose();
        }

        SetCachedValue("metadata:tables", tables);
        return tables;
    }

    public async Task<IEnumerable<string>> GetViewsAsync()
    {
        if (TryGetCachedValue("metadata:views", out IEnumerable<string>? cachedViews))
            return cachedViews!;

        var views = new List<string>();
        var (conn, shouldDispose) = await GetOpenConnectionAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = _defaultTimeoutSeconds;
            cmd.CommandText = "SELECT TABLE_SCHEMA + '.' + TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                views.Add(reader.GetString(0));
        }
        finally
        {
            if (shouldDispose) conn.Dispose();
        }

        SetCachedValue("metadata:views", views);
        return views;
    }

    public async Task<IEnumerable<string>> GetStoredProceduresAsync()
    {
        if (TryGetCachedValue("metadata:procedures", out IEnumerable<string>? cachedProcedures))
            return cachedProcedures!;

        var procs = new List<string>();
        var (conn, shouldDispose) = await GetOpenConnectionAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = _defaultTimeoutSeconds;
            cmd.CommandText = "SELECT SPECIFIC_SCHEMA + '.' + SPECIFIC_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_TYPE = 'PROCEDURE'";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                procs.Add(reader.GetString(0));
        }
        finally
        {
            if (shouldDispose) conn.Dispose();
        }

        SetCachedValue("metadata:procedures", procs);
        return procs;
    }

    public async Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync()
    {
        if (TryGetCachedValue("metadata:foreignkeys", out IEnumerable<ForeignKeyInfo>? cachedFks))
            return cachedFks!;

        var fks = new List<ForeignKeyInfo>();
        var (conn, shouldDispose) = await GetOpenConnectionAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = _defaultTimeoutSeconds;
            cmd.CommandText = @"SELECT 
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
        INNER JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id";
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
        }
        finally
        {
            if (shouldDispose) conn.Dispose();
        }

        SetCachedValue("metadata:foreignkeys", fks);
        return fks;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string query)
        => await ExecuteQueryAsync(query, _defaultMaxRows, _defaultTimeoutSeconds, CancellationToken.None);

    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string query, int maxRows, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        var results = new List<Dictionary<string, object?>>();
        var (conn, shouldDispose) = await GetOpenConnectionAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = query;
            cmd.CommandTimeout = timeoutSeconds > 0 ? timeoutSeconds : _defaultTimeoutSeconds;

            var effectiveMaxRows = maxRows > 0 ? maxRows : _defaultMaxRows;

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (results.Count < effectiveMaxRows && await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i) ?? $"col{i}";
                    row[colName] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
                }
                results.Add(row);
            }
        }
        finally
        {
            if (shouldDispose) conn.Dispose();
        }
        return results;
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteStoredProcedureAsync(string procedureName, Dictionary<string, object>? parameters)
    {
        var results = new List<Dictionary<string, object?>>();
        var (conn, shouldDispose) = await GetOpenConnectionAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = procedureName;
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandTimeout = _defaultTimeoutSeconds;
            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = kv.Key;
                    p.Value = kv.Value ?? DBNull.Value;
                    cmd.Parameters.Add(p);
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
        }
        finally
        {
            if (shouldDispose) conn.Dispose();
        }
        return results;
    }

    public async Task<IEnumerable<ColumnInfo>> GetColumnsAsync(string tableOrView)
    {
        var cacheKey = $"metadata:columns:{tableOrView.Trim()}";
        if (TryGetCachedValue(cacheKey, out IEnumerable<ColumnInfo>? cachedColumns))
            return cachedColumns!;

        var columns = new List<ColumnInfo>();
        string schema = "dbo";
        string name = tableOrView;
        if (tableOrView.Contains('.'))
        {
            var parts = tableOrView.Split('.', 2);
            schema = parts[0];
            name = parts[1];
        }

        var (conn, shouldDispose) = await GetOpenConnectionAsync();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandTimeout = _defaultTimeoutSeconds;

            if (name.StartsWith("#"))
            {
                // Las temp tables locales se registran en tempdb y su nombre real tiene un sufijo.
                cmd.CommandText = @"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
            FROM tempdb.INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_NAME LIKE @name";
                var pName = cmd.CreateParameter();
                pName.ParameterName = "@name";
                pName.Value = name + "%";
                cmd.Parameters.Add(pName);

                // Si el usuario pasó esquema explícito (p. ej. "dbo.#TestTable"), podemos filtrar por esquema.
                if (!string.IsNullOrEmpty(schema))
                {
                    cmd.CommandText += " AND TABLE_SCHEMA = @schema";
                    var pSchema = cmd.CreateParameter();
                    pSchema.ParameterName = "@schema";
                    pSchema.Value = schema;
                    cmd.Parameters.Add(pSchema);
                }
            }
            else
            {
                cmd.CommandText = @"SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @name";
                var p1 = cmd.CreateParameter();
                p1.ParameterName = "@schema";
                p1.Value = schema;
                cmd.Parameters.Add(p1);
                var p2 = cmd.CreateParameter();
                p2.ParameterName = "@name";
                p2.Value = name;
                cmd.Parameters.Add(p2);
            }

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

            SetCachedValue(cacheKey, columns);
            return columns;
        }
        finally
        {
            if (shouldDispose) conn.Dispose();
        }
    }

    private bool TryGetCachedValue<T>(string key, out T? value)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_cacheLock)
        {
            if (_metadataCache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAt > now && entry.Value is T typed)
                {
                    value = typed;
                    return true;
                }

                _metadataCache.Remove(key);
            }
        }

        value = default;
        return false;
    }

    private void SetCachedValue<T>(string key, T value)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(_metadataCacheTtl);
        var cacheEntry = new CacheEntry(expiresAt, value!);

        lock (_cacheLock)
        {
            _metadataCache[key] = cacheEntry;
        }
    }

    private sealed record CacheEntry(DateTimeOffset ExpiresAt, object Value);

    // IMetadataCache implementation
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _metadataCache.Clear();
        }
    }

    public Dictionary<string, DateTimeOffset> GetCacheStatus()
    {
        lock (_cacheLock)
        {
            return _metadataCache.ToDictionary(kv => kv.Key, kv => kv.Value.ExpiresAt);
        }
    }
}