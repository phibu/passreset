---
phase: 09-security-hardening
plan: 02
requirements: [STAB-014]
status: complete
commits:
  - 03804de feat(web): add AuditEvent allowlist DTO with reflection redaction tests
  - a272fa2 test(security): add STAB-014 RateLimitAndRecaptchaTests
---

# Plan 09-02 Summary — STAB-014

## Outcome

POST `/api/password` rate-limit and reCAPTCHA branches are now covered by
integration tests. Rate limiter proven to emit 429 on the 6th request within
the 5-min fixed window, and per-factory partitions proven independent.
reCAPTCHA-enabled path proven to reject invalid tokens via real Google
siteverify (D-19 no-abstraction).

## Files Modified

- `src/PassReset.Tests/Web/Controllers/RateLimitAndRecaptchaTests.cs` — 4 integration
  tests across 2 factory fixtures (`RateLimitFactory`, `RecaptchaEnabledFactory`).

The production AuditEvent DTO (committed earlier as `03804de`) provides the
allowlist type used by 09-03's structured logging path and is a prerequisite
for downstream audit work.

## Verification

- `dotnet test --filter "FullyQualifiedName~RateLimitAndRecaptchaTests"` → 4/4 green.
- Rate limit: 6th in-window request → 429 ✓
- Rate limit: fresh factory first request → 200 ✓
- Recaptcha enabled + bad token → InvalidCaptcha ✓
- Recaptcha disabled + empty token → proceeds ✓

## Notes

The `RecaptchaEnabledFactory` needed an explicit `SiteKey` (non-empty per the
option validator) alongside the always-fail `PrivateKey`. Used Google's public
test site key; no client-side widget is invoked.
