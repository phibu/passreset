# Phase 8: Configuration Schema & Sync - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-16
**Phase:** 08-config-schema-sync
**Areas discussed:** Schema format & location, Validation timing & failure mode, Upgrade sync behavior, Comment handling & operator docs

---

## Gray Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| Schema format & location | STAB-007+008: How to kill JSON comments AND define the authoritative schema | ✓ |
| Validation timing & failure mode | STAB-009: When/how pre-flight validation runs | ✓ |
| Upgrade sync behavior | STAB-010+011: Sync behavior on upgrade for additions/obsolete/overrides | ✓ |
| Comment handling & operator docs | STAB-007+012: How comments get removed and where docs live | ✓ |

**User's choice:** All four selected.

---

## Schema format & location

### Q: Where should the authoritative schema live, and in what format?

| Option | Description | Selected |
|--------|-------------|----------|
| JSON Schema file | `appsettings.schema.json` (Draft 2020-12), single source of truth, standard tooling | ✓ |
| C# POCO manifest (generate JSON) | C# classes with attributes drive everything; build step emits schema | |
| PowerShell manifest (.psd1) | Windows-native; poor C# tooling | |
| YAML manifest | Human-readable but adds YamlDotNet + powershell-yaml deps | |

**User's choice:** JSON Schema file (Recommended).

### Q: How does the schema relate to the template file?

| Option | Description | Selected |
|--------|-------------|----------|
| Schema primary, template is example | Schema defines keys/types/defaults; template must validate against it (CI) | ✓ |
| Template primary, schema generated | CI generates schema from template; loses "required"/"obsolete" metadata | |
| Both checked in, both hand-maintained | Two authoritative files; drift inevitable | |

**User's choice:** Schema primary, template is example (Recommended).

### Q: What schema metadata is in scope for v1.4.0?

| Option | Description | Selected |
|--------|-------------|----------|
| Required vs optional per key | Drives pre-flight validation (STAB-009) | ✓ |
| Default values | Drives upgrade sync additive-merge (STAB-010) | ✓ |
| Obsolete/deprecated markers | Custom `x-passreset-obsolete`; drift check flags them (STAB-010) | ✓ |
| Descriptions + examples | Generates docs automatically; cheap nice-to-have | |

**User's choice:** Required + Default + Obsolete (descriptions/examples deferred).

### Q: Schema validation scope — semantic rules?

| Option | Description | Selected |
|--------|-------------|----------|
| Structural + types + enums only | PowerShell Test-Json handles this; semantic rules in C# IValidateOptions | ✓ |
| Everything in schema (if/then/else) | One source of truth; PowerShell if/then support spotty | |
| Minimal — structure only | All validation in C#; PowerShell can't validate without dotnet | |

**User's choice:** Structural + types + enums only (Recommended).

---

## Validation timing & failure mode

### Q: When does pre-flight validation run?

| Option | Description | Selected |
|--------|-------------|----------|
| Install-time (PowerShell) | Test-Json -Schema in installer after merging | ✓ |
| Startup (ASP.NET IValidateOptions) | ValidateOnStart() fails fast; event log entry | ✓ |
| Standalone CLI (`passreset validate`) | Separate executable for ad-hoc validation | |
| CI (validate template against schema) | Prevents developer regressions | ✓ |

**User's choice:** Install-time + Startup + CI (standalone CLI deferred).

### Q: Startup validation failure mode in production?

| Option | Description | Selected |
|--------|-------------|----------|
| Fail fast — app won't start, event log entry | ValidateOnStart throws; IIS 502; event log has full error | ✓ |
| Start degraded — /health reports unhealthy | Boot but 503; can diagnose remotely but users see broken form | |
| Start with warnings — log and continue | Silent degradation; defeats pre-flight purpose | |

**User's choice:** Fail fast (Recommended).

### Q: Error message format?

| Option | Description | Selected |
|--------|-------------|----------|
| Field path + reason + remediation | `{field.path}: {reason}. Edit {path} or run Install-PassReset.ps1 -Reconfigure.` | ✓ |
| Field path + reason only | Standard ASP.NET format; operator has to know remediation path | |
| All errors at once (aggregated) | One message; long and hard to read in event log | |

**User's choice:** Field path + reason + remediation (Recommended).

---

## Upgrade sync behavior

### Q: Default sync behavior on upgrade?

| Option | Description | Selected |
|--------|-------------|----------|
| Additive-merge: add missing keys, leave everything alone | Schema defaults fill gaps; obsolete reported only; overrides untouched | ✓ |
| Interactive review each key diff | Y/N per key; tedious for 40+ keys | |
| Report-only, operator applies manually | Maximum safety; shifts burden back to operators | |

**User's choice:** Additive-merge (Recommended).

### Q: What explicit controls does the installer expose?

