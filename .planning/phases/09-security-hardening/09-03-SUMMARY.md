---
phase: 09-security-hardening
plan: 03
requirements: [STAB-015]
status: complete
commits:
  - 03804de feat(web): add AuditEvent allowlist DTO with reflection redaction tests
  - 400d6c7 feat(web): add SdId syslog setting with RFC 5424 syntax validation
  - ad4e93e feat(web): add AuditEvent+SdId overload to SiemSyslogFormatter
  - 442db00 feat(security): add STAB-015 LogEvent(AuditEvent) overload on SiemService
---

# Plan 09-03 Summary — STAB-015

## Outcome

SIEM operators can now index security events by `outcome`, `eventType`,
`user`, `ip`, `traceId`, and optional `detail` SD-PARAMs via an RFC 5424
STRUCTURED-DATA element. Events flow through a compile-time-redacted
`AuditEvent` record (no Password/Token/Secret/PrivateKey/ApiKey fields)
to prevent accidental secret leaks. The SD-ID is configurable via
`SiemSettings.Syslog.SdId` (default `passreset@32473`), validated for
RFC 5424 syntax (non-empty, ≤32 chars, no `=`/space/`]`/`"`).

## Files Modified

- `src/PassReset.Web/Services/AuditEvent.cs` — allowlist DTO record.
- `src/PassReset.Web/Services/ISiemService.cs` — `LogEvent(AuditEvent)` overload.
- `src/PassReset.Web/Services/SiemService.cs` — overload impl + `EmitSyslogStructured`.
- `src/PassReset.Web/Services/SiemSyslogFormatter.cs` — Format overload with `sdId` +
  `AuditEvent` that emits structured-data elements using the existing `EscapeSd` helper.
- `src/PassReset.Web/Models/SiemSettings.cs` — `SdId` property (default `passreset@32473`).
- `src/PassReset.Web/Models/SiemSettingsValidator.cs` — SD-ID syntax rule.
- `src/PassReset.Tests/Web/Services/AuditEventRedactionTests.cs` — reflection-based
  proof that `AuditEvent` has no secret-named properties.
- `src/PassReset.Tests/Web/Services/SiemSyslogFormatterTests.cs` — extended for
  structured-data formatting, escape semantics, and SD-ID parameter.

## Verification

- `dotnet test --filter "FullyQualifiedName~AuditEventRedactionTests|FullyQualifiedName~SiemSyslogFormatterTests"` → 17/17 green.
- `ISiemService.LogEvent(AuditEvent evt)` overload is callable from controllers.
- No new `SiemEventType` enum members added (D-11 preserved).
- Hot-path no-throw invariant preserved (try/catch-swallow-and-log in both
  legacy and structured emission paths).

## Notes

During phase 9 parallel-wave execution, some of this plan's edits landed as
uncommitted working-tree modifications when the subagent interrupted itself.
Those were reconciled inline via commit `442db00`. See the 2026-04-17 lessons
in `tasks/lessons.md` for the parallel-execution fallout that motivated
switching to sequential execution for the rest of the phase.
