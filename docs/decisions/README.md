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
