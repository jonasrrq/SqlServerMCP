# MCP Skills en JonasRq.DatabaseMCP

El servidor distribuye **Agent Skills** usando la extensión oficial
`io.modelcontextprotocol/skills` (SEP-2640) sobre el primitivo MCP `resources`. Un cliente
compatible puede descubrir skills con `skills/list`, obtener una entrada con `skills/get` y
leer `SKILL.md` y sus archivos con `resources/read`.

> Skills no es lo mismo que Resources. `resources/read` transporta bytes; `skills/list` y
> `skills/get` publican la identidad, el frontmatter y el manifiesto verificable de una skill.
> Un `resources/read` de un `SKILL.md` no activa la skill por sí solo: la activación y la
> aprobación son responsabilidad del host MCP.

## Implementación

- Paquete: `ModelContextProtocol.Extensions.Skills` **2.4.0** (API experimental
  `MCPEXP005`; el proyecto la habilita explícitamente).
- Métodos: `skills/list`, `skills/get`, `resources/list`, `resources/read`.
- Capacidad: `io.modelcontextprotocol/skills` + `resources`.
- `resources/directory/read` / `directoryRead` **no** está implementado por el SDK 2.4.0;
  el servidor no lo declara y los clientes no deben llamarlo.
- Las tools existentes siguen registradas con `WithToolsFromAssembly` y no cambian.

```
Skills/ (contenido del proyecto) ─┐
recursos embebidos ───────────────┼─> proveedores ─> SkillRegistry ─┬─> skills/list, skills/get
directorios externos ─────────────┘                                 └─> resources/list, resources/read
```

`SkillRegistry` mantiene un snapshot inmutable de entradas validadas y de los bytes de sus
archivos. Cada recarga construye un snapshot nuevo y lo intercambia de forma atómica; los
handlers leen siempre el snapshot actual. Un fallo de recarga conserva el snapshot anterior.

## Estructura de una skill

```text
Skills/
└── skill-name/
    ├── SKILL.md
    ├── references/
    ├── examples/
    ├── scripts/
    └── assets/
```

Reglas:

- `SKILL.md` es obligatorio en la raíz.
- El frontmatter YAML debe incluir `name` y `description`.
- El nombre debe usar minúsculas ASCII, dígitos y guiones simples (máximo 64 caracteres).
- El último segmento del directorio debe coincidir con `name`.
- Se admiten archivos complementarios; se publican como recursos
  `skill://<skill-name>/<ruta-relativa>`.
- Los scripts son archivos de la skill. **No se ejecutan** automáticamente.

Ejemplo de `SKILL.md`:

```markdown
---
name: example-skill
description: Describe cuándo usar esta skill y qué workflow aplica.
license: MIT
metadata:
  author: team
  version: "1.0"
---

# Example skill

## Cuándo usarla

Usa esta skill cuando...

## Procedimiento

1. Lee `references/guide.md`.
2. Aplica las reglas del equipo.
3. Verifica el resultado antes de responder.

## Restricciones

- No ejecutes comandos destructivos.
- No inventes datos.
```

## Skill incluida

El servidor incluye `Skills/sqlservermcp` con:

- `skill://sqlservermcp/SKILL.md`
- `skill://sqlservermcp/tools.json`

La skill viaja como contenido del proyecto, en el output de `dotnet publish` y dentro del
paquete NuGet (`tools/net11.0/any/Skills/...`). El mismo contenido se puede cargar desde
recursos embebidos activando `IncludeEmbedded`.

## Modos de carga

### A. Contenido del proyecto

Por defecto `Skills:IncludeProjectContent=true`. El proveedor lee
`AppContext.BaseDirectory/Skills` y trata cada subdirectorio inmediato con `SKILL.md` como
una skill. `Skills/**` se copia al output y al publish.

### B. Directorios externos

Configura uno o varios roots:

```json
{
  "Skills": {
    "ExternalDirectories": [
      "C:\\skills\\team",
      "/opt/skills"
    ]
  }
}
```

Cada root puede ser:

- un directorio de skill que contiene `SKILL.md`, o
- un directorio cuyos hijos inmediatos contienen `SKILL.md`.

