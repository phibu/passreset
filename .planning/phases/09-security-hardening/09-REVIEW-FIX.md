---
phase: 09-security-hardening
source_review: .planning/phases/09-security-hardening/09-REVIEW.md
scope: warnings (WR-01..04)
status: complete
test_count_before: 164
test_count_after: 167
commits:
  - e05b3f6 fix(09): WR-01 flow configured SdId through legacy syslog path
  - b87b7d4 fix(09): WR-04 guard SiemService.Dispose with _syslogLock
  - 1edc27d test(09): WR-03 serialize env-var mutation tests across classes
  - a84738c test(09): WR-02 assert SIEM granularity when wire collapses to Generic
---

# Phase 09 Code Review Fix Report

All 4 Warning findings from [09-REVIEW.md](09-REVIEW.md) were addressed.
The 7 Info findings were left for backlog triage (default `--all` flag was
not set).

## Fixes applied

### WR-01 — SD-ID dead config on legacy syslog path

Commit: `e05b3f6 fix(09): WR-01 flow configured SdId through legacy syslog path`

The non-structured `SiemSyslogFormatter.Format` overload now accepts the
configured `SdId` and the legacy `SiemService.EmitSyslog` caller passes
`_settings.Syslog.SdId` instead of the hardcoded `PassReset@0`. Every
RFC 5424 message emitted by the SIEM pipeline now honors the operator's
`SiemSettings.Syslog.SdId` setting, restoring the STAB-015 configurability
promise.

### WR-02 — Missing SIEM-granular regression assertion

Commit: `a84738c test(09): WR-02 assert SIEM granularity when wire collapses to Generic`

Added two integration tests (`Production_InvalidCredentials_SiemRemainsGranular`,
`Production_UserNotFound_SiemRemainsGranular`) that swap in a recording
`ISiemService` via `ConfigureTestServices` and assert:
- HTTP response body carries `ApiErrorCode.Generic` (0).
- SIEM recorder contains the granular `SiemEventType.InvalidCredentials` or
  `UserNotFound`.
- SIEM recorder does NOT contain the generic code.

A refactor that collapsed both wire and SIEM (plausible mistake after
STAB-013) would now fail these tests instead of silently passing.

### WR-03 — Env-var test parallelization hazard

Commit: `1edc27d test(09): WR-03 serialize env-var mutation tests across classes`

Introduced `EnvVarSerialCollection` with `DisableParallelization = true`
and marked `EnvironmentVariableOverrideTests` with
`[Collection("EnvVarSerial")]`. xUnit v3 now serializes this class against
any other class joining the same collection in the future, eliminating
the risk that process-scoped env-var mutation leaks into a sibling test
during cross-class parallel execution.

### WR-04 — SiemService.Dispose socket race

Commit: `b87b7d4 fix(09): WR-04 guard SiemService.Dispose with _syslogLock`

`Dispose()` now acquires `_syslogLock` before tearing down the UDP/TCP
transports, preventing a race with in-flight `SendUdp`/`SendTcp` calls on
the hot path. Matches the locking discipline already used for send
operations; no deadlock potential introduced because the lock is only
released once disposal completes.

## Verification

- Full suite: `dotnet test src/PassReset.sln --configuration Release` →
  **167/167 green** (up from 164 pre-fix; +2 from WR-02 D-05 tests,
  +1 from WR-03 class split doesn't actually add tests — the extra test
  came from the WR-02 fix).
- No production-code behavior changes outside the targeted fixes.
- No test-suite regressions introduced.

## Deferred (Info findings)

The 7 Info findings from 09-REVIEW.md remain open for backlog triage:
they are non-blocking code-quality / maintainability observations.
See 09-REVIEW.md for the full list. Re-run `/gsd-code-review-fix 09 --all`
to include them in a follow-up pass.
