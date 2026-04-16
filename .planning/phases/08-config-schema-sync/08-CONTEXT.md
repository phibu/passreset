# Phase 8: Configuration Schema & Sync - Context

**Gathered:** 2026-04-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Establish `appsettings.Production.json` as a schema-governed artifact: valid JSON (no inline comments), backed by an authoritative machine-readable schema, validated at install-time and startup, and safely synced on upgrade without destroying operator overrides. Scope: STAB-007..012 (gh#22, #24, #25, #26, #27, #37). Two surfaces: PowerShell deploy scripts (`deploy/Install-PassReset.ps1`) and the ASP.NET Core host (`src/PassReset.Web/Program.cs` + options classes). No new settings keys are introduced; no secret-storage work (that's Phase 9 STAB-017 / Phase 13 V2-003).

</domain>

<decisions>
## Implementation Decisions

### STAB-008 — Authoritative schema (gh#27)
- **D-01**: Schema format is **JSON Schema Draft 2020-12**, checked in as `src/PassReset.Web/appsettings.schema.json` (co-located with template so it ships in publish output).
- **D-02**: Schema is the **single source of truth**. `appsettings.Production.template.json` is a fully-populated example that MUST validate against the schema. CI enforces this.
- **D-03**: Schema metadata in scope for v1.4.0: `required`, `default`, and custom `x-passreset-obsolete: true` marker for deprecated keys. **Out of scope for v1.4.0:** `description` / `examples` / auto-generated docs (future enhancement).
- **D-04**: Validation scope is **structural + types + enums only** (keyword set: `type`, `required`, `enum`, `pattern`, `minimum`/`maximum`, `default`). Cross-field semantic rules (e.g., "if `Recaptcha.Enabled` then `SiteKey` required") live in C# `IValidateOptions<T>`, not in the schema. Draft 2020-12 `if/then/else` is deliberately avoided to keep PowerShell `Test-Json` compatible.

### STAB-009 — Pre-flight validation (gh#25)
- **D-05**: Validation runs in **three places**: install-time (PowerShell via `Test-Json -Schema`), startup (ASP.NET Core `IValidateOptions<T>` + `ValidateOnStart()`), and CI (template validated against schema on every push).
- **D-06**: **No standalone `passreset validate` CLI in v1.4.0.** Operators validate by running `Install-PassReset.ps1 -Reconfigure -WhatIf` or by restarting IIS and checking the Event Log. Reconsider in a later milestone if operators ask for it.
- **D-07**: Startup failure mode: **fail fast.** `ValidateOnStart()` throws at DI container build; ASP.NET Core module returns 502; full error (field path + reason + remediation) is written to Windows Application Event Log under source `PassReset`.
- **D-08**: Error message format: `{field.path}: {reason} (got "{actual}"). Edit {live config path} or run Install-PassReset.ps1 -Reconfigure.` Example: `PasswordChangeOptions.PortalLockoutThreshold: must be integer >= 1 (got "three"). Edit C:\inetpub\PassReset\appsettings.Production.json or run Install-PassReset.ps1 -Reconfigure.`

### STAB-010 — Safe upgrade sync (gh#24)
- **D-09**: Default sync behavior on upgrade is **additive-merge**: walk the schema, add any key missing from live config using the schema's `default`, write back. **Never modify existing values.** **Never auto-remove obsolete keys.**
- **D-10**: Sync is **deep**: missing nested keys at any depth are added (not just top-level sections). If a nested section exists, missing children inside it are still added. If an operator removed a key that the schema declares, sync **re-adds it from the default** — schema is authoritative about what must exist.
- **D-11**: Obsolete keys (schema `x-passreset-obsolete: true`): upgrade detects them and **prompts per-key** in interactive mode (`Remove obsolete key 'X'? [Y/N]`, default N). In `-Force` / unattended mode, obsolete keys are **reported but never removed** (safe default). No `-ConfigSync Full` mode in v1.4.0.

### STAB-011 — Explicit sync controls (gh#26)
- **D-12**: New installer parameter **`-ConfigSync <Merge|Review|None>`**:
  - `Merge` (default on upgrade) — additive-merge per D-09/D-10, obsolete keys reported only.
  - `Review` — interactive per-key prompt for every added or obsolete key.
  - `None` — skip sync entirely; legacy behavior for operators who manage config themselves.
- **D-13**: When `-ConfigSync` is not supplied AND an upgrade is detected AND session is not `-Force`: installer **prompts interactively** — `Config sync: [M]erge additions / [R]eview each / [S]kip? [M]`. `-Force` without `-ConfigSync` defaults to `Merge`.
- **D-14**: **No `-WhatIf` dry-run in scope** for v1.4.0 (SupportsShouldProcess is inherited from the installer but sync-specific dry-run is out of scope — revisit if operators ask). **No automatic backup file** in v1.4.0 — upgrade's file-preservation via robocopy `/XF` already keeps the last-committed state recoverable.

### STAB-007 — Valid JSON template (gh#22)
- **D-15**: `appsettings.Production.template.json` becomes **pure JSON** — strip all `//` comments. Operator-facing documentation moves entirely to `docs/appsettings-Production.md` (already exists; expand it).
- **D-16**: No runtime comment-stripping code path in installer or C#. The template ships as valid JSON; production copy is valid JSON; all parsers (PowerShell `ConvertFrom-Json`, `Test-Json`, ASP.NET `JsonConfigurationProvider`) work without special handling.

### STAB-012 — Drift check tolerates current config (gh#37)
- **D-17**: Schema-drift check at `Install-PassReset.ps1:755-794` is **rewritten on top of D-02/D-15**. It now: (a) reads the schema (not the template) to enumerate required keys, (b) reads live production config (guaranteed comment-free post-D-15), (c) reports missing keys + obsolete keys. The current template-based comparison is removed.
- **D-18**: Drift check **must run even when live config is JSON-valid but missing keys** — current failure mode is that it silently skips when the live file parses successfully. New implementation always runs the schema pass.

### Claude's Discretion
- PowerShell JSON Schema library: `Test-Json -Schema` is built into PowerShell 6+; on Windows PowerShell 5.1 the cmdlet lacks `-Schema` support. Planner decides whether to (a) require PowerShell 7.x at install time, or (b) use a shim via `dotnet passreset validate` / embedded .NET call. Capture as a research item before planning.
- C# JSON Schema library for startup validation (if schema-based validation is wired there rather than plain `IValidateOptions<T>`): choice between `JsonSchema.Net`, `NJsonSchema`, or hand-rolled `IValidateOptions` with DataAnnotations. Planner decides; prefer the one already pulled in by a transitive dependency to avoid adding a NuGet.
- Exact `x-passreset-obsolete` key name and deprecation message format: planner picks a consistent convention.
- Event Log source registration (`PassReset`): if not already registered, installer must register it with `New-EventLog`. Planner confirms whether the app or installer owns this.

### Folded Todos
None — no pending todos matched Phase 8 scope.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/REQUIREMENTS.md` §"Configuration Schema & Sync (Phase 8)" — STAB-007..012 acceptance criteria (lines 26–33)
- `.planning/ROADMAP.md` §"Phase 8: Configuration Schema & Sync" — success criteria + cross-phase dependencies (lines 49–62)
- `.planning/PROJECT.md` §"Active (v1.4.0)" — phase scope + key decisions

### Prior phase context (carry-forward decisions — DO NOT redesign)
- `.planning/phases/07-installer-deployment-fixes/07-CONTEXT.md` — Phase 7 established: `Write-Step`/`Write-Ok`/`Write-Warn`/`Abort` helpers for operator prompts; `-Force` = safe-default unattended mode; `[CmdletBinding(SupportsShouldProcess)]` on installer.

### Code surfaces being modified
- `src/PassReset.Web/appsettings.Production.template.json` — strip comments (STAB-007), must validate against new schema (STAB-008)
- `src/PassReset.Web/appsettings.schema.json` — **NEW** JSON Schema Draft 2020-12, authoritative key manifest
- `src/PassReset.Web/Program.cs` — wire `IValidateOptions<T>` + `ValidateOnStart()` for each options class (STAB-009)
- `src/PassReset.Web/Models/*.cs` — options classes (`ClientSettings`, `PasswordChangeOptions`, `SmtpSettings`, `SiemSettings`, etc.) get validators
- `deploy/Install-PassReset.ps1` — Test-Json pre-flight (STAB-009), `-ConfigSync` param + prompt (STAB-011), schema-driven additive-merge sync (STAB-010), rewritten drift check (STAB-012)
- `deploy/Publish-PassReset.ps1` — ensure `appsettings.schema.json` is copied into publish output alongside template
- `.github/workflows/ci.yml` (or similar) — CI step to Test-Json template against schema

### Operator-facing docs to update
- `docs/appsettings-Production.md` — absorb all comment content from the template (STAB-015), add `-ConfigSync` mode reference, event log troubleshooting section
- `docs/IIS-Setup.md` — note about event log source registration (if owned by installer)
- `UPGRADING.md` — document `-ConfigSync` modes, behavior on upgrade, how to diagnose failed startup validation
- `CHANGELOG.md` — entries for STAB-007..012 under v1.4.0 `[Unreleased]`

### External references (research)
- JSON Schema Draft 2020-12 spec: https://json-schema.org/draft/2020-12
- PowerShell `Test-Json -Schema` requires PowerShell 6+ — confirm minimum version policy
- ASP.NET Core options pattern: `IValidateOptions<T>`, `ValidateOnStart()`, `OptionsBuilder<T>.Validate()`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Install-PassReset.ps1:755-794` — existing schema-drift-check block; Phase 8 rewrites the inside of this `if (IsUpgrade) { ... }` branch rather than restructuring the outer flow.
- `Install-PassReset.ps1:644-650` — fresh-install template-copy logic; sync work extends this (upgrade path) but doesn't change fresh-install semantics.
- `Install-PassReset.ps1:368-373` — robocopy `/XF` already preserves `appsettings.Production.json` / `appsettings.Local.json` / `logs\` during file mirror. Sync writes happen AFTER robocopy, so file-preservation is intact.
- `Write-Step` / `Write-Ok` / `Write-Warn` / `Abort` helpers — reuse for all new prompts (Phase 7 convention).
- Phase 7 `-Force` semantics (safe default for unattended mode) — `-ConfigSync` follows the same pattern: `-Force` without explicit `-ConfigSync` → `Merge`.

### Established Patterns
- `IOptions<T>` / `IOptionsSnapshot<T>` DI registration in `Program.cs` — already in use for `ClientSettings`, `PasswordChangeOptions`, `SmtpSettings`, `SiemSettings`, `EmailNotificationSettings`, `PasswordExpiryNotificationSettings`. Phase 8 adds `.Validate(...)` / `.ValidateOnStart()` to each.
- Options classes live in `src/PassReset.Web/Models/` and `src/PassReset.Common/` — no model-layer restructure needed.
- Event Log writes: confirm existing convention (none found in initial scout — planner verifies).

### Integration Points
- Schema file: lives beside template in `src/PassReset.Web/`; MSBuild `Content` include ensures it's copied to output. `Publish-PassReset.ps1` must carry it into the release zip.
- `-ConfigSync` parameter: declared at the top of `Install-PassReset.ps1` alongside `-Force`, `-CertThumbprint`, etc. Validated via `[ValidateSet('Merge','Review','None')]`.
- Startup validation hook: `WebApplicationBuilder.Services.AddOptions<T>().Bind(...).Validate(...).ValidateOnStart()` chain in `Program.cs`.
- CI validation: new job step in `.github/workflows/ci.yml` — `pwsh -Command "Test-Json -Path src/PassReset.Web/appsettings.Production.template.json -SchemaFile src/PassReset.Web/appsettings.schema.json"`.

</code_context>

<specifics>
## Specific Ideas

- **Obsolete marker convention:** use `x-passreset-obsolete: true` with optional `x-passreset-obsolete-since: "1.3.2"` (schema `x-` extensions are standard for custom metadata). Upgrade prints: `Obsolete: {path} — no longer used as of v{since}. Safe to remove.`
- **Interactive sync prompt wording:** `Config sync: [M]erge additions / [R]eview each / [S]kip? [M]` — matches Phase 7 tone and uses `Read-Host` with default-on-empty.
- **Deep-merge semantics:** treat arrays as atomic (never merge array contents — if key exists, leave the whole array alone). This avoids surprising operators who customized `RestrictedAdGroups`, `AllowedAdGroups`, `AlertOnEvents`, `TrustedCertificateThumbprints`, etc.
- **Event Log source name:** `PassReset` (single source, all events). Installer registers on fresh install if missing.
- **Test-Json version gate:** Install-PassReset.ps1 already runs on Windows PowerShell 5.1 at minimum in some environments. Planner must decide: require pwsh 7.x (document in `docs/IIS-Setup.md`), or use a .NET bridge. Record the decision in PLAN.md.
- **CI validation fail message:** include schema path + line number — `Test-Json` emits location info; surface it verbatim in the CI log.

</specifics>

<deferred>
## Deferred Ideas

- **`passreset validate` standalone CLI** — nice-to-have for remote diagnosis; defer until operators ask (not in STAB requirements).
- **Auto-generated `docs/appsettings-Production.md` from schema descriptions** — requires schema `description` field (excluded from v1.4.0 scope per D-03). Future phase.
- **`-ConfigSync Full` mode that removes obsolete keys** — speculative. Safe default (report only) is adequate for v1.4.0.
- **`-WhatIf` dry-run for sync** — SupportsShouldProcess already on the cmdlet, but explicit `-Preview` output format is out of scope. If operators want it, add in v1.4.x.
- **Automatic backup file before sync writes** (`.bak-<timestamp>`) — robocopy `/XF` already preserves pre-upgrade state; redundant. Revisit if a sync bug ever destroys data.
- **JSON Schema `description`/`examples` + auto-docs** — deferred from D-03 above. Future phase.
- **Cross-field semantic validation in schema (if/then/else)** — kept in C# for v1.4.0. Move to schema later if we outgrow `IValidateOptions<T>`.
- **Move dependency detection to shared `PassReset.Prereqs.psm1` module** — carried over from Phase 7 deferred. Still deferred.

### Reviewed Todos (not folded)
None — no pending todos matched Phase 8 scope.

</deferred>

---

*Phase: 08-config-schema-sync*
*Context gathered: 2026-04-16*
