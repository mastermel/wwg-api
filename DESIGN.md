# wwg — Design & Implementation Plan

> **Status:** Design agreed; Phase 1 in progress (steps 1–6 done).
> **Last updated:** 2026-09-27
>
> This document describes the design **as it currently stands**. The reasons
> for significant changes are recorded in the decision log,
> [`docs/decisions/`](docs/decisions/README.md).

## 1. Context

`wwg` is the **Wasatch Wargamers Campaign App**. This repository holds both
parts:

- **`api/`:** a .NET 10 REST API (the focus of most of this document).
- **`web/`:** a React + TypeScript front-end, which calls the API through a
  generated TypeScript SDK.

In production, the API also serves the built front-end, so the whole app is
one container on one origin.

Users sign up and create **Campaigns**. The creator runs the campaign as its
**Umpire**. Other users join as **Players** through a shareable join link. The
Umpire builds **Armies** made of **Units** and assigns Players to command them.
Players can see who is in the campaign and which Armies exist, but only see the
Units of the Army they command. A site-wide **Admin** can see and edit
everything, and manage user accounts.

> Note: the repo was first created (2023) as `wwg-api`, a planned *GraphQL*
> API. That direction was replaced by a **.NET 10 REST API**, and the repo later
> became the home of the front-end too (decision
> [0002](docs/decisions/0002-single-repo-for-api-and-web.md)).

### Goals

- A clean, conventional .NET 10 Web API that is easy to extend with new features.
- A self-describing API: OpenAPI is the contract, and the front-end consumes a
  **generated TypeScript SDK** instead of hand-written fetch calls.
- Confidence through **integration tests** that exercise every endpoint over HTTP
  against a real, throwaway SQLite database.
- Low operational overhead: SQLite, one container, automated CI/CD.
- API, SDK and UI changes land **together**: one commit or PR can change an
  endpoint, regenerate the client and update the UI.

### Non-goals (for now)

- Unit tests. Behaviour is verified through endpoint-level integration tests.
- Horizontal scaling / multi-instance deployment. SQLite is single-writer; that
  is an accepted trade-off for a club-sized app.
- Email verification (at sign-up or on email change).
- Two-factor authentication.
- Database backups.
- Extra campaign fields (dates, game system, status…). Unit details beyond a name.
- GraphQL.

## 2. Decisions

| Area | Decision |
|---|---|
| Runtime | .NET 10 (LTS), ASP.NET Core |
| Endpoint style | **Minimal APIs**, route groups per feature, `TypedResults` |
| Data access | Entity Framework Core 10, code-first migrations |
| Database | SQLite (file on disk; in-memory for tests) |
| Primary keys | **GUID v7** (`Guid.CreateVersion7()`), generated in the app |
| Validation | **Built-in .NET 10 validation** (`AddValidation()` + DataAnnotations) |
| Errors | RFC 9457 Problem Details everywhere |
| Auth | **ASP.NET Core Identity** (built-in user store, hashing, lockout, bearer tokens) behind **our own thin auth endpoints** |
| Session validity | Security stamp checked **on every request**, so deleted users and password changes take effect immediately |
| Password policy | Minimum 8 characters, no forced character classes |
| Rate limiting | Built-in ASP.NET Core rate limiter on auth endpoints, per client IP |
| Roles | Site-wide **Admin**. Per-campaign **Umpire** (at most one) and **Player** |
| Admins | `Admin:Emails` config is the full list, **synced at startup only** (register first, then add to config) |
| Email | Password reset (and email-change notice) sent through **generic SMTP** (MailKit) |
| API contract | Built-in `Microsoft.AspNetCore.OpenApi`, emitted at build time |
| API docs UI | Swagger UI over the generated OpenAPI document |
| Repository | **One repo** (`wwg`): `api/` (.NET) and `web/` (React), shared config at the root |
| Client SDK | **Orval**, run in `web/` against the committed `api/openapi.json`; generated code not committed |
| Front-end | React + TypeScript, **npm**. Other stack details to be decided in a front-end design step |
| API evolution | Prefer additive changes; `oasdiff` **warns** about breaking changes on PRs |
| Testing | xUnit + `WebApplicationFactory`, fresh in-memory SQLite DB per test |
| CI | **GitHub Actions** on every PR and push to `main`: `api` job (format, build, test, contract up to date) and `web` job (lint, typecheck, build) |
| CD | On push to `main` only: build and push the image to **Docker Hub** |
| Image tags | `latest` and `vYYYYMMdd.HHmm` (UTC) |
| Hosting | **One Docker image**: the API serves the built SPA (same origin, no CORS). Runs on a VPS/home server with the SQLite file on a mounted volume |
| Formatting | **CSharpier** (automatic, near-zero config) |
| Analyzers | Built-in .NET analyzers at **Recommended**, + **Meziantou.Analyzer**, + **BannedApiAnalyzers** |
| Enforcement | Warnings fail the build; **Husky.Net** pre-commit hook formats staged files; CI checks formatting |

## 3. Architecture

### 3.1 Repository layout

```
wwg/
├── global.json                  # pins .NET 10 SDK, enables Microsoft Testing Platform
├── .editorconfig                # style rules for C# and TS + analyzer tuning
├── .gitignore
├── .dockerignore                # keeps node_modules, bin/obj etc. out of the build context
├── .config/dotnet-tools.json    # local tools: CSharpier, Husky.Net, dotnet-ef
├── .husky/                      # pre-commit hook config (covers api/ and web/)
├── .vscode/extensions.json      # recommended editor extensions
├── .github/
│   ├── workflows/ci.yml         # api + web jobs, Docker image on main
│   └── dependabot.yml           # NuGet, npm, Actions, Docker
├── CLAUDE.md                    # repo-wide conventions for AI-assisted work
├── DESIGN.md
├── README.md
├── Dockerfile                   # node build → dotnet publish → runtime image
├── docker-compose.yml           # production-style run
├── docker-compose.dev.yml       # local dev extras (Mailpit)
├── docs/decisions/              # decision log (one short file per decision)
├── api/
│   ├── Wwg.slnx
│   ├── Directory.Build.props    # nullable, warnings-as-errors, analyzers
│   ├── Directory.Packages.props # central package versions + global analyzers
│   ├── Directory.Build.targets  # auto-installs the Husky.Net git hook
│   ├── BannedSymbols.txt        # APIs that must not be used (§4.1)
│   ├── CLAUDE.md                # API-specific conventions
│   ├── openapi.json             # generated at build time, committed: the contract
│   ├── src/
│   │   └── Wwg.Api/
│   │       ├── Program.cs
│   │       ├── Features/        # vertical slices: one folder per resource
│   │       │   ├── Auth/        # register, login, refresh, forgot/reset password
│   │       │   ├── Account/     # /api/me: profile, email, password
│   │       │   ├── Admin/       # users, all campaigns, set umpire
│   │       │   ├── Campaigns/   # campaigns, members, join codes
│   │       │   ├── Join/        # join link preview + join
│   │       │   ├── Armies/
│   │       │   └── Units/
│   │       ├── Data/
│   │       │   ├── WwgDbContext.cs
│   │       │   ├── Entities/
│   │       │   ├── Configurations/  # IEntityTypeConfiguration<T> per entity
│   │       │   └── Migrations/
│   │       └── Infrastructure/  # errors, OpenAPI, auth, email, SPA hosting, interceptors
│   └── tests/
│       └── Wwg.Api.IntegrationTests/
└── web/                         # React app (structure decided in the front-end design step)
    ├── package.json
    ├── orval.config.ts          # reads ../api/openapi.json
    ├── CLAUDE.md                # front-end conventions
    └── src/
        └── api/generated/       # Orval output, git-ignored
```

**What lives where:**
- **Root:** only files that are truly repo-wide. `global.json` and
  `.config/dotnet-tools.json` stay here because `dotnet` looks for them from
  the current folder upward, so they work from anywhere in the repo.
- **`api/`:** everything .NET. The MSBuild props/targets live here so they
  only apply to .NET projects.
- **`web/`:** everything Node. It has its own `package.json`, and there's no
  root `package.json`.
- **`CLAUDE.md` in each area:** Claude Code loads a folder's `CLAUDE.md` when
  working there, so API and front-end conventions stay separate.

A single API project organised by **feature folders**. Splitting into
`Domain`/`Infrastructure` projects is deliberately deferred until there's a
concrete reason.

Each feature exposes one `Map{Feature}Endpoints(this IEndpointRouteBuilder)`
extension, called from `Program.cs`. Handlers are static methods (not inline
lambdas) so they're readable. Their return types, such as
`Results<Ok<T>, NotFound, ValidationProblem>`, give the OpenAPI generator exact
metadata.

### 3.2 Data layer

- **Code-first migrations**, committed. Applied on startup (safe with a single
  instance), controlled by a config flag (`Database:MigrateOnStartup`) so it
  can be turned off.
- The connection string is `ConnectionStrings:Default`. A relative file path
  is resolved against the content root, so the dev database
  (`api/src/Wwg.Api/wwg.db`, git-ignored) doesn't depend on the working
  directory.
- `WwgDbContext` derives from
  `IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>`, so Identity tables
  live in the same database with GUID keys.
- One `IEntityTypeConfiguration<T>` per entity; no data attributes on entities.
- Entities are never returned from endpoints. Endpoints return DTOs, mapped by
  hand (extension methods / `Select` projections). No AutoMapper.
- **IDs:** entities get `Id = Guid.CreateVersion7()` on construction. v7 GUIDs
  are time-ordered, which keeps inserts index-friendly. Queries still **order by
  `CreatedAt`** (then `Id` as a tie-breaker), never by `Id` alone.
- **Enums** (e.g. campaign role) are stored as strings.
- **Audit fields:** `CreatedAt` / `UpdatedAt` set by a `SaveChanges`
  interceptor using the injected `TimeProvider`. Entities opt in through
  `IHasCreatedAt` / `IHasUpdatedAt`; the app's own entities get both from the
  `Entity` base class, and `AppUser` (which must derive from Identity's user)
  has `CreatedAt` only.
- **SQLite gotchas to design around:**
  - No native `DateTimeOffset`, so EF can't order or compare on it. Store
    timestamps as **UTC `DateTime`**.
  - SQLite hands `DateTime`s back as `Kind=Unspecified`. Serialized to JSON,
    they'd have no `Z`, and the browser would read them as local time. A
    **global EF convention** (`ConfigureConventions`) applies a value converter
    to every `DateTime` / `DateTime?` that marks it as `Utc` when read.
  - Text comparison is **case-sensitive** by default (`BINARY` collation), and
    EF translates `.Contains()` to `instr()`. So searches and sorts on
    `FirstName`, `LastName` and `Email` would miss "Mel" when searching "mel".
    These columns use **`COLLATE NOCASE`**. It only folds ASCII letters, which
    is acceptable here.
  - No `decimal` math in SQL. Avoid `decimal` for anything sorted or aggregated.
  - No `rowversion`. Optimistic concurrency, if needed, uses a manual token.
  - Limited `ALTER TABLE`. EF rebuilds tables for some migrations, which is
    fine, but generated migrations should be reviewed.
  - Enable **WAL journal mode** for better concurrent reads.
  - Enforce foreign keys (on by default in `Microsoft.Data.Sqlite`), since the
    cascade/set-null rules in §5.1 depend on them.

### 3.3 API conventions

- Routes: `/api/{resource}` (plural, kebab-case). Route IDs use the `:guid`
  constraint (`/members/{memberId:guid}`), so literal segments like
  `/members/me` never collide with them.
- **Shallow nesting:** collections are nested under their parent
  (`GET/POST /api/campaigns/{id}/armies`), while single items have their own
  flat route (`GET/PUT/DELETE /api/armies/{id}`). This avoids deep URLs like
  `/campaigns/{c}/armies/{a}/units/{u}`.
- Status codes: `GET` 200, `POST` 201 + `Location`, `PUT` 200, `DELETE` 204,
  400 validation, 401 not signed in, 403/404 per below, **409 conflict**,
  **429 rate limited**.
- **Conflicts:** handlers check business rules first (e.g. "email already in
  use", "this Player already commands an army") and return a clear error. The
  database's unique indexes are the final guarantee. If two requests race past
  the check, the `DbUpdateException` for the unique-constraint failure is caught
  centrally and returned as **409** Problem Details, never a 500.
- **Operations that change several rows** (e.g. set Umpire: demote old, promote
  new, unassign army) happen in a **single `SaveChanges`**, so they succeed or
  fail as a whole.
- **Hidden vs forbidden:** if a user isn't allowed to know a resource exists
  (e.g. a campaign they're not in), the API returns **404**. If they can see
  that it exists but can't perform the action (e.g. a Player renaming an Army,
  or viewing another Player's Units), the API returns **403**.
- **Problem Details** (`application/problem+json`) for every error:
  `AddProblemDetails()` + exception handler + status-code pages, so nothing
  ever returns an HTML or empty error body.
- **Validation:** DataAnnotations on request records, enforced automatically
  by .NET 10's `AddValidation()`. Failures return `ValidationProblemDetails`,
  with error keys in camelCase to match the JSON (`items[0].name`).
  Rules that need the database (e.g. "commander must be a Player in this
  campaign", "email already in use") are checked in the handler and returned in
  the same shape.
- **Paging:** list endpoints that can grow large (campaigns, admin lists)
  support `?page=&pageSize=` and return a shared `PagedResponse<T>`
  (`items`, `page`, `pageSize`, `totalCount`).
  - Defaults: page 1, size 25. Maximum size **100**; larger values are a
    validation error.
  - Always a stable sort order (a sort key, then `Id`), so pages don't shuffle.
  - Small child collections (members, armies, units) return plain arrays.
- **Input tidying:** name-like strings (campaign, army, unit, first and last
  names) are **trimmed** before validation and saving. Emails are trimmed;
  case is kept as entered (Identity matches on its normalized form).
  Trimming is opt-in per property with `[Trimmed]`, which trims while the JSON
  is read, so validation sees the tidied value. Passwords are never trimmed
  (decision [0003](docs/decisions/0003-request-validation-and-trimming.md)).
- Every endpoint has an explicit, stable **name / operationId**
  (`.WithName("GetCampaign")`) and tags. These become Orval's hook names
  (`useGetCampaign`) and its file grouping.
- **No CORS.** The front-end and API share one origin in production (§3.11),
  and in development Vite proxies API calls (§3.11). Leaving CORS switched
  off means there's nothing to misconfigure.

### 3.4 Authentication

**We use ASP.NET Core Identity — just not its ready-made HTTP endpoints.**

Identity has two layers:

1. **The engine:** `UserManager` / `SignInManager`, the user and role store,
   password hashing, password rules, lockout, security stamps, reset tokens,
   and the bearer token handler. **We use all of this, configured through
   `IdentityOptions`** (e.g. `Password.RequiredLength = 8`,
   `RequireUppercase = false`, …).
2. **`MapIdentityApi<TUser>()`:** a fixed set of HTTP endpoints on top of the
   engine. **We don't use this layer**, because:
   - Its `/register` request is fixed to email + password. There's no way to
     require first and last name at sign-up.
   - It can't be partly mapped. It always adds 2FA, email-confirmation and
     `/manage/*` endpoints, and changing email through it requires a
     confirmation email, which we've chosen not to do.
   - Its operation names and request shapes aren't ours, which makes the
     generated TypeScript SDK less consistent with the rest of the API.

   Our endpoints (§5.3) are thin, usually a few lines each calling
   `UserManager` / `SignInManager`, so the cost of owning them is small.

**Details:**

- **Sign-up:** email, password, first name, last name. No email confirmation.
  A successful sign-up returns tokens, so the user is logged in straight away.
  The Identity `UserName` is kept equal to the email.
- **Identity options that must change from the defaults:**
  - `User.RequireUniqueEmail = true` (default is `false`).
  - `User.AllowedUserNameCharacters = ""`. The default list rejects valid
    email addresses such as `o'brien@example.com`, and since the username *is*
    the email, email format is validated separately.
  - Password rules per §2.
- **Tokens:** login returns an access token and a refresh token (Identity's
  bearer scheme). The SPA sends `Authorization: Bearer …` and calls `/refresh`
  when the access token expires. These are *opaque* tokens protected by
  ASP.NET Data Protection, not JWTs.
  - **Data Protection keys are persisted** to the mounted volume. Otherwise
    every container restart would log everyone out.
  - **Access token lifetime: 30 minutes**; refresh token lifetime: 14 days
    (Identity default).
- **Security stamp checked on every request.** By default, bearer access tokens
  aren't checked against the database, so a deleted user or changed password
  would keep working until the token expired. A deleted user could even cause
  a 500, e.g. creating a campaign that references a user row that no longer
  exists.
  - After authentication, a small middleware calls
    `SignInManager.ValidateSecurityStampAsync(principal)`. That's one indexed
    lookup per request.
  - If the user is gone or the stamp has changed, the request gets a 401.
  - Deletion, password change, email change and "sign out everywhere"
    therefore take effect **immediately**.
- **Refresh endpoint.** Because we don't use `MapIdentityApi`, our `/refresh`
  must do everything its version does:
  1. Unprotect the token with the bearer scheme's `RefreshTokenProtector`.
  2. Reject it if `ExpiresUtc` has passed, using `TimeProvider`.
  3. Validate the security stamp; reject if the user is gone or the stamp
     changed.
  4. Issue a new token pair.

  Each rejection path has its own integration test.
- **Logout:** opaque bearer tokens can't be revoked individually.
  - Normal logout is client-side: the SPA discards its tokens.
  - `POST /api/me/sign-out-everywhere` rotates the security stamp, which
    immediately invalidates every access and refresh token for that user.
- **Password reset:**
  1. `POST /api/auth/forgot-password { email }` always returns 200, whether or
     not the account exists, so the endpoint can't be used to find out which
     emails are registered.
  2. If the account exists, an email is sent with a link to the React app:
     `{App:PublicUrl}/reset-password?email=…&code=…`. The URL comes from config,
     never from the request's `Host` header, which an attacker could forge
     to point reset links at their own site.
  3. The React page calls `POST /api/auth/reset-password { email, code,
     newPassword }`.
- **Change password** (logged in): requires the current password. This updates
  the security stamp, which signs out every other session. The response
  includes **new tokens** so the current session keeps working.
- **Change email** (logged in): requires the current password. Updates
  `Email` and `UserName` together and rejects an address already in use. No
  confirmation step, which matches sign-up. As a safeguard against account
  takeover, a **notice is sent to the old address**. Like a password change,
  it signs out other sessions and returns new tokens.
- **Lockout** after repeated failed logins (Identity defaults: 5 attempts, 5
  minutes). This only applies if login calls `PasswordSignInAsync(…,
  lockoutOnFailure: true)`, so that's a tested requirement.
- **Rate limiting** (`AddRateLimiter`), partitioned by client IP. Needs the
  real client IP behind the proxy (§3.10).
  - `auth` policy: login, register, refresh (e.g. 10 requests/minute).
  - `email` policy: forgot-password (e.g. 3 requests per 15 minutes). This
    prevents using us to spam someone's inbox or run up SMTP costs.
  - Rejected requests get **429** Problem Details.
  - Limits are configurable, and set very high in the test factory so they
    don't interfere, with dedicated tests for the limits themselves.
- **Account enumeration:** forgot-password never reveals whether an email
  exists. Sign-up and change-email necessarily do ("email already in use");
  that's accepted, and the rate limits make mass probing impractical.
- Endpoints require sign-in by default (fallback policy). Anonymous endpoints
  opt out explicitly.
- The OpenAPI document declares the bearer security scheme, so Swagger UI's
  **Authorize** button works and Orval knows which calls need a token.

### 3.5 Authorization

- **Admin** is an Identity role. Admins pass every campaign permission check.
  - **`Admin:Emails` is the full list of Admins, synced at startup only.** On
    startup, every existing account whose email is listed gets the Admin
    role, and every Admin whose email is *not* listed loses it.
  - Promotion **never** happens at sign-up or on email change. Since emails
    aren't verified, promoting at sign-up would let anyone who registers a
    listed address first become Admin.
  - Operating procedure: **register the account first, then add its email to
    config and restart.** If someone else had already registered that address,
    registration fails and you'd know before granting anything.
  - There's no endpoint to grant or remove Admin.
- **Campaign roles** come from the `CampaignMember` row (§5.1), not Identity
  roles.
- **Permissions are declared on each endpoint, not left to each handler to
  remember.** Handlers that must remember to call an access check are one
  forgotten line away from a security hole. Instead:
  - Each campaign-scoped endpoint declares its minimum access, e.g.
    `.RequireCampaignAccess(CampaignAccess.Member)` or
    `.RequireCampaignAccess(CampaignAccess.Umpire)`.
  - A shared **endpoint filter** resolves the campaign from the route. That
    means `{campaignId}` directly, or via `{armyId}` / `{unitId}` with one
    small query. It loads the caller's relationship (Admin / Umpire / Player /
    none), applies the 404-vs-403 rule (§3.3), and then puts a
    `CampaignContext` (campaign ID, caller's role, member ID) in the request
    for the handler to use.
  - Rules that depend on the specific row, like "Players may only view units
    of the army they command", are checked in the handler against
    `CampaignContext`. There are few of these, and each has a permission test.
  - Queries that return lists apply the same rules in the query itself, rather
    than filtering in memory.
- **Every endpoint must declare its access rule.** A convention test (§3.8)
  fails if any endpoint has none of: `AllowAnonymous`, `AdminOnly`,
  `RequireCampaignAccess`, or an explicit "any signed-in user" marker.
- **All admin-only actions live under `/api/admin`**, and an `AdminOnly`
  policy is applied once to that route group. There are no admin-only query
  parameters or one-off admin routes elsewhere.

### 3.6 Email

- Our own small `IEmailService` abstraction (roughly
  `SendAsync(EmailMessage, CancellationToken)`). It's deliberately not named
  `IEmailSender`, to avoid confusion with Identity's `IEmailSender<TUser>`,
  which doesn't fit our emails (e.g. the email-changed notice).
- The production implementation uses **MailKit**. `System.Net.Mail.SmtpClient`
  is discouraged by Microsoft for new code, can't do implicit TLS (port 465),
  and has no OAuth2 support.
- Emails sent: **password reset link**, **email-changed notice** (to the old
  address).
- Settings are bound from config section `Smtp` and validated at startup:

  | Key | Example |
  |---|---|
  | `Smtp:Host` | `smtp.example.com` |
  | `Smtp:Port` | `587` |
  | `Smtp:Security` | `StartTls` / `SslOnConnect` / `None` / `Auto` |
  | `Smtp:Username` | |
  | `Smtp:Password` | (secret) |
  | `Smtp:FromAddress` | `noreply@example.com` |
  | `Smtp:FromName` | `Wasatch Wargamers` |

- **If `Smtp:Host` is empty**, emails are logged instead of sent (with a warning
  at startup). This lets the app run before a real SMTP service is set up.
- **Local dev:** `docker-compose.dev.yml` runs **Mailpit**, a local SMTP server
  with a web inbox, so emails can be checked by hand.
- **Tests:** `IEmailService` is replaced with a fake that records messages, so
  tests can pull the reset code out of the "sent" email and finish the flow.
- Email bodies are simple HTML + text templates in code. No template engine
  yet.

### 3.7 OpenAPI, Swagger UI & the TypeScript SDK

- `Microsoft.AspNetCore.OpenApi` generates the document, served at
  `/openapi/v1.json`. **Swagger UI** (`Swashbuckle.AspNetCore.SwaggerUI`) is
  served at `/swagger` in development (optionally in production).
- Document transformers add the API title/version, the bearer security scheme,
  and any schema tweaks needed for clean TypeScript output.
- Auth endpoints that return tokens declare an explicit token response type,
  so the SDK gets a typed result.
- **Build-time emit** via `Microsoft.Extensions.ApiDescription.Server` writes
  **`api/openapi.json`** on every build. It's committed: it is the contract,
  and its diff is how API changes get reviewed. Local builds keep it current,
  and CI fails if a commit's `openapi.json` doesn't match its code (§3.9).
  - ⚠️ To do this, the build **launches the app** (through a tool whose entry
    assembly is `GetDocument.Insider`). Startup work with side effects must
    not run then: migrations, Admin sync, and fail-fast config validation such
    as required SMTP or frontend settings.
  - `Program.cs` checks one `IsGeneratingOpenApiDocument` flag (entry assembly
    name) and skips that work. The document itself only needs endpoint
    metadata.
- **The SDK is generated in `web/` from `api/openapi.json`.**
  - `web/orval.config.ts` reads `../api/openapi.json` and generates TanStack
    Query hooks + TS types into `web/src/api/generated/`.
  - Generation runs automatically before `dev`, `build` and `typecheck` (npm
    `pre…` scripts), so the client always matches the committed contract.
  - The **generated code isn't committed** (it's git-ignored). The reviewable
    artifact is `openapi.json`; the TypeScript is derived from it.
  - One PR can change an endpoint, the contract and the UI together. If an API
    change breaks the front-end, the `web` CI job fails in that same PR.
- **Breaking changes are less risky, but not risk-free.** API and front-end
  deploy together, so they can't drift apart. The remaining risk is a browser
  tab that loaded the *old* front-end before a deploy and keeps calling the
  new API.
  - Prefer **additive** changes: new optional fields and new endpoints.
  - On PRs, **`oasdiff breaking`** compares the PR's `openapi.json` with
    `main`'s and posts a **warning** listing any breaking changes. It doesn't
    fail the check, because a coordinated change in the same PR is normal now.
  - The front-end can detect a new deploy (e.g. a version header or a
    `/version.json`) and prompt the user to reload. That gets decided in the
    front-end design.
- ⚠️ .NET 10 emits **OpenAPI 3.1** by default. Orval's handling of 3.1 (notably
  nullable written as a type array) is checked when the front-end is
  scaffolded (Phase 1). If it's a problem, one option switches the document to
  3.0.

### 3.8 Integration testing

- **xUnit v3** + `WebApplicationFactory<Program>`, hosting the real app
  in-process. Tests use `HttpClient` exactly like a real client.
- **Fresh app and database per test:** tests derive from `ApiTest`, and xUnit
  creates a new instance per test, so each test gets its own factory and its
  own SQLite **in-memory** database. The database is a named, shared-cache
  in-memory database: the app reaches it through its normal
  `ConnectionStrings:Default` (so tests run the real database setup,
  interceptor included), and the factory holds one connection open to keep it
  alive for the test's lifetime. No shared state, so tests can run in
  parallel.
- **Migrations run once, then get copied.** Running every migration for
  every test gets slower as migrations pile up. Instead:
  - Once per test run, the **real migrations** are applied to a *template*
    in-memory database, so migrations are still tested.
  - Each test gets a copy made with SQLite's `BackupDatabase`, which takes
    milliseconds.
- **Faster password hashing in tests only.** Identity's hasher uses 100,000
  PBKDF2 iterations on purpose, and permission tests create several users
  each. The test factory lowers `PasswordHasherOptions.IterationCount` (e.g.
  to 1). Production keeps the default.
- Test helpers:
  - `CreateUserClientAsync(…)`: signs up a user through the real
    `/api/auth/register` endpoint and returns a client with the bearer token.
  - `CreateAdminClientAsync()`: same, using an email listed in the test
    config's `Admin:Emails`.
  - Scenario builders, e.g. "campaign with an Umpire, two Players, and an Army
    commanded by Player 1", so permission tests stay short.
  - Direct `DbContext` access for seeding and checking what was saved.
  - Assertion helpers for Problem Details / validation responses.
  - Fake email service (above) and `FakeTimeProvider` for predictable results,
    e.g. testing token expiry by moving the clock forward.
- Every endpoint gets tests for: happy path, validation failure, not found,
  unauthenticated (401), and **each role in the permission matrix (§5.2)**:
  Admin, Umpire, commanding Player, other Player, non-member.
- **Permission tests are data-driven, to mirror §5.2.** Each action has one
  `[Theory]`, with a row per role and the expected status:

  ```csharp
  [Theory]
  [InlineData(Role.Admin, 200)]
  [InlineData(Role.Umpire, 200)]
  [InlineData(Role.Commander, 403)]
  [InlineData(Role.OtherPlayer, 403)]
  [InlineData(Role.NonMember, 404)]
  public async Task RenameArmy_ByRole_ReturnsExpectedStatus(Role role, int expected)
  ```

  A change to the permission table in this document maps directly to a change
  in the tests.
- **Convention tests** check the whole app's endpoint list
  (`EndpointDataSource`), so rules hold without relying on review. They fail
  if any endpoint:
  - declares no access rule (§3.5);
  - has no name (operationId) or tag, which the SDK depends on;
  - isn't under `/api` (except `/health` and the OpenAPI/Swagger routes).
- **Pending-migration test:** fails if `DbContext.Database.HasPendingModelChanges()`
  is true, i.e. someone changed an entity or configuration but didn't add a
  migration.

### 3.9 CI/CD (GitHub Actions)

One workflow, `.github/workflows/ci.yml`, with three jobs:

| Job | Pull request | Push to `main` / manual |
|---|:-:|:-:|
| `api`: format, build, contract check, tests | ✅ | ✅ |
| `web`: generate SDK, lint, typecheck, build | ✅ | ✅ |
| `docker`: build and push the image | – | ✅ (after `api` and `web` pass) |

1. **`api` job:**
   - Set up .NET 10 and restore tools.
   - Check formatting (`dotnet csharpier check .`).
   - Build in Release, so any analyzer warning fails the build. The build also
     regenerates `api/openapi.json`.
   - **Contract check:** `git diff --exit-code api/openapi.json`. If the build
     changed it, the committed contract doesn't match the code, and the job
     fails with a message to rebuild and commit it.
   - Run all integration tests.
   - **PRs only:** `oasdiff breaking` against `main`'s `openapi.json`, as a
     warning (§3.7).
2. **`web` job:** in `web/`, run `npm ci`, generate the SDK from
   `api/openapi.json`, then lint, typecheck and build. Front-end tests are
   added here once the front-end design decides on them. This runs in
   parallel with `api`; it only needs the committed contract.
3. **`docker` job** (`main` only, after both jobs pass): log in to Docker Hub
   and build and push the multi-stage image (§3.10) with
   `docker/build-push-action`, tagged:
   - `latest`
   - `vYYYYMMdd.HHmm`: UTC time, computed once per run, e.g. `v20260927.1430`.

   Image name comes from a repo variable (`DOCKERHUB_IMAGE`). Credentials come
   from secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` (a Docker Hub access
   token, not the account password).

   The image carries standard **OCI labels** (`org.opencontainers.image.revision`
   = git SHA, `.created`, `.source`, `.version`), generated by
   `docker/metadata-action`. Any running image can then be traced back to its
   exact commit (`docker inspect`).

**CI never commits to the repo**, so the workflow only needs read access to
the repo contents. Protecting `main` later just means making the `api` and
`web` jobs required checks; nothing else changes.

**Dependency updates:** `.github/dependabot.yml` opens weekly PRs for:
- NuGet packages in `api/` (Dependabot understands `Directory.Packages.props`),
  grouped so related packages (e.g. all `Microsoft.*`, EF Core) arrive together;
- npm packages in `web/`, grouped similarly;
- GitHub Actions versions;
- the Docker base images in `Dockerfile`.

Each Dependabot PR runs the normal PR checks, so an update that breaks the
build or tests is visible before merging.

### 3.10 Deployment

- Multi-stage `Dockerfile` at the repo root:
  1. **`node` stage:** `npm ci` + `npm run build` in `web/` → `web/dist/`.
  2. **`sdk` stage:** `dotnet publish` the API.
  3. **Runtime stage:** `aspnet` image, non-root user. It contains the
     published API with the front-end build copied into its `wwwroot/`.
- `.dockerignore` keeps `node_modules`, `bin/`, `obj/`, test output and local
  databases out of the build context.
- One volume mounted at `/data` holding the SQLite DB and the Data Protection
  keys.
- **Reverse proxy: Traefik**, also running in Docker on the server.
  - Traefik terminates TLS (and sets HSTS). The container serves plain HTTP on
    port 8080 (the .NET image default). The app doesn't use
    `UseHttpsRedirection`, which could loop behind the proxy.
  - The app joins a Docker network shared with Traefik and is routed by
    Docker labels. It publishes **no ports**, so it's only reachable through
    Traefik.
  - The shared network gets a **fixed subnet** in the compose file, so the
    trusted-network setting below stays stable when containers restart.
  - **One replica only:** SQLite is single-writer.
- **Forwarded headers:** `UseForwardedHeaders` (for `X-Forwarded-For` and
  `X-Forwarded-Proto`), trusting loopback plus the configured proxies
  (`ForwardedHeaders:KnownProxies` / `KnownNetworks`). Behind Traefik, trust
  the shared network's subnet (e.g.
  `ForwardedHeaders__KnownNetworks__0=172.20.0.0/16`), not Traefik's IP,
  which can change. Without this, every request appears to come from the
  proxy, which breaks per-IP rate limiting, and generated URLs (`Location`
  headers) use `http://`.
  - Only the last hop is read (forward limit 1), which is right with Traefik
    alone. If another proxy or CDN (e.g. Cloudflare) is ever put in front,
    Traefik's `forwardedHeaders.trustedIPs` and the app's forward limit both
    need adjusting.
- **Health checks:** the runtime image has no `curl`, so the Docker
  `HEALTHCHECK` can't just curl `/health`; the approach is chosen in step 19
  (e.g. a small check mode built into the app). Traefik can also health-check
  `/health` itself.
- `docker-compose.yml` for running it on the server, with the Traefik labels,
  the shared network and the `/data` volume. Updating means pulling the new
  `latest` (or a specific `vYYYYMMdd.HHmm`) and restarting.
- Production configuration comes from environment variables:
  `ConnectionStrings__Default` (defaults to `Data Source=/data/wwg.db`),
  `Database__MigrateOnStartup` (default `true`), `Smtp__*`,
  `Admin__Emails__0…`, `App__PublicUrl`, `ForwardedHeaders__*`.

### 3.11 Front-end hosting & local development

**Production: the API serves the SPA.**
- `UseStaticFiles()` serves the built front-end from `wwwroot/`.
- `MapFallbackToFile("index.html")` sends every other non-file URL (e.g.
  `/campaigns/123`) to the SPA, so client-side routes work on reload.
- **API routes never fall through to the SPA.** A catch-all
  `/api/{**path}` route returns a Problem Details **404**, so a mistyped API
  URL gets a JSON error rather than `index.html` with a 200. The same applies
  to `/openapi` and `/health`. A test covers both behaviours.
- **Caching:**
  - Vite's hashed files under `/assets/` get
    `Cache-Control: public, max-age=31536000, immutable`.
  - `index.html` gets `no-cache`, so a new deploy is picked up on the next
    page load.
- **Security headers** on SPA responses: a Content-Security-Policy
  (`default-src 'self'`, tuned to what the front-end needs),
  `X-Content-Type-Options: nosniff`, `Referrer-Policy`, and
  `frame-ancestors 'none'`. The SPA holds bearer tokens, so preventing XSS
  matters.
- Serving the SPA is skipped if `wwwroot/index.html` doesn't exist (in
  development and in tests).
- One origin means no CORS, and reset/join links use `App:PublicUrl`.

**Development: two processes, one origin from the browser's view.**
- The API runs on `http://localhost:5102` (`dotnet run` / `dotnet watch`).
- The front-end runs on Vite's dev server (`http://localhost:5173`, with hot
  reload). Vite **proxies** `/api`, `/openapi` and `/swagger` to the API.
  The browser only ever talks to `:5173`, so there's still no CORS.
- `App:PublicUrl` is `http://localhost:5173` in development, so reset links
  in Mailpit open the Vite app.

## 4. Cross-cutting concerns

| Concern | Approach |
|---|---|
| Logging | Built-in `ILogger`, structured JSON console logs in production |
| Configuration | `appsettings.{Environment}.json` + env vars; user-secrets in dev. **Every settings section** (`App`, `Smtp`, `Admin`, `Auth`, `RateLimits`, `ForwardedHeaders`) is a typed options class with DataAnnotations, `ValidateDataAnnotations()` and `ValidateOnStart()`, so bad config fails at startup with a clear message. `App:PublicUrl` (the app's public URL, e.g. `https://wwg.example.com`) is required outside development |
| Health check | `GET /health` (includes a DB check) for Docker and the proxy |
| Time | `TimeProvider` injected everywhere; faked in tests |
| Code quality | See §4.1 |
| Packages | Central package management (`Directory.Packages.props`) |

### 4.1 Code style, linting & conventions

The aim: formatting is never discussed, and most rules are enforced by the
compiler rather than by review. Anything a tool can't check is written down
below.

**Layers:**

| Layer | Tool | What it does | Where it runs |
|---|---|---|---|
| Formatting | **CSharpier** (local dotnet tool) | Rewrites layout/whitespace into one canonical form. Nothing to configure beyond line width (100) | Editor on save, pre-commit hook, CI check |
| Code style | `.editorconfig` + `EnforceCodeStyleInBuild` | Naming, file-scoped namespaces, unused usings, `var`, modern syntax | Build (a few rules fail the build; the rest are editor suggestions) |
| Quality & bugs | Built-in .NET analyzers, `AnalysisMode=Recommended` | Reliability, security, performance, API-usage rules | Build |
| Practical pitfalls | **Meziantou.Analyzer** | Pass `CancellationToken`s through, explicit string comparison/culture, async misuse, common API mistakes | Build |
| Design decisions | **BannedApiAnalyzers** + `BannedSymbols.txt` | Makes calls that go against this design fail to compile, with a message saying what to use instead | Build |
| Nullability | `<Nullable>enable</Nullable>` + warnings as errors | Null-safety across the codebase | Build |

- Analyzer packages are added to **every project at once** with
  `<GlobalPackageReference>` in `Directory.Packages.props`.
  `BannedSymbols.txt` sits in `api/` and is shared the same way (via `Directory.Build.props`).
- **Warnings are errors everywhere**, locally and in CI. There's no
  "warnings are fine locally" mode, so a green local build means a green CI
  build.
- **Rule tuning:** every rule that is switched off or lowered goes in
  `.editorconfig` with a one-line comment saying why. Expected starting
  suppressions:
  - `MA0004` (`ConfigureAwait(false)`): ASP.NET Core has no synchronization
    context, so this is noise.
  - `MA0048` (file name must match type name): allows several small DTO
    records in one `{Feature}Dtos.cs` file.
  - Any `CA` rules from `Recommended` that don't fit a web API. These get
    decided during setup, not in advance.
- **Formatting vs. `dotnet format`:** CSharpier owns layout. `dotnet format`
  is still useful locally to auto-fix style and analyzer issues
  (`dotnet format style`, `dotnet format analyzers`), but we don't run its
  `whitespace` mode, which would fight CSharpier.

**Banned APIs** (initial `BannedSymbols.txt`):

| Banned | Use instead | Why |
|---|---|---|
| `DateTime.Now` / `UtcNow`, `DateTimeOffset.Now` / `UtcNow` | Injected `TimeProvider` | Testable time (§4) |
| `Guid.NewGuid()` | `Guid.CreateVersion7()` | Time-ordered IDs (§3.2) |
| `System.Net.Mail.SmtpClient` | `IEmailService` (MailKit) | §3.6 |
| `System.Console` | `ILogger<T>` | Structured logging |
| `Task.Result`, `Task.Wait()`, `Thread.Sleep` | `await`, `Task.Delay` | No sync-over-async |
| `DatabaseFacade.EnsureCreated()` | Migrations | Tests must run the real migrations (§3.8) |
| `FromSqlRaw`, `ExecuteSqlRaw` | `FromSql`, `ExecuteSql` (interpolated, parameterized) | SQL injection safety |

A test that truly needs a banned API (rare) uses a `#pragma warning disable`
with a comment saying why.

**Pre-commit hook (Husky.Net):**

- Husky.Net is a .NET local tool, so no Node is needed. Its config lives in
  `.husky/` and is committed.
- **pre-commit:** runs CSharpier on the staged `.cs` files and re-stages
  them. It deliberately doesn't build or test; that would make every commit
  slow, and CI covers it.
  - Caveat: if a file is only *partly* staged, re-staging it also stages the
    rest of that file's changes.
- Installed automatically: an MSBuild target runs `dotnet husky install` on
  restore, so a fresh clone gets the hook on its first build. It's skipped
  when `CI=true`.
- Hooks can be bypassed with `git commit --no-verify` in an emergency. CI
  still catches unformatted code.

**Editor setup:** `.vscode/extensions.json` recommends C# Dev Kit and the
CSharpier extension. CSharpier also has plugins for Rider and Visual Studio.
Format-on-save is recommended.

**Conventions (not tool-enforced):**

- **Visibility:** types are `internal` and `sealed` by default. They're only
  made `public` or unsealed when needed. (.NET 10 generates a `public` `Program`
  class automatically, so the test factory can reach it without declaring one.)
- **DTOs:** `public sealed record`s (the validation generator only finds public
  types), named `{Verb}{Resource}Request` and `{Resource}Response` /
  `{Resource}Summary`. Attributes on positional parameters use the `property:`
  target, or OpenAPI misses them (decision 0003).
- **Endpoints:**
  - Handlers are `internal static` methods returning `Results<…>`. Not
    `private`: the XML comment generator skips private methods.
  - Every handler takes a `CancellationToken` and passes it to EF Core.
    Meziantou flags missed ones.
  - XML doc `<summary>` on each handler; this becomes the OpenAPI description.
- **EF Core:**
  - Read queries use `AsNoTracking()` and project straight to DTOs with
    `Select`.
  - No lazy loading.
  - No raw SQL except through the parameterized APIs.
- **Nullability:** no `!` (null-forgiving) without a comment explaining why
  it's safe.
- **Logging:** message templates with named placeholders, never string
  interpolation (CA2254 enforces this).
- **Comments** explain *why*, not *what*. No `#region`s.
- **Tests:**
  - Named `Action_Scenario_ExpectedResult`, one behaviour per test.
  - Setup goes through the shared helpers and scenario builders.
- **Commits:** [Conventional Commits](https://www.conventionalcommits.org/)
  (`feat:`, `fix:`, `chore:`, `docs:`, `build:`, `ci:`, `test:`, `refactor:`).
- A `CLAUDE.md` summarising these conventions and the commit rules will be
  added, so AI-assisted changes follow them too.

## 5. Domain model

### 5.1 Entities

```
AppUser (Identity)            Campaign
  Id           Guid             Id           Guid
  Email / UserName              Name         string (required, ≤100)
  FirstName    string (≤100)    Description  string? (≤2000)
  LastName     string (≤100)    JoinCode     string (unique)
  CreatedAt                     CreatedAt / UpdatedAt

CampaignMember                Army
  Id           Guid             Id           Guid
  CampaignId   → Campaign       CampaignId   → Campaign
  UserId       → AppUser        Name         string (required, ≤100)
  Role         Umpire|Player    CommanderId  → CampaignMember? (null = unassigned)
  CreatedAt / UpdatedAt         CreatedAt / UpdatedAt
  (CreatedAt = when they joined)

Unit
  Id           Guid
  ArmyId       → Army
  Name         string (required, ≤100)
  CreatedAt / UpdatedAt
```

**Rules enforced in the database:**

- `CampaignMember (CampaignId, UserId)` unique: a user is in a campaign at most
  once.
- **At most one Umpire per campaign**: a partial unique index on `CampaignId`
  where `Role = 'Umpire'`. A campaign can have *no* Umpire if the Umpire's
  account was deleted (see below).
- A Player commands **at most one Army** per campaign: a unique index on
  `Army.CommanderId` (SQLite allows many NULLs, so there can be many unassigned
  armies).
- `Army.CommanderId` points at a **member**, not a user. That keeps the
  commander inside the same campaign.
- **Cascades:**
  - Deleting a Campaign deletes its members, armies and units.
  - Deleting an Army deletes its units.
  - Deleting a CampaignMember (Player leaves/removed, or user deleted) sets
    `Army.CommanderId` to NULL. The Army and its Units are kept.
  - Deleting a user deletes their CampaignMember rows (and Identity data).
    Their campaigns are **not** deleted.

**Rules enforced in the handlers:**

- A commander must be a member with the **Player** role in the Army's campaign.
  The Umpire can't command an Army.
- The Umpire can't leave or be removed as a normal member.
- **Umpire-less campaigns:** if an Admin deletes a user who was an Umpire,
  their campaigns remain with no Umpire. Only an Admin can manage them until an
  Admin **sets a new Umpire** (`PUT /api/admin/campaigns/{id}/umpire`, §5.3):
  - The chosen user can be an existing Player (promoted) or any other user
    (added as a member).
  - If the promoted Player commanded an Army, that Army becomes unassigned.
  - The same action can replace an existing Umpire; the old Umpire becomes a
    Player.

**Join codes:** a random 128-bit value, base64url-encoded (~22 characters),
unique per campaign, created with the campaign. The Umpire can **regenerate**
it, which makes the old link stop working. The React app builds the link
(e.g. `{App:PublicUrl}/join/{code}`, using `window.location.origin`).

### 5.2 Permission matrix

| Action | Admin | Umpire | Player (commander) | Player (other) | Non-member |
|---|:-:|:-:|:-:|:-:|:-:|
| View campaign details | ✅ | ✅ | ✅ | ✅ | 404 |
| Edit / delete campaign | ✅ | ✅ | 403 | 403 | 404 |
| View / regenerate join code | ✅ | ✅ | 403 | 403 | 404 |
| View member list (incl. each Player's army name) | ✅ | ✅ | ✅ | ✅ | 404 |
| Remove a Player | ✅ | ✅ | self only (leave) | self only (leave) | 404 |
| List armies (name + commander) | ✅ | ✅ | ✅ all | ✅ all | 404 |
| View an army's **units** | ✅ | ✅ | ✅ own army | 403 | 404 |
| Create / edit / delete army | ✅ | ✅ | 403 | 403 | 404 |
| Assign / unassign commander | ✅ | ✅ | 403 | 403 | 404 |
| Create / rename / delete unit | ✅ | ✅ | 403 | 403 | 404 |

- Any signed-in user can create a campaign, and becomes its Umpire.
- The join link preview is public; anyone with the code can see the campaign
  name. Joining requires signing in. Joining a campaign you're already in does
  nothing.
- Players see every Army's name and commander, **including unassigned
  armies**, but only see Units for the Army they command.

**Admin (site-wide):**

| Action | Admin | Everyone else |
|---|:-:|:-:|
| List / search users | ✅ | 403 |
| View a user (incl. their campaigns and roles) | ✅ | 403 |
| Delete a user | ✅ (not themselves) | 403 |
| List all campaigns (`/api/admin/campaigns`) | ✅ | 403 |
| Set a campaign's Umpire | ✅ | 403 |

### 5.3 Endpoints (first pass)

**Auth** (anonymous)

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Sign up (email, password, first, last) → tokens |
| POST | `/api/auth/login` | Email + password → tokens |
| POST | `/api/auth/refresh` | Refresh token → new tokens |
| POST | `/api/auth/forgot-password` | Send reset email (always 200) |
| POST | `/api/auth/reset-password` | Email + code + new password |

**Account** (signed in)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/me` | Current user: id, email, names, `isAdmin` |
| PUT | `/api/me` | Update first/last name |
| PUT | `/api/me/email` | Change email (needs current password) → new tokens |
| PUT | `/api/me/password` | Change password (needs current password) → new tokens |
| POST | `/api/me/sign-out-everywhere` | Rotate security stamp; all tokens stop working |

**Admin** (Admin only)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/admin/users` | Paged list, `?search=` on name/email |
| GET | `/api/admin/users/{id}` | User details + campaigns and roles |
| DELETE | `/api/admin/users/{id}` | Delete user (not self) |
| GET | `/api/admin/campaigns` | Paged list of every campaign, incl. umpire-less ones |
| PUT | `/api/admin/campaigns/{id}/umpire` | Set the Umpire `{ userId }` |

**Campaigns & membership**

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/campaigns` | Campaigns I'm in, with my role (paged) |
| POST | `/api/campaigns` | Create; caller becomes Umpire |
| GET | `/api/campaigns/{id}` | Details, Umpire (may be null), my role |
| PUT | `/api/campaigns/{id}` | Edit name/description |
| DELETE | `/api/campaigns/{id}` | Delete campaign and everything in it |
| GET | `/api/campaigns/{id}/join-code` | Current join code (Umpire) |
| POST | `/api/campaigns/{id}/join-code` | Regenerate join code (Umpire) |
| GET | `/api/campaigns/{id}/members` | Members with role and commanded army (id, name) |
| DELETE | `/api/campaigns/{id}/members/{memberId}` | Remove a Player (Umpire/Admin) |
| DELETE | `/api/campaigns/{id}/members/me` | Leave the campaign (Players) |
| GET | `/api/join/{code}` | **Anonymous** preview: campaign name, Umpire name |
| POST | `/api/join/{code}` | Join as Player (repeat calls are harmless) |

**Armies & units**

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/campaigns/{id}/armies` | All armies: id, name, commander |
| POST | `/api/campaigns/{id}/armies` | Create army (optional commander) |
| GET | `/api/armies/{id}` | Army + commander + units (403 for other Players) |
| PUT | `/api/armies/{id}` | Rename (name only) |
| DELETE | `/api/armies/{id}` | Delete army and units |
| PUT | `/api/armies/{id}/commander` | Assign commander `{ memberId }` |
| DELETE | `/api/armies/{id}/commander` | Unassign commander |
| POST | `/api/armies/{id}/units` | Add unit |
| PUT | `/api/units/{id}` | Rename unit |
| DELETE | `/api/units/{id}` | Delete unit |

## 6. Open questions

None blocking. Items to revisit later:

- **Front-end design** (before step 9): routing, UI component library,
  forms and validation, token storage and refresh handling, detecting new
  deploys, linting/formatting (ESLint, Prettier or Biome), front-end testing,
  and how front-end screens are sequenced alongside the API steps.
- Unit details, extra campaign fields.
- Letting users delete their own account.
- Real SMTP provider values.
- Database backups.

## 7. Implementation plan

Work is split into four phases. Each step ends with the build green and the
tests passing. Each phase is a good point to stop and review.

### Phase 1 — Foundation (nothing user-facing yet)

1. ✅ **Repo housekeeping**
   - Update `README.md`; replace the Node `.gitignore` with a .NET one.
   - Add `global.json`, `.editorconfig`, `Directory.Build.props`,
     `Directory.Packages.props`.
2. ✅ **Scaffold**
   - `Wwg.slnx`, `Wwg.Api` (empty web project), `Wwg.Api.IntegrationTests`
     (xUnit v3 on Microsoft Testing Platform), and a smoke test.
   - `Program.cs` split into `Add…` / `Use…` / `Map…` extension methods.
   - Moved into `api/` for the single-repo layout (decision 0002).
3. ✅ **Code quality tooling** (§4.1)
   - Local tool manifest (root) with CSharpier and Husky.Net; format the
     codebase.
   - Pre-commit hook + auto-install target. It covers `api/` now and gets
     `web/` tasks in step 9.
   - `AnalysisMode=Recommended`, Meziantou.Analyzer and BannedApiAnalyzers as
     global package references; `BannedSymbols.txt`.
   - Tune rules (suppressions documented in `.editorconfig`).
   - `.vscode/extensions.json`; root and `api/` `CLAUDE.md`.
4. ✅ **Infrastructure**
   - Problem Details, exception handler, status-code pages.
   - `AddValidation()`; input trimming approach.
   - OpenAPI document + Swagger UI; JSON options (camelCase, enums as strings).
   - Validated options pattern (§4) used for every settings section from here
     on, starting with `App`.
   - Forwarded headers, `TimeProvider`, `GET /health`.
5. ✅ **Data layer**
   - `WwgDbContext` (Identity-based, GUID keys), `AppUser` with names.
   - Base entity (GUID v7 id, audit fields), audit interceptor.
   - Conventions: UTC `DateTime` converter, `NOCASE` on searchable text.
   - Unique-constraint → 409 mapping.
   - SQLite connection setup (WAL), migrate-on-startup flag.
   - Initial migration (Identity tables).
6. ✅ **Test harness**
   - `WebApplicationFactory` with a migrated template DB cloned per test
     (`BackupDatabase`).
   - `FakeTimeProvider`, Problem Details assertions. (The fake email service
     moved to step 11, with `IEmailService`.)
   - Convention tests (operationId, tags, `/api` prefix) and the
     pending-migration test.
   - First tests: `/health` returns healthy; unknown `/api/…` route returns
     Problem Details 404.
7. **CI: `api` job**
   - `ci.yml` running on PRs and pushes to `main`: format check, build, tests.
   - `dependabot.yml` (NuGet, Actions).
8. **Contract pipeline**
   - Build-time `api/openapi.json` emit, with the
     `IsGeneratingOpenApiDocument` guard around startup side effects.
   - CI contract-up-to-date check; `oasdiff breaking` warning on PRs.
9. **Front-end design & scaffold**
   - Discuss and record the front-end design (see §6), then:
   - Create `web/` (Vite + React + TypeScript, npm).
   - Orval config reading `../api/openapi.json`, generation hooked into the
     npm scripts, generated folder git-ignored. Check that OpenAPI 3.1 works
     with Orval, and switch to 3.0 if needed.
   - Vite dev proxy to the API.
   - Front-end lint/format tooling, Husky pre-commit tasks for `web/`,
     `web/CLAUDE.md`.
   - CI `web` job; Dependabot `npm` entry.
   - A first page that calls `/health` through the generated client, to prove
     the whole path works.

**Phase 1 is done when:** a PR runs the `api` and `web` jobs in CI (format,
build, contract check, tests, lint, typecheck), Swagger UI shows the health
endpoint, and the Vite app shows the API's health through the generated SDK.

### Phase 2 — Accounts

10. **Auth**
   - Identity with bearer tokens; option overrides (unique email, username
     characters, password rules).
   - Register, login (with lockout), refresh (with all the checks in §3.4).
   - Per-request security stamp validation; fallback auth policy.
   - Rate limiting (`auth` policy).
   - Persisted Data Protection keys; Admin sync from config at startup.
   - Test auth helpers, cheap password hashing in tests, and the "every
     endpoint declares an access rule" convention test.
11. **Email & password reset:** `IEmailService`, MailKit SMTP + logging
    fallback, fake email service for tests, Mailpit dev compose; forgot/reset endpoints with the `email`
    rate limit.
12. **Account:** `/api/me` get/update, change email (+ notice to old address),
    change password, sign out everywhere.
13. **Admin users:** list/search (paged, `NOCASE`), details, delete (not self).

### Phase 3 — Campaigns

14. **Campaigns:** entity + membership, CRUD, paged list.
    `RequireCampaignAccess` endpoint filter + `CampaignContext`. Scenario
    builders and data-driven permission tests.
15. **Join flow:** join codes, preview/join/regenerate, member list,
    remove Player, leave (`/members/me`).
16. **Admin campaigns:** `/api/admin/campaigns` list and set Umpire, including
    the umpire-less campaign cases and the user-deletion test.
17. **Armies:** CRUD, separate commander assign/unassign endpoints and rules,
    list with commanders; member list shows commanded army.
18. **Units:** create/rename/delete; units visible only to Umpire, Admin and
    the commander.

### Phase 4 — Release

19. **Docker & SPA hosting:** API serves the SPA (static files, fallback,
    `/api` 404s, caching and security headers, §3.11); multi-stage
    `Dockerfile` (node → sdk → runtime), `.dockerignore`,
    `docker-compose.yml` for Traefik (labels, shared network with a fixed
    subnet, no published ports), volume layout, health check (§3.10).
20. **CD:** Docker Hub push step in CI with `latest` + `vYYYYMMdd.HHmm` tags and
    OCI labels.
21. **First deploy** to the server behind Traefik, with the SMTP values and
    `ForwardedHeaders__KnownNetworks__0` (the shared network's subnet) filled
    in. Register the Admin account, then add it to `Admin:Emails` and restart.

> Docker could move earlier (after Phase 1) if you'd like a deployable image
> from the start. It doesn't depend on anything in Phases 2–3.
>
> **Front-end screens** for Phases 2–3 are sequenced in the front-end design
> step (step 9). Most likely each API step is followed by its screens, so
> every feature is usable end to end before the next begins.

### Before starting: things you'll need to set up

- .NET 10 SDK and Node.js 24 installed locally (and Docker, for Mailpit).
- Rename the GitHub repo to `wwg` (then update the local `origin` remote).
- Docker Hub: a repository for the image and an access token.
- GitHub repo: secrets `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN` and variable
  `DOCKERHUB_IMAGE` (only needed by Phase 4).
