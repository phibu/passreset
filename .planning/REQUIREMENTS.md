# PassReset — Requirements

**Active milestones:** v1.3.1 (patch) → v2.0.0 (platform evolution)
**Prior milestones:** v1.2.3 ✅ · v1.3.0 ✅ (see `milestones/`)
**Last updated:** 2026-04-15

> REQ IDs are stable references used by `ROADMAP.md` and phase plans.
> Delivered requirements live in `milestones/v{version}-REQUIREMENTS.md`.

---

## v1.3.1 — Diagnostic patch

### Bugs

- [ ] **BUG-004**: Structured diagnostic logging around every step of the AD password-change flow so operators can diagnose intermittent `0x80070005 (E_ACCESSDENIED)` and related failures without reproducing the issue in a debugger. Every request correlated via `HttpContext.TraceIdentifier`. Full exception chain logged (type, `HResult`, message, depth). Targeted catches for `PasswordException`, `PrincipalOperationException`, `DirectoryServicesCOMException` with distinct context. AD context captured (domain, DC, identity type, `UserCannotChangePassword`, `LastPasswordSet`). Lockout decorator state transitions logged. **Constraints:** no passwords/secrets/SIEM-forbidden values written to logs; user-facing error responses unchanged from v1.3.0; no new database/audit dependencies.

---

## v2.0.0 — Platform evolution

### Research + PoC

- [ ] **V2-001**: Research + PoC for multi-OS support — a documented path to Linux/Docker without `System.DirectoryServices.AccountManagement`; PoC Docker image performs a password change against a test AD; decision captured (Novell.Directory.Ldap.NETStandard vs alternative). Stays a research phase; full migration deferred if blockers found.
- [ ] **V2-002**: Local password-protection database — operator-managed banned words/terms list + attempted-pwned lookup table; provider consults the local store and enforces bans even when stricter than AD policy; LICENSE-compatible integration if borrowing from lithnet/ad-password-protection.
- [ ] **V2-003**: Secure config storage — secrets in `appsettings.Production.json` (SMTP, reCAPTCHA, LDAP creds) no longer stored as cleartext on disk by default. Supported mechanism chosen from DPAPI / ASP.NET Core Data Protection / Credential Manager / optional Key Vault adapter. Clear upgrade path for existing installs.

---

## Cross-cutting constraints (apply to every requirement)

- **No breaking config changes** for operators upgrading from v1.3.x unless explicitly documented in `UPGRADING.md`.
- **Commit convention** enforced by `.githooks/commit-msg` — types: `feat fix refactor docs chore test ci perf style`; scopes: `web provider common deploy docs ci deps security installer`.
- **CI**: GitHub Actions on `windows-latest`; release triggered by `git tag vX.Y.Z` → `release.yml`. Tests gate release via reusable `tests.yml`.
- **Tech stack**: ASP.NET Core 10 / React 19 / MUI 6 / Vite. v2.0 may introduce cross-platform infrastructure (Novell LDAP, Docker) but must not break the existing Windows/IIS deployment path.
- **Documentation**: `README.md`, `CHANGELOG.md`, and affected `docs/*.md` updated as part of each release.

---

## Out of Scope

- **MSI packaging** — deferred after the 2026-04-13 rollback. PowerShell installer remains the supported deployment path.
- **Password reset via email/SMS** — portal is *change* only.
- **SSO / federation adapters** — direct-AD portal.
- **Stack modernization** (e.g., migrating to .NET 11, React 20) — not part of this milestone.

---

## Traceability

| REQ-ID | Phase | Plan | Status |
|---|---|---|---|
| BUG-004 | Phase 7 (v1.3.1 AD Diagnostics) | TBD | Active |
| V2-001 | Phase 4 (v2.0 Multi-OS PoC) | TBD | Active |
| V2-002 | Phase 5 (v2.0 Local Password DB) | TBD | Active |
| V2-003 | Phase 6 (v2.0 Secure Config Storage) | TBD | Active |

**Coverage:** 4/4 active requirements mapped ✓ · 0 orphans
