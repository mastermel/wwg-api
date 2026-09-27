# 0003. Public DTOs, property-targeted attributes and opt-in trimming

- **Date:** 2026-09-27
- **Status:** Accepted

## Context

Setting up .NET 10 validation (`AddValidation()`) and input trimming (step 4)
showed that:
- The validation source generator only discovers **public** request types.
  With `internal` DTOs (the default in §4.1), validation silently never runs.
- DataAnnotations on a positional record's parameters are validated, but the
  OpenAPI generator only reads them from the **property**, so `maxLength` etc.
  were missing from the document.
- §3.3 said which strings are trimmed but not how. Trimming every string
  globally would also trim passwords. Trimming in the handler is too late,
  because validation runs first.
- A custom JSON converter on a property hides its type from the OpenAPI
  schema generator (`{}` instead of `"type": "string"`).
- Validation error keys are C# property names (`Name`), not the JSON names
  (`name`) the client sent.

## Decision

- DTOs (request and response records) are **`public sealed record`s**. Other
  types stay `internal sealed` by default.
- Attributes on positional record parameters use the **`property:` target**,
  e.g. `[property: Trimmed, Required, StringLength(100)] string Name`.
- An opt-in **`[Trimmed]`** attribute (a JSON converter) trims a string while
  the request body is read, before validation. It goes on name-like fields and
  emails, never passwords. An OpenAPI schema transformer restores the
  `string` type on `[Trimmed]` properties.
- Validation error keys are converted to **camelCase** in the Problem Details
  customization (`items[0].name`).

## Consequences

- One rule to remember instead of silent failures: DTOs are public, and
  attributes use `property:`. Both are in `api/CLAUDE.md`.
- No endpoint validates input yet, so trimming and validation are first
  covered by integration tests with the register endpoint (step 10).
