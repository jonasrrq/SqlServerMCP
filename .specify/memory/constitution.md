<!--
Sync Impact Report

- Version change: 1.0.0 -> 1.1.0
- Modified principles:
  - I. Enfoque MCP y contratos estables — enriquecido con herramientas reales (GetMetadata, ExecuteQuery,
    GetColumns) y stack exacto (ModelContextProtocol v1.3.0, net10.0)
  - II. Seguridad y manejo de secretos — ampliado con DiagnosticSanitizer, HttpAuthEvaluator,
    HttpHostGuardOptions y variables de entorno concretas documentadas en código
  - III. Pruebas obligatorias — detallado con stack de testing real (xunit, FluentAssertions,
    Moq, coverlet) y cobertura de archivos de test existentes
  - IV. Observabilidad — actualizado con IAuditLogger/InMemoryAuditLogger, ToolRateLimiter,
    códigos de error MCP-* y flag MCP_INCLUDE_DEBUG_DETAILS
  - V. Simplicidad — reforzado con QuerySecurity (regex de palabras peligrosas), paginación y
    límites por defecto concretos
- Added sections: Stack tecnológico y configuración de entorno
- Removed sections: none
- Templates requiring updates:
  - .specify/templates/plan-template.md: ✅ actualizado — "Constitution Check" con gates concretos
  - .specify/templates/spec-template.md: ✅ sin cambios necesarios — estructura alineada
  - .specify/templates/tasks-template.md: ✅ sin cambios necesarios — ya incluye seguridad/observabilidad
- Follow-up TODOs: ninguno pendiente
-->

# SqlServerMCP Constitution

## Core Principles

### I. Enfoque MCP y contratos estables

SqlServerMCP expone exactamente dos herramientas MCP públicas: `GetMetadata` (metadatos de tablas,
vistas, procedimientos y relaciones) y `ExecuteQuery` (consultas SQL de solo lectura con paginación).
Cualquier cambio incompatible en nombre, parámetros o forma de respuesta de estas herramientas REQUIERE:
(a) incremento de versión semántica MAJOR, (b) documentación de migración en la PR y
(c) aprobación explícita en revisión. Los parámetros sensibles (credenciales, tokens) NUNCA
forman parte del contrato público de las herramientas. El stack tecnológico canónico es
.NET 10 (`net10.0`), `ModelContextProtocol` v1.3.0 y `ModelContextProtocol.AspNetCore` v1.3.0
con `Microsoft.Data.SqlClient` v7.0.1.

### II. Seguridad y manejo de secretos (MANDATORY)

Las credenciales DEBEN provenir exclusivamente de variables de entorno o un secret manager.
El argumento `--password` está prohibido en CLI para evitar exposición en historial de shell.
Variables obligatorias: `SQLSERVER_PASSWORD` o `SQLSERVER_CONNECTION_STRING`; variables
opcionales de autenticación HTTP: `MCP_HTTP_AUTH_ENABLED`, `MCP_AUTH_TOKEN`,
`MCP_AUTH_HEADER_NAME`. Toda excepción pasa por `DiagnosticSanitizer` para eliminar
`Password=` y `User ID=` antes de exponerse. El guard de hosts HTTP (`HttpHostGuardOptions`)
y el evaluador de autenticación (`HttpAuthEvaluator`) aplican cuando están habilitados.
Configuraciones de depuración (`MCP_INCLUDE_DEBUG_DETAILS=true`) SOLO se permiten en
entornos de desarrollo y DEBEN documentarse explícitamente.

### III. Pruebas obligatorias y TDD preferente (NON-NEGOTIABLE)

Toda lógica de negocio y cada herramienta MCP DEBE tener cobertura de tests automatizados.
Stack de testing: xunit, FluentAssertions, Moq y coverlet. Archivos de test actuales que
DEBEN mantenerse verdes: `AuditLoggerTests`, `CacheAdminTests`, `HttpAuthOptionsTests`,
`HttpHostGuardOptionsTests`, `MetadataToolTests`, `ProgramTests`, `QuerySecurityTests`,
`SqlServerMetadataProviderCacheTests`, `SqlServerMetadataProviderTests`, `ToolRateLimiterTests`.
TDD es preferente: el test rojo precede a la implementación siempre que sea práctico.
Ningún PR que modifique contratos MCP puede fusionarse sin tests que validen el cambio.
Los tests de integración que dependan de SQL Server real se marcan con skip explícito
cuando no hay instancia disponible.

### IV. Observabilidad y errores sanitizados

El servicio usa `IAuditLogger` (implementación en memoria: `InMemoryAuditLogger`) para
registrar todas las invocaciones de `ExecuteQuery` y llamadas a herramientas con usuario,
query, límites, éxito y código de error. Los errores expuestos al consumidor MCP siempre
usan códigos estructurados (`MCP-METADATA-001`, `MCP-RATE-001`, `MCP-QUERY-ERR`) con
mensajes no sensibles. Los detalles de excepción se incluyen solo cuando
`MCP_INCLUDE_DEBUG_DETAILS=true`. El rate limiter (`ToolRateLimiter`) aplica ventana
deslizante configurable (`MCP_QUERY_MAX_ROWS`, `MCP_QUERY_TIMEOUT_SECONDS`). Métricas
de rate-limiting (código `MCP-RATE-001` con tiempo de reintento) DEBEN mantenerse
funcionales y testeadas.

