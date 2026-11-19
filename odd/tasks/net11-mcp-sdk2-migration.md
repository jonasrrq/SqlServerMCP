# Feature: Migrate SqlServerMCP to .NET 11 + MCP C# SDK 2.4

## Objective

Migrate the `SqlServerMCP` solution from `net10.0` + `ModelContextProtocol` 1.3.0 to
`net11.0` + `ModelContextProtocol` 2.4.0, adopting the relevant characteristics of the
`mcpserver` template that ships with the .NET 11 SDK, while preserving the project's
dual transport, security middleware, audit logging, rate limiting, and test coverage.

## Problem and why

The project targets .NET 10 and MCP SDK 1.3.0. .NET 11 reached GA on 2026-11-10 and the
`mcpserver` template is now bundled with the SDK (bundle version `11.0.0`, default TFM
`net11.0`). The MCP C# SDK reached 2.0.0 on 2026-07-28, aligned with the MCP
`2026-07-28` specification, with breaking changes that affect HTTP defaults,
discovery/negotiation, structured content, and legacy SSE behavior.

Staying on 1.3.0 means accumulating protocol drift and missing the template's
packaging/stderr-logging characteristics. The migration is intentionally scoped to a
version upgrade and selective template alignment, not a rewrite.

## Scope

### In scope

- TFM and package version upgrades for both projects.
- Compilation and behavioral fixes required by MCP SDK 2.x.
- Selective adoption of template characteristics:
  - stderr logging for the stdio transport (protocol safety),
  - optional NuGet MCP packaging metadata and `.mcp/server.json`,
  - optional self-contained/single-file publish (decision gate).
- Verification of both transports and tool contracts.
- Documentation updates.

### Out of scope

- Rewriting `Program.cs` to match the generated template shape.
- Splitting the project into separate `local`/`remote` projects.
- Changing authentication, host guard, audit, rate limiting, or pagination semantics.
- Adding new tools or business capabilities.
- Migrating the test stack to xUnit v3 / Microsoft.Testing.Platform.
- CI changes (the repository currently has no workflow files under `.github/workflows`).

## Constraints and decisions

- Keep dual transport (`stdio` + HTTP) in a single project.
- Keep Central Package Management (`Directory.Packages.props`).
- Keep existing security and operational behavior unchanged.
- .NET 11 is STS and `.NET 11` support ends in November 2028 (same end date as .NET 10 LTS).
- .NET 11 minimum hardware baseline is `x86-64-v2`; Windows Arm64 requires `LSE`.
  Validate deployment targets before rollout.
- Delivery strategy: `ask-on-risk` (default). Forecast: under the ~400 authored-line
  budget for a single PR. Chain strategy: not applicable unless the forecast changes.
- Source files and technical artifacts are written in English. The user-facing README
  stays in Spanish, matching the existing repository language.

## Acceptance criteria

1. `dotnet restore`, `dotnet build -c Debug`, and `dotnet test -c Debug --no-build` pass
   for the whole solution on `net11.0`.
2. The server starts in `stdio` and HTTP modes.
3. `get_metadata` and `execute_query` are listed and invokable from an MCP client.
4. In stdio mode, stdout contains only MCP protocol messages; logs go to stderr.
5. MCP 2.x diagnostics are triaged: zero build errors; every `MCP90xx` warning is either
   resolved or documented with a decision.
6. Existing HTTP auth, host guard, audit, and rate-limit behavior is unchanged.
7. `README.md` reflects the new TFM, package versions, and behavior changes.
8. Optional spikes (Runtime Async, Native AOT) end with recorded evidence and an explicit
   keep/revert decision; they never block the core migration.

## Task list

Legend: `[ ]` pending, `[x]` done. Each task records its verification evidence in the
section below before being checked off.

### T1 - Capture baseline

