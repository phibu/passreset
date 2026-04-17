---
status: partial
phase: 07-04-installer-deployment-fixes
source: [07-04-SUMMARY.md]
started: 2026-04-16
updated: 2026-04-17
---

## Current Test

[blocked — no IIS host available; resume when a Windows Server test box with an existing PassReset install is on hand]

## Tests

### A. Same-version reconfigure — interactive (STAB-002)
expected: PassReset is installed at version `vX.Y.Z`. Re-run `.\Install-PassReset.ps1` with the same build (same version). The installer prints the detection block showing `Installed : vX.Y.Z` / `Incoming  : vX.Y.Z` and the note `Incoming version is the SAME as installed - this will RE-CONFIGURE, not upgrade`. The prompt reads **`Re-configure existing installation? [Y/N]`** (the word "upgrade" must NOT appear in the prompt). Answering `Y` logs `Reconfigure mode - skipping file mirror; existing publish folder preserved` and the app-pool / binding / config logic still re-runs. Site remains reachable.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

### B. Same-version reconfigure — `-Force` (STAB-002)
expected: Same setup as A. Run `.\Install-PassReset.ps1 -Force <other params>`. No interactive prompt. Installer logs `-Force specified - re-configuring without file mirror`. File mirror is skipped; downstream logic runs.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

### C. Upgrade preserves AppPool identity (STAB-003 regression)
expected: Existing PassReset AppPool is configured with `SpecificUser` identity (e.g., `DOMAIN\svc-passreset`). Deploy a higher-version build and run `.\Install-PassReset.ps1`. Upgrade path runs. Console output does **NOT** contain the string `Could not read existing AppPool identity`. Post-install verification: `Get-WebConfigurationProperty -PSPath 'IIS:\' -Filter "system.applicationHost/applicationPools/add[@name='PassReset']" -Name processModel.userName` returns the original `DOMAIN\svc-passreset` identity.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

## Summary

total: 3
passed: 0
issues: 0
pending: 0
skipped: 0
blocked: 3

## Gaps
