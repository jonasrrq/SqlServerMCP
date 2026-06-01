using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SqlServerMCP;

public sealed class InMemoryAuditLogger : IAuditLogger
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private readonly int _maxEntries;

    public InMemoryAuditLogger(int maxEntries = 1000)
    {
        _maxEntries = Math.Max(10, maxEntries);
    }

    public void LogQuery(string user, string query, int maxRows, int timeoutSeconds, bool success, string? errorCode = null)
    {
        var sanitized = SanitizeQuery(query);
        var entry = new AuditEntry(DateTimeOffset.UtcNow, "ExecuteQuery", user ?? "-", new Dictionary<string, object?> {
            { "query", sanitized }, { "maxRows", maxRows }, { "timeoutSeconds", timeoutSeconds }
        }, success, errorCode);
        Enqueue(entry);
    }

    public void LogToolCall(string user, string toolName, object? parameters, bool success, string? errorCode = null)
    {
        var entry = new AuditEntry(DateTimeOffset.UtcNow, toolName, user ?? "-", new Dictionary<string, object?> { { "params", parameters } }, success, errorCode);
        Enqueue(entry);
    }

    private void Enqueue(AuditEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > _maxEntries && _entries.TryDequeue(out _)) { }
    }

    private static string SanitizeQuery(string query)
    {
        if (string.IsNullOrEmpty(query)) return string.Empty;
        // Replace string literals with ***
        var noStrings = Regex.Replace(query, "'(?:[^']|'')*'", "'***'");
        // Collapse whitespace
        return Regex.Replace(noStrings, "\\s+", " ").Trim();
    }

    public AuditEntry[] GetEntries() => _entries.ToArray();

    public void Clear() { while (_entries.TryDequeue(out _)) { } }
}

public sealed record AuditEntry(DateTimeOffset Timestamp, string Tool, string User, Dictionary<string, object?> Parameters, bool Success, string? ErrorCode)
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
}
