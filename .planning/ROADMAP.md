# PassReset — Roadmap

**Milestone chain:** v1.2.3 ✅ → v1.3.0 ✅ → v1.3.1 (active) → v2.0.0
**Granularity:** coarse
**Parallelization:** enabled
**Created:** 2026-04-14
**Last updated:** 2026-04-15

## Shipped Milestones

- ✅ **v1.2.3 Hotfix** (2026-04-14) — 3 P1 bugs fixed. See [`milestones/v1.2.3-ROADMAP.md`](milestones/v1.2.3-ROADMAP.md).
- ✅ **v1.3.0 Test Foundation + UX Features** (2026-04-15) — QA-001 + FEAT-001..004. See [`milestones/v1.3.0-ROADMAP.md`](milestones/v1.3.0-ROADMAP.md).

## Active Phases — v1.3.1 (next release)

- [ ] **Phase 7: v1.3.1 AD Diagnostics** — Structured logging for E_ACCESSDENIED and other AD password-change failures (promoted from backlog 999.1)

## Active Phases — v2.0.0

- [ ] **Phase 4: v2.0 Multi-OS PoC** — Research cross-platform path and produce Docker PoC
- [ ] **Phase 5: v2.0 Local Password DB** — Operator-managed banned-words + attempted-pwned lookup store
- [ ] **Phase 6: v2.0 Secure Config Storage** — Eliminate cleartext secrets from appsettings.Production.json

## Phase Details

### Phase 7: v1.3.1 AD Diagnostics
**Goal**: Diagnose intermittent `0x80070005 (E_ACCESSDENIED)` (and related) password change failures by adding structured logging around every step of the AD password change flow. External behavior unchanged — only internal diagnostics improved.
**Depends on**: v1.3.0 (shipped)
**Parallel with**: None (ships as a standalone diagnostic patch before v2.0 work starts)
**Target release**: v1.3.1 (patch release)
**Requirements**: BUG-004
**Success Criteria** (what must be TRUE):
  1. Every AD password-change call path logs structured events for: user lookup (before/after), `ChangePasswordInternal` (before/after), and `Save()` (before/after) — including AD context (domain, DC hostname, identity type, `UserCannotChangePassword`, `LastPasswordSet` — never secrets)
  2. Exceptions are logged with the full exception chain: type, `HResult`, message, and depth of each inner exception
  3. Targeted catches exist for `PasswordException`, `PrincipalOperationException`, and `DirectoryServicesCOMException` with distinct log context per type
  4. Every request correlates via `HttpContext.TraceIdentifier`, emitted once at controller entry and included in every downstream log entry during the request
  5. Lockout decorator logs state transitions (counter increments, thresholds crossed, window evictions)
  6. No passwords, plaintext, or SIEM-forbidden values leak into logs — spot-checkable via a simple grep rule
  7. User-facing error responses are unchanged from v1.3.0
**Plans**: 1 plan (single cohesive logging refactor across provider + controller + lockout decorator)

### Phase 4: v2.0 Multi-OS PoC
**Goal**: A documented, evidence-backed decision on cross-platform viability, validated by a working Docker PoC against a test AD
**Depends on**: v1.3.0 (shipped)
**Parallel with**: None (findings gate Phases 5 and 6 design)
**Target release**: v2.0.0 (research deliverable; production migration may be deferred)
**Requirements**: V2-001
**Success Criteria** (what must be TRUE):
  1. A research document exists comparing `Novell.Directory.Ldap.NETStandard` (and alternatives) against the current `System.DirectoryServices.AccountManagement` usage, with a recommended path
  2. A Docker image builds from the repo and performs a successful password change against a test AD without `S.DS.AM`
  3. An explicit go/no-go decision on full Linux support is captured in `PROJECT.md` Key Decisions
  4. A provider abstraction boundary is identified (or confirmed sufficient) such that future cross-platform work doesn't require a rewrite
**Plans**: TBD

### Phase 5: v2.0 Local Password DB
**Goal**: Operators can enforce banned-word and attempted-pwned rules locally, independent of (and stricter than) AD policy
**Depends on**: Phase 4 (provider-abstraction findings inform integration shape)
**Parallel with**: Phase 6 (could overlap once Phase 4 lands, but coarse granularity keeps them sequential by default)
**Target release**: v2.0.0
**Requirements**: V2-002
**Success Criteria** (what must be TRUE):
  1. Operators can add and remove banned terms via a documented mechanism; changes take effect without code rebuild
  2. A local attempted-pwned lookup store exists and is consulted during password change; matches reject the change with a distinct `ApiErrorCode`
  3. Local rules are enforced even when AD would accept the password (strictly additive)
  4. Any borrowed logic (e.g., from lithnet/ad-password-protection) has a LICENSE-compatible integration documented in the repo
**Plans**: TBD

### Phase 6: v2.0 Secure Config Storage
**Goal**: Secrets in `appsettings.Production.json` are never stored as cleartext on disk by default, with a clear upgrade path for existing installs
**Depends on**: Phase 4 (cross-platform constraints shape mechanism choice — e.g., DPAPI is Windows-only)
**Parallel with**: Phase 5 (independent of V2-002 scope)
**Target release**: v2.0.0
**Requirements**: V2-003
**Success Criteria** (what must be TRUE):
  1. SMTP, reCAPTCHA, and LDAP credentials can be stored via a secure mechanism (DPAPI / ASP.NET Core Data Protection / Credential Manager / optional Key Vault adapter) chosen and documented
  2. A fresh install has no cleartext secrets on disk by default
  3. An existing v1.3.x install can upgrade to v2.0 and migrate its secrets following a documented procedure in `UPGRADING.md`
  4. `docs/Secret-Management.md` reflects the new default and documents fallback/override knobs
**Plans**: TBD

## Cross-Phase Dependencies

| From | To | Nature |
|---|---|---|
| Phase 4 | Phase 5 | Provider-abstraction decision informs local-DB integration point |
| Phase 4 | Phase 6 | Platform decision (Windows-only vs cross-platform) constrains secret-storage mechanism |

## Parallelism Map

- **Sequential default:** Phase 4 → Phase 5 → Phase 6
- Phases 5 and 6 *could* run in parallel once Phase 4 lands; coarse granularity keeps them sequential unless capacity allows.

## Progress

| Phase | Plans Complete | Status | Completed |
|---|---|---|---|
| 7. v1.3.1 AD Diagnostics | 0/1 | Not started | — |
| 4. v2.0 Multi-OS PoC | 0/0 | Not started | — |
| 5. v2.0 Local Password DB | 0/0 | Not started | — |
| 6. v2.0 Secure Config Storage | 0/0 | Not started | — |

## Coverage

- Active requirements: **4** (BUG-004 for v1.3.1; V2-001, V2-002, V2-003 for v2.0)
- Mapped: **4/4** ✓
- Orphans: **0**

---
*Last updated: 2026-04-15 (promoted backlog 999.1 → Phase 07 / v1.3.1 AD Diagnostics)*
