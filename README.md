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

> Nota importante (MCP SDK 1.3):
>
> El transporte recomendado es **Streamable HTTP** (`app.MapMcp()`, URL base `http://localhost:5000`) y se ejecuta en modo **stateless** por defecto.
> SSE legado (`/sse` + `/message`) queda deshabilitado por defecto y se habilita solo con `MCP_ENABLE_LEGACY_SSE=true`.

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
    "SqlServerMCP": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "./SqlServerMCP/SqlServerMCP.csproj",
        "--",
        "--mode", "stdio",
        "--server", "localhost",
        "--database", "Northwind",
        "--user", "sa"
      ],
      "env": {
        "SQLSERVER_PASSWORD": "tu_clave"
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
    "SqlServerMCP": {
      "url": "http://localhost:5000",
      "type": "http"
    }
  }
}
```

Con auth opcional habilitada en HTTP:

```jsonc
{
  "servers": {
    "SqlServerMCP": {
      "url": "http://localhost:5000",
      "type": "http",
      "headers": {
        "X-MCP-Auth": "mi_token_local"
      }
    }
  }
}
```

## Herramientas MCP expuestas

Desde `MetadataTool`:

- `GetMetadata`: tablas, vistas, procedimientos y llaves foráneas.
- `GetColumns`: columnas de tabla/vista.
- `ExecuteQuery`: ejecución de consulta SQL de solo lectura.
- `ExecuteStoredProcedure`: ejecución de SP con parámetros.
- `ClearMetadataCache`: limpia la caché de metadatos.
- `GetMetadataCacheStatus`: devuelve claves/expiración de la caché.
- `GetAuditEntries`: devuelve auditoría en memoria para depuración.

## Uso desde cliente

Recomendado: usa el SDK oficial (TypeScript o Python) o el MCP Inspector. El servidor expone transporte Streamable HTTP en la URL base (por defecto `http://localhost:5000`).

- Autenticación HTTP (opcional): envía el token configurado en `MCP_AUTH_TOKEN` como `Authorization: Bearer <token>` o en el header `X-MCP-Auth` (configurable con `MCP_AUTH_HEADER_NAME`).
- Endpoints importantes:
  - Streamable HTTP: POST `http://localhost:5000/` (transport layer manejado por el SDK/inspector).
  - SSE legado (solo si `MCP_ENABLE_LEGACY_SSE=true`): base `http://localhost:5000/sse`.

Ejemplo rápido (usar SDK cuando sea posible):

Node (recomendado con SDK):

```js
// pseudocódigo — usa el SDK oficial en lugar de peticiones manuales cuando sea posible
import { McpClient } from '@modelcontextprotocol/client';
const client = new McpClient('http://localhost:5000', { headers: { 'X-MCP-Auth': process.env.MCP_AUTH_TOKEN } });
const res = await client.callTool('ExecuteQuery', { query: 'SELECT TOP 10 * FROM sys.tables' });
console.log(res);
```

Python (cuando no exista SDK preferido):

```py
import requests
url = 'http://localhost:5000/'
headers = {'Content-Type': 'application/json', 'X-MCP-Auth': 'mi_token_local'}
payload = {
  # el SDK construye mensajes conforme a la especificación MCP; aquí usamos un ejemplo genérico
  'tool': 'ExecuteQuery',
  'parameters': { 'query': 'SELECT TOP 10 * FROM sys.tables' }
}
resp = requests.post(url, json=payload, headers=headers, timeout=30)
print(resp.status_code, resp.text)
```

Notas prácticas:
- Preferir siempre el SDK oficial o el MCP Inspector: manejan negociación, streaming y reproducibilidad de transportes (stdio vs http).
- Para peticiones ad-hoc use `POST /` con el header de autenticación si está habilitado.
- Respeto de límites: el servidor aplica `MCP_QUERY_MAX_ROWS` y `MCP_QUERY_TIMEOUT_SECONDS`.
- Paginación: cuando use `ExecuteQuery` en grandes conjuntos use parámetros `page`/`pageSize` si su cliente/SDK los expone.

Si necesitas ejemplos concretos para un cliente (Node/Python/Rust), indícamelo y genero snippets listos para copiar.

## Publicar y compartir el skill

Hemos añadido las herramientas necesarias para generar un paquete de distribución del skill `SqlServerMCP`:

- `./.agents/skills/SqlServerMCP/SKILL.md` — el contenido a distribuir (licencia MIT).
- `make-release.ps1` — script PowerShell para copiar y crear un ZIP listo para distribuir.

Para crear el paquete de release desde la raíz del repo:

```powershell
.\make-release.ps1
```

El ZIP resultante quedará en `./dist/SqlServerMCP-skill.zip`.

¿Quieres que además publique automáticamente el skill en un repo Git (crear branch + commit + tag)? Si es así dime el remote y el branch destino.

## Skill para agentes (machine-readable)

Además del `SKILL.md`, el skill ahora incluye un descriptor machine-readable con las herramientas y sus esquemas en JSON:

- `./.agents/skills/SqlServerMCP/tools.json` — lista de herramientas, parámetros y esquema de salida (JSON Schema). Esto facilita que agentes (Copilot u otros) descubran y validen parámetros automáticamente.

Los consumidores automáticos pueden usar `tools.json` para generar formularios, validadores o helpers de invocación en clientes.

Ejemplo rápido (validar payload en Node con Ajv):

```js
import Ajv from 'ajv';
import tools from './.agents/skills/SqlServerMCP/tools.json';

const ajv = new Ajv();
const schema = tools.tools.find(t => t.name === 'ExecuteQuery').parameters;
const validate = ajv.compile(schema);
const payload = { query: 'SELECT TOP 1 * FROM sys.tables' };
console.log(validate(payload));
```

## Distribución del skill `SqlServerMCP`

Incluimos un skill cliente en `.agents/skills/SqlServerMCP/SKILL.md` dentro de este repositorio. Este skill contiene ejemplos listos para usar, recomendaciones de seguridad y un "cheatsheet" de herramientas expuestas.

Para usarlo localmente o distribuirlo:

- Incluye la carpeta `.agents/skills/SqlServerMCP` en tu paquete/repo. El archivo principal es `SKILL.md` con licencia MIT.
- Opcional: publica el directorio como un paquete (por ejemplo, npm o un repositorio Git) para que tus equipos puedan instalarlo/consultarlo.

Ejemplo de comandos para preparar la distribución (opcional):

```powershell
# copia el skill al folder de release
mkdir -Force .\dist\skills
robocopy .\.agents\skills\SqlServerMCP .\dist\skills\SqlServerMCP /E

# empaqueta el release (zip)
powershell -Command "Compress-Archive -Path .\dist\skills\SqlServerMCP -DestinationPath .\dist\SqlServerMCP-skill.zip -Force"
```

Si quieres, creo un script `make-release.ps1` que automatice la copia y el zip, y actualizo el README con instrucciones paso a paso para publicar el skill.

## Seguridad y límites implementados

- `ExecuteQuery` solo permite `SELECT` / `WITH`
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
- `MCP_MODE`
- `MCP_ENABLE_LEGACY_SSE`
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
