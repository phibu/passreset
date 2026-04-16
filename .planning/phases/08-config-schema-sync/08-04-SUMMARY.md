---
phase: 08-config-schema-sync
plan: 04
subsystem: installer
tags: [installer, config-sync, schema-validation, event-log, pwsh7]
requires: [08-01]
provides:
  - "Installer pre-sync scaffolding (pwsh7 requirement, EventLog source registration, Test-Json pre-flight, -ConfigSync param + interactive prompt)"
  - "Resolved $ConfigSync variable downstream plans 08-05 / 08-06 plug into"
affects: [deploy/Install-PassReset.ps1]
tech-stack:
  added: []
  patterns:
    - "D-05 install-time schema validation via Test-Json -SchemaFile"
    - "D-07 fail-fast Abort on invalid config (never silent skip)"
    - "D-12 / D-13 -ConfigSync param + interactive prompt resolution (Merge default under -Force)"
    - "One-time idempotent EventLog source registration via [EventLog]::SourceExists gate"
key-files:
  created: []
  modified:
    - deploy/Install-PassReset.ps1
decisions:
  - "pwsh 7+ required (Test-Json -SchemaFile unavailable in Windows PowerShell 5.1)"
  - "Installer owns Event Log source registration (one-time, idempotent)"
  - "Key-path separator for future sync/drift walkers: ':' (ASP.NET Core options-binding convention) — applied downstream in plans 08-05 / 08-06"
  - "Pre-flight runs BEFORE ConfigSync resolution so sync never operates on invalid config"
  - "Missing schema → Write-Warn + continue (graceful degradation; CI in plan 08-02 catches this upstream)"
requirements: [STAB-009, STAB-011]
metrics:
  duration: ~15min (continuation)
  completed: 2026-04-16
---

# Phase 08 Plan 04: Installer pre-sync scaffolding Summary

PowerShell 7 requirement, Event Log source registration, `-ConfigSync` parameter with interactive prompt resolution, and Test-Json pre-flight validation of live `appsettings.Production.json` against `appsettings.schema.json` — establishing the scaffolding that downstream plans 08-05 (additive-merge sync) and 08-06 (drift-check rewrite) plug into.

## What Was Built

### 1. `#Requires -Version 7.0` (line 2)
Adjacent to existing `#Requires -RunAsAdministrator`. Required because `Test-Json -SchemaFile` is only available in pwsh 7+.

### 2. `-ConfigSync` parameter (lines 94-95)
```powershell
[ValidateSet('Merge','Review','None')]
[string] $ConfigSync = ''
```
Empty default resolves post-upgrade-detection. PowerShell-native ValidateSet rejects bad values at param binding (T-08-12 mitigation).

### 3. Event Log source registration (lines 218-233)
Runs in prerequisites block AFTER .NET Hosting Bundle check, BEFORE publish-folder resolution. Idempotent via `[System.Diagnostics.EventLog]::SourceExists('PassReset')` gate. Failure is Write-Warn (non-fatal) so install still proceeds; runtime `EventLog.WriteEntry` falls back silently per plan 08-03 contract.

### 4. Test-Json pre-flight (lines 399-431)
Upgrade-only gate. Skipped on fresh install (no live config yet). Missing schema file → Write-Warn + continue. Validation failure → `Abort` with operator-actionable field-path error (D-07 fail-fast). Placed BEFORE ConfigSync resolution so sync never runs against an invalid file.

### 5. ConfigSync resolution (lines 437-456)
Per D-13:
- `-Force` + no param → `Merge` with Write-Ok
- Interactive upgrade → prompt `'  Config sync: [M]erge additions / [R]eview each / [S]kip? [M]'` (exact D-13 wording); switch maps `R*`→Review, `S*`→None, default→Merge
- Fresh install → `None` silently (template just copied)
- Param supplied → echo with Write-Ok

## Insertion-Point Line Numbers for Downstream Plans

| Landmark | Line | Consumed by |
|----------|------|-------------|
| Pre-flight validation block ends | ~431 | 08-06 (drift-check rewrite can extend pre-flight reporting) |
| ConfigSync resolution block ends | ~456 | 08-05 (additive-merge sync reads `$ConfigSync` here) |
| Existing naive drift-check block | ~813-855 | 08-06 replaces this entire block with schema-driven drift walker |
| Starter-config copy block | ~696-710 | Unchanged; fresh-install template copy stays as-is |

