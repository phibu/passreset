---
status: partial
phase: 07-01-installer-deployment-fixes
source: [07-01-SUMMARY.md]
started: 2026-04-16
updated: 2026-04-17
---

## Current Test

[blocked — no IIS host available; resume when a Windows Server test box is on hand]

## Tests

### 1. Uninstall with -KeepFiles on Windows PowerShell 5.1
expected: Elevated `powershell.exe -> .\Uninstall-PassReset.ps1 -KeepFiles` runs to completion with no ParserError. IIS site and AppPool are removed. Publish folder under `C:\inetpub\PassReset` (or configured path) is still present.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

### 2. Fresh install after step 1
expected: `.\Install-PassReset.ps1 -Force ...` restores a working deployment.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

### 3. Uninstall without -KeepFiles on Windows PowerShell 5.1
expected: Elevated `powershell.exe -> .\Uninstall-PassReset.ps1` runs to completion. IIS site + AppPool removed. Publish folder removed.
result: blocked
blocked_by: physical-device
reason: "User blocked all — no Windows Server / IIS box available for operator UAT at this session"

### 4. Uninstall with -KeepFiles on PowerShell 7.x
expected: Elevated `pwsh.exe -> .\Uninstall-PassReset.ps1 -KeepFiles` runs to completion with no ParserError. Same outcomes as step 1 (cross-shell parity).
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