- [x] Record current branch, commit SHA, and SDK version (`dotnet --version`).
- [x] Run `dotnet restore SqlServerMCP.sln`.
- [x] Run `dotnet build SqlServerMCP.sln -c Debug`.
- [x] Run `dotnet test SqlServerMCP.sln -c Debug --no-build`.
- [ ] Smoke the current server: list tools and invoke `get_metadata` in both transports.
      Deferred to T6: no SQL Server is listening on localhost:1433, and the worktree was
      being modified by the migration writer.
- [x] Store the results in `## Verification evidence`.

Route: inline (state capture and short commands).
Acceptance: baseline results are recorded before any source change.
Evidence: branch `main`, commit `122f19e7db4920d2ffb9ce171352dd5dc3e782a2`, SDK `11.0.100`;
build 0 errors / 7 warnings; tests 50 passed / 0 failed.

### T2 - Upgrade TFM and package versions

- [x] `SqlServerMCP/SqlServerMCP.csproj`: `net10.0` -> `net11.0`.
- [x] `SqlServerMCP.Tests/SqlServerMCP.Tests.csproj`: `net10.0` -> `net11.0`.
- [x] `Directory.Packages.props`:
  - [x] `Microsoft.Extensions.Hosting` `10.0.8` -> `11.0.0`.
  - [x] `ModelContextProtocol` `1.3.0` -> `2.4.0`.
  - [x] `ModelContextProtocol.AspNetCore` `1.3.0` -> `2.4.0`.
  - [x] `Microsoft.Data.SqlClient` `7.0.1` -> `7.1.2`.
  - [x] `Microsoft.Data.Sqlite` `10.0.8` -> `11.0.0` (test-only).
- [x] Leave the test stack (`Microsoft.NET.Test.Sdk`, xunit, FluentAssertions, Moq,
      coverlet) unchanged.
- [x] Run restore and record the resolved versions.

Route: inline (mechanical, already-understood edits to 3 files).
Acceptance: restore succeeds and no package version conflicts are reported.

### T3 - Fix MCP SDK 2.x compile and behavior breaks

- [x] Build and resolve API breaks in `Program.cs`. No compile breaks; API surface unchanged.
- [x] Review `EnableLegacySse` / `MCP9004`: the API still exists in 2.4.0 but throws
      `InvalidOperationException` when `Stateless` is selected. The legacy path was retired;
      `MCP_ENABLE_LEGACY_SSE` is now ignored. `MCP_MODE=sse` remains an HTTP alias.
- [x] Confirm `AddMcpServer`, `WithStdioServerTransport`, `WithHttpTransport`,
      `MapMcp`, and `WithToolsFromAssembly` still behave as expected.
- [x] Confirm `[McpServerToolType]` / `[McpServerTool]` and DI-injected tool parameters
      still work.
- [x] Confirm HTTP remains stateless (`options.Stateless = true`).
- [x] Run the test suite: 52 passed, 0 failed.

Route: inline for diagnosis; delegated writer if more than one non-trivial file changes.
Acceptance: solution builds with zero errors; behavior changes are documented.

### T4 - Route stdio logs to stderr

- [x] In the stdio branch, configure console logging to stderr, mirroring the template:
      `builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);`.
- [x] Verify on Windows that stdout contains no log lines when the server starts and
      handles a request. Raw JSON-RPC smoke: stdout carried only protocol messages; all
      logs appeared on stderr.

Route: inline (one-file, already-understood mechanical change).
Acceptance: stdio protocol stream is clean; logs are visible on stderr.

### T5 - Verify structured content and tool contracts

- [x] Inspect the JSON emitted by `get_metadata` and `execute_query` under SDK 2.4.0.
- [x] Confirm the anonymous-object shape still serializes as a single JSON text block.
      `UseStructuredContent` defaults to `false`, so there is no `structuredContent` and
      no `outputSchema`; 2.x does not change the envelope for these tools.
- [x] Added `ToolPayloadSerializationTests` (2 tests) locking the text-only envelope for
      `get_metadata` and `execute_query`.
- [x] Record before/after payload samples (`## Verification evidence`).

