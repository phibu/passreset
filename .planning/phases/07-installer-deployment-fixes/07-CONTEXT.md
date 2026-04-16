# Phase 7: Installer & Deployment Fixes - Context

**Gathered:** 2026-04-16
**Status:** Ready for planning

<domain>
## Phase Boundary

Fix six bugs (STAB-001..006) opened against v1.3.2 that affect installer/uninstaller reliability and the consecutive-password-change crash. Two surfaces: PowerShell deploy scripts (`deploy/Install-PassReset.ps1`, `deploy/Uninstall-PassReset.ps1`) and the AD provider hot path (`PassReset.PasswordProvider`). No new features, no MSI rewrite, no architectural changes.

</domain>

<decisions>
## Implementation Decisions

### STAB-001 — Port-80 conflict detection (gh#19)
- **D-01**: Detect port-80 conflict before calling `New-Website`. List what's bound (typically `Default Web Site`) and offer interactive choices: stop conflicting site, use alternate port, or abort.
- **D-02**: For `-Force` (unattended/CI mode), default to **alternate port** (next free port starting at 8080). Never silently stop another site.
- **D-03**: Always print the final URL clearly at install-end so operators know whether to use `:80`, `:443`, or `:8080`.

### STAB-004 — Consecutive change crash (gh#36)
- **D-04**: Defense in depth — pre-check `pwdLastSet` vs domain `minPwdAge` BEFORE calling `SetPassword`/`ChangePassword`. Reject early with `ApiErrorCode.PasswordTooRecentlyChanged` and include the remaining time in the error payload (e.g., "X minutes remaining").
- **D-05**: KEEP the existing catch block in `PasswordChangeProvider` as a safety net for races / domain-policy edge cases / replication lag. The pre-check is the fast path; the catch is the floor.
- **D-06**: Pre-check uses **service-account credentials when configured, falls back to bound-user creds** — matches the existing pattern in `GetUsersInGroup` / `GetUserEmail`. Avoids requiring users to have read access on their own `pwdLastSet` (some AD configs deny this).

