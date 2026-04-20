---
phase: 10-operational-readiness
verified: 2026-04-20T00:00:00Z
status: human_needed
score: 4/4 requirements verified in code
re_verification:
  previous_status: none
  previous_score: n/a
  gaps_closed: []
  gaps_remaining: []
  regressions: []
human_verification:
  - test: "Operator UAT of Install-PassReset.ps1 post-deploy verification"
    expected: "Scenarios A (success), B (failure on stopped AppPool), C (-SkipHealthCheck bypass), D (-Force still verifies) behave per deploy/HUMAN-UAT-10-02.md"
    why_human: "Requires clean Windows Server VM with IIS + AD domain-join. No Pester infra (out of scope per CONTEXT.md R-05). Explicitly pre-flagged as deferred checkpoint (10-02-CHECKPOINT.md), same pattern as Phase 7 operator UAT deferral."
---

# Phase 10 — Operational Readiness Verification Report

**Phase Goal:** Close v1.4.0 by making operational posture observable, self-verifying, and CI-gated (STAB-018..021). No new product surfaces.
**Verified:** 2026-04-20
**Status:** human_needed (automated verification PASSED; one operator-UAT checkpoint deferred — formally accepted per CONTEXT.md R-05 / phase-7 pattern)
**Re-verification:** No — initial verification

---

## Goal Achievement — Per-Requirement Verdict

| Req       | Verdict        | Evidence                                                                                                                                                                                                                                                                                                                                                                                       |
| --------- | -------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| STAB-018  | PASS           | `HealthController.cs`: `GetAsync` at L53, nested `checks` with `ad`/`smtp`/`expiryService` at L58-76, `CheckSmtpAsync` uses `TcpClient.ConnectAsync(host, port, cts.Token)` with 3s CTS (L92), `skipped` short-circuit returns `("skipped",0,true)` at L85, `CheckExpiryService` (L102) — no sync `TcpClient.Connect(` remains (grep: 0 matches). `Program.cs` wires `IExpiryServiceDiagnostics` in all 3 DI branches (L155 debug, L177-178 prod-enabled, L182 prod-disabled). Secrets omitted from response (type is anonymous DTO with only status/latency/timestamp). 11 new tests green (HealthControllerTests, SmtpProbeTests, ExpiryServiceDiagnosticsTests). Commits 45452b2 + 2891ba0 + 94bd10e present. |
| STAB-019  | PASS (code) + HUMAN_NEEDED (UAT) | `Install-PassReset.ps1` L99-101: `[switch] $SkipHealthCheck = $false` added to param block. L944-997: STAB-019 post-deploy block — `Invoke-WebRequest` to `/api/health` (L968) + `/api/password` (L969), `$maxAttempts = 10` (L956), `Start-Sleep 2s` retry, hard-fail with `exit 1` on timeout (L981), ASCII-only success banner `Health OK -- AD: ..., SMTP: ..., ExpiryService: ...` (L989), `-SkipHealthCheck` bypass at L995. `deploy/HUMAN-UAT-10-02.md` exists with scenarios A-D. Task 3 (operator UAT) deferred per 10-02-CHECKPOINT.md — same Phase 7 pattern. Commits d579046 + bedc341 + ddd2b94 present. |
| STAB-020  | PASS           | `.github/workflows/tests.yml`: `security-audit` job at L58, parallel (no `needs:`, runs-on windows-latest). Steps: checkout + .NET/Node setup + `npm ci` + `dotnet restore` + `npm audit --json` (L86) + `dotnet list ... --vulnerable --include-transitive` (L126) + `GITHUB_STEP_SUMMARY` post (L158-161). Gate: high/critical only; moderate/low via `Write-Warning`. Both audit steps have explicit `exit 1` on unsuppressed findings (L119, L150). Allowlist: `deploy/security-allowlist.json` with empty `advisories: []` + `_readme` (90-day policy); reader filters `expires -gt $today` so expired entries fall through as unfixed. `docs/Security-Audit-Allowlist.md` documents schema + 90-day policy + Dependabot/CodeQL independence (D-15). No `continue-on-error` in job. Commits 8257d45 + 77e38df + ce5ef8b present. |
| STAB-021  | PASS           | `PasswordForm.tsx`: `AdPasswordPolicyPanel` import at L22, render at L302 — BEFORE Username TextField at L309 (DOM-order above, no Collapse/Accordion wrapper). `AdPasswordPolicyPanel.tsx`: `role="region"` L35 + `aria-label="Password requirements"` L36 — no `aria-expanded`/`aria-controls` (a11y test guards regression). `ShowAdPasswordPolicy = true` across all 4 files (config-schema-sync invariant intact): `ClientSettings.cs` L27, `appsettings.json` L108, `appsettings.Production.template.json` L106, `appsettings.schema.json` L246. No server-side API change (`PasswordController.GetPolicy` logic untouched — D-18). No FGPP/PSO introduced (D-17). Tests `AdPasswordPolicyPanel.test.tsx` + `AdPasswordPolicyPanel.a11y.test.tsx` created. Commits fd22d05 + 1ced4e7 + d1cd3c5 + f9427eb present. |

