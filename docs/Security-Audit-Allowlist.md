# Security Audit Allowlist

Operator guide for managing documented exceptions to the CI security-audit gate (STAB-020).

## Purpose

STAB-020 ships a CI security gate that runs on every push and pull request. The gate wraps `npm audit` (frontend) and `dotnet list package --vulnerable --include-transitive` (backend) inside `.github/workflows/tests.yml`. Any unknown **high** or **critical** advisory fails the build.

`deploy/security-allowlist.json` is the **documented exception path**. When an advisory cannot be fixed immediately (no upstream patch, transitive dependency waiting on a parent bump, non-exploitable in our context, etc.), an allowlist entry lets CI pass while the issue stays tracked and time-boxed.

## File Location

- **Path:** `deploy/security-allowlist.json`
- **Format:** JSON object with `_readme` (string) and `advisories` (array) top-level keys.

## Adding an Entry

Open a pull request that edits `deploy/security-allowlist.json`. Each entry is a JSON object with **exactly these four fields**:

| Field       | Type   | Description                                                                                                         |
| ----------- | ------ | ------------------------------------------------------------------------------------------------------------------- |
| `id`        | string | Advisory identifier, format `GHSA-xxxx-xxxx-xxxx`. Matches both the npm `via[].url` tail and the dotnet text output regex. |
| `rationale` | string | 1–3 sentences explaining why the advisory cannot be fixed right now (e.g., "upstream has no patch; exposure limited to X"). |
| `expires`   | string | ISO 8601 date (`YYYY-MM-DD`). MUST be within **90 days** of the PR merge date.                                       |
| `scope`     | string | Exactly `"npm"` or `"nuget"`. Determines which scanner applies the suppression.                                      |

### Worked Example

```json
{
  "_readme": "STAB-020 allowlist. Suppress documented npm/NuGet advisories. Every entry expires in <=90 days and must be re-reviewed. Scope is 'npm' or 'nuget'. CI reads this file; expired entries fail the build.",
  "advisories": [
    {
      "id": "GHSA-1234-5678-abcd",
      "rationale": "Transitive dependency of eslint; upstream patch pending. Vulnerability requires untrusted config input which we do not accept at runtime.",
      "expires": "2026-07-15",
      "scope": "npm"
    }
  ]
}
```

Walkthrough of the fields above:

- `id`: exact GHSA from the npm audit advisory URL (`https://github.com/advisories/GHSA-1234-5678-abcd`).
- `rationale`: explains the upstream status and our exposure — reviewers use this to accept the risk.
- `expires`: 2026-07-15 is within 90 days of a ~2026-04 merge. After this date, CI treats the advisory as unfixed again.
- `scope`: `"npm"` — the dotnet audit step ignores this entry.

## Expiration Policy

- **Entries expire in 90 days or less.** The 90-day cap is enforced by the **reviewer** during PR code review; there is no automatic rejection of `expires` values > 90 days (process-gated, per D-12).
- **Expired entries do not suppress anything.** Once `expires` is in the past, the allowlist filter in CI skips the entry and the advisory falls back to "unfixed" — the build fails. VALIDATION row 10-03-03 explicitly tests this path.
- **No silent drift.** Every stale entry surfaces as a CI failure, which forces re-review.

## Renewing an Entry

When an advisory is still unfixed at expiration, open a **new PR** that:

1. Updates only the `expires` field to a new date (again ≤ 90 days out).
2. Appends to `rationale` with a re-review note, e.g. `"re-reviewed 2026-07-10 — upstream still pending"`.

Do not silently bump `expires` without updating `rationale` — the audit trail (`git log deploy/security-allowlist.json`) depends on it.

## Relationship to Dependabot and CodeQL

STAB-020 is a **PR/push gate only**. Dependabot and CodeQL continue their periodic deep scans **independently** (per D-15):

- **Dependabot** opens PRs for package updates on its own schedule. The allowlist **does not** suppress Dependabot alerts.
- **CodeQL** runs its own security analysis. The allowlist **does not** affect CodeQL findings.

If an advisory appears in Dependabot or CodeQL but is allowlisted here, the allowlist entry covers only the `tests.yml` security-audit gate — remediate the Dependabot/CodeQL finding on its own track.

## Commit Convention

Changes to `deploy/security-allowlist.json` use one of:

- `chore(security): add allowlist entry for GHSA-xxxx-xxxx-xxxx`
- `chore(security): renew allowlist entry for GHSA-xxxx-xxxx-xxxx`
- `chore(deps): remove expired allowlist entry for GHSA-xxxx-xxxx-xxxx`

See [CLAUDE.md](../CLAUDE.md#commit-convention) for the full scope list.

## Related Documents

- [docs/Secret-Management.md](Secret-Management.md) — credential-handling operator guide
- [docs/Known-Limitations.md](Known-Limitations.md) — documented trade-offs
- STAB-020 tracking issue in the project backlog