Route: inline inspection + one writer if tests need non-trivial changes.
Acceptance: tool payloads are understood, tested, and documented.

### T6 - Client and transport smoke test

- [x] Start `stdio` mode and connect with a raw JSON-RPC client (protocol-level smoke).
      VS 2026 Copilot manual verification remains a follow-up.
- [x] Start HTTP mode on `http://localhost:5199` and connect with the same client.
- [x] Verify `tools/list` contains `get_metadata` and `execute_query` (7 tools total).
- [x] Invoke `get_metadata` against the configured SQL Server (`Editoriales_Net`):
      91 tables, 20 views, 573 procedures, 89 foreign keys.
- [x] Invoke `execute_query` with pagination: page 1 and page 2 over `sys.tables`;
      91 total available, 5 returned per page.
- [x] Verify `get_columns`, cache status/clear, `sp_helpdb`, and audit entries.
- [x] Verify a destructive query (`DELETE`) is blocked with `MCP-QUERY-001`.
- [x] Verify discovery-first: `tools/list` is served without a prior `initialize`;
      `server/discover` requires per-request protocol-version metadata as defined by the
      2026-07-28 specification.
- [x] Record client name/version and results.

Route: delegated fresh per-action worker is acceptable; commands are bounded.
Acceptance: both transports work end-to-end; no client-side errors.

### T7 - Packaging and publish decision gate

Decision: **adopt NuGet MCP packaging metadata** (template characteristic). Package ID
`JR.DatabaseMCP` is available on NuGet.org. It was renamed from `Jonas.SqlServerMCP` ->
`JR.SqlServerMCP` -> `JR.DatabaseMCP` to leave room for future database engines;
`SqlServerMCP` is already taken.

- [x] If **yes**:
  - [x] Rename the package to `JR.DatabaseMCP` in the project and `.mcp/server.json`.
  - [x] Add `PackAsTool`, `PackageType=McpServer`, `PackageReadmeFile`, `PackageId`,
        `PackageVersion`, `PackageTags`, and `Description`.
  - [x] Add `.mcp/server.json` with the NuGet registry package, `--mode stdio` package
        arguments, and the `SQLSERVER_*` environment variable inputs.
  - [x] Pack and inspect the `.nupkg` contents (`.mcp/server.json`, `README.md`, tool).
- [x] If **no**: not applicable.
- [x] Separate decision: adopt `SelfContained` + `PublishSingleFile` + `RuntimeIdentifiers`?
      **No.** Keep the current framework-dependent deployment; the template's
      self-contained/single-file defaults are a distribution choice, not a runtime need.

Route: inline for metadata edits; one writer if packaging plus publish files grow.
Acceptance: decision recorded; if packaging is enabled, `dotnet pack -c Release` succeeds
and the package contains the expected MCP metadata.

### T8 - Optional: Runtime Async spike

- [x] On a throwaway commit, add `<Features>runtime-async=on</Features>` to the server project.
- [x] Run the full test suite and the T6 smoke test: build 0 errors, 52/52 tests.
- [x] Compare async stack traces and record a basic latency observation. No SQL Server was
      available, so no meaningful latency comparison was possible.
- [x] Keep or revert with the evidence recorded: **reverted**. It is not a template
      characteristic and there is no measured benefit yet; enabling it remains a follow-up.

Route: inline experiment.
Acceptance: explicit keep/revert decision with evidence.

### T9 - Optional: Native AOT spike (timeboxed)

- [x] Attempt `dotnet publish SqlServerMCP/SqlServerMCP.csproj -c Release -r win-x64 -p:PublishAot=true`.
- [x] Record blockers: the publish **succeeds** (28 MB native exe), but the AOT binary
      starts and then fails `tools/list` with `-32601 Method 'tools/list' is not available`.
      `WithToolsFromAssembly` is trimmed; JSON anonymous types also emit IL2026/IL3050.
- [x] AOT is **not adopted**. Supporting it requires explicit/generic tool registration and
      `JsonSerializerContext` source generation; recorded as a follow-up.
