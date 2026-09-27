# 0001. Design review: security, correctness and maintainability changes

- **Date:** 2026-09-27
- **Status:** Accepted

## Context

The design was reviewed as a whole before implementation started. Everything
before this point was decided in conversation and is recorded directly in
`DESIGN.md`. The review found gaps that would otherwise have surfaced as bugs
or rework during implementation.

## Decision

Adopt all of the review's recommendations:

**Security**
- `Admin:Emails` is synced to the Admin role **at startup only**. Emails are
  unverified, so promoting at sign-up or on email change would let anyone
  claim Admin by registering a listed address first.
- Validate the Identity security stamp **on every request**, so deleted users
  and changed credentials stop working immediately rather than when the access
  token expires. This also prevents 500s from requests by deleted users.
- Our custom `/refresh` endpoint explicitly repeats `MapIdentityApi`'s checks
  (expiry, security stamp, user exists).
- Add `POST /api/me/sign-out-everywhere`; logout is otherwise client-side.
- Rate limiting on auth and forgot-password; forwarded headers behind the
  proxy so limits see the real client IP.
- Identity overrides: `RequireUniqueEmail = true`; unrestricted username
  characters (usernames are emails); lockout on failed login.

**Correctness**
- SQLite: mark `DateTime`s UTC on read; `NOCASE` collation on searchable text.
- Skip startup side effects during build-time OpenAPI generation.
- Unique-constraint races return 409 rather than 500.
- Commander assignment gets its own endpoints, so a rename can't unassign a
  commander by accident.
- All admin-only actions move under `/api/admin`.

**Maintainability and tests**
- Campaign permissions are declared per endpoint and enforced by a shared
  endpoint filter. A convention test fails if any endpoint lacks an access
  rule, name or tag.
- Pending-migration test; validated options for all settings.
- Migrations run once into a template DB that is cloned per test; cheap
  password hashing in tests; data-driven permission tests.
- Dependabot; OCI labels on images.

**API contract**
- The React repo generates its SDK from its own pinned copy of `openapi.json`.
- `oasdiff breaking` runs on PRs; intentional breaks need a `breaking-change`
  label.

**Documentation**
- `DESIGN.md` describes the current design; this log records why it changed.

## Consequences

- Slightly more infrastructure up front: endpoint filter, convention tests,
  template DB, security stamp middleware. All of it is scheduled in the plan
  (`DESIGN.md` §7).
- One extra indexed database lookup per authenticated request, which is
  negligible for SQLite at this scale.
- Deployment gains a manual step: register the Admin account before listing it
  in config.
- Breaking API changes now require an explicit label and a coordinated
  frontend update.
