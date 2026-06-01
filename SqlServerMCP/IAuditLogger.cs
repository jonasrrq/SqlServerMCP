namespace SqlServerMCP;

public interface IAuditLogger
{
    void LogQuery(string user, string query, int maxRows, int timeoutSeconds, bool success, string? errorCode = null);
    void LogToolCall(string user, string toolName, object? parameters, bool success, string? errorCode = null);
}
