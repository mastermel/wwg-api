# api/

.NET 10 ASP.NET Core Minimal API. Full conventions: DESIGN.md §4.1.

## Commands (from the repo root)

```sh
dotnet build api/Wwg.slnx              # warnings are errors
dotnet test --solution api/Wwg.slnx    # integration tests (xUnit v3, Microsoft Testing Platform)
dotnet csharpier format .              # format (CSharpier owns layout; line width 100)
dotnet csharpier check .               # what CI runs
```

Don't run `dotnet format whitespace`; it fights CSharpier. `dotnet format style` and
`dotnet format analyzers` are fine for auto-fixes.

## Build setup

- `Directory.Build.props`: nullable, warnings as errors, `AnalysisMode=Recommended`, code style
  enforced in build.
- `Directory.Packages.props`: central package versions. Analyzers (Meziantou, BannedApiAnalyzers)
  are `GlobalPackageReference`s, so they apply to every project.
- `BannedSymbols.txt`: APIs that must not be used, each with what to use instead. An ID without a
  parameter list only matches the parameterless overload, so list every overload.
- `Directory.Build.targets`: installs the Husky.Net hook on restore.

## Code conventions

- Types are `internal sealed` by default; only make them `public` or unsealed when needed.
- Feature folders under `src/Wwg.Api/Features/`. Each feature exposes one
  `Map{Feature}Endpoints(this IEndpointRouteBuilder)`, called from `MapApiEndpoints`.
- Handlers are `internal static` methods (not lambdas) returning `Results<…>`, with an XML
  `<summary>`, which becomes the OpenAPI summary. They can't be `private`: the XML comment
  generator skips private methods. Every handler takes a `CancellationToken` and passes it on.
- Every endpoint has `.WithName("{Verb}{Resource}")` (the operationId, and the SDK's function
  name) and `.WithTags(...)`.
- DTOs are `sealed record`s: `{Verb}{Resource}Request`, `{Resource}Response` /
  `{Resource}Summary`. Several can share a `{Feature}Dtos.cs` file.
- EF Core: reads use `AsNoTracking()` and project to DTOs with `Select`. No lazy loading. Raw SQL
  only through the interpolated `FromSql` / `ExecuteSql`.
- Time comes from the injected `TimeProvider`; IDs from `Guid.CreateVersion7()`.
- No `!` (null-forgiving) without a comment saying why it's safe.
- Logging uses message templates with named placeholders, never interpolation.
- Comments explain *why*, not *what*. No `#region`s.

## Tests

- Endpoint-level integration tests only (no unit tests), over HTTP against a throwaway SQLite
  database.
- Named `Action_Scenario_ExpectedResult`, one behaviour per test.
- Setup goes through the shared helpers and scenario builders.
- A test that truly needs a banned API uses `#pragma warning disable RS0030` with a comment.
