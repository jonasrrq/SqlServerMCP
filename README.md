# MCP Server en .NET 9 para SQL Server
Introducción al MCP Server y sus requisitos
El Model Context Protocol (MCP) es un protocolo abierto diseñado para estandarizar cómo las aplicaciones ofrecen “herramientas” (tools) a los modelos de lenguaje, de modo que un LLM pueda descubrir, invocar y procesar datos externos (bases de datos, APIs, etc.) sin lógica ad-hoc por cada caso 
Microsoft for Developers
Systenics Solutions AI
. En este escenario, un “MCP Server” hospeda esas herramientas y define dos modos de comunicación con el agente:

STDIO (Standard Input / Output): envía y recibe mensajes JSON-RPC a través de la consola, ideal para prototipos locales y clientes LLM que consumen desde STDIN/STDOUT (por ejemplo, Claude Desktop, Agent Builder u otros entornos de escritorio) 
Systenics Solutions AI
microsoft.github.io
.

SSE (Server‐Sent Events): expone un endpoint HTTP al que el cliente se suscribe para recibir eventos en tiempo real (streaming), indicado cuando el agente necesita un flujo continuo de metadatos o actualizaciones (por ejemplo, múltiples consultas largas de relaciones) 
strathweb.com
Microsoft Learn
.

Objetivo principal: Crear un MCP Server en .NET 9 que se conecte a una instancia de SQL Server, recopile y normalice:

Listado completo de tablas, vistas y procedimientos almacenados.

Todas las relaciones (llaves foráneas) existentes, incluyendo ejemplos de relaciones complejas (muchos-a-muchos, filtros condicionales, jerarquías), de modo que el agente LLM pueda generar informes gerenciales precisos y completos.

El sistema debe:

Funcionamiento por STDIO para local y pruebas.

Exposición de un endpoint HTTP / SSE para producción o clientes que requieran streaming.

Uso de .NET 9 y dependencias ligeras (Microsoft.Data.SqlClient, System.Net.ServerSentEvents, ModelContextProtocol SDK).

Cumplir el estándar MCP de Anthropic, de modo que cualquier agente compatible pueda descubrir y llamar a las “tools” sin modificar el protocol

# Uso con Cursor y VS Code (Integración MCP)

## Ejemplo de configuración para VS Code o Cursor

Agrega la siguiente sección en tu `settings.json` o `.vscode/mcp.json` para registrar tu servidor MCP SQL Server:

```jsonc
"mcp": {
  "servers": {
    "mcp-server-sqlserver": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "./SqlServerMCP/SqlServerMCP.csproj",
        "--mode", "stdio", --sse
        "--server", "192.168.1.13",
        "--database", "Northwind",
        "--user", "sa",
        "--password", "tu_clave"
      ]
    }
  }
}
```

Esto lanzará el servidor MCP en modo HTTP/SSE en `http://localhost:5000/mcp`. Puedes usar la URL directamente en Cursor o VS Code para conectar el agente MCP:

```jsonc
"my-mcp-server-sqlserver": {
  "url": "http://localhost:5000/sse"
}
```

- Cambia los parámetros de conexión según tu entorno.
- Puedes lanzar el servidor en modo STDIO cambiando `--mode` a `stdio` ó  `sse` y omitiendo la URL.

> Ejemplo inspirado en la configuración de otros servidores MCP como `fetch` y servidores HTTP/SSE.