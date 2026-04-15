---
phase: 07-v1-3-1-ad-diagnostics
fixed_at: 2026-04-15T00:00:00Z
review_path: .planning/milestones/v1.3.1-phases/07-v1-3-1-ad-diagnostics/07-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 07: Code Review Fix Report

**Fixed at:** 2026-04-15
**Source review:** `.planning/milestones/v1.3.1-phases/07-v1-3-1-ad-diagnostics/07-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (Warning only — Info findings deferred)
- Fixed: 2
- Skipped: 0

## Fixed Issues

### WR-01: ExceptionChainLogger has no cycle protection or depth bound

**Files modified:** `src/PassReset.PasswordProvider/ExceptionChainLogger.cs`
**Commit:** f9c50ae
**Applied fix:** Replaced the unbounded `for` walker with a `while` loop that (a) caps traversal at `MaxDepth = 32` frames, (b) uses a `HashSet<Exception>` with `ReferenceEqualityComparer.Instance` to detect cycles, and (c) appends an `ExceptionChainSentinel` frame to the emitted chain whenever either bound is hit so log evidence records the truncation/cycle reason. Preserved lowercase anonymous-type member names (`depth`/`type`/`hresult`/`message`) to keep existing `ExceptionChainLoggerTests` assertions green — IN-03 schema-casing remains open as a separate Info finding.

### WR-02: Redaction tests do not exercise the real provider catch paths

**Files modified:** `src/PassReset.Tests/PasswordProvider/PasswordLogRedactionTests.cs`
**Commit:** 12b036c
**Applied fix:** Adopted the "document the gap and rename misleading tests" branch of the reviewer's fix guidance. Driving the real `PasswordChangeProvider.ChangePasswordInternal` COMException paths requires either a live `UserPrincipal` or refactoring the provider to accept a swappable AD abstraction — out of scope for phase 07. Instead:
1. Renamed `DebugPasswordChangeProvider_DoesNotLogPlaintext` → `FakeProvider_DoesNotLogPlaintext` to accurately reflect that the test drives `FakeInvalidCredsProvider`, not the real debug provider.
2. Renamed `LockoutPasswordChangeProvider_DoesNotLogPlaintext` → `LockoutDecorator_DoesNotLogPlaintext_OverFakeInner` to clarify the decorator-over-fake scope.
3. Added a class-level XML doc comment explicitly documenting the coverage gap, enumerating the uncovered real-provider catch sites (lines 485 / 505 / 515 / 345 / 466 / 377 / 386), and noting that redaction safety of those sites rests on (a) none of the real templates passing plaintext passwords as template args (verified by code review) and (b) the existing `ExceptionChainLogger_CapturesInnerExceptionMessages_AcceptedRisk` test that directly exercises the helper those sites invoke.

**Verification:** Full solution rebuild succeeded (0 errors, pre-existing xUnit1051 warnings only). Full xUnit suite (`dotnet test src/PassReset.sln --configuration Release`) executed 81 tests, all passed.

## Skipped Issues

None.

---

_Fixed: 2026-04-15_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
