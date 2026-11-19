# Database MCP Server on .NET 11 (SQL Server engine)

Servidor MCP en C# para consultar metadatos y ejecutar consultas. Hoy implementa SQL Server; el paquete `JR.DatabaseMCP` usa un nombre genérico para incorporar otros motores más adelante.

## Estado actual del proyecto

- **Framework**: `net11.0`
- **Motor actual**: SQL Server (`MCP_DB_PROVIDER=sqlserver`)
- **Gestión de paquetes**: centralizada con `Directory.Packages.props`
- **Transporte MCP**:
  - `stdio` (integración local con editores/agentes)
  - HTTP (Streamable HTTP) usando `ModelContextProtocol.AspNetCore`

## Selección de motor

El servidor valida el motor al arrancar:

- CLI: `--db-provider sqlserver`
- Entorno: `MCP_DB_PROVIDER=sqlserver`
- Default: `sqlserver`
- Valores soportados hoy: `sqlserver`. Cualquier otro valor falla al inicio con `InvalidOperationException`.

Este selector es el punto de extensión para PostgreSQL, MySQL y SQLite.

## Versiones NuGet vigentes

Las versiones se administran de forma central en `Directory.Packages.props`.

### Runtime

- `Microsoft.Data.SqlClient` `7.1.2`
- `Microsoft.Extensions.Hosting` `11.0.0`
- `ModelContextProtocol` `2.4.0`
- `ModelContextProtocol.AspNetCore` `2.4.0`

### Testing

- `Microsoft.NET.Test.Sdk` `18.6.0`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `3.1.5`
- `FluentAssertions` `8.10.0`
- `Moq` `4.20.72`
- `coverlet.collector` `10.0.1`
- `Microsoft.Data.Sqlite` `11.0.0`

## Prerrequisitos

- SDK de **.NET 11** instalado
- Acceso a una instancia de SQL Server

## Migración a .NET 11 y MCP SDK 2.4

- MCP SDK 2.4 implementa la especificación `2026-07-28` con negociación discovery-first y fallback a `initialize` para clientes anteriores.
- HTTP sigue en modo **stateless** (`options.Stateless = true`).
- El **SSE legado fue retirado**: `MCP_ENABLE_LEGACY_SSE` ya no tiene efecto. `MCP_MODE=sse` se acepta como alias de `http`.
- En `stdio`, todos los logs se escriben en **stderr**; stdout queda reservado para el protocolo.
- Las tools devuelven un bloque de texto JSON (sin `structuredContent` ni `outputSchema`), porque `UseStructuredContent` sigue desactivado.
- El paquete NuGet `JR.DatabaseMCP` incluye `.mcp/server.json` y declara `--mode stdio` para clientes MCP.
- Native AOT compila, pero `tools/list` no está disponible con `WithToolsFromAssembly`; requiere registro genérico y JSON source-generation antes de soportarlo.

## Configuración local

El proyecto carga automáticamente un archivo `.env` si existe en la raíz del repo o en un directorio padre.

- `.env` **no se sube al repo**
- `.env-example` contiene la plantilla recomendada
- las variables ya definidas en el entorno del sistema **no se sobrescriben**
- puedes desactivar la carga automática con:
  - `MCP_DISABLE_DOTENV=true`

Configuración local recomendada:

- `MCP_MODE=http`
- `SQLSERVER_ENCRYPT=Mandatory`
- `SQLSERVER_TRUST_SERVER_CERTIFICATE=true`
- `MCP_INCLUDE_DEBUG_DETAILS=true`

Para TLS/SSL en local:

- `Strict` puede fallar en instancias locales antiguas o sin soporte/certificado adecuado
- para desarrollo, `Mandatory + TrustServerCertificate=true` suele ser el mejor equilibrio
- en producción, usa certificado válido y `TrustServerCertificate=false`

## Compilar y probar

Desde la raíz del repo:

```powershell
dotnet restore SqlServerMCP.sln
dotnet build SqlServerMCP.sln -c Debug
dotnet test SqlServerMCP.sln -c Debug --no-build
```

## Paquete NuGet (MCP)

El servidor se empaqueta como MCP server (`PackageType=McpServer`) con manifiesto `.mcp/server.json`:

```powershell
dotnet pack SqlServerMCP/SqlServerMCP.csproj -c Release
```

El paquete `JR.DatabaseMCP` declara `--mode stdio` y los inputs `SQLSERVER_*` para clientes MCP. El nombre es genérico para futuros motores; el binario actual implementa SQL Server. La publicación en NuGet.org queda como decisión de release.

## Instalación en clientes MCP

