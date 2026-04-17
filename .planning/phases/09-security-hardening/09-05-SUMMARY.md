---
phase: 09-security-hardening
plan: 05
requirements: [STAB-017]
status: complete
commits:
  - 3569281 docs(security): STAB-017 env-var secrets workflow + STAB-013..017 CHANGELOG rollup
---

# Plan 09-05 Summary — STAB-017

## Outcome

SMTP, LDAP service-account, and reCAPTCHA secrets can now be sourced from
process environment variables or `dotnet user-secrets` via ASP.NET Core's
default `__` binding convention (D-15/D-16). Zero production-code changes;
one integration test plus 6 documentation updates. Secret externalization is
unblocked for production today without committing to the v2.0 DPAPI/Key Vault
mechanism (V2-003).

## Files Modified

- `src/PassReset.Tests/Web/Startup/EnvironmentVariableOverrideTests.cs` —
  3 integration tests: env-var override precedence, `[JsonIgnore]` leak regression
  (Pitfall 5), and test-isolation cleanup via `IDisposable`.
- `docs/Secret-Management.md` — STAB-017 section with user-secrets + appcmd + mapping table.
- `docs/IIS-Setup.md` — AppPool env-var appcmd snippets for each of the 3 secrets plus a STAB-016 HTTPS-binding reminder.
- `docs/appsettings-Production.md` — secrets/env-var override table with precedence notes.
- `docs/Known-Limitations.md` — STAB-013 wire-vs-SIEM divergence documentation.
- `CONTRIBUTING.md` — `dotnet user-secrets` developer workflow snippet.
- `CHANGELOG.md` — `[Unreleased]` rollup entries for STAB-013, STAB-014, STAB-015, STAB-016, STAB-017.

## Verification

- `dotnet test --filter "FullyQualifiedName~EnvironmentVariableOverrideTests"` → 3/3 green.
- `SmtpSettings__Password` env var overrides appsettings value ✓
- GET `/api/password` body never contains `PrivateKey` string even when set via env var ✓
- Unset env var path uses appsettings value ✓
- No production-code changes (`git diff --stat src/PassReset.Web/` against pre-plan state is empty).
- Installer does NOT set env vars (D-18 preserved).
- No custom `PASSRESET_` env-var prefix introduced (D-16 preserved).

## Notes

The plan was marked `autonomous: false` to allow operators to human-verify the
doc wording before the phase closes. The docs are written to copy-paste
directly into an operator console or a dev terminal; review any phrasing you
want to adjust before cutting the v1.4.0 tag.

Cross-phase doc tasks (STAB-013 wire-vs-SIEM note in Known-Limitations.md,
full STAB-013..017 CHANGELOG rollup) are included in this plan's commit per
the plan frontmatter contract.

## Human-Verify Checkpoint

Please confirm before phase close:
- [ ] Secret-Management.md STAB-017 section is operator-accurate for your environment.
- [ ] IIS-Setup.md appcmd snippets match your IIS 10.x/II1.x conventions.
- [ ] CHANGELOG.md entries under `[Unreleased]` reflect the actual scope of each requirement.
- [ ] CONTRIBUTING.md user-secrets snippet matches your expected dev bootstrap.
