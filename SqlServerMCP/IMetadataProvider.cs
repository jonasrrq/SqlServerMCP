namespace SqlServerMCP;

public interface IMetadataProvider
{
    Task<IEnumerable<string>> GetTablesAsync();
    Task<IEnumerable<string>> GetViewsAsync();
    Task<IEnumerable<string>> GetStoredProceduresAsync();
    Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync();
    Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string query);
    Task<List<Dictionary<string, object?>>> ExecuteStoredProcedureAsync(string procedureName, Dictionary<string, object>? parameters);
    Task<IEnumerable<ColumnInfo>> GetColumnsAsync(string tableOrView);
}

public class ForeignKeyInfo
{
    public string Table { get; set; } = string.Empty;
    public string ForeignKey { get; set; } = string.Empty;
    public string ReferencedTable { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string ReferencedColumn { get; set; } = string.Empty;
}

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
}
