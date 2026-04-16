# Phase 7: Installer & Deployment Fixes - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-16
**Phase:** 07-installer-deployment-fixes
**Areas discussed:** Port-80 conflict (#19), Consecutive change crash (#36), Dependency detection (#21)

---

## Gray Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| Port-80 conflict handling (#19) | Default Web Site bound on :80 | ✓ |
| Same-version re-run prompt (#20) | 're-configure' vs 'upgrade' | (deferred to Claude's discretion) |
| Consecutive change crash (#36) | E_ACCESSDENIED on 2nd change | ✓ |
| Dependency detection scope (#21) | IIS roles + hosting bundle | ✓ |

---

## Port-80 Conflict (#19)

**Question:** When IIS Default Web Site already binds :80 on a fresh install, what should the installer do?

| Option | Description | Selected |
|--------|-------------|----------|
| Detect + prompt (stop / alternate / abort) | Interactive detection with `-Force` defaulting to safest non-destructive path | ✓ |
| Auto-stop Default Web Site with confirmation | Single Y/N to stop Default Web Site | |
| Always use alternate port; never touch other sites | Silent fallback to :8080+ | |

**User's choice:** Detect + prompt: stop Default Web Site, alternate port, or abort (Recommended)
**Notes:** `-Force` mode picks alternate-port default to keep unattended installs non-destructive.

---

## Consecutive Change Crash (#36)

**Question:** How should we prevent E_ACCESSDENIED when min-pwd-age hasn't elapsed?

| Option | Description | Selected |
|--------|-------------|----------|
| Pre-check + keep catch block | Defense in depth | ✓ |
| Pre-check only | Rely on pre-check; remove catch | |
| Catch-only (extend BUG-004 mapping) | Let AD throw, catch, map | |

**User's choice:** Both: pre-check pwdLastSet + keep catch block (Recommended)
**Notes:** Pre-check is the fast path with precise error message; catch is the floor for races.

### Follow-up: Pre-check credential source

| Option | Description | Selected |
|--------|-------------|----------|
| Read as the bound user | User reads their own pwdLastSet | |
| Service account if configured, else bound user | Matches GetUsersInGroup pattern | ✓ |
| Claude's discretion | Defer to existing patterns | |

**User's choice:** Use service account if configured, fall back to bound user
**Notes:** Consistency with existing provider patterns; avoids AD configs that deny self-read of pwdLastSet.

---

## Dependency Detection (#21)

**Question:** What should the installer detect and offer to install?

| Option | Description | Selected |
|--------|-------------|----------|
| Detect IIS roles + hosting bundle; DISM auto-install with consent | Must-haves only, prompt before DISM | ✓ |
| Detect-and-warn only; print commands, never auto-install | Conservative | |
| Full IIS-Setup.md checklist incl. URL Rewrite, ARR | Thorough but over-scoped | |

**User's choice:** Detect IIS roles + hosting bundle; auto-install via DISM with consent (Recommended)
**Notes:** Hosting bundle stays manual download per Microsoft's recommended path.

### Follow-up: Behavior when user declines auto-install

| Option | Description | Selected |
|--------|-------------|----------|
| Print exact remediation commands and exit cleanly | User chose this path; exit 0 | ✓ |
| Abort with error — missing prereqs are a hard fail | Exit 1 | |
| Continue install and let it fail naturally | Bad UX, leaves partial state | |

**User's choice:** Print exact remediation commands and exit cleanly (Recommended)

---

## Claude's Discretion

- **STAB-002 (gh#20 same-version prompt)** — implementation details for "re-configure" prompt branch in upgrade-detection logic
- **STAB-003 (gh#23 AppPool identity warning)** — diagnose and fix the `Get-ItemProperty processModel.identityType` read failure
- **STAB-005 (gh#39 Uninstall ParserError)** — apply lowest-risk encoding fix (UTF-8 BOM + replace Unicode `─` with ASCII `---`); validate on PS 5.1 and 7.x

## Deferred Ideas

- MSI packaging revisit (out of scope per 2026-04-13 rollback)
- URL Rewrite / ARR auto-install (out of scope for STAB-006)
- Refactor dependency detection into shared PS module (premature)
- Cache pwdLastSet across consecutive requests (premature optimization)