### V. Simplicidad y mínima superficie de ataque

`QuerySecurity.ValidateReadOnlyQuery` DEBE bloquear toda consulta que no comience con
`SELECT` o `WITH`, contenga `;` (múltiples sentencias) o incluya keywords peligrosas
(`ALTER`, `DROP`, `TRUNCATE`, `DELETE`, `INSERT`, `UPDATE`, `MERGE`, `CREATE`, `EXEC`,
`EXECUTE`, `GRANT`, `REVOKE`, `DENY`). Límites por defecto: 1000 filas máximo, 30s timeout,
30 peticiones por ventana de 60s. Todos los límites son configurables por variables de
entorno. La caché de metadatos (`MCP_METADATA_CACHE_TTL_SECONDS`, default 60s) reduce
carga en SQL Server. Preferir configuración simple y auditable; la complejidad adicional
DEBE justificarse explícitamente en la PR.

## Stack tecnológico y configuración de entorno

**Runtime**: .NET 10 (`net10.0`), C# con nullable habilitado, implicit usings.

**Dependencias runtime**: `Microsoft.Data.SqlClient` 7.0.1 · `Microsoft.Extensions.Hosting` 10.0.8 ·
`ModelContextProtocol` 1.3.0 · `ModelContextProtocol.AspNetCore` 1.3.0.

**Dependencias de test**: xunit 2.9.3 · FluentAssertions 8.10.0 · Moq 4.20.72 ·
coverlet.collector 10.0.1 · Microsoft.Data.Sqlite 10.0.8.

**Transportes soportados**: Streamable HTTP stateless (producción) y `stdio` (integración local).
SSE legado disponible con `MCP_ENABLE_LEGACY_SSE=true` (require modo stateful).

**Variables de entorno clave** (documentadas en `README.md` y `.env-example`):

| Variable | Propósito | Requerida |
|---|---|---|
| `SQLSERVER_PASSWORD` | Contraseña SQL Server | Sí (si no hay CONNECTION_STRING) |
| `SQLSERVER_CONNECTION_STRING` | Cadena de conexión completa | Alternativa a PASSWORD |
| `SQLSERVER_SERVER` | Host SQL Server | No (default: `localhost`) |
| `SQLSERVER_DATABASE` | Base de datos | No (default: `Northwind`) |
| `SQLSERVER_USER` | Usuario SQL | No (default: `sa`) |
| `SQLSERVER_ENCRYPT` | Modo cifrado TLS | No (default: Mandatory) |
| `SQLSERVER_TRUST_SERVER_CERTIFICATE` | Confiar cert. autofirmado | No (default: false) |
| `MCP_MODE` | Transporte: `http` o `stdio` | No (default: `http`) |
| `MCP_HTTP_AUTH_ENABLED` | Activar auth HTTP | No (default: false) |
| `MCP_AUTH_TOKEN` | Token de autenticación HTTP | Sí si AUTH_ENABLED=true |
| `MCP_QUERY_MAX_ROWS` | Máximo de filas devueltas | No (default: 1000) |
| `MCP_QUERY_TIMEOUT_SECONDS` | Timeout de consulta | No (default: 30) |
| `MCP_METADATA_CACHE_TTL_SECONDS` | TTL caché de metadatos | No (default: 60) |
| `MCP_INCLUDE_DEBUG_DETAILS` | Incluir stack en errores | No (solo desarrollo) |
| `MCP_AUDIT_MAX_ENTRIES` | Máximo entradas audit log | No (default: 1000) |
| `MCP_HTTP_HOST_GUARD_ENABLED` | Activar guard de hosts | No (default: false) |

## Flujo de Desarrollo y Revisión

- Todo cambio se propone vía PR con descripción, justificación, tests verdes y plan de migración
  si afecta contratos MCP.
- Revisión mínima: un revisor técnico más CI verde (`dotnet test` sin fallos).
- Complejidad adicional DEBE justificarse con alternativas más simples consideradas.
- Gestión de paquetes centralizada en `Directory.Packages.props`; versiones ad-hoc en `.csproj`
  individuales están prohibidas.
- Comandos canónicos de build/test:
  ```powershell
  dotnet restore SqlServerMCP.sln
  dotnet build SqlServerMCP.sln -c Debug
  dotnet test SqlServerMCP.sln -c Debug --no-build
  ```

## Governance

Enmiendas a esta constitución requieren:

1. Un PR que describa el cambio, su justificación y el impacto en contratos y tests.
2. Evidencia de tests automáticos que validen la nueva norma o la excepción.
3. Aprobación de al menos un mantenedor (owner) y paso verde en CI.
4. Un plan de migración para cambios incompatibles.

Versionado:

- Cambios incompatibles en contratos MCP o eliminación de principios → MAJOR bump.
- Nuevas políticas/principios o adición de secciones materiales → MINOR bump.
- Correcciones de redacción, clarificaciones, añadir detalles técnicos → PATCH bump.

**Version**: 1.1.0 | **Ratified**: 2026-06-03 | **Last Amended**: 2026-06-03

