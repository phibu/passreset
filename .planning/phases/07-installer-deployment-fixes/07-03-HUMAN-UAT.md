---
status: partial
phase: 07-03-installer-deployment-fixes
source: [07-03-SUMMARY.md]
started: 2026-04-16
updated: 2026-04-17
---

## Current Test

[blocked — no IIS host available; resume when a Windows Server test box is on hand]

## Tests

### A. Port-80 conflict — interactive (STAB-001)
expected: IIS Default Web Site is running on port 80. Elevated `powershell.exe -> .\Install-PassReset.ps1` displays the three-choice prompt `[1] Stop conflicting site(s) / [2] Alternate port (8080-8090) / [3] Abort`. Selecting `[2]` completes the install on the first free port in 8080..8090 and prints `[OK] PassReset reachable at http://<host>:<port>/`.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

### B. Port-80 conflict — `-Force` (STAB-001 D-02)
expected: Default Web Site still holds port 80. Elevated `.\Install-PassReset.ps1 -Force <other params>` runs without prompting, logs `-Force specified - port 80 in use, defaulting to alternate port 8080` (or the first free 8081..8090 if 8080 is taken), completes install, and never stops the foreign site.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

### C. Missing IIS features — single DISM prompt (STAB-006)
expected: Simulate with `Uninstall-WindowsFeature Web-Default-Doc -ErrorAction SilentlyContinue` (or another feature from `$requiredFeatures`). Run `.\Install-PassReset.ps1`. Installer lists missing features and shows one prompt `Install missing IIS features now via DISM? [Y/N]`. Answering `Y` runs DISM and proceeds. Answering `N` prints the exact `dism /online /enable-feature /featurename:<X> /all /norestart` commands and `exit 0` (no stack trace).
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

### D. Missing Hosting Bundle — clean exit (STAB-006 D-09)
expected: On a box without the .NET 10 Hosting Bundle installed (simulate by renaming the registry key `HKLM:\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost` or testing on a fresh box), the installer prints the Microsoft download URL `https://dotnet.microsoft.com/download/dotnet/10.0`, instructs to re-run after install, and exits with code `0`.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

## Summary

total: 4
passed: 0
issues: 0
pending: 0
skipped: 0
blocked: 4

## Gaps
