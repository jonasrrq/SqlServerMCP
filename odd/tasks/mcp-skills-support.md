# Feature: MCP Skills distribution for the database MCP server

## Objective

Add first-class Agent Skills distribution to the MCP server using the official
`io.modelcontextprotocol/skills` extension (SEP-2640), so any compatible MCP client can
discover skills, retrieve their metadata, and read `SKILL.md` and supporting files through
the server. The system is generic and independent of the SQL Server domain.

## Research findings

- MCP C# SDK `ModelContextProtocol.Extensions.Skills` **2.4.0** implements SEP-2640:
  `skills/list`, `skills/get`, `resources/read`, capability declaration, manifests with
  SHA-256 digests/sizes, frontmatter parsing, and resource authoring.
- The APIs are experimental (`MCPEXP005`). The official sample suppresses
  `MCPEXP001;MCPEXP005`.
- The SDK does **not** implement `resources/directory/read` or declare `directoryRead`.
  That optional part of the extension is out of scope and will be documented.
- The SDK can author skills from directories/files (`McpServerSkillPackage`) and register
  fixed packages, but its built-in registration snapshots resources at startup. To support
  external reload, this implementation uses `WithSkills` custom list/get handlers plus
  custom resource list/read handlers backed by a mutable `SkillRegistry`.
- Existing tools are registered with `WithToolsFromAssembly`; skills resources are new and
  do not replace tools.

## Architecture

```
Skills/ (content) ─┐
Embedded resources ├─> ISkillProvider ─> SkillRegistry ─┬─> skills/list, skills/get
External dirs ─────┘                                  ├─> resources/list, resources/read
                                                       └─> periodic reload (optional)
```

- `SkillRegistry` holds an immutable snapshot of validated `McpServerSkillPackage` entries
  and their file bytes, swapped atomically on reload.
- `ProjectContentSkillProvider` reads `AppContext.BaseDirectory/Skills`.
- `ExternalDirectorySkillProvider` reads configured roots; each immediate child with
  `SKILL.md` is a skill (a root containing `SKILL.md` is also accepted).
- `EmbeddedSkillProvider` reads `Skills/...` embedded resources.
- Validation uses the SDK package authoring (frontmatter, URI, manifest, safe file paths)
  plus local name, limits, duplicate, and path checks.
- `SkillOptions` binds from `appsettings.json` (`Skills` section) and environment variables
  (`Skills__*`, plus `MCP_SKILLS_*` aliases).

## Scope

### In scope

- `ModelContextProtocol.Extensions.Skills` 2.4.0 and `MCPEXP005` opt-in.
- Project-content, external-directory, and embedded skill providers.
- Central `SkillRegistry` with duplicate/conflict handling, limits, reload, and safe reads.
- `skills/list`, `skills/get`, `resources/list`, `resources/read` through the official SDK.
- Configuration in `appsettings.json` and environment variables.
- Security: name validation, path traversal protection, symlink/reparse rejection, no script
  execution, file reads limited to the current manifest.
- Unit and in-memory protocol tests.
- Release publish and Docker verification.
- Documentation and an example skill served by the server.

### Out of scope

- `resources/directory/read` / `directoryRead` (not implemented by the SDK; optional).
- Nested-skill activation UX and host-side approval (host responsibility per spec).
- OAuth/authorization changes.
- New business-domain tools or SQL Server changes.
- FileSystemWatcher-based instant reload; periodic reload is used instead.

## Tasks

### T1 - Package and configuration foundation

- [x] Add `ModelContextProtocol.Extensions.Skills` 2.4.0 to CPM and both projects.
- [x] Suppress `MCPEXP001;MCPEXP005` where the Skills API is intentionally consumed.
- [x] Add `appsettings.json` with the `Skills` section and copy-to-output/publish.
- [x] Add `SkillOptions` with appsettings + `Skills__*` + `MCP_SKILLS_*` binding.
- [x] Add `SkillConflictPolicy` and validation/limits.

### T2 - Safe file and embedded providers

- [x] Add `SkillNameValidator` (Agent Skills name rules + directory/name match).
- [x] Add a safe directory reader: no symlinks/reparse points, no hidden/system/dot files by
      default, bounded file count/total bytes, relative path validation.
- [x] Add `ProjectContentSkillProvider`, `ExternalDirectorySkillProvider`, and
      `EmbeddedSkillProvider` behind `ISkillProvider`.
- [x] Add `SkillSource`/`SkillFileSource` models with MIME type and digest metadata.

### T3 - SkillRegistry

- [x] Build validated packages with `McpServerSkillPackage.Create` and explicit URIs.
- [x] Detect duplicate URIs and apply `Fail` / `FirstWins` / `LastWins`.
- [x] Expose `ListSkillsAsync`, `GetSkillAsync`, `ListResourcesAsync`, `ReadResourceAsync`,
      and `ReloadAsync`.