---

## Observable Truths Coverage

| #  | Truth                                                                                   | Status     | Evidence                                     |
| -- | --------------------------------------------------------------------------------------- | ---------- | -------------------------------------------- |
| 1  | /api/health returns nested ad/smtp/expiryService checks with status/latency/last_checked | VERIFIED   | HealthController.cs L58-76                   |
| 2  | Aggregate rollup: unhealthy > degraded > healthy                                        | VERIFIED   | HealthController.cs L59-61 (Contains logic)  |
| 3  | SMTP skipped=true when both email features disabled                                     | VERIFIED   | HealthController.cs L85                      |
| 4  | No secrets in health response body                                                      | VERIFIED   | Test `Health_Body_ContainsNoSecrets` green   |
| 5  | Async ConnectAsync with 3s CTS (no sync Connect)                                        | VERIFIED   | grep `TcpClient.Connect(` → 0 matches        |
| 6  | IExpiryServiceDiagnostics injected in all 3 DI branches                                 | VERIFIED   | Program.cs L155, L177-178, L182              |
| 7  | Installer calls /api/health + /api/password after deploy                                | VERIFIED   | Install-PassReset.ps1 L968-969               |
| 8  | 10x2s retry loop with hard-fail exit 1                                                  | VERIFIED   | Install-PassReset.ps1 L956, L981             |
| 9  | -SkipHealthCheck bypass (default false)                                                 | VERIFIED   | Install-PassReset.ps1 L101, L995             |
| 10 | ASCII-only Health OK banner                                                             | VERIFIED   | Install-PassReset.ps1 L989                   |
| 11 | Operator UAT actually executed on VM                                                    | HUMAN      | Deferred per 10-02-CHECKPOINT.md + Phase 7 pattern |
| 12 | security-audit job parallel to tests (no needs)                                         | VERIFIED   | tests.yml L58; python yaml parse confirms    |
| 13 | npm audit gates high+critical only                                                      | VERIFIED   | tests.yml L86-119 (severity filter)          |
| 14 | dotnet vulnerable gates High/Critical via GHSA regex                                    | VERIFIED   | tests.yml L126-150                           |
| 15 | Expired allowlist entries fail CI                                                       | VERIFIED   | tests.yml `Where-Object { $_.expires -gt $today }` filter (L92-94, L131-133) — expired IDs drop from $validIds, fall through as unfixed |
| 16 | Moderate/low print as warnings, do not fail                                             | VERIFIED   | Write-Warning branches in both steps         |
| 17 | Panel renders above Username                                                            | VERIFIED   | PasswordForm.tsx L302 (panel) < L309 (Username label) |
| 18 | Panel visible by default, no disclosure widget                                          | VERIFIED   | ShowAdPasswordPolicy=true × 4; no Collapse/Accordion in panel |
| 19 | role=region + aria-label "Password requirements"                                        | VERIFIED   | AdPasswordPolicyPanel.tsx L35-36             |
| 20 | No FGPP/PSO / no server-side change                                                     | VERIFIED   | PasswordController.GetPolicy unchanged; D-17/D-18 honored |

**Score:** 19/20 VERIFIED in code; 1 human-verified deferred (STAB-019 UAT) — matches Phase 7 acceptance pattern.

---

## Required Artifacts — All Present