Si el paquete aún no está publicado, usa `dotnet run --project ... -- --mode stdio` o añade `--add-source` con la ruta a `SqlServerMCP\bin\Release` en la configuración de `dnx`.

### Claude Desktop (stdio local)

Edita `%APPDATA%\Claude\claude_desktop_config.json` y añade:

```json
{
  "mcpServers": {
    "SqlServerMCP": {
      "command": "dnx",
      "args": [
        "JR.DatabaseMCP@0.1.0-beta",
        "--",
        "--mode", "stdio"
      ],
      "env": {
        "MCP_DB_PROVIDER": "sqlserver",
        "SQLSERVER_CONNECTION_STRING": "Server=localhost;Database=Editoriales_Net;User Id=sa;Password=TU_CLAVE;Encrypt=Mandatory;TrustServerCertificate=True;"
      }
    }
  }
}
```

Reinicia Claude Desktop y revisa **Settings → Developer** y los logs en `%APPDATA%\Claude\logs`. No escribas credenciales reales en un archivo versionado.

### Claude Code (stdio local)

```powershell
claude mcp add --env 'SQLSERVER_CONNECTION_STRING=Server=localhost;Database=Editoriales_Net;User Id=sa;Password=TU_CLAVE;Encrypt=Mandatory;TrustServerCertificate=True;' `
  --transport stdio SqlServerMCP `
  -- dnx JR.DatabaseMCP@0.1.0-beta -- --mode stdio

claude mcp list
```

También puedes declararlo en `.mcp.json` en la raíz del proyecto:

```json
{
  "mcpServers": {
    "SqlServerMCP": {
      "type": "stdio",
      "command": "dnx",
      "args": ["JR.DatabaseMCP@0.1.0-beta", "--", "--mode", "stdio"],
      "env": {
        "MCP_DB_PROVIDER": "sqlserver",
        "SQLSERVER_CONNECTION_STRING": "Server=localhost;Database=Editoriales_Net;User Id=sa;Password=TU_CLAVE;Encrypt=Mandatory;TrustServerCertificate=True;"
      }
    }
  }
}
```

### ChatGPT desktop (MCP remoto)

ChatGPT **no lanza procesos locales**: sus custom MCP servers son remotos. Ejecuta el servidor en HTTP y expónlo por HTTPS:

```powershell
$env:MCP_HTTP_AUTH_ENABLED = "true"
$env:MCP_AUTH_TOKEN = "TU_TOKEN"
dotnet run --project ./SqlServerMCP/SqlServerMCP.csproj -- --mode http
```

Expón `http://localhost:5000` con un túnel HTTPS (Cloudflare Tunnel, ngrok u otro). En ChatGPT: **Settings → Plugins → Add custom MCP server**, usa la URL pública y el token (`Authorization: Bearer <token>` o `X-MCP-Auth`). La app de escritorio usa los mismos plugins de tu cuenta.

Limitaciones: ChatGPT no ejecuta stdio local, este servidor no implementa OAuth (solo token estático) y no expone las tools `search`/`fetch`, así que sirve para tool calling de un custom MCP server, no para deep research/company knowledge.

### VS Code, Visual Studio y Cursor

| Cliente | Archivo | Clave |
|---|---|---|
| VS Code | `.vscode/mcp.json` | `servers` |
| Visual Studio 2026 | `.mcp.json` (raíz de la solución) | `servers` |
| Cursor | `.cursor/mcp.json` | `mcpServers` |

Formato para VS Code / Visual Studio:

```json
{
  "servers": {
    "SqlServerMCP": {
      "type": "stdio",
      "command": "dnx",
      "args": ["JR.DatabaseMCP@0.1.0-beta", "--", "--mode", "stdio"],
      "env": {
        "MCP_DB_PROVIDER": "sqlserver",
        "SQLSERVER_CONNECTION_STRING": "Server=localhost;Database=Editoriales_Net;User Id=sa;Password=TU_CLAVE;Encrypt=Mandatory;TrustServerCertificate=True;"
      }
    }
  }
}
```

Para Cursor, usa la misma entrada bajo `mcpServers`. Para HTTP, cambia el bloque por:

```json
{
  "type": "http",
  "url": "http://localhost:5000"
}
```

## Ejecución

> Seguridad: este proyecto **ya no lee `--password`** desde argumentos CLI para evitar exposición en historial de shell/procesos.
> Define `SQLSERVER_PASSWORD` (o `SQLSERVER_CONNECTION_STRING`) por variables de entorno/secret manager.

### 1) Modo `stdio`

