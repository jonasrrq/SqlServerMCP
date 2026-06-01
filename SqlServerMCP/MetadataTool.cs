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

            // Return both camelCase (for existing clients/tests) and PascalCase structured object
            return new { tables, views, procedures = procs, foreignKeys = fks, structured = new MetadataResult { Tables = tables, Views = views, Procedures = procs, ForeignKeys = fks } };
        }
        catch (Exception ex)
        {
            return CreateSafeError("MCP-METADATA-001", "No fue posible obtener metadatos.", ex);
        }
    }

    [McpServerTool, Description("Ejecuta una consulta SQL de solo lectura y devuelve los resultados con límites de seguridad.")]
    public static async Task<object> ExecuteQuery(
        IMetadataProvider provider,
        [Description("Consulta SQL a ejecutar (solo SELECT/CTE)")] string query,
        [Description("Límite máximo de filas a devolver (1-10000)")] int maxRows = 1000,
        [Description("Timeout en segundos para la consulta")] int timeoutSeconds = 30,
        [Description("Página a devolver (1-based)")] int page = 1,
        [Description("Tamaño de página (filas por página)")] int pageSize = 100)
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
                // normalize pagination inputs
                var requestedPage = Math.Max(1, page);
                var requestedPageSize = Math.Max(1, Math.Min(10000, pageSize));

                // if provider supports maxRows, ensure we fetch at least as many rows as needed for the requested page
                var effectiveMaxRows = Math.Max(maxRows, requestedPage * requestedPageSize);

                // fetch rows with effective limit where supported; some mocks/tests only setup parameterless overload
                List<Dictionary<string, object?>> rows;
                var limitedTask = provider.ExecuteQueryAsync(query, effectiveMaxRows, timeoutSeconds);
                if (limitedTask is null)
                {
                    rows = await provider.ExecuteQueryAsync(query);
                }
                else
                {
                    rows = await limitedTask;
                }

                auditLogger?.LogQuery("-", query, effectiveMaxRows, timeoutSeconds, true, null);

                var totalAvailable = rows.Count;
                var skip = (requestedPage - 1) * requestedPageSize;
                var pageRows = rows.Skip(skip).Take(requestedPageSize).ToList();

                // Build columns list from pageRows keys
                var columns = pageRows.SelectMany(r => r.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

                var result = new QueryResult
                {
                    Columns = columns,
                    Rows = pageRows,
                    Pagination = new PaginationResult { Page = requestedPage, PageSize = requestedPageSize, TotalAvailable = totalAvailable, Returned = pageRows.Count }
                };

                return new { result, columns, rows = pageRows, pagination = result.Pagination };
            }
            catch (Exception ex)
            {
                auditLogger?.LogQuery("-", query, maxRows, timeoutSeconds, false, "MCP-QUERY-ERR");

                var correlationId = Guid.NewGuid().ToString("N");
                var includeDebugDetails = ParseBool(Environment.GetEnvironmentVariable("MCP_INCLUDE_DEBUG_DETAILS"), false);

                if (includeDebugDetails && ex is not null)
                {
                    // If we got a NullReferenceException (from awaiting a null Task), try to call the parameterless overload to get the real underlying exception
                    Exception? underlying = ex;
                    if (ex.Message?.Contains("Object reference not set", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        try
                        {
                            await provider.ExecuteQueryAsync(query);
                        }
                        catch (Exception realEx)
                        {
                            underlying = realEx;
                        }
                    }

                    var debug = DiagnosticSanitizer.BuildDebugDetail(underlying);
                    if (string.IsNullOrWhiteSpace(debug))
                    {
                        var text = underlying?.ToString() ?? string.Empty;
                        debug = DiagnosticSanitizer.BuildDebugDetail(new Exception(text));
                    }

                    return new { error = true, errorCode = "MCP-QUERY-001", correlationId, message = "No fue posible ejecutar la consulta.", debugDetail = debug };
                }

                return new { error = true, errorCode = "MCP-QUERY-001", correlationId, message = "No fue posible ejecutar la consulta." };
            }
        }
        catch (Exception ex)
        {
            return CreateSafeError("MCP-QUERY-001", "No fue posible ejecutar la consulta.", ex);
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
                var rows = await sqlProvider.ExecuteStoredProcedureAsync(procedureName, parameters ?? new());
                return new { rows, structured = new StoredProcedureResult { Rows = rows } };
            }
            return CreateSafeError("MCP-PROC-001", "El proveedor no soporta ejecución de procedimientos almacenados.");
        }
        catch (Exception ex)
        {
            return CreateSafeError("MCP-PROC-001", "No fue posible ejecutar el procedimiento almacenado.", ex);
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
            return new ColumnsResult { Columns = columns };
        }
        catch (Exception ex)
        {
            return CreateSafeError("MCP-COLUMNS-001", "No fue posible obtener las columnas.", ex);
        }
    }

    private static object CreateSafeError(string code, string message, Exception? exception = null)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var includeDebugDetails = ParseBool(Environment.GetEnvironmentVariable("MCP_INCLUDE_DEBUG_DETAILS"), false);

        if (includeDebugDetails && exception is not null)
        {
            return new
            {
                error = true,
                errorCode = code,
                correlationId,
                message,
                debugDetail = DiagnosticSanitizer.BuildDebugDetail(exception)
            };
        }

        return new
        {
            error = true,
            errorCode = code,
            correlationId,
            message
        };
    }

    [McpServerTool, Description("Limpia la caché de metadatos en el proveedor SQL (solo para SqlServerMetadataProvider).")]
    public static object ClearMetadataCache(IMetadataProvider provider)
    {
        try
        {
            if (provider is IMetadataCache cache)
            {
                cache.ClearCache();
                return new ClearCacheResult { Cleared = true };
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
                return status.Select(kv => new CacheStatusItem { Key = kv.Key, ExpiresAt = kv.Value }).ToArray();
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

    private static bool ParseBool(string? value, bool defaultValue)
        => bool.TryParse(value, out var parsed) ? parsed : defaultValue;
}
