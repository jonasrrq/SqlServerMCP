using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SqlServerMCP;

[McpServerToolType]
public static class MetadataTool
{
    private static readonly ToolRateLimiter QueryRateLimiter = CreateQueryRateLimiter();

    [McpServerTool, Description("Obtiene metadatos de SQL Server: tablas, vistas, procedimientos y relaciones.")]
    public static async Task<object> GetMetadata(IMetadataProvider provider)
    {
        try
        {
            var tablesTask = provider.GetTablesAsync();
            var viewsTask = provider.GetViewsAsync();
            var procsTask = provider.GetStoredProceduresAsync();
            var fksTask = provider.GetForeignKeysAsync();

            await Task.WhenAll(tablesTask, viewsTask, procsTask, fksTask);

            var tables = await tablesTask;
            var views = await viewsTask;
            var procs = await procsTask;
            var fks = await fksTask;

            return new { tables, views, procedures = procs, foreignKeys = fks };
        }
        catch (Exception)
        {
            return CreateSafeError("MCP-METADATA-001", "No fue posible obtener metadatos.");
        }
    }

    [McpServerTool, Description("Ejecuta una consulta SQL de solo lectura y devuelve los resultados con límites de seguridad.")]
    public static async Task<object> ExecuteQuery(
        IMetadataProvider provider,
        [Description("Consulta SQL a ejecutar (solo SELECT/CTE)")] string query,
        [Description("Límite máximo de filas a devolver (1-10000)")] int maxRows = 1000,
        [Description("Timeout en segundos para la consulta")] int timeoutSeconds = 30)
    {
        try
        {
            if (!QueryRateLimiter.TryAcquire("ExecuteQuery", out var retryAfterSeconds))
            {
                return CreateSafeError("MCP-RATE-001", $"Límite de consultas excedido. Reintenta en {retryAfterSeconds}s.");
            }

            QuerySecurity.ValidateReadOnlyQuery(query);

            // Attempt to log the query (best-effort)
            IAuditLogger? auditLogger = null;
            try { auditLogger = (IAuditLogger?)ServiceProviderAccessor.Current?.GetService(typeof(IAuditLogger)); } catch { }

            try
            {
                if (provider is SqlServerMetadataProvider sqlProvider)
                {
                    var result = await sqlProvider.ExecuteQueryAsync(query, maxRows, timeoutSeconds);
                    auditLogger?.LogQuery("-", query, maxRows, timeoutSeconds, true, null);
                    return result;
                }

                var res = await provider.ExecuteQueryAsync(query);
                auditLogger?.LogQuery("-", query, maxRows, timeoutSeconds, true, null);
                return res;
            }
            catch (Exception ex)
            {
                auditLogger?.LogQuery("-", query, maxRows, timeoutSeconds, false, "MCP-QUERY-ERR");
                throw;
            }
        }
        catch (Exception)
        {
            return CreateSafeError("MCP-QUERY-001", "No fue posible ejecutar la consulta.");
        }
    }

    [McpServerTool, Description("Ejecuta un procedimiento almacenado con parámetros opcionales y devuelve los resultados.")]
    public static async Task<object> ExecuteStoredProcedure(
        IMetadataProvider provider,
        [Description("Nombre del procedimiento almacenado")] string procedureName,
        [Description("Parámetros del procedimiento (clave:valor)")] Dictionary<string, object>? parameters = null)
    {
        try
        {
            if (provider is SqlServerMetadataProvider sqlProvider)
            {
                return await sqlProvider.ExecuteStoredProcedureAsync(procedureName, parameters ?? new());
            }
            throw new InvalidOperationException("El proveedor no soporta ejecución de procedimientos almacenados.");
        }
        catch (Exception)
        {
            return CreateSafeError("MCP-PROC-001", "No fue posible ejecutar el procedimiento almacenado.");
        }
    }

    [McpServerTool, Description("Obtiene las columnas de una tabla o vista de SQL Server.")]
    public static async Task<object> GetColumns(
        IMetadataProvider provider,
        [Description("Nombre de la tabla o vista (puede incluir esquema, ej: dbo.Empleados)")] string tableOrView)
    {
        try
        {
            var columns = await provider.GetColumnsAsync(tableOrView);
            return columns;
        }
        catch (Exception)
        {
            return CreateSafeError("MCP-COLUMNS-001", "No fue posible obtener las columnas.");
        }
    }

    private static object CreateSafeError(string code, string message)
        => new
        {
            error = true,
            errorCode = code,
            correlationId = Guid.NewGuid().ToString("N"),
            message
        };

    [McpServerTool, Description("Limpia la caché de metadatos en el proveedor SQL (solo para SqlServerMetadataProvider).")]
    public static object ClearMetadataCache(IMetadataProvider provider)
    {
        try
        {
            if (provider is IMetadataCache cache)
            {
                cache.ClearCache();
                return new { cleared = true };
            }

            return CreateSafeError("MCP-CACHE-001", "El proveedor no soporta gestión de caché.");
        }
        catch (Exception)
        {
            return CreateSafeError("MCP-CACHE-002", "No fue posible limpiar la caché.");
        }
    }

    [McpServerTool, Description("Obtiene estado simple de la caché (claves y expiración) para depuración.")]
    public static object GetMetadataCacheStatus(IMetadataProvider provider)
    {
        try
        {
            if (provider is IMetadataCache cache)
            {
                var status = cache.GetCacheStatus();
                return status.Select(kv => new { key = kv.Key, expiresAt = kv.Value }).ToArray();
            }

            return CreateSafeError("MCP-CACHE-003", "El proveedor no soporta gestión de caché.");
        }
        catch (Exception)
        {
            return CreateSafeError("MCP-CACHE-004", "No fue posible obtener el estado de la caché.");
        }
    }

    [McpServerTool, Description("Devuelve las entradas de auditoría en memoria (solo si InMemoryAuditLogger está habilitado). Útil para depuración.")]
    public static object GetAuditEntries()
    {
        try
        {
            var logger = ServiceProviderAccessor.Current?.GetService(typeof(IAuditLogger)) as InMemoryAuditLogger;
            if (logger is null)
                return CreateSafeError("MCP-AUDIT-001", "No hay un auditor en memoria disponible.");

            return logger.GetEntries();
        }
        catch (Exception)
        {
            return CreateSafeError("MCP-AUDIT-002", "No fue posible obtener las entradas de auditoría.");
        }
    }

    private static ToolRateLimiter CreateQueryRateLimiter()
    {
        var maxRequests = ParseInt(Environment.GetEnvironmentVariable("MCP_QUERY_RATE_LIMIT_MAX_REQUESTS"), 30);
        var windowSeconds = ParseInt(Environment.GetEnvironmentVariable("MCP_QUERY_RATE_LIMIT_WINDOW_SECONDS"), 60);
        return new ToolRateLimiter(maxRequests, TimeSpan.FromSeconds(windowSeconds));
    }

    private static int ParseInt(string? value, int defaultValue)
        => int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
}