```powershell
$env:SQLSERVER_PASSWORD = "tu_clave"
dotnet run --project ./SqlServerMCP/SqlServerMCP.csproj -- `
  --mode stdio `
  --server localhost `
  --database Northwind `
  --user sa
```

### 2) Modo HTTP (Streamable HTTP)

```powershell
$env:SQLSERVER_PASSWORD = "tu_clave"
dotnet run --project ./SqlServerMCP/SqlServerMCP.csproj -- `
  --mode http `
  --server localhost `
  --database Northwind `
  --user sa
```

El servidor arranca en `http://localhost:5000`.

> Nota importante (MCP SDK 2.4):
>
> El transporte recomendado es **Streamable HTTP** (`app.MapMcp()`, URL base `http://localhost:5000`) y se ejecuta en modo **stateless** por defecto.
> El **SSE legado fue retirado**; `MCP_ENABLE_LEGACY_SSE` ya no tiene efecto y `MCP_MODE=sse` se acepta solo como alias de `http`.

### 3) HTTP con autenticación opcional

La autenticación HTTP es **opcional** y está desactivada por defecto.

Variables:

- `MCP_HTTP_AUTH_ENABLED=false|true`
- `MCP_AUTH_TOKEN`
- `MCP_AUTH_HEADER_NAME` (por defecto `X-MCP-Auth`)

Si activas auth:

- el cliente puede enviar `Authorization: Bearer <token>`
- o el header configurado en `MCP_AUTH_HEADER_NAME`

Ejemplo local:

```powershell
$env:MCP_HTTP_AUTH_ENABLED = "true"
$env:MCP_AUTH_TOKEN = "mi_token_local"
$env:MCP_AUTH_HEADER_NAME = "X-MCP-Auth"
dotnet run --project ./SqlServerMCP/SqlServerMCP.csproj -- --mode http
```

## MCP Inspector

Usa **Transport Type = Streamable HTTP** con:

- **Server URL**: `http://localhost:5000`

El SSE legado ya no está disponible; no uses la ruta `/sse`.

## Desarrollo local (sin publicar el paquete)

Para clientes que lanzan procesos locales, puedes usar `dotnet run` en lugar de `dnx`:

```jsonc
{
  "servers": {
    "SqlServerMCP": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "D:\\Desarrollo\\SqlServerMCP\\SqlServerMCP\\SqlServerMCP.csproj",
        "--",
        "--mode", "stdio"
      ],
      "env": {
        "MCP_DB_PROVIDER": "sqlserver",
        "SQLSERVER_CONNECTION_STRING": "Server=localhost;Database=Editoriales_Net;User Id=sa;Password=TU_CLAVE;Encrypt=Mandatory;TrustServerCertificate=True;"
      }
    }
  }
}
```

Para HTTP local:

```powershell
dotnet run --project ./SqlServerMCP/SqlServerMCP.csproj -- --mode http
```

```jsonc
{
  "servers": {
    "SqlServerMCP": {
      "type": "http",
      "url": "http://localhost:5000",
      "headers": { "X-MCP-Auth": "mi_token_local" }
    }
  }
}
```

## Herramientas MCP expuestas

Los nombres MCP son snake_case; entre paréntesis, el método C#:

- `get_metadata` (`GetMetadata`): tablas, vistas, procedimientos y FKs.
- `get_columns` (`GetColumns`): columnas de tabla/vista.
- `execute_query` (`ExecuteQuery`): SELECT/CTE con `maxRows`, `timeoutSeconds`, `page` y `pageSize`.
- `execute_stored_procedure` (`ExecuteStoredProcedure`): SP con parámetros.
- `clear_metadata_cache` (`ClearMetadataCache`): limpia la caché.
- `get_metadata_cache_status` (`GetMetadataCacheStatus`): claves/expiración.
- `get_audit_entries` (`GetAuditEntries`): auditoría en memoria.

## Uso desde cliente

Recomendado: usa el SDK oficial de MCP o el MCP Inspector. El servidor expone Streamable HTTP en la URL base (por defecto `http://localhost:5000`).

- Autenticación HTTP opcional: `Authorization: Bearer <token>` o `X-MCP-Auth: <token>`.
- Endpoint Streamable HTTP: `POST http://localhost:5000/`.
- Las tools se llaman por su nombre MCP (`execute_query`, `get_metadata`, etc.), no por el método C#.

