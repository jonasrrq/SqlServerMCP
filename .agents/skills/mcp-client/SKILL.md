---
name: mcp-client
description: Cliente guía y ejemplos para interactuar con servidores MCP (Model Context Protocol). Incluye ejemplos prácticos para Node.js (SDK), Python y curl, autenticación, paginación y recomendaciones de seguridad.
license: MIT
---

# MCP Client Skill

Este skill documenta cómo un cliente (humano o agente) debe interactuar con un servidor MCP como `SqlServerMCP`.

## Objetivo
Proveer patrones de uso, ejemplos de código y buenas prácticas para invocar herramientas exposadas por un servidor MCP vía Streamable HTTP o SDKs oficiales.

## Recomendaciones generales
- Usa el SDK oficial (TypeScript o Python) cuando esté disponible: maneja negociación, transporte y streaming correctamente.
- Si usas HTTP directo, realiza POST a la URL base (`http://host:port/`) y delega la construcción del mensaje al SDK cuando sea posible.
- Siempre respeta límites de filas y timeout definidos por el servidor (`MCP_QUERY_MAX_ROWS`, `MCP_QUERY_TIMEOUT_SECONDS`).
- Implementa reintentos con backoff exponencial para errores transitorios (5xx, timeouts).
- No envíes credenciales en línea de comandos; usa variables de entorno o un gestor de secretos.

## Autenticación
- Si el servidor tiene `MCP_HTTP_AUTH_ENABLED=true` debes enviar el token en uno de estos lugares:
  - Header `Authorization: Bearer <token>`
  - Header personalizado (por defecto `X-MCP-Auth`) con el valor del token

## Ejemplo (Node.js, con SDK recomendado)

1) Instala el SDK (ejemplo):

```bash
npm install @modelcontextprotocol/client
```

2) Uso básico:

```js
import { McpClient } from '@modelcontextprotocol/client';

const client = new McpClient('http://localhost:5000', {
  headers: { 'X-MCP-Auth': process.env.MCP_AUTH_TOKEN }
});

async function run() {
  const result = await client.callTool('ExecuteQuery', { query: 'SELECT TOP 10 * FROM sys.tables' });
  console.log(result); // SDK devuelve tanto texto como contenido estructurado si está disponible
}

run();
```

## Ejemplo (Python, requests - genérico)

```py
import os
import requests

url = 'http://localhost:5000/'
headers = {'Content-Type': 'application/json'}
if os.getenv('MCP_AUTH_TOKEN'):
    headers['X-MCP-Auth'] = os.getenv('MCP_AUTH_TOKEN')

payload = {
    'tool': 'ExecuteQuery',
    'parameters': { 'query': 'SELECT TOP 10 * FROM sys.tables' }
}

resp = requests.post(url, json=payload, headers=headers, timeout=30)
print(resp.status_code)
print(resp.text)
```

> Nota: el ejemplo HTTP es genérico; el SDK construye mensajes conforme a la especificación MCP y es preferible.

## Paginación
- El servidor puede exponer parámetros `page` y `pageSize` o un `pagination` wrapper en la respuesta. Si tu cliente necesita páginas, solicita `page`/`pageSize` al llamar a la herramienta (o usa las utilidades del SDK).

## Manejo de errores y debug
- El servidor retorna objetos con `error`, `errorCode` y `correlationId` para traza.
- Para depuración local activa `MCP_INCLUDE_DEBUG_DETAILS=true` (solo en entornos de desarrollo).

## Seguridad
- Nunca expongas `SQLSERVER_PASSWORD` o tokens en repositorios.
- Usa HTTPS en producción y valida `MCP_HTTP_HOST_GUARD_ENABLED`/`MCP_ALLOWED_HOSTS` si expones el servidor públicamente.

## Quick cheatsheet
- URL base streamable HTTP: `http://host:port/`
- Auth header: `Authorization: Bearer <token>` o `X-MCP-Auth: <token>`
- Tool names: `ExecuteQuery`, `GetMetadata`, `GetColumns`, `ExecuteStoredProcedure`, `ClearMetadataCache`, `GetMetadataCacheStatus`, `GetAuditEntries`.

Si necesitas snippets específicos para un cliente (axios, fetch, aiohttp, curl con payload real del SDK), dime el stack y los creo listos para usar.