- `src/PassReset.Web/Services/IExpiryServiceDiagnostics.cs` ✓
- `src/PassReset.Web/Services/NullExpiryServiceDiagnostics.cs` ✓
- `src/PassReset.Web/Controllers/HealthController.cs` ✓ (modified)
- `src/PassReset.Web/Program.cs` ✓ (modified)
- `src/PassReset.Web/Services/PasswordExpiryNotificationService.cs` ✓ (modified)
- `src/PassReset.Tests/Web/Controllers/HealthControllerTests.cs` ✓
- `src/PassReset.Tests/Services/SmtpProbeTests.cs` ✓
- `src/PassReset.Tests/Services/ExpiryServiceDiagnosticsTests.cs` ✓
- `deploy/Install-PassReset.ps1` ✓ (modified)
- `deploy/HUMAN-UAT-10-02.md` ✓
- `.github/workflows/tests.yml` ✓ (modified; security-audit job)
- `deploy/security-allowlist.json` ✓
- `docs/Security-Audit-Allowlist.md` ✓
- `src/PassReset.Web/ClientApp/src/components/PasswordForm.tsx` ✓ (modified)
- `src/PassReset.Web/Models/ClientSettings.cs` ✓ (default flipped)
- `src/PassReset.Web/appsettings.json` ✓ (modified)
- `src/PassReset.Web/appsettings.Production.template.json` ✓ (modified)
- `src/PassReset.Web/appsettings.schema.json` ✓ (modified)
- `src/PassReset.Web/ClientApp/src/components/__tests__/AdPasswordPolicyPanel.test.tsx` ✓
- `src/PassReset.Web/ClientApp/src/components/__tests__/AdPasswordPolicyPanel.a11y.test.tsx` ✓

---

## Key Link Verification

| From                          | To                                    | Status | Detail                                          |
| ----------------------------- | ------------------------------------- | ------ | ------------------------------------------------|
| HealthController              | IExpiryServiceDiagnostics             | WIRED  | L27 field + L36 ctor + all 3 DI branches in Program.cs |
| HealthController              | IOptions<SmtpSettings/Email/Expiry>   | WIRED  | Ctor injection; consumed in CheckSmtpAsync      |
| PasswordExpiryNotificationSvc | _lastTickTicks (Interlocked)          | WIRED  | Tests cover concurrent read/write               |
| Installer post-deploy block   | /api/health + /api/password           | WIRED  | Invoke-WebRequest L968-969, retry loop wraps    |
| tests.yml security-audit      | deploy/security-allowlist.json        | WIRED  | Get-Content + ConvertFrom-Json both audit steps |
| ci.yml                        | tests.yml (includes security-audit)   | WIRED  | Existing workflow_call; new job auto-inherited  |
| PasswordForm                  | AdPasswordPolicyPanel (above Username)| WIRED  | L302 render, L309 Username — DOM order correct  |

---

## Regressions — None

- Backend: 179/179 green (`dotnet test --configuration Release`).
- Frontend: 54/54 green (`vitest run`).
- Existing Phase 8 config-schema-sync invariant intact (ShowAdPasswordPolicy coherent across 4 files).
- ESLint: 2 pre-existing errors in BrandHeader.tsx + usePolicy.ts — NOT introduced by Phase 10 (tracked separately).

---

## Anti-Patterns / Stubs — None

No TODO/FIXME/placeholder found in Phase 10 modified files. All implementations are substantive (HealthController is ~150 LOC of real logic; installer block is ~55 LOC; security-audit job is ~105 YAML lines with actual parsing logic; PasswordForm move is structural, no dead code).

---

## Human Verification Required

### 1. Install-PassReset.ps1 operator UAT (STAB-019)

**Test:** Execute `deploy/HUMAN-UAT-10-02.md` scenarios A–D on a clean Windows Server 2019+ VM with IIS + AD domain-join.

- Scenario A (success): `Health OK --` line + exit 0.
- Scenario B (failure): 10 retry warnings + `Post-deploy health check failed after 10 attempts.` + exit ≥ 1.
- Scenario C (skip): `Skipping post-deploy health check` line + no IIS access-log entry.
- Scenario D (force): verification runs under `-Force`.

**Expected:** All four scenarios pass, or formally deferred as `DEFERRED — physical host unavailable` per Phase 7 precedent.

**Why human:** Installer exercise requires real IIS + AD infra. No Pester infra — explicitly out of scope per CONTEXT.md R-05. Pre-flagged as `checkpoint:human-verify gate="blocking"` in plan 10-02 Task 3. Can be closed with `approved` (full UAT) or `deferred — no VM` (same precedent used in phases 07-01/07-03/07-04).

---

## Close-Out Assessment

**Phase 10 is ready to close for v1.4.0**, modulo the pre-flagged STAB-019 operator-UAT checkpoint which is a documented deferral (same pattern as Phase 7). All four requirements deliver against their stated goals; all 13 claimed commits are present; all 10 new artifacts exist; all key links are wired; regression suite is green; ESLint warnings are pre-existing (not introduced this phase). Accept the deferral with `deferred — no VM`, sign off HUMAN-UAT-10-02.md accordingly, then cut v1.4.0 tag.

---

_Verified: 2026-04-20_
_Verifier: Claude (gsd-verifier)_