- [x] Paginate `skills/list` and `resources/list`.
- [x] Return `InvalidParams` for unknown skill/resource URIs.
- [x] Keep an immutable snapshot and swap it atomically on successful reload.

### T4 - MCP integration

- [x] Add `WithSkillDistribution` using `WithSkills` custom handlers and
      `WithListResourcesHandler` / `WithReadResourceHandler`.
- [x] Wire the registry into both HTTP and stdio branches of `Program`.
- [x] Register the reload background service when `ReloadOnChange` is enabled.
- [x] Keep existing tools and transports unchanged when skills are disabled.

### T5 - Built-in skill content

- [x] Add `Skills/sqlservermcp/SKILL.md` and `tools.json` as project content.
- [x] Include `Skills/**` in output, publish output, and the NuGet tool package.
- [x] Embed the same content for the optional embedded provider.
- [x] Verify the skill URI is `skill://sqlservermcp/SKILL.md`.

### T6 - Tests

- [x] Unit tests: project/external/embedded discovery, metadata, manifests, limits,
      duplicates, invalid names, missing files, path traversal, reload.
- [x] Protocol tests over in-memory pipes: `skills/list`, `skills/get`,
      `resources/read`, verified file reads, unknown URI errors, and tools compatibility.
- [x] Release publish check: `Skills/` and `appsettings.json` are present.
- [x] Docker check: container starts and `skills/list` returns the built-in skill.

### T7 - Documentation

- [x] `docs/skills.md`: architecture, creating skills, directory layout, project/embedded/
      external loading, configuration, client discovery/read examples, security, limits, and
      `directoryRead` compatibility limitation.
- [x] README section linking to the guide and the built-in skill URI.

### T8 - Final verification

- [x] `dotnet build` and `dotnet test` green.
- [x] Protocol smoke against a real server process (stdio and/or HTTP).
- [x] Package/publish content check.
- [x] Plan evidence updated; work-unit commits on the feature branch.

## Acceptance criteria

1. A compatible client sees `io.modelcontextprotocol/skills` in server capabilities.
2. `skills/list` returns the built-in skill with frontmatter and a complete manifest.
3. `skills/get` returns the same entry by URI and `InvalidParams` for unknown URIs.
4. `resources/read` returns `SKILL.md` and supporting files; manifest digests/sizes match.
5. Project content works in Debug, Release publish, and the NuGet tool package.
6. External directories load skills and can be reloaded without restart.
7. Embedded skills load when enabled and can be combined with other providers.
8. Path traversal, invalid names, symlinks, duplicate URIs, and missing files are rejected
   or handled according to policy.
9. Existing tools and transports continue to work when skills are enabled or disabled.
10. Docs explain configuration, creation, discovery, security, and limitations.

## Verification evidence

| Check | Result | Date |
|---|---|---|
| SDK/API research | `ModelContextProtocol.Extensions.Skills` 2.4.0; SEP-2640 Final; `MCPEXP005` | 2026-11-19 |
| Spec re-check before close | `skills/list`, `skills/get`, `resources/read`; 512 files / 16 MiB; final URI segment = frontmatter `name` | 2026-11-19 |
| Build/tests | Release build 0 errors; `dotnet test` 79/79 passed (Debug and Release) | 2026-11-19 |
| Protocol skills/list/get/read | Real stdio: capability `io.modelcontextprotocol/skills`, `resources`; list/get/read OK; 7 tools preserved | 2026-11-19 |
| Release publish content | `appsettings.json` and `Skills/sqlservermcp/{SKILL.md,tools.json}` in publish output and in `JonasRq.DatabaseMCP.0.2.0-beta.nupkg` | 2026-11-19 |
| Docker skills/list | Image `jonasrq/databasemcp:0.1.0-beta` starts in stdio; `skills/list` returns `skill://sqlservermcp/SKILL.md`; tools intact | 2026-11-19 |
| Version | Package bumped to `0.2.0-beta` (0.1.0-beta already published) | 2026-11-19 |

## Progress

- Route: delegated/direct hybrid with the parent as sole writer; tests and protocol smoke run as
  bounded actions after each work unit. No SDD state was created.
- Work-unit commits: recorded in the feature branch history (`git log --oneline`).
- Final state: feature complete, docs updated, Docker verified; not pushed and no PR created.

## Mirror status

- Local document: `odd/tasks/mcp-skills-support.md`.
- Engram mirror: unavailable in this runtime (Engram MCP tools are not exposed after the
  server restart). Local document is authoritative; resynchronize when available.