Con `ReloadOnChange=true`, `SkillReloadService` vuelve a cargar los proveedores cada
`ReloadIntervalSeconds`; los clientes ven el nuevo snapshot sin reiniciar el servidor.

### C. Recursos embebidos

`IncludeEmbedded=true` lee los recursos con prefijo `Skills/` del ensamblado. El proyecto
embebe `Skills/**` con `LogicalName=Skills/...`, por lo que la skill incluida también está
disponible sin depender de archivos en disco.

### Combinación y conflictos

Las tres fuentes se combinan. Si dos fuentes publican el mismo `skill://.../SKILL.md`:

- `Fail` (default): falla el arranque/recarga con el origen duplicado.
- `FirstWins`: conserva la primera fuente en el orden de proveedores.
- `LastWins`: reemplaza con la última.

Los nombres no son identificadores globales; la identidad es el URI dentro del servidor.

## Configuración

`SqlServerMCP/appsettings.json`:

```json
{
  "Skills": {
    "Enabled": true,
    "IncludeProjectContent": true,
    "IncludeEmbedded": false,
    "ExternalDirectories": [],
    "ReloadOnChange": false,
    "ReloadIntervalSeconds": 30,
    "StrictValidation": true,
    "ConflictPolicy": "Fail",
    "PageSize": 50,
    "MaxFilesPerSkill": 512,
    "MaxTotalBytesPerSkill": 16777216,
    "ProjectContentDirectory": "Skills",
    "EmbeddedResourcePrefix": "Skills/",
    "IncludeHiddenAndSystemFiles": false
  }
}
```

También se admite la configuración estándar de .NET (`Skills__Enabled`,
`Skills__ExternalDirectories__0`, etc.) y los alias `MCP_SKILLS_*`:

| Variable | Default | Uso |
|---|---|---|
| `MCP_SKILLS_ENABLED` | `true` | Activa/desactiva toda la extensión |
| `MCP_SKILLS_INCLUDE_PROJECT_CONTENT` | `true` | Carga `Skills/` del output |
| `MCP_SKILLS_INCLUDE_EMBEDDED` | `false` | Carga recursos embebidos |
| `MCP_SKILLS_EXTERNAL_DIRECTORIES` | vacío | Roots separados por `;` o `,` |
| `MCP_SKILLS_RELOAD_ON_CHANGE` | `false` | Recarga periódica |
| `MCP_SKILLS_RELOAD_INTERVAL_SECONDS` | `30` | Intervalo de recarga |
| `MCP_SKILLS_STRICT_VALIDATION` | `true` | Falla o salta skills inválidas |
| `MCP_SKILLS_CONFLICT_POLICY` | `Fail` | `Fail`, `FirstWins`, `LastWins` |
| `MCP_SKILLS_PAGE_SIZE` | `50` | Paginación de listados (1-1000) |
| `MCP_SKILLS_MAX_FILES_PER_SKILL` | `512` | Límite por skill |
| `MCP_SKILLS_MAX_TOTAL_BYTES_PER_SKILL` | `16777216` | 16 MiB por skill |
| `MCP_SKILLS_PROJECT_CONTENT_DIRECTORY` | `Skills` | Directorio de contenido |
| `MCP_SKILLS_EMBEDDED_RESOURCE_PREFIX` | `Skills/` | Prefijo de recursos |
| `MCP_SKILLS_INCLUDE_HIDDEN_AND_SYSTEM_FILES` | `false` | Incluye dotfiles/ocultos |

`Skills:Enabled=false` no declara la extensión ni registra recursos de skills; las tools
existentes siguen funcionando.

## Cliente MCP

Comprueba la capacidad antes de llamar a los métodos:

```csharp
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Skills;
using ModelContextProtocol.Protocol;

if (!client.ServerCapabilities.SupportsSkills())
{
    return; // Cliente sin soporte: usa las tools normales.
}

IList<Skill> skills = await client.ListSkillsAsync();
Skill skill = await client.GetSkillAsync("skill://sqlservermcp/SKILL.md");
ReadResourceResult contents = await client.ReadSkillResourceAsync(skill, skill.Uri);
```

