# Decision log

[`DESIGN.md`](../../DESIGN.md) describes the design **as it is now**. This
folder records **why** it changed, so the reasoning survives after the design
doc is updated.

## When to add an entry

Add one when a decision:

- changes something already in `DESIGN.md`, or
- picks between real alternatives that someone might later question
  (a library, a data-model rule, a security trade-off).

Small, obvious choices don't need an entry.

## Format

One file per decision: `NNNN-short-title.md`, numbered in order. Keep it short:

```markdown
# NNNN. Title

- **Date:** YYYY-MM-DD
- **Status:** Accepted | Superseded by NNNN

## Context
What prompted the decision.

## Decision
What we chose.

## Consequences
What this makes easier or harder; follow-up work.
```

Entries aren't edited after they're accepted, except to mark them superseded.
A reversal gets a new entry.

## Index

| # | Decision | Date |
|---|---|---|
| [0001](0001-design-review-hardening.md) | Design review: security, correctness and maintainability changes | 2026-09-27 |
| [0002](0002-single-repo-for-api-and-web.md) | One repository for the API and the React front-end | 2026-09-27 |
| [0003](0003-request-validation-and-trimming.md) | Public DTOs, property-targeted attributes and opt-in trimming | 2026-09-27 |
| [0004](0004-refresh-token-in-httponly-cookie.md) | Refresh token in an HttpOnly cookie, access token in memory | 2026-09-27 |
| [0005](0005-front-end-stack.md) | Front-end stack: Mantine, TanStack Router/Query, installable PWA | 2026-09-27 |
| [0006](0006-deploy-after-phase-1.md) | Deploy after Phase 1, on Komodo, before building accounts | 2026-09-27 |
| [0007](0007-end-to-end-tests.md) | End-to-end tests against the production image | 2026-09-28 |
| [0008](0008-backups-in-the-app.md) | Database backups, made by the app | 2026-09-28 |
| [0009](0009-campaign-map-stack.md) | The campaign map: MapLibre, OpenFreeMap, Mapterhorn and a server-side geocoder | 2026-09-28 |
| [0010](0010-turns-factions-and-visibility.md) | Turns in step, per army; orders; factions; one visibility rule | 2026-09-28 |
| [0011](0011-umpire-edits-orders.md) | The Umpire edits orders on a commander's behalf | 2026-09-29 |
| [0012](0012-admin-masquerade.md) | Admins masquerade as other users | 2026-09-30 |
| [0013](0013-wasatch-wargamers-branding.md) | The app is called Wasatch Wargamers, with a new mark | 2026-09-30 |
| [0014](0014-hex-grid-movement.md) | A hex grid, terrain and the rules' movement replace free movement | 2026-09-30 |
| [0015](0015-global-factions-and-units.md) | Factions and units are the club's, shared by every campaign | 2026-09-30 |
