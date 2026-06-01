name: mcp-client
description: Skill para que agentes/IA (Copilot u otros) invoquen el MCP `SqlServerMCP` con contexto y metas claras. Expone metadata de herramientas, esquemas de entrada/salida y ejemplos listos para llamadas automated.
license: MIT
---

# Skill para agentes: invocar MCP con contexto

Este skill está diseñado para que agentes IA (p. ej. Copilot) llamen al servidor `SqlServerMCP` de forma semántica, con metadatos de herramientas, esquemas y ejemplos que faciliten decisiones automatizadas.

Principios:
- Proveer descripción y schema de cada herramienta para que el agente pueda construir parámetros válidos.
- Incluir ejemplos de invocación y `context` opcional (correlationId, userIntent, maxRows).
- Devolver un `structuredOutput` schema para que el agente pueda procesar respuestas sin parsing libre.

Cómo usar (resumen para agentes):
1. Llamar `GetMetadata` o `GetMetadataCacheStatus` para descubrir tablas y capacidades.
2. Preparar `parameters` de la herramienta objetivo según el schema abajo.
3. Enviar POST a la URL base (`http://host:port/`) con headers de auth si aplica.
4. Consumir `result.structured` cuando esté disponible; fallbacks a `result.text`.

Formato recomendado del mensaje HTTP (ejemplo mínimo que usan los SDKs):

{
  "tool": "ExecuteQuery",
  "parameters": { ... },
  "context": {
    "correlationId": "<uuid>",
    "userIntent": "obtener primeras 10 tablas para revisión rápida"
  }
}

Autenticación:
- Header `Authorization: Bearer <token>` o `X-MCP-Auth: <token>`.

Rate limits / seguridad:
- Respetar `MCP_QUERY_RATE_LIMIT_MAX_REQUESTS` y ventanas. Evitar peticiones paralelas excesivas.

Tool definitions (esquemas sencillos que el agente puede usar para validar parámetros):

1) ExecuteQuery
 - description: Ejecuta una consulta SQL de solo lectura (SELECT/ WITH). Se validan solo-lectura y límites.
 - parameters:
   - query: string (required)
   - maxRows: int (optional)
   - timeoutSeconds: int (optional)
 - output (structured): { rows: [ { columns: [string], values: [[...]] } ], rowCount: int }

2) GetMetadata
 - description: Devuelve tablas, vistas y procedimientos disponibles.
 - parameters: { filter: string? }
 - output (structured): { tables: [ { name, schema, type } ], procedures: [...] }

3) GetColumns
 - description: Columnas de una tabla o vista.
 - parameters: { objectName: string (required) }
 - output (structured): { columns: [ { name, dataType, isNullable, maxLength } ] }

4) ExecuteStoredProcedure
 - description: Ejecuta un SP con parámetros de entrada y devuelve tablas/result sets.
 - parameters: { procedureName: string (required), parameters: object (name->value) }
 - output (structured): { resultSets: [ { columns: [...], rows: [...] } ] }

5) ClearMetadataCache / GetMetadataCacheStatus
 - description: operaciones administrativas para cache de metadatos.
 - parameters: none / none
 - output: status object / confirmation

6) GetAuditEntries
 - description: Devuelve entradas de auditoría en memoria (útil para diagnóstico).
 - parameters: { since: ISODate? , limit: int? }
 - output: { entries: [ { timestamp, tool, parameters, resultSummary, correlationId } ] }

Ejemplos para agentes (directrices):
- Intent: "listar tablas del esquema dbo" → Step1: call `GetMetadata` with filter="dbo.%" → Step2: call `GetColumns` for chosen table → Step3: return structured answer with sample rows via `ExecuteQuery` limited.

Ejemplo de payload para `ExecuteQuery` con contexto:

{
  "tool": "ExecuteQuery",
  "parameters": {
    "query": "SELECT TOP 10 Id, Name FROM dbo.Customers ORDER BY Id",
    "maxRows": 10
  },
  "context": {
    "correlationId": "123e4567-e89b-12d3-a456-426614174000",
    "userIntent": "mostrar ejemplos de clientes para revisión rápida",
    "safety": { "maskSecrets": true }
  }
}

Respuesta esperada (estructura):

{
  "error": false,
  "result": {
    "text": "...human readable summary...",
    "structured": {
      "rows": [ { "columns": ["Id","Name"], "values": [[1, "ACME"] , [2, "Contoso"] ] } ],
      "rowCount": 2
    }
  },
  "correlationId": "123e4567-e89b-12d3-a456-426614174000"
}

Notas finales para desarrolladores de agentes:
- Prefiere `structured` cuando exista: evita parsing de texto.
- Incluye `correlationId` en cada llamada para trazabilidad y correlación en logs/audit.
- Si la operación falla devuelve `errorCode` y `message` — usar lógica de reintento o fallback si procede.

Si quieres, adapto este skill para exportar automáticamente una lista de herramientas en formato JSON (machine-readable) o genero un paquete NPM/PyPI con helpers de invocación. ¿Cuál prefieres? 
