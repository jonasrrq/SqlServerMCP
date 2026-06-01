# MCP Server on .NET 10 for SQL Server

Servidor MCP en C# que expone herramientas para consultar metadatos y ejecutar consultas en SQL Server.

## Estado actual del proyecto

- **Framework**: `net10.0`
- **Gestión de paquetes**: centralizada con `Directory.Packages.props`
- **Transporte MCP**:
  - `stdio` (integración local con editores/agentes)
  - HTTP (Streamable HTTP) usando `ModelContextProtocol.AspNetCore`

## Versiones NuGet vigentes

Las versiones se administran de forma central en `Directory.Packages.props`.

### Runtime

- `Microsoft.Data.SqlClient` `7.0.1`
- `Microsoft.Extensions.Hosting` `10.0.8`
- `ModelContextProtocol` `1.3.0`
- `ModelContextProtocol.AspNetCore` `1.3.0`

### Testing

- `Microsoft.NET.Test.Sdk` `18.6.0`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.5`
- `FluentAssertions` `8.10.0`
- `Moq` `4.20.72`
- `coverlet.collector` `10.0.1`
- `Microsoft.Data.Sqlite` `10.0.8`

## Prerrequisitos

- SDK de **.NET 10** instalado
- Acceso a una instancia de SQL Server

## Compilar y probar

Desde la raíz del repo:

```bash
dotnet restore SqlServerMCP.sln
dotnet build SqlServerMCP.sln -c Debug
dotnet test SqlServerMCP.sln -c Debug --no-build
```

## Ejecución

### 1) Modo `stdio`

```bash
dotnet run --project ./SqlServerMCP/SqlServerMCP.csproj -- \
  --mode stdio \
  --server localhost \
  --database Northwind \
  --user sa \
  --password "tu_clave"
```

### 2) Modo HTTP (Streamable HTTP)

```bash
dotnet run --project ./SqlServerMCP/SqlServerMCP.csproj -- \
  --mode sse \
  --server localhost \
  --database Northwind \
  --user sa \
  --password "tu_clave"
```

El servidor arranca en `http://localhost:5000`.

> Nota importante (MCP SDK 1.3):
>
> El transporte recomendado es **Streamable HTTP** (`app.MapMcp()`, URL base `http://localhost:5000`).
> Para compatibilidad con clientes antiguos, este proyecto también habilita SSE legado (`/sse` + `/message`).

## MCP Inspector (evitar error 400)

Si en Inspector eliges **Transport Type = SSE**, usa:

- **Server URL**: `http://localhost:5000/sse`

Si eliges modo HTTP/Streamable, usa:

- **Server URL**: `http://localhost:5000`

`http://localhost:5000` con tipo **SSE** suele fallar porque SSE legado espera la ruta `/sse`.

## Integración con VS Code / Cursor (mcp.json)

### Servidor por comando (recomendado para `stdio`)

```jsonc
{
  "servers": {
    "mcp-server-sqlserver": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "./SqlServerMCP/SqlServerMCP.csproj",
        "--",
        "--mode", "stdio",
        "--server", "localhost",
        "--database", "Northwind",
        "--user", "sa",
        "--password", "tu_clave"
      ]
    }
  }
}
```

### Servidor por URL (modo HTTP)

Si tienes el servidor levantado en HTTP, registra la URL base MCP:

```jsonc
{
  "servers": {
    "mcp-server-sqlserver-http": {
      "url": "http://localhost:5000",
      "type": "http"
    }
  }
}
```

## Herramientas MCP expuestas

Desde `MetadataTool`:

- `GetMetadata`: tablas, vistas, procedimientos y llaves foráneas.
- `GetColumns`: columnas de tabla/vista.
- `ExecuteQuery`: ejecución de consulta SQL.
- `ExecuteStoredProcedure`: ejecución de SP con parámetros.

## Notas de seguridad

- Evita credenciales hardcodeadas en ambientes reales.
- Usa secretos del entorno (variables de entorno/gestor de secretos).
- Limita permisos del usuario SQL a lo estrictamente necesario.
