---
phase: 07-installer-deployment-fixes
fixed_at: 2026-04-16T00:00:00Z
review_path: .planning/phases/07-installer-deployment-fixes/07-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 07: Code Review Fix Report

**Fixed at:** 2026-04-16
**Source review:** .planning/phases/07-installer-deployment-fixes/07-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (WR-01, WR-02, WR-03)
- Fixed: 3
- Skipped: 0
- Info findings (IN-01 … IN-04) were out of scope (`fix_scope: critical_warning`) and left untouched.

## Fixed Issues

### WR-01: STAB-001 does not restore stopped conflicting sites on later abort

**Files modified:** `deploy/Install-PassReset.ps1`
**Commit:** `ed89657`
**Applied fix:** Added a script-scoped `$script:StoppedForeignSites` list and a
`Restore-StoppedForeignSites` helper. Redefined `Abort` to call the helper
before exiting so every abort path (alternate-port exhaustion, ACL failure,
robocopy >=8, startup failure without backup, rollback failure) restarts the
foreign sites the operator chose to stop in port-80 conflict resolution
(option `[1]`). The switch `'1'` branch now appends each stopped site name to
the list. The existing rollback `catch` block calls `Abort` on failure paths,
so it inherits the restore behaviour without further changes. PowerShell AST
parse-check passed.

### WR-02: Upgrade-path "reachable URL" prints $HttpPort instead of real binding

**Files modified:** `deploy/Install-PassReset.ps1`
**Commit:** `2329ba1`
**Applied fix:** Upgrade branch now reads the actual HTTP binding(s) from IIS
via `Get-WebBinding -Name $SiteName -Protocol http` and announces each one by
splitting `bindingInformation` on `:` to extract the port. Handles the
HTTPS-only case (no HTTP binding) explicitly. Fresh-install branch is
unchanged (still uses `$selectedHttpPort`). PowerShell AST parse-check passed.

### WR-03: PreCheckMinPwdAge calls FindByIdentity without an IdentityType

**Files modified:** `src/PassReset.PasswordProvider/PasswordChangeProvider.cs`
**Commit:** `d392176`
**Applied fix:** Replaced `UserPrincipal.FindByIdentity(ctx, username)` with
`FindUser(ctx, username)`, the existing private helper that iterates
`AllowedUsernameAttributes` (samaccountname → userprincipalname → mail) and
handles `DOMAIN\user`, bare sam, and `user@domain` input forms. This matches
the resolution used by `PerformPasswordChangeAsync`, eliminating the
fast-path bypass for users configured via mail-only resolution or passing
`DOMAIN\user`. The `try`/`catch` fail-open semantics are preserved — any
exception still returns `null` and falls through to the post-hoc
`COMException` defense-in-depth floor. Release build: 0 errors. Test suite:
87/87 pass.

## Verification

- `deploy/Install-PassReset.ps1` — parsed successfully with
  `[System.Management.Automation.Language.Parser]::ParseFile` (no syntax
  errors) after both edits.
- `src/PassReset.PasswordProvider/PasswordChangeProvider.cs` —
  `dotnet build src/PassReset.sln --configuration Release` succeeded (0
  errors, only pre-existing xUnit1051 test-style warnings) and
  `dotnet test src/PassReset.sln --configuration Release` reported
  **87 passed, 0 failed, 0 skipped**.

---

_Fixed: 2026-04-16_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