| Option | Description | Selected |
|--------|-------------|----------|
| `-ConfigSync <mode>` parameter | Modes: Merge / Review / None | ✓ |
| Interactive prompt when no flag + upgrade detected | `[M]erge / [R]eview / [S]kip? [M]` | ✓ |
| Dry-run flag: `-ConfigSync Merge -WhatIf` | Preview changes; free via SupportsShouldProcess | |
| Backup before any write | `appsettings.Production.json.bak-<timestamp>` | |

**User's choice:** `-ConfigSync` param + interactive prompt. Dry-run and backup deferred.

### Q: Obsolete keys — how aggressively?

| Option | Description | Selected |
|--------|-------------|----------|
| Report, never remove | Operator removes by hand; safest | |
| Prompt to remove per key | Interactive Y/N per obsolete key; default N | ✓ |
| Auto-remove with `-ConfigSync Full` explicit opt-in | Additional mode for clean-config operators | |

**User's choice:** Prompt to remove per key.

**Note:** In `-Force` / unattended mode, obsolete keys are reported but never removed (falls back to safer behavior when no operator is present).

### Q: Nested structures — how deep does merge go?

| Option | Description | Selected |
|--------|-------------|----------|
| Top-level-key merge only | If SiemSettings exists, don't touch nested contents | |
| Deep merge — add missing nested keys too | Walk schema recursively, add at any depth | ✓ |
| Deep merge for new objects only | Add missing whole sections; skip inside existing sections | |

**User's choice:** Deep merge (add missing nested keys at any depth).

### Q: Deep merge edge case — operator REMOVED a nested key on purpose?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, re-add (schema defines what MUST exist) | Schema is authoritative; upgrade heals removal | ✓ |
| No, leave the gap alone | Respect operator intent; validation will catch | |
| Only re-add if required; skip if optional | Most nuanced but hardest to explain | |

**User's choice:** Yes, re-add — schema is authoritative.

---

## Comment handling & operator docs

### Q: How do we handle comments in template (STAB-007)?

| Option | Description | Selected |
|--------|-------------|----------|
| Strip comments when copying template → production | Template keeps comments; installer strips on copy | (initial) |
| Remove comments from template entirely | Template becomes pure JSON; docs move to docs/ | |
| Keep comments, use .NET JsonCommentHandling.Skip | PowerShell still fails (STAB-012 root cause) | |
| Rename `.json` to `.jsonc` | Doesn't solve PowerShell parsing | |

**User's choice (initial):** Strip when copying. **Conflicted with Q3 answer below; resolved to "remove from template entirely".**

### Q: How does STAB-012 get fixed?

| Option | Description | Selected |
|--------|-------------|----------|
| Upstream fix — installer's comment-strip runs before drift check | Stripped live file + in-memory stripped template compared | ✓ |
| Pre-parse sanitizer in the drift-check function | Targeted but duplicates logic | |
| Switch drift check to Newtonsoft.Json | Complex interop for one check | |

**User's choice:** Upstream fix.

**Note:** Became moot after Q3 resolution — template has no comments, so no strip step needed.

### Q: Where do operator-facing config docs live long-term?

| Option | Description | Selected |
|--------|-------------|----------|
| Both — brief comments in template + full docs in docs/ | Developer ergonomic + narrative reference | |
| Only docs/ — strip all comments from template | Single source of truth | ✓ |
| Only template — inline comments, delete docs | No narrative/security guidance | |

**User's choice:** Only docs/ — strip all comments from template.

### Q (follow-up): Confirm resolution of conflict?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, template = pure JSON, docs in docs/ | Cleanest; STAB-007 and STAB-012 both resolved trivially | ✓ |
| Actually, keep comments + add strip step | Revert to first answer | |

**User's choice:** Yes — template becomes pure JSON.

---

## Claude's Discretion

The following are left to the planner to resolve during research/planning:

- **PowerShell version gate:** `Test-Json -Schema` requires PowerShell 6+; Windows PowerShell 5.1 lacks `-Schema`. Planner decides: require pwsh 7.x at install (document in IIS-Setup.md) OR .NET bridge.
- **C# JSON Schema library choice** (if used beyond `IValidateOptions<T>`): `JsonSchema.Net`, `NJsonSchema`, or DataAnnotations-only. Prefer transitively-pulled library.
- **Exact `x-passreset-obsolete` convention:** custom extension field name and deprecation message format. Planner picks consistent wording.
- **Event Log source registration:** installer vs. app-owned. Planner confirms current state and picks.

## Deferred Ideas

- Standalone `passreset validate` CLI (nice-to-have, no STAB requires it)
- Auto-generated `docs/appsettings-Production.md` from schema descriptions (needs description field)
- `-ConfigSync Full` mode (auto-remove obsolete keys)
- `-WhatIf` dry-run for sync operations
- Automatic pre-sync backup files (robocopy `/XF` already preserves state)
- Schema `description`/`examples` + auto-docs generation
- Cross-field semantic validation in schema (if/then/else)
- Shared `PassReset.Prereqs.psm1` module (carried over from Phase 7 deferred)