Equivalentes JSON-RPC:

```json
{ "jsonrpc": "2.0", "id": 1, "method": "skills/list", "params": {} }
```

```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "skills/get",
  "params": { "uri": "skill://sqlservermcp/SKILL.md" }
}
```

```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "resources/read",
  "params": { "uri": "skill://sqlservermcp/SKILL.md" }
}
```

`skills/list` devuelve el frontmatter y un manifiesto completo con `uri`, `digest`
(`sha256:...`) y `size` de cada archivo. El cliente debe verificar el tamaño y el digest al
leer, y comparar el frontmatter del `SKILL.md` con la entrada antes de cargarlo. Un archivo
que no figure en el manifiesto no debe leerse bajo esa aprobación.

## Seguridad

- **Path traversal**: `resources/read` solo resuelve URIs presentes en el snapshot actual;
  no hay resolución de rutas arbitrarias en tiempo de request.
- **Symlinks/reparse points**: se rechazan al leer directorios.
- **Archivos ocultos/system/dotfiles**: excluidos por defecto.
- **Límites**: máximo de archivos y bytes por skill; los defaults siguen la recomendación
  de la especificación (512 archivos / 16 MiB).
- **Scripts**: se sirven como archivos, nunca se ejecutan.
- **Contenido no confiable**: una skill es texto de instrucciones; el host debe aplicar
  aprobación y tratar el contenido como input no confiable.
- **Logs**: solo se registran orígenes y mensajes de error; no se registran credenciales.
- **Sin autorización automática**: `resources/list`/`skills/list` no conceden acceso a
  archivos; el cliente debe aplicar su propia política y el servidor debe proteger recursos
  si se añaden fuentes autenticadas.

## Límites y compatibilidad

- `resources/directory/read` no está implementado por el SDK 2.4.0; el servidor no declara
  `directoryRead`. Los clientes deben obtener la lista de archivos del manifiesto de
  `skills/list`/`skills/get`.
- Clientes sin la extensión ignoran `skills/list`/`skills/get` y siguen usando `tools/list`
  y las tools convencionales.
- `resources/list` incluye los archivos de las skills; `skills/list` es la fuente
  autoritativa para descubrir skills.
- Los nombres de skill son etiquetas; la identidad es el par servidor + URI. Dos servidores
  pueden servir `skill://sqlservermcp/SKILL.md` sin colisionar entre sí.
- La extensión es experimental en el SDK (`MCPEXP005`); la especificación SEP-2640 es
  Final, pero la implementación del SDK puede evolucionar.

## Release, publish y Docker

```powershell
dotnet publish SqlServerMCP/SqlServerMCP.csproj -c Release -o .\out
```

Verifica que existan:

- `out/Skills/sqlservermcp/SKILL.md`
- `out/Skills/sqlservermcp/tools.json`
- `out/appsettings.json`

Docker:

```powershell
docker build -t jonasrq/databasemcp:local .
docker run --rm -i -e SQLSERVER_PASSWORD=dummy jonasrq/databasemcp:local --mode stdio
```

La imagen copia `Skills/` y `appsettings.json` al runtime. En HTTP, el contenedor escucha en
`http://0.0.0.0:8080` (`MCP_HTTP_URL`).

## Pruebas

```powershell
dotnet test SqlServerMCP.sln -c Debug
```

La suite cubre descubrimiento project/external/embedded, manifiestos, lectura de archivos,
límites, duplicados, nombres inválidos, path traversal, recarga, paginación, configuración,
protocolo in-memory (`skills/list`, `skills/get`, `resources/read`, `tools/list`) y
compatibilidad con tools existentes.

## Referencias

- SEP-2640 Skills Extension:
  https://modelcontextprotocol.io/seps/2640-skills-extension
- Especificación de la extensión:
  https://modelcontextprotocol.io/extensions/skills/overview
- Agent Skills:
  https://agentskills.io/specification
- MCP C# SDK:
  https://csharp.sdk.modelcontextprotocol.io/
