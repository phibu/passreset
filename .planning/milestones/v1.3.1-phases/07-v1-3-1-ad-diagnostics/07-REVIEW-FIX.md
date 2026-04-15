---
phase: 07-v1-3-1-ad-diagnostics
fixed_at: 2026-04-15T00:00:00Z
review_path: .planning/milestones/v1.3.1-phases/07-v1-3-1-ad-diagnostics/07-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 07: Code Review Fix Report

**Fixed at:** 2026-04-15
**Source review:** `.planning/milestones/v1.3.1-phases/07-v1-3-1-ad-diagnostics/07-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope (Critical + Warning): 3
- Fixed: 3
- Skipped: 0

Build: `dotnet build src/PassReset.sln --configuration Release` — succeeded (0 errors, 10 pre-existing xUnit1051 warnings).
Tests: `dotnet test` — 81/81 passed.

## Fixed Issues

### WR-01: Message template arity mismatch in ChangePassword COM error log

**Files modified:** `src/PassReset.PasswordProvider/PasswordChangeProvider.cs`
**Commit:** f7d4fbe
**Applied fix:** Split the message template to include four named holes (`{HResult}`, `{Status}`, `{UseAutomaticContext}`, `{AllowSetPasswordFallback}`) matching the four positional arguments. Reformatted args one-per-line for readability. Replaced ambiguous `{Auto}` / `{Allow}` with the full, structured-sink-friendly property names as suggested by the reviewer.

### WR-02: Redaction tests do not exercise real PasswordChangeProvider log paths

**Files modified:** `src/PassReset.Tests/PasswordProvider/PasswordLogRedactionTests.cs`
**Commit:** 2657152
**Applied fix:** Added a new `ExceptionChainLogger_CapturesInnerExceptionMessages_AcceptedRisk` fact that builds a 3-level inner-exception chain whose messages carry both sentinels, invokes `ExceptionChainLogger.LogExceptionChain` directly, and asserts the sentinels DO appear in captured properties. XML doc on the test explicitly documents AD-supplied exception messages as the single accepted-risk leakage channel and names `ExceptionChainLogger` as the required future-redaction hook point. Mocking a real `PrincipalContext` is impractical (Windows-only, sealed types), so we adopted the reviewer's alternative proposal.

### WR-03: Redundant Debug + Warning log for every lockout failure increment

**Files modified:** `src/PassReset.PasswordProvider/LockoutPasswordChangeProvider.cs`
**Commit:** f7296c4
**Applied fix:** Removed the `LogDebug("lockout counter {Count}/{Threshold} for {Username}")` call that duplicated the `LogWarning` emission on the same hot path. Kept the `LogWarning` because SIEM / operator visibility of portal-failure counters is the intended behaviour per CONTEXT.md.

---

_Fixed: 2026-04-15_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