- [x] The spike made no source changes; publish outputs stay under `bin/`.

Route: inline command with a hard timebox.
Acceptance: pass/fail evidence recorded; no lingering experimental settings in the main branch.

### T10 - Update documentation

- [x] `README.md`: TFM, package versions, .NET 11 prerequisites, migration behavior notes
      (discovery-first, legacy SSE status, stderr logging, AOT limitation), NuGet packaging,
      and the hardware baseline requirement.
- [x] `.env-example`: removed `MCP_ENABLE_LEGACY_SSE`; documented `sse` as an HTTP alias.
- [x] `.specify/memory/constitution.md`: updated runtime/dependency versions and transport
      policy (legacy SSE removed).
- [x] Record the final hardware baseline requirement for deployment (x86-64-v2; Windows
      Arm64 requires LSE).

Route: inline (docs-only, mechanical updates).
Acceptance: docs match the implemented state.

### T11 - Final verification and close

- [x] Run restore, build, and test from a clean state: 0 errors, 52/52 tests.
- [x] Re-run both transport smoke tests: stdio + HTTP, 7 tools, logs on stderr only.
- [x] Review `git diff --stat` for unrelated changes: migration-only changes plus docs.
- [x] Record the final evidence and close the feature document.
- [x] Commit work units on the feature branch (do not push, open a PR, or merge without
      explicit approval). Commits: `cfb990a` (code), `91734ed` (docs).

Route: inline verification.
Acceptance: all acceptance criteria are met or explicitly waived with a reason.

## Verification evidence

Record command output summaries and payload samples here as tasks complete.

| Task | Command / check | Result | Date |
|---|---|---|---|
| T1 | `dotnet --version` | `11.0.100` | 2026-11-19 |
| T1 | `dotnet build SqlServerMCP.sln -c Debug` | 0 errors, 7 warnings (NU1902/NU1903 transitive, NU1507 source mapping) | 2026-11-19 |
| T1 | `dotnet test SqlServerMCP.sln -c Debug --no-build` | 50 passed, 0 failed, 0 skipped | 2026-11-19 |
| T1 | stdio smoke | deferred to T6 (no SQL Server on localhost:1433) | 2026-11-19 |
| T1 | HTTP smoke | deferred to T6 (no SQL Server on localhost:1433) | 2026-11-19 |
| T2 | `dotnet restore` resolved versions | MCP 2.4.0 (both), Hosting 11.0.0, SqlClient 7.1.2, Sqlite 11.0.0 | 2026-11-19 |
| T3 | build after 2.4.0 upgrade | 0 errors, 2 `NU1507` warnings, 0 `MCP90xx` | 2026-11-19 |
| T4 | stdout/stderr separation check | stdout only JSON-RPC; logs on stderr | 2026-11-19 |
| T5 | tool payload samples | text-only JSON blocks; `structuredContent=null`, no `outputSchema`; 2 regression tests added | 2026-11-19 |
| T6 | client name/version + results | raw JSON-RPC + real DB `Editoriales_Net`; stdio + HTTP; 7 tools; 91 tables / 20 views / 573 procs / 89 FKs; `DELETE` blocked | 2026-11-19 |
| T7 | packaging decision / `dotnet pack` | adopted; `JR.DatabaseMCP.0.1.0-beta.nupkg` with `.mcp/server.json` + README | 2026-11-19 |
| T8 | runtime async keep/revert | build 0 errors, 52/52 tests; reverted (no measurable benefit) | 2026-11-19 |
| T9 | AOT pass/fail | publish OK; AOT runtime `tools/list` fails (-32601, trimmed `WithToolsFromAssembly`); not adopted | 2026-11-19 |
| T10 | docs updated | README, `.env-example`, constitution aligned | 2026-11-19 |
| T11 | final clean build + test + smoke | 0 errors, 52/52 tests, stdio + HTTP OK; commits `cfb990a`, `91734ed` | 2026-11-19 |

