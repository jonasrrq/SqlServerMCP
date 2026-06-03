<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->

# SqlServerMCP — Agent Instructions

Servidor MCP en **.NET 10 / C#** que expone herramientas para consultar metadatos y ejecutar consultas en SQL Server mediante los transportes `stdio` y Streamable HTTP.

## Build & Test

```powershell
dotnet restore SqlServerMCP.sln
dotnet build SqlServerMCP.sln -c Debug
dotnet test SqlServerMCP.sln -c Debug --no-build
```

- Framework objetivo: `net10.0`
- Versiones NuGet gestionadas de forma centralizada en [Directory.Packages.props](../Directory.Packages.props) — **nunca añadas `Version=` en el `.csproj`**

## Architecture

```
Client → Transport (HTTP/stdio) → MetadataTool → IMetadataProvider → SqlServerMetadataProvider
```

| Archivo | Rol |
|---------|-----|
| [Program.cs](../SqlServerMCP/Program.cs) | Bootstrap, detección de modo, carga `.env`, registro de servicios |
| [MetadataTool.cs](../SqlServerMCP/MetadataTool.cs) | Herramientas MCP públicas (`[McpServerTool]`) + rate limiting + manejo de errores |
| [QuerySecurity.cs](../SqlServerMCP/QuerySecurity.cs) | Validación SQL: sólo SELECT/WITH; bloquea ALTER/DROP/DELETE/INSERT/UPDATE/MERGE/EXEC/GRANT/REVOKE/DENY y puntos y coma |
| [IMetadataProvider.cs](../SqlServerMCP/IMetadataProvider.cs) | Abstracción que separa lógica de herramientas del acceso a datos |
| [SqlServerMetadataProvider.cs](../SqlServerMCP/SqlServerMetadataProvider.cs) | Implementación real: consulta `INFORMATION_SCHEMA`, caché con TTL |
| [HttpAuthOptions.cs](../SqlServerMCP/HttpAuthOptions.cs) | Auth HTTP opcional (Bearer token o header personalizado) |
| [HttpHostGuardOptions.cs](../SqlServerMCP/HttpHostGuardOptions.cs) | Middleware de allowlist Host/Origin |
| [ToolRateLimiter.cs](../SqlServerMCP/ToolRateLimiter.cs) | Sliding window (defecto: 30 req/60 s por tool key) |
| [InMemoryAuditLogger.cs](../SqlServerMCP/InMemoryAuditLogger.cs) | Cola concurrente de auditoría; sanitiza literales de string y passwords |
| [DiagnosticSanitizer.cs](../SqlServerMCP/DiagnosticSanitizer.cs) | Enmascara passwords y User IDs en mensajes de error |

## MCP Tools expuestas (7)

`GetMetadata` · `GetColumns` · `ExecuteQuery` · `ExecuteStoredProcedure` · `ClearMetadataCache` · `GetMetadataCacheStatus` · `GetAuditEntries`

Todas retornan `object` con forma de éxito `{ result, ... }` o error `{ error: true, errorCode: "MCP-XXX-###", correlationId, message }`.

## Convenciones clave

- **Sin `--password` en CLI** — la contraseña sólo viene de `SQLSERVER_PASSWORD` (env var). Nunca exposición en argumentos.
- **`ServiceProviderAccessor.Current`** — antipatrón necesario para acceder al DI desde métodos estáticos de herramientas MCP. Usar sólo para loggers.
- **Tipo de retorno `object`** — los tools devuelven objetos anónimos (requisito del SDK MCP); no cambiar a tipos fuertes.
- **Logging best-effort** — las llamadas de auditoría/diagnóstico están envueltas en try-catch para nunca lanzar.
- **Nullable habilitado** — `<Nullable>enable</Nullable>`; resolver todas las advertencias de nullabilidad.

## Testing

- **Stack**: xUnit + Moq + FluentAssertions
- `using Xunit;` es global (configurado en el `.csproj`); no importar redundantemente.
- Tests async con `async Task`, aserciones fluentes (`.Should().Be()`, `.Should().Throw<>()`).
- Usar `Mock<IMetadataProvider>` para aislar las herramientas de la base de datos.

## Configuración

Ver [README.md](../README.md#configuración-local) para la lista completa de variables de entorno.

Variables clave de desarrollo local (en `.env`, no comitear):
```
MCP_MODE=http
SQLSERVER_PASSWORD=<tu_clave>
SQLSERVER_ENCRYPT=Mandatory
SQLSERVER_TRUST_SERVER_CERTIFICATE=true
MCP_INCLUDE_DEBUG_DETAILS=true
```

