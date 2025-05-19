using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SqlServerMCP;

[McpServerToolType]
public static class MetadataTool
{
    [McpServerTool, Description("Obtiene metadatos de SQL Server: tablas, vistas, procedimientos y relaciones.")]
    public static async Task<object> GetMetadata(IMetadataProvider provider)
    {
        try
        {
            var tables = await provider.GetTablesAsync();
            var views = await provider.GetViewsAsync();
            var procs = await provider.GetStoredProceduresAsync();
            var fks = await provider.GetForeignKeysAsync();
            return new { tables, views, procedures = procs, foreignKeys = fks };
        }
        catch (Exception ex)
        {
            return new { error = true, message = ex.Message, exceptionType = ex.GetType().Name };
        }
    }

    [McpServerTool, Description("Ejecuta una consulta SQL arbitraria y devuelve los resultados.")]
    public static async Task<object> ExecuteQuery(
        IMetadataProvider provider,
        [Description("Consulta SQL a ejecutar")] string query)
    {
        try
        {
            if (provider is SqlServerMetadataProvider sqlProvider)
            {
                return await sqlProvider.ExecuteQueryAsync(query);
            }
            throw new InvalidOperationException("El proveedor no soporta ejecución de consultas.");
        }
        catch (Exception ex)
        {
            return new { error = true, message = ex.Message, exceptionType = ex.GetType().Name };
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
        catch (Exception ex)
        {
            return new { error = true, message = ex.Message, exceptionType = ex.GetType().Name };
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
        catch (Exception ex)
        {
            return new { error = true, message = ex.Message, exceptionType = ex.GetType().Name };
        }
    }
}