### STAB-006 — Dependency detection (gh#21)
- **D-07**: Detect required IIS roles/features (`Web-Server`, `Web-Asp-Net45`, `Web-Net-Ext45`) + .NET 10 Hosting Bundle. Scope is **must-haves only** — URL Rewrite, ARR, request filtering rules are NOT in scope (they're optional per `docs/IIS-Setup.md`).
- **D-08**: If IIS roles missing, prompt `'Install via DISM? [Y/N]'` (session is already elevated). On Y → run `dism /online /enable-feature` for each missing feature. On N → print exact remediation commands and exit 0 cleanly.
- **D-09**: If .NET Hosting Bundle missing, do NOT auto-install (Microsoft's recommended path is manual download). Print download URL + version requirement and exit 0 cleanly.
- **D-10**: All dependency checks run BEFORE the existing publish-folder resolution and upgrade detection — fail fast.

### Claude's Discretion
- **STAB-002 (gh#20 same-version prompt)**: Detect installed version == incoming version, prompt `'Re-configure existing installation?'` (not "upgrade"), skip the file-mirror copy on confirm. Implementation details left to planner.
- **STAB-003 (gh#23 AppPool identity warning)**: Diagnose why `Get-ItemProperty processModel.identityType` is failing on first read in v1.3.2 (likely error-handling regression). Fix the read; warning + fallback message must not appear when AppPool exists with valid identity. Implementation details left to planner.
- **STAB-005 (gh#39 Uninstall ParserError)**: Almost certainly an encoding issue (BOM, smart quotes, or Unicode `─` box-drawing chars in section comments). Apply lowest-risk fix: re-save as UTF-8 with BOM and replace Unicode `─` with ASCII `---`. Validate by parsing on a clean Windows PowerShell 5.1 + PowerShell 7.x session. No structural rewrite.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and prior decisions
- `.planning/REQUIREMENTS.md` §v1.4.0 — STAB-001..006 acceptance criteria (lines 19–24)
- `.planning/ROADMAP.md` §"Phase 7: Installer & Deployment Fixes" — success criteria + dependencies
- `.planning/PROJECT.md` §"Active (v1.4.0)" — phase scope summary + key decisions

### Prior phase context (carry-forward decisions — DO NOT redesign)
- `.planning/milestones/v1.2.3-REQUIREMENTS.md` — BUG-003 establishes the AppPool identity-preservation contract that STAB-003 is restoring
- `.planning/milestones/v1.3.1-REQUIREMENTS.md` — BUG-004 establishes the `E_ACCESSDENIED → PasswordTooRecentlyChanged` error mapping that STAB-004 extends to the consecutive-change case

### Code surfaces being modified
- `deploy/Install-PassReset.ps1` — port-80 detection (STAB-001), same-version prompt (STAB-002), AppPool read (STAB-003), dependency detection (STAB-006)
- `deploy/Uninstall-PassReset.ps1` — encoding/parser fix (STAB-005)
- `src/PassReset.PasswordProvider/PasswordChangeProvider.cs` — pre-check + catch (STAB-004)
- `src/PassReset.Common/ApiErrorCode.cs` — confirm `PasswordTooRecentlyChanged` exists from BUG-004

### Operator-facing docs to update
- `docs/IIS-Setup.md` — must reflect the new auto-install path and manual fallback for dependencies (STAB-006)
- `docs/Known-Limitations.md` — remove any "min-pwd-age trips a generic error" entry once STAB-004 ships
- `CHANGELOG.md` — entries for all six STAB items under v1.4.0 `[Unreleased]`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Write-Step` / `Write-Ok` / `Write-Warn` / `Abort` helpers in both deploy scripts ([Install-PassReset.ps1:101-104](deploy/Install-PassReset.ps1#L101-L104)) — use these for all new prompts to keep tone consistent.
- `.NET Hosting Bundle` detection at [Install-PassReset.ps1:144-156](deploy/Install-PassReset.ps1#L144-L156) — STAB-006 extends this pattern, doesn't replace it.
- `BUG-003` AppPool identity capture at [Install-PassReset.ps1:302-351](deploy/Install-PassReset.ps1#L302-L351) — STAB-003 fixes the read, doesn't redesign the preserve logic.
- Upgrade-detection `[version]::TryParse` block at [Install-PassReset.ps1:202-217](deploy/Install-PassReset.ps1#L202-L217) — STAB-002 adds an "equal version" branch alongside the existing downgrade/upgrade branches.

### Established Patterns
- Service-account-with-fallback: `PasswordChangeProvider.GetUsersInGroup` and similar methods already use service-account credentials when configured, fall back otherwise. STAB-004 pre-check follows the same pattern (D-06).
- `ApiErrorCode` returns: provider methods return `ApiErrorItem?` (null on success), never throw. STAB-004 pre-check returns `ApiErrorItem` directly without bouncing through a COMException.
- `[CmdletBinding(SupportsShouldProcess)]` on Install-PassReset.ps1 means `-WhatIf` already works for net new actions — use `$PSCmdlet.ShouldProcess(...)` for new destructive operations (DISM install).

### Integration Points
- Pre-check for STAB-004 belongs in `PasswordChangeProvider.PerformPasswordChangeAsync` immediately after authentication succeeds, BEFORE invoking the AD password-change call.
- Port-80 detection for STAB-001 belongs immediately before the `New-Website` call (~ line 264 of current Install-PassReset.ps1).
- Dependency detection for STAB-006 belongs at the very top of Install-PassReset.ps1, replacing/extending the current Hosting-Bundle-only block at lines 144–156.

</code_context>

<specifics>
## Specific Ideas

- For STAB-004 pre-check error message: include exact remaining time, e.g., `"Password was changed Y minutes ago; AD policy requires X minutes between changes (Z minutes remaining)."` — operators can't fix this, but users can stop hammering refresh.
- For STAB-001 alternate-port default: pick `8080` first; if also taken, increment to `8081`, `8082`, ... up to `8090`. If all 11 ports taken, abort with guidance (genuine box pollution).
- For STAB-006 DISM consent prompt: list all missing features in one message, single Y/N — don't prompt per feature.
- For STAB-005: validate the fix on **both** Windows PowerShell 5.1 and PowerShell 7.x — operators may use either.

</specifics>

<deferred>
## Deferred Ideas

- **MSI packaging revisit** — out of scope, rolled back 2026-04-13. Stays out.
- **Auto-install URL Rewrite / ARR** — out of scope for STAB-006; PassReset doesn't require them. If a future phase needs them, add as STAB-006a in v1.4.x.
- **Move dependency detection to a shared `PassReset.Prereqs.psm1` module** — refactoring opportunity, not a bug fix. Defer until Phase 8 or later.
- **Pre-check optimization: cache pwdLastSet across consecutive requests in the same session** — performance optimization, not in scope. Premature.

</deferred>

---

*Phase: 07-installer-deployment-fixes*
*Context gathered: 2026-04-16*
