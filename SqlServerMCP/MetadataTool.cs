using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SqlServerMCP;

[McpServerToolType]
public static class MetadataTool
{
    [McpServerTool, Description("Obtiene metadatos de SQL Server: tablas, vistas, procedimientos y relaciones.")]
    public static async Task<object> GetMetadata(IMetadataProvider provider)
    {
        var tables = await provider.GetTablesAsync();
        var views = await provider.GetViewsAsync();
        var procs = await provider.GetStoredProceduresAsync();
        var fks = await provider.GetForeignKeysAsync();
        return new { tables, views, procedures = procs, foreignKeys = fks };
    }

    [McpServerTool, Description("Ejecuta una consulta SQL arbitraria y devuelve los resultados.")]
    public static async Task<object> ExecuteQuery(
        IMetadataProvider provider,
        [Description("Consulta SQL a ejecutar")] string query)
    {
        if (provider is SqlServerMetadataProvider sqlProvider)
        {
            return await sqlProvider.ExecuteQueryAsync(query);
        }
        throw new InvalidOperationException("El proveedor no soporta ejecución de consultas.");
    }

    [McpServerTool, Description("Ejecuta un procedimiento almacenado con parámetros opcionales y devuelve los resultados.")]
    public static async Task<object> ExecuteStoredProcedure(
        IMetadataProvider provider,
        [Description("Nombre del procedimiento almacenado")] string procedureName,
        [Description("Parámetros del procedimiento (clave:valor)")] Dictionary<string, object>? parameters = null)
    {
        if (provider is SqlServerMetadataProvider sqlProvider)
        {
            return await sqlProvider.ExecuteStoredProcedureAsync(procedureName, parameters ?? new());
        }
        throw new InvalidOperationException("El proveedor no soporta ejecución de procedimientos almacenados.");
    }
}
