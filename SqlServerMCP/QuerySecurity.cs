using System.Text.RegularExpressions;

namespace SqlServerMCP;

public static partial class QuerySecurity
{
    public static void ValidateReadOnlyQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new InvalidOperationException("La consulta no puede estar vacía.");

        var normalized = query.Trim();

        if (normalized.Contains(';'))
            throw new InvalidOperationException("No se permiten múltiples sentencias SQL.");

        if (!StartsWithReadOnlyKeyword(normalized))
            throw new InvalidOperationException("Solo se permiten consultas de solo lectura (SELECT o WITH). ");

        if (DangerousKeywordRegex().IsMatch(normalized))
            throw new InvalidOperationException("La consulta contiene palabras clave no permitidas.");
    }

    private static bool StartsWithReadOnlyKeyword(string normalized)
        => normalized.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\b(ALTER|DROP|TRUNCATE|DELETE|INSERT|UPDATE|MERGE|CREATE|EXEC|EXECUTE|GRANT|REVOKE|DENY)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousKeywordRegex();
}