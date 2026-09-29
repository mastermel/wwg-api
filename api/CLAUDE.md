# api/

.NET 10 ASP.NET Core Minimal API. Full conventions: DESIGN.md §4.1.

## Commands (from the repo root)

```sh
dotnet build api/Wwg.slnx              # warnings are errors
dotnet test --solution api/Wwg.slnx    # integration tests (xUnit v3, Microsoft Testing Platform)
dotnet csharpier format .              # format (CSharpier owns layout; line width 100)
dotnet csharpier check .               # what CI runs
dotnet ef migrations add <Name> --project api/src/Wwg.Api --output-dir Data/Migrations
```

Review generated migrations before committing (SQLite rebuilds tables for some changes). The
pre-commit hook formats them like any other file.

Don't run `dotnet format whitespace`; it fights CSharpier. `dotnet format style` and
`dotnet format analyzers` are fine for auto-fixes.

## The API contract

Every build writes `api/openapi.json`, the contract the front-end SDK is generated from. Commit it
with the code change that caused it; CI fails if it's out of date. Startup work with side effects
(or that needs real settings) must be skipped when `BuildTime.IsGeneratingOpenApiDocument` is
true, because the build launches the app to write the document.

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
- DTOs are **`public`** `sealed record`s: `{Verb}{Resource}Request`, `{Resource}Response` /
  `{Resource}Summary`. Several can share a `{Feature}Dtos.cs` file. They must be public: the
  validation source generator ignores internal types, and validation then silently never runs.
- Attributes on positional record parameters use the `property:` target, or OpenAPI misses them:
  `[property: Trimmed, Required, StringLength(100)] string Name`.
- `[Trimmed]` trims name-like strings and emails while the JSON is read (before validation).
  Never on passwords.
- `PropertySchemaTransformer` keeps the OpenAPI schema honest: it restores the string type on
  `[Trimmed]` properties and adds `minLength: 1` to `[Required]` strings (which reject empty
  strings), so the SDK's generated Zod schemas validate the same way the API does.
- **Access rules:** sign-in is required by default, but every endpoint declares its rule:
  `.AllowAnonymous()`, `.RequireSignedIn()`, `.AdminOnly()` (on the `/api/admin` group) or
  `.RequireCampaignAccess(CampaignAccess.Member | Commander | Umpire, routeId)`.
  `EndpointConventionTests` fails for any endpoint without one.
- Campaign endpoints: the `RequireCampaignAccess` filter finds the campaign from the route's `{id}`
  (a campaign's, or an army's with `CampaignRouteId.Army`). It gives 404 to non-members (Admins pass)
  and 403 to members without the role, then sets `HttpContext.CampaignContext()` (campaign ID,
  Admin flag, role, member ID) for the handler. Handlers still take `Guid id`, or OpenAPI
  doesn't declare the path parameter. Row-level rules are checked in the handler.
- Tokens: `TokenService` issues them (access token in the body, refresh token only in the
  cookie). Don't use Identity's sign-in or `MapIdentityApi`, which would put both in the body.
- Entities derive from `Entity` (v7 GUID id, audit fields set by `AuditInterceptor`) and are
  configured in `Data/Configurations/` with `IEntityTypeConfiguration<T>`, not data attributes.
  Name/email columns that are searched or sorted use `UseCollation("NOCASE")`.
- Never return entities from endpoints; project to DTOs.
- Lists that can grow: `PagedResponse<T>` via `ToPagedAsync(page, pageSize)` after a stable
  `OrderBy` (a sort key, then `Id`); validate `page >= 1` and `pageSize` 1–100 on the parameters.
- Text search: `EF.Functions.Like(column, Search.ContainsPattern(term), Search.EscapeCharacter)`.
  Never `.Contains()`: on SQLite it's case-sensitive even on NOCASE columns.
- EF Core: reads use `AsNoTracking()` and project to DTOs with `Select`. No lazy loading. Raw SQL
  only through the interpolated `FromSql` / `ExecuteSql`.
- Races: a row the access filter found is loaded with `SingleOrGoneAsync` (404 if deleted
  meanwhile), never `SingleAsync` (500). `ConflictExceptionHandler` turns unique, foreign-key and
  trigger violations and concurrency failures into 409s; Identity results go through
  `ThrowIfFailed` / `ThrowIfConcurrencyFailure`.
- `ExecuteUpdate` / `ExecuteDelete` skip `AuditInterceptor`: set `UpdatedAt` yourself.
- The commander rules are also SQLite triggers (migration `AddCommanderRules`). A migration that
  rebuilds `Armies` or `CampaignMembers` drops them; recreate them (a `DatabaseTests` test fails
  otherwise).
- Emails: build an `EmailMessage` (HTML and text, with user values HTML-encoded) and queue it with
  `IEmailQueue`; never send inline. Tests read them from `Emails` (`FakeEmailService`).
- Time comes from the injected `TimeProvider`; IDs from `Guid.CreateVersion7()`.
- No `!` (null-forgiving) without a comment saying why it's safe.
- Logging uses message templates with named placeholders, never interpolation.
- Comments explain *why*, not *what*. No `#region`s.

## Tests

- Endpoint-level integration tests only (no unit tests), over HTTP against a throwaway SQLite
  database.
- Named `Action_Scenario_ExpectedResult`, one behaviour per test.
- Test classes derive from `Support/ApiTest`: each test gets its own app, in-memory database (a
  copy of the migrated template), `Client`, fake `Clock` and `WithDbAsync` for direct DbContext
  access. Don't share state between tests.
- Assert errors with `AssertProblemAsync(status)` / `AssertValidationProblemAsync(fields…)`.
- Signed-in clients: `CreateUserClientAsync(email?)` and `CreateAdminClientAsync()` (bearer set,
  refresh cookie in the client's jar). Test clients use https://localhost so Secure cookies work.
- `EndpointConventionTests` fail if an endpoint has no name or tag, or isn't under `/api`.
- Setup goes through the shared helpers and scenario builders: `CreateCampaignScenarioAsync()`
  gives a campaign with an Admin, an Umpire, a Commander (a Player commanding the army "First
  Corps"), a Player with no army and an outsider; `scenario.As(role)` is that user's client. Permission tests are theories over the roles, one row per §5.2 cell.
- Read JSON with `ReadAsAsync<T>()` / `GetAsAsync<T>(path)` (`TestJson`: string enums).
- A test that truly needs a banned API uses `#pragma warning disable RS0030` with a comment.
- Extra test services (an EF interceptor, say) go in `App.TestServices` in the test class's
  constructor, before the first client. `InterruptingInterceptor` forces a race: it runs a
  statement just before the next matching SQL command (see `ConcurrencyTests`).
