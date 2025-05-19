namespace SqlServerMCP;

public interface IMetadataProvider
{
    Task<IEnumerable<string>> GetTablesAsync();
    Task<IEnumerable<string>> GetViewsAsync();
    Task<IEnumerable<string>> GetStoredProceduresAsync();
    Task<IEnumerable<ForeignKeyInfo>> GetForeignKeysAsync();
    Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string query);
    Task<List<Dictionary<string, object?>>> ExecuteStoredProcedureAsync(string procedureName, Dictionary<string, object>? parameters);
}

public class ForeignKeyInfo
{
    public string Table { get; set; } = string.Empty;
    public string ForeignKey { get; set; } = string.Empty;
    public string ReferencedTable { get; set; } = string.Empty;
    public string Column { get; set; } = string.Empty;
    public string ReferencedColumn { get; set; } = string.Empty;
}