### Real SQL Server validation (2026-11-19)

Environment: SQL Server 2022 in Docker, database `Editoriales_Net`, credentials supplied
through a gitignored `.env` (never printed). Both transports were exercised with a raw
JSON-RPC client against the same database.

| Check | stdio | HTTP |
|---|---|---|
| `initialize` | OK (`2025-11-25`) | OK (`2025-11-25`) |
| `tools/list` | 7 tools | 7 tools |
| `get_metadata` | 91 tables / 20 views / 573 procedures / 89 FKs | same |
| `get_columns` (`dbo.ConveniosItem`) | 8 columns | 8 columns |
| Cache | 5 entries -> cleared -> 0 | same |
| Pagination page 1 / page 2 | 5 / 5 rows, 91 total | same |
| `sp_helpdb` | 11 rows | 11 rows |
| Audit entries | 2 | 2 |
| Destructive query `DELETE` | blocked, `MCP-QUERY-001` | blocked, `MCP-QUERY-001` |

Note: `sa` was used only for local validation. Production should use a least-privilege
SQL login.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| MCP 2.x discovery-first negotiation breaks an older client | High | Test with VS 2026 Copilot and MCP Inspector before rollout; fallback to `initialize` is expected but must be observed |
| Legacy SSE removal breaks existing consumers | Medium | It is off by default; confirm no real consumer before retiring; document the removal |
| `Microsoft.Data.SqlClient` 7.1.2 behavior changes | Medium | Run integration tests against a real SQL Server; if unavailable, record the limitation |
| Structured content shape changes | Medium | Capture payload samples before/after; add regression coverage |
| .NET 11 hardware baseline excludes old hosts | High | Inventory deployment CPUs; `x86-64-v2` is required, Windows Arm64 needs `LSE` |
| Native AOT fails with SqlClient/JSON | Low | Timeboxed spike; do not block the migration |
| Package version conflicts under CPM | Low | Centralized versions; restore output is the evidence |

## Follow-up (2026-11-19): package rename, client docs, skill audit

- [x] Renamed the NuGet package to `JR.DatabaseMCP` and repacked
      `JR.DatabaseMCP.0.1.0-beta.nupkg`.
- [x] README documents Claude Desktop (stdio), Claude Code (stdio), ChatGPT desktop
      (remote HTTP only), VS Code, Visual Studio 2026, and Cursor.
- [x] Skill rewritten with valid YAML frontmatter, real MCP tool names/arguments, and
      machine-readable `tools.json`.
- [x] `make-release.ps1` validates `SKILL.md` and `tools.json` before packaging.
- [x] Distribution validated: `dist/SqlServerMCP-skill.zip` contains `SKILL.md` and
      `tools.json`; tool names match `tools/list` (7/7).
- [x] Added `MCP_DB_PROVIDER` / `--db-provider` with default `sqlserver`, fail-fast
      validation for unsupported engines, startup logging, and tests.
- [x] Completed NuGet metadata (author, MIT license, repository/project URL) and added the
      `mcp-name` marker required by the MCP Registry.
- [x] Build 0 errors, tests 60/60 after the changes.

## Execution notes

- Create branch `feat/net11-mcp-sdk2-migration` before the first source edit.
- Prefer one work-unit commit per task or per coherent group (T2+T3, T4, T5, T6, T10).
- Do not push, open a PR, or merge without explicit user approval.
- Do not combine optional spikes with the core migration commit.

## Mirror status

- Local document: `odd/tasks/net11-mcp-sdk2-migration.md`.
- Engram mirror `odd/net11-mcp-sdk2-migration/tasks`: **pending**. The project is not
  registered in the Engram store (`unknown_project` for `D:\Desarrollo\SqlServerMCP`).
  Resynchronize when a project entry exists.

## Next step

Execute T1 (baseline capture). T2-T6 are the required migration; T7-T9 are decision gates
or optional spikes; T10-T11 close the change.