Ejemplo de llamada JSON-RPC (lo que hace un cliente MCP por debajo):

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "execute_query",
    "arguments": {
      "query": "SELECT TOP 10 name FROM sys.tables ORDER BY name",
      "maxRows": 10,
      "page": 1,
      "pageSize": 10
    }
  }
}
```

No envíes `{ "tool": "...", "parameters": {...} }`: eso no es MCP. Para HTTP manual usa JSON-RPC con `Accept: application/json, text/event-stream`; para todo lo demás, un SDK o el Inspector.

Notas prácticas:
- Límites: `MCP_QUERY_MAX_ROWS` y `MCP_QUERY_TIMEOUT_SECONDS`.
- Paginación: usa `page`/`pageSize` en `execute_query`.
- Errores: la tool devuelve `error: true`, `errorCode` y `message`; no reintentes a ciegas.

## Skill para agentes (machine-readable)

El skill incluye el contrato real de las tools:

- `.agents/skills/SqlServerMCP/SKILL.md` — instrucciones, reglas de seguridad y contrato de salida.
- `.agents/skills/SqlServerMCP/tools.json` — nombres MCP, argumentos y esquemas JSON.

Validación de argumentos con Ajv:

```js
import Ajv from 'ajv';
import tools from './.agents/skills/SqlServerMCP/tools.json';

const ajv = new Ajv();
const schema = tools.tools.find(t => t.name === 'execute_query').inputSchema;
const validate = ajv.compile(schema);
console.log(validate({ query: 'SELECT TOP 1 name FROM sys.tables', maxRows: 1 }));
```

## Distribución del skill

El skill vive en `.agents/skills/SqlServerMCP` y se distribuye como ZIP:

```powershell
.\make-release.ps1
```

El script valida el frontmatter de `SKILL.md` y que `tools.json` sea JSON válido, copia el skill a `dist\skills\SqlServerMCP` y genera `dist\SqlServerMCP-skill.zip`.

Contenido del ZIP:
- `SKILL.md` — instrucciones para el agente.
- `tools.json` — contrato machine-readable de las tools.

Para instalarlo en otro agente, copia `skills/SqlServerMCP` en la ruta de skills del cliente o publica el ZIP en tu repositorio de distribución.

## Seguridad y límites implementados

- `execute_query` solo permite `SELECT` / `WITH`
- se bloquean consultas peligrosas y múltiples sentencias
- límite configurable de filas y timeout:
  - `MCP_QUERY_MAX_ROWS`
  - `MCP_QUERY_TIMEOUT_SECONDS`
- rate limiting configurable:
  - `MCP_QUERY_RATE_LIMIT_MAX_REQUESTS`
  - `MCP_QUERY_RATE_LIMIT_WINDOW_SECONDS`
- errores sanitizados con:
  - `errorCode`
  - `correlationId`
- detalles de depuración opcionales con:
  - `MCP_INCLUDE_DEBUG_DETAILS=true`

## Caché y auditoría

- caché TTL para metadatos:
  - `MCP_METADATA_CACHE_TTL_SECONDS`
- auditoría en memoria:
  - `MCP_AUDIT_MAX_ENTRIES`

La auditoría registra intentos y resultados de ejecución de consultas con sanitización básica para evitar exponer secretos.

## Variables de entorno principales

- `SQLSERVER_CONNECTION_STRING`
- `SQLSERVER_PASSWORD`
- `SQLSERVER_SERVER`
- `SQLSERVER_DATABASE`
- `SQLSERVER_USER`
- `SQLSERVER_ENCRYPT`
- `SQLSERVER_TRUST_SERVER_CERTIFICATE`
- `SQLSERVER_CONNECT_TIMEOUT`
- `MCP_DB_PROVIDER`
- `MCP_MODE`
- `MCP_HTTP_URL`
- `MCP_HTTP_AUTH_ENABLED`
- `MCP_AUTH_TOKEN`
- `MCP_AUTH_HEADER_NAME`
- `MCP_INCLUDE_DEBUG_DETAILS`
- `MCP_QUERY_MAX_ROWS`
- `MCP_QUERY_TIMEOUT_SECONDS`
- `MCP_METADATA_CACHE_TTL_SECONDS`
- `MCP_QUERY_RATE_LIMIT_MAX_REQUESTS`
- `MCP_QUERY_RATE_LIMIT_WINDOW_SECONDS`
- `MCP_AUDIT_MAX_ENTRIES`

## Notas de seguridad

- Evita credenciales hardcodeadas en ambientes reales.
- Usa secretos del entorno (variables de entorno/gestor de secretos).
- Limita permisos del usuario SQL a lo estrictamente necesario.
- En producción, usa `SQLSERVER_ENCRYPT=Mandatory` o `Strict` y `SQLSERVER_TRUST_SERVER_CERTIFICATE=false`.
- Activa `MCP_HTTP_AUTH_ENABLED=true` si vas a exponer el servidor por HTTP fuera de un entorno totalmente controlado.
