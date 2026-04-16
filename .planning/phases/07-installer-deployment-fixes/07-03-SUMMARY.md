---
phase: 07-installer-deployment-fixes
plan: 03
status: complete
requirements_addressed: [STAB-001, STAB-006]
completed: 2026-04-16
uat_pending: 07-03-HUMAN-UAT.md
---

## What was built

Two independent logic blocks added to `deploy/Install-PassReset.ps1`:

- **STAB-006 dependency detection** — replaces the old `Install-WindowsFeature` invocation with a `dism.exe` flow gated by `$PSCmdlet.ShouldProcess`. Missing IIS features trigger one Y/N consent prompt (not per-feature); `-Force` enables them non-interactively. DISM exit `3010` (reboot pending) is treated as success per Microsoft convention. Missing or wrong-version .NET 10 Hosting Bundle no longer aborts — prints the download URL and exits `0` cleanly (D-09).
- **STAB-001 port-80 conflict detection** — before `New-Website` on a fresh install, `Get-WebBinding -Port 80` is inspected. A conflicting foreign site triggers a 3-choice prompt (Stop / Alternate port 8080..8090 / Abort). `-Force` picks the first free alternate port and never silently stops a foreign site (D-02). Installer prints the final reachable URL(s) after binding setup.

## Key files

### Modified
- `deploy/Install-PassReset.ps1` (two commits)
  - `9436e5c` — STAB-006 dependency detection (lines 131-186)
  - `199c526` — STAB-001 port-80 detection (~lines 398-460) + reachable URL announce (lines 517-526)

## Final union of `$requiredFeatures`

```
Web-Server
Web-WebServer
Web-Static-Content
Web-Default-Doc
Web-Http-Errors
Web-Http-Logging
Web-Filtering
Web-Mgmt-Console
```

## Deviations from plan

1. **Did NOT add `Web-Asp-Net45` / `Web-Net-Ext45`** — the plan Task 1 step 2 instructed to add both, but `Install-PassReset.ps1` lines 117-118 have an explicit code comment (left over from an earlier phase's code review) explaining that these are .NET Framework 4.x features, not required for ASP.NET Core, and not available on Server 2019+. Trusting the code comment over the plan per user confirmation on 2026-04-16.
2. **Reachable-URL host** — plan referenced `$SiteHostName`, but the installer has no such parameter. Used `$env:COMPUTERNAME` instead (matches the typical LAN-reachable name).
3. **Port-80 detection scoped to fresh installs on port 80** — the plan ran the block unconditionally before `New-Website`. On upgrade the site already has a binding (preserved), so running the detection would false-positive against PassReset's own binding. Added `-not $siteExists -and $selectedHttpPort -eq 80` guard so the logic only runs when it can actually act.

## Acceptance evidence

Static checks (all pass on PS 5.1):

| Check | Result |
|-------|--------|
| `Parser::ParseFile` zero errors | PASS |
| Regex `Get-WebBinding -Port 80` present | PASS |
| Alt-port range `8080..8090` present | PASS |
| `PassReset reachable at` Write-Ok | PASS |
| `$selectedHttpPort` used 13 times (≥3 required) | PASS |
| Port-80 block line (403) < New-Website call line (463) | PASS |
| Reachable print (520) > alt-port scan (430) | PASS |
| `-Force` branch (lines 445-457) contains NO `Stop-Website` | PASS |
| `$PSCmdlet.ShouldProcess` wraps DISM + Stop-Website invocations | PASS |
| Hosting Bundle missing branch contains `dotnet.microsoft.com/download/dotnet/10.0` + `exit 0` | PASS |
| Single Y/N DISM consent prompt (`Install missing IIS features now via DISM?`) | PASS |

## Tasks

| Task | Status | Notes |
|------|--------|-------|
| 1 — STAB-006 dependency detection, single DISM prompt, Hosting Bundle exit 0 | complete | commit `9436e5c` |
| 2 — STAB-001 port-80 block + reachable URL announce | complete | commit `199c526` |
| 3 — Operator UAT (A/B/C/D) | **pending** — persisted to `07-03-HUMAN-UAT.md` | user deferred runtime UAT |

## Requirements addressed
- **STAB-001** — ROADMAP Phase 7 Success Criterion #1 (gh#19).
- **STAB-006** — ROADMAP Phase 7 Success Criterion #6 (gh#21).

## Self-Check

- [x] Parser::ParseFile → 0 errors on PS 5.1.
- [x] DISM invocation gated by `$PSCmdlet.ShouldProcess`.
- [x] Single Y/N consent, not per-feature.
- [x] Hosting Bundle missing → `exit 0` with download URL.
- [x] Port-80 detection runs BEFORE `New-Website`.
- [x] `-Force` mode never stops a foreign site.
- [x] Reachable URL(s) printed at end of site configuration.
- [ ] Operator UAT on real Windows Server/IIS box (A/B/C/D) — persisted to `07-03-HUMAN-UAT.md`.

## Not done (per CONTEXT)
- No `PassReset.Prereqs.psm1` module extraction (explicitly deferred).
- No URL Rewrite / ARR / request-filtering rule changes (explicitly out of scope per D-07).
