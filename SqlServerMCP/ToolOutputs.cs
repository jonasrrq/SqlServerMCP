namespace SqlServerMCP;

public class MetadataResult
{
    public IEnumerable<string> Tables { get; init; } = Array.Empty<string>();
    public IEnumerable<string> Views { get; init; } = Array.Empty<string>();
    public IEnumerable<string> Procedures { get; init; } = Array.Empty<string>();
    public IEnumerable<ForeignKeyInfo> ForeignKeys { get; init; } = Array.Empty<ForeignKeyInfo>();
}

public class QueryResult
{
    public string[] Columns { get; init; } = Array.Empty<string>();
    public List<Dictionary<string, object?>> Rows { get; init; } = new();
    public int RowCount => Rows?.Count ?? 0;
    public PaginationResult? Pagination { get; init; }
}

public class PaginationResult
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalAvailable { get; init; }
    public int Returned { get; init; }
}

public class StoredProcedureResult
{
    public List<Dictionary<string, object?>> Rows { get; init; } = new();
}

public class ColumnsResult
{
    public IEnumerable<ColumnInfo> Columns { get; init; } = Array.Empty<ColumnInfo>();
}

public class ClearCacheResult
{
    public bool Cleared { get; init; }
}

public class CacheStatusItem
{
    public string Key { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
}