## Files Modified

- `deploy/Install-PassReset.ps1` — single cohesive edit (+81 LOC)

## Decisions Recap

| Decision | Choice | Rationale |
|----------|--------|-----------|
| PowerShell version | pwsh 7+ required | `Test-Json -SchemaFile` unavailable in 5.1 |
| Event Log source ownership | Installer (one-time) | Runtime `EventLog.WriteEntry` needs source pre-registered; installer already runs elevated |
| Key-path separator (downstream) | `:` | ASP.NET Core `IOptions<>` binding convention — applied in 08-05 / 08-06 |
| Pre-flight placement | BEFORE ConfigSync resolution | Sync must never operate on invalid config |
| Missing-schema behavior | Write-Warn + continue | CI gate in 08-02 prevents shipping without schema; runtime graceful |

## Deviations from Plan

### Atomic grouping of Tasks 1 & 2

**Reason:** Prior executor (paused before validate+commit of Task 1) had already applied most of Task 1's edits in the working tree. Continuation agent added the Task 2 pre-flight block and committed both as a single cohesive `feat(08-04): ...` commit rather than resetting and re-committing per task.

**Justification:** Both tasks modify the same file (`deploy/Install-PassReset.ps1`), share the same verification command (pwsh parser check), and the Task 2 pre-flight sits immediately upstream of the Task 1 ConfigSync resolution — they form one atomic installer-scaffolding unit. Splitting would create an intermediate commit where the pre-flight is absent but ConfigSync runs unguarded.

**Impact:** None to downstream work. Plans 08-05 and 08-06 consume the resolved `$ConfigSync` variable and the pre-flight's already-validated live config regardless of commit granularity.

### Minor: `$prodConfig` assigned twice

The pre-flight block (line ~403) and the later starter-config block (line ~719) both declare `$prodConfig = Join-Path $PhysicalPath 'appsettings.Production.json'`. Re-assignment to an identical value is idempotent and strict-mode-safe. Left as-is to minimize diff in the well-tested starter-config block; future refactor can hoist the declaration to a single location near the `$PhysicalPath` resolution.

## Auth Gates

None.

## Verification Results

- `pwsh -NoProfile -File .tmp-parse-check.ps1` → `PARSE OK` (ran before commit; tmp file deleted)
- All must_haves from frontmatter confirmed via grep:
  - `#Requires -Version 7.0` at line 2 ✓
  - `ValidateSet('Merge','Review','None')` at line 94 ✓
  - `New-EventLog -LogName Application -Source PassReset` at line 224 ✓
  - `[System.Diagnostics.EventLog]::SourceExists` at line 223 ✓
  - Exact prompt wording at line 444 ✓
  - `Test-Json` + `-SchemaFile` + `appsettings.schema.json` ref at lines 415-417 ✓
  - `Abort "appsettings.Production.json failed schema validation:"` at line 426 ✓
  - `$ConfigSync = 'Merge'` (line 440) + `default { 'Merge' }` (line 448) — 2 branches ✓

## Manual Test Result

Not executed — no IIS test environment available in this session. The pwsh parser check and grep verification cover STAB-005 regression guard (installer still parses) plus the structural must_haves. Live dry-run will be exercised downstream when plans 08-05 / 08-06 land and a full end-to-end upgrade scenario is testable.

## Known Stubs

None. `$ConfigSync` is resolved but no action is taken on it in this plan — consumers (plans 08-05 / 08-06) are explicitly scoped separately per CONTEXT.md. Not a stub; documented hand-off.

## Self-Check: PASSED

- FOUND: `deploy/Install-PassReset.ps1` (modified)
- FOUND commit: `1d9b16e` (feat(08-04): add installer pre-sync scaffolding ...)
- FOUND: all 8 must_haves truths (verified via grep above)
- FOUND: key_links patterns (`Test-Json.*-SchemaFile.*appsettings\.schema\.json` at lines 415-417; `ValidateSet\('Merge','Review','None'\)` at line 94)
- Parse check: PARSE OK under pwsh 7
