namespace SqlServerMCP;

/// <summary>
/// Database engines supported by the server. SQL Server is the only implementation today;
/// this enum is the extension point for future engines.
/// </summary>
public enum DatabaseEngine
{
    SqlServer
}

/// <summary>
/// Resolves the configured database engine from CLI or environment configuration.
/// </summary>
public static class DatabaseEngineSelector
{
    public const string EnvironmentVariable = "MCP_DB_PROVIDER";
    public const string CliOption = "--db-provider";
    public const string DefaultValue = "sqlserver";

    public static DatabaseEngine Parse(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? DefaultValue : value.Trim();

        return normalized.ToLowerInvariant() switch
        {
            "sqlserver" => DatabaseEngine.SqlServer,
            _ => throw new InvalidOperationException(
                $"Unsupported database provider '{value}'. Supported values: {DefaultValue} " +
                $"({EnvironmentVariable} / {CliOption}).")
        };
    }
}
