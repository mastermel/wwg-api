# wwg — Design & Implementation Plan

> **Status:** Phases 1–10 (steps 1–38) are done; Phase 10 was Admins masquerading.
> Next: Phase 11, step 41: factions and units shared by every campaign (then steps 42–51).
> **Last updated:** 2026-09-30
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

Since Phase 8 (§7), each campaign has a **map** of its area, and progresses in
**turns**. Armies are on **sides**, and take their units from the club's shared **library**
of factions and units (decision [0015](docs/decisions/0015-global-factions-and-units.md)). In each turn, every army's commander
gives each unit an order (Move or Hold) on the map and submits the turn; the
Umpire approves it, and opens the next turn once every army has moved
(decisions [0009](docs/decisions/0009-campaign-map-stack.md) and
[0010](docs/decisions/0010-turns-factions-and-visibility.md)). Everyone sees
every army and its units; only where they are is private. From Phase 9 the
Umpire can also edit any army's orders in the open turn, and submit for it
(decision [0011](docs/decisions/0011-umpire-edits-orders.md)).

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
- Extra campaign fields (dates, game system…); a campaign's stage (setup, running) follows
  from its turns. Unit details beyond name, type, FF and points.
- The campaign map offline (it needs a connection).
- Battles: fighting them, their losses and their aftermath are played out in person, at the
  table (decision 0014). The app may point out contact; the Umpire records what came of it by
  editing units.
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
| Session tokens | Access token (30 min) **in memory**; refresh token (30 days, sliding) in an **`HttpOnly` cookie** scoped to `/api/auth` |
| Password policy | Minimum 8 characters, no forced character classes |
| Rate limiting | Built-in ASP.NET Core rate limiter on auth endpoints, per client IP |
| Roles | Site-wide **Admin**. Per-campaign **Umpire** (at most one) and **Player** |
| Admins | `Admin:Emails` config is the full list, **synced at startup only** (register first, then add to config) |
| Email | Password reset (and email-change notice) sent through **generic SMTP** (MailKit) |
| API contract | Built-in `Microsoft.AspNetCore.OpenApi`, emitted at build time |
| API docs UI | Swagger UI over the generated OpenAPI document |
| Repository | **One repo** (`wwg`): `api/` (.NET) and `web/` (React), shared config at the root |
| Client SDK | **Orval**, run in `web/` against the committed `api/openapi.json`; generated code not committed |
| Front-end | **Wasatch Wargamers** (the app's name; `wwg-campaigner` in code): React + TypeScript SPA, **npm**, Vite, **Mantine**, TanStack Router + Query, React Hook Form + Zod; installable **PWA**, read-only offline (§3.12) |
| Campaign map | **MapLibre GL JS** with **OpenFreeMap** vector tiles, **Mapterhorn** hillshading, NATO-style unit symbols (drawn by the app), place search through our own endpoint (decision [0009](docs/decisions/0009-campaign-map-stack.md)); §3.13 |
| Turns | In step across armies, per army, one order per unit (Move or Hold), approved by the Umpire; one visibility rule for positions (decision [0010](docs/decisions/0010-turns-factions-and-visibility.md)) |
| API evolution | Prefer additive changes; `oasdiff` **warns** about breaking changes on PRs |
| Testing | xUnit + `WebApplicationFactory`, fresh in-memory SQLite DB per test |
| CI | **GitHub Actions** on every PR and push to `main`: `api` job (format, build, test, contract up to date) and `web` job (lint, format, typecheck, test, build) |
| CD | On push to `main` only: build and push the image to **Docker Hub** |
| Image tags | `latest` and `vYYYYMMdd.HHmmss` (UTC) |
| Hosting | **One Docker image**: the API serves the built SPA (same origin, no CORS). Runs on a VPS/home server with the SQLite file on a mounted volume |
| Backups | The app snapshots its SQLite file (`VACUUM INTO`) before startup migrations and on a schedule, onto the volume (decision [0008](docs/decisions/0008-backups-in-the-app.md)) |
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
│   ├── workflows/ci.yml         # api, web and e2e jobs; Docker image on main
│   └── dependabot.yml           # NuGet, npm (web, e2e), Actions, Docker
├── CLAUDE.md                    # repo-wide conventions for AI-assisted work
├── DESIGN.md
├── README.md
├── Dockerfile                   # web build → dotnet publish → runtime image
├── docker-compose.dev.yml       # local dev extras (Mailpit)
├── docs/
│   ├── decisions/               # decision log (one short file per decision)
│   ├── development.md           # local development guide
│   └── operations.md            # backups, restoring, rolling back a deploy
├── scripts/dev.sh               # API + Vite (+ Mailpit) together
├── e2e/                         # Playwright end-to-end tests and their stack (§3.8)
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
│   │       └── Infrastructure/  # errors, OpenAPI, auth, email, backups, SPA hosting, interceptors
│   └── tests/
│       └── Wwg.Api.IntegrationTests/
└── web/                         # React app; full layout in §3.12
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
  - Text comparison is **case-sensitive** by default (`BINARY` collation).
    `FirstName`, `LastName` and `Email` use **`COLLATE NOCASE`**, so sorting
    and equality ignore case (ASCII letters only, which is acceptable here).
  - **Searches use `LIKE`, not `.Contains()`.** EF translates `.Contains()`
    to `instr()`, which is case-sensitive *even on `NOCASE` columns*
    (checked against SQLite in step 13), so "mel" wouldn't find "Mel".
    `LIKE` ignores ASCII case. `Search.ContainsPattern` builds the pattern
    with `%` and `_` escaped, for `EF.Functions.Like(column, pattern,
    Search.EscapeCharacter)`.
  - No `decimal` math in SQL. Avoid `decimal` for anything sorted or aggregated.
  - No `rowversion`. Optimistic concurrency, if needed, uses a manual token.
  - Limited `ALTER TABLE`. EF rebuilds tables for some migrations, which is
    fine, but generated migrations should be reviewed. A rebuild drops the
    table's triggers (§5.1), and a test checks they're still there.
  - `ExecuteUpdate` / `ExecuteDelete` skip the audit interceptor, so a bulk
    update sets `UpdatedAt` itself.
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
  - `POST`s that create nothing return 200: register, login and refresh (a
    token), regenerating a join code (the new code) and joining a campaign
    you're already in (201 when it adds you).
- **Conflicts:** handlers check business rules first (e.g. "email already in
  use", "this Player already commands an army") and return a clear error. The
  database's unique indexes are the final guarantee. If two requests race past
  the check, the `DbUpdateException` for the unique-constraint failure is caught
  centrally and returned as **409** Problem Details, never a 500.
  The same handler (`ConflictExceptionHandler`) covers the other races:
  - a foreign key failing (the army a unit is being added to was just
    deleted), or a row changed or deleted under an update (EF's concurrency
    check, and Identity's on the user's `ConcurrencyStamp`): **409**, reload
    and try again;
  - a row deleted after the access filter found it and before the handler
    loaded it (`SingleOrGoneAsync`): **404**.

  `ConcurrencyTests` makes each race happen on cue, with an EF command
  interceptor that runs the "other request's" statement just before the
  command it would break.
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
  A successful sign-up signs the user in straight away (access token +
  refresh cookie).
  The Identity `UserName` is kept equal to the email.
- **Identity options that must change from the defaults:**
  - `User.RequireUniqueEmail = true` (default is `false`).
  - `User.AllowedUserNameCharacters = ""`. The default list rejects valid
    email addresses such as `o'brien@example.com`, and since the username *is*
    the email, email format is validated separately.
  - Password rules per §2.
- **Tokens** (decision
  [0004](docs/decisions/0004-refresh-token-in-httponly-cookie.md)): Identity's
  bearer scheme issues an access token and a refresh token. These are
  *opaque* tokens protected by ASP.NET Data Protection, not JWTs.
  - The **access token** is returned in the response body and kept **in
    memory** by the SPA, which sends `Authorization: Bearer …`. Lifetime
    **30 minutes**.
  - The **refresh token** never reaches JavaScript. It's set as a cookie,
    `__Secure-wwg-refresh`, with `HttpOnly; Secure; SameSite=Strict;
    Path=/api/auth`, so injected script can't steal it. Lifetime **30 days,
    sliding**: every refresh issues a new refresh token and cookie, so a
    session lasts as long as the app is used at least once every 30 days.
  - Every response that signs someone in (register, login, refresh, change
    password, change email) returns `{ accessToken, expiresIn }` and sets the
    cookie.
  - **CSRF:** the cookie is only sent to `/api/auth/*` and only on same-site
    requests (`SameSite=Strict`), and the refresh response can't be read
    cross-origin, so no antiforgery token is needed for the supported
    browsers.
  - The cookie uses `Max-Age`, not `Expires`, so its lifetime doesn't
    depend on the server's and browser's clocks agreeing.
  - Old refresh tokens stay valid until they expire (they aren't stored
    server-side). That's also why several tabs refreshing at once don't
    conflict. The security stamp (below) is what revokes them.
  - In development, Chrome accepts `Secure` cookies on `http://localhost`;
    Safari may not, so Safari is tested against HTTPS.
  - Swagger UI's **Authorize** button takes the access token from a login
    response.
  - **Data Protection keys are persisted** to the mounted volume. Otherwise
    every container restart would log everyone out. They're stored
    unencrypted (ASP.NET logs a warning about that at startup), which is
    acceptable on a volume only the app's container mounts.
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
  must do everything its version does, reading the token from the cookie:
  1. Unprotect the token with the bearer scheme's `RefreshTokenProtector`
     (a missing cookie is a 401).
  2. Reject it if `ExpiresUtc` has passed, using `TimeProvider`.
  3. Validate the security stamp; reject if the user is gone or the stamp
     changed.
  4. Issue a new token pair: the access token in the body, the refresh token
     in a fresh cookie.

  Each rejection path has its own integration test.
- **Logout:** opaque bearer tokens can't be revoked individually.
  - Normal logout is `POST /api/auth/logout`, which expires the refresh
    cookie (script can't delete an `HttpOnly` cookie), and the SPA drops its
    access token. An access token already issued keeps working until it
    expires (at most 30 minutes).
  - `POST /api/me/sign-out-everywhere` rotates the security stamp, which
    immediately invalidates every access and refresh token for that user.
- **Masquerade** (decision 0012): an Admin can use the app as another user,
  without their password.
  - `POST /api/admin/users/{id}/masquerade` issues the user's own tokens (their
    claims and roles) plus claims for the Admin's ID, the Admin's security
    stamp and when it ends (`Auth:MasqueradeLifetime`, 8 hours), replacing the
    refresh cookie. Not as yourself, and not while masquerading.
  - Refreshing a masquerade also checks it hasn't ended and the Admin still
    exists, is an Admin and has the same stamp. Neither token outlives it.
  - `POST /api/auth/masquerade/end` checks the same and issues the Admin's own
    tokens. Sign out while masquerading signs out entirely.
  - The app log records each start and end. `GET /api/me` includes
    `masquerade` (the Admin's name, when it ends) or null.
- **Password reset:**
  1. `POST /api/auth/forgot-password { email }` always returns 204, whether or
     not the account exists, so the endpoint can't be used to find out which
     emails are registered. The email is queued and sent in the background,
     so the response time doesn't give it away either.
  2. If the account exists, an email is sent with a link to the React app:
     `{App:PublicUrl}/reset-password?email=…&code=…`. The URL comes from config,
     never from the request's `Host` header, which an attacker could forge
     to point reset links at their own site.
  3. The React page calls `POST /api/auth/reset-password { email, code,
     newPassword }`. A bad code and an unknown email get the same error.
     Success ends every session (security stamp) and clears any lockout.
  - Links last `Auth:PasswordResetLinkLifetime` (default 2 hours). Like
    lockout, Identity checks this against the system clock, not the injected
    `TimeProvider`.
- **Change password** (logged in): requires the current password. This updates
  the security stamp, which signs out every other session. The response
  includes **new tokens** so the current session keeps working.
- **Change email** (logged in): requires the current password. Updates
  `Email` and `UserName` together (with the new security stamp, in one save)
  and rejects an address already in use (409) or the current one. No
  confirmation step, which matches sign-up. As a safeguard against account
  takeover, a **notice is sent to the old address**. Like a password change,
  it signs out other sessions and returns new tokens.
- **Lockout** after repeated failed logins (Identity defaults: 5 attempts, 5
  minutes). Login calls `CheckPasswordSignInAsync(…, lockoutOnFailure:
  true)`, and that's a tested requirement. So do change-email and
  change-password when they check the current password: otherwise a stolen
  access token could be used to guess it, slowed only by the rate limit. A
  locked account is refused there too (a `currentPassword` error). Identity's lockout reads the
  system clock, not the injected `TimeProvider`, so its expiry test moves
  `LockoutEnd` instead of the fake clock.
- **Rate limiting** (`AddRateLimiter`), partitioned by client IP. Needs the
  real client IP behind the proxy (§3.10).
  - `auth` policy: login, register, reset-password (e.g. 10 requests/minute).
  - `refresh` policy: token refresh, far looser (120/minute). Every page load
    and tab refreshes, and club members often share one IP (the same Wi-Fi),
    so the `auth` limit refused them. Refresh needs a valid refresh cookie, so
    there's nothing to guess.
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
  - The fallback policy also applies to requests that match no endpoint, so
    the pipeline runs routing and authorization *after* static files and
    Swagger UI, and unmatched requests get anonymous 404 fallbacks.
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
  - A shared **endpoint filter** resolves the campaign from the route's
    `{id}`: the campaign's own, or an army's or a unit's with one small
    query. It loads the caller's relationship (Admin / Umpire / Player /
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
  address). From Phase 8, the **turn emails**, each to the other side of the
  action:
  - an army's turn submitted → the Umpire (submitted by the Umpire, from
    Phase 9 → the army's commander);
  - approved, sent back or reverted (with the Umpire's notes) → the army's
    commander; from Phase 9 these list the orders the Umpire set;
  - a new turn opened (or the campaign started) → every commander.
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

- **Emails are queued** (`IEmailQueue`, a bounded in-memory channel) and sent
  by a background service. Requests never wait on SMTP, and forgot-password's
  response time doesn't reveal whether an account exists.
  - A failed send is tried again after 10 seconds, 1 minute and 5 minutes, then
    given up and logged as an error. A retry waits on its own, so the emails
    behind it still go out.
  - Emails still queued or waiting to retry are lost if the app stops. They're
    a reset link or a notice, which the user can ask for again.
- **If `Smtp:Host` is empty**, emails are logged instead of sent (with a warning
  at startup). This lets the app run before a real SMTP service is set up.
  Reset links then appear in the log, so configure SMTP before real use.
- **Local dev:** `docker-compose.dev.yml` runs **Mailpit**, a local SMTP server
  with a web inbox (http://localhost:8025), so emails can be checked by hand.
  `scripts/dev.sh` starts it when Docker is available, and the Development
  `Smtp` settings point at it.
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
  - One flag, `BuildTime.IsGeneratingOpenApiDocument` (entry assembly name),
    skips that work: `Program.cs` skips database setup, options skip
    `ValidateOnStart`, and the Swagger UI settings aren't read. The app runs
    in Production with no settings at that point. The document itself only
    needs endpoint metadata.
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
  - The front-end detects a new deploy through its service worker and
    prompts the user to reload (§3.12).
- .NET 10 emits **OpenAPI 3.1** by default, and Orval handles it (checked in
  step 9.3, §3.12).

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

**End-to-end tests** (`e2e/`, decision [0007](docs/decisions/0007-end-to-end-tests.md)):

- Playwright drives the **production image**, behind Caddy with TLS (as Traefik
  in production) and with Mailpit catching emails: `e2e/compose.yaml`, at
  `https://localhost:8443`. `e2e/stack.sh up` starts it from an empty database
  and creates the Admin account.
- HTTPS isn't optional: WebKit drops the `Secure` refresh cookie over plain
  http, even on localhost.
- Projects: desktop **Chromium** and **WebKit on an iPhone** profile.
  Playwright supports service workers in Chromium only, so the offline test
  runs there; offline on iOS Safari is checked by hand before a release.
- Each test signs up its own users (unique emails), so tests run in parallel
  against one database. Setup goes through the API only where the UI isn't
  what's being tested (registering a user who then uses the app).
- Covered: sign-up (and a taken email), staying signed in, sign-out, the
  sign-in redirect, password reset by email; the account page (name, email
  with the notice to the old address, password, signing out everywhere, and
  other devices signed out); campaigns (create, edit, delete, offline); join
  links (the signed-out round trip, a new link, leave, remove); admin (an
  Umpire's account deleted, a new Umpire set; admin screens hidden from
  others); armies and units (added, edited, deleted) and who sees them; the image's hosting (security
  headers, deep links, API 404s, health).
- Not covered here: rate limits (raised in the e2e stack; the API tests cover
  them) and anything the API or component tests already pin down in detail.

### 3.9 CI/CD (GitHub Actions)

One workflow, `.github/workflows/ci.yml`, with four jobs:

| Job | Pull request | Push to `main` / manual |
|---|:-:|:-:|
| `api`: format, build, contract check, tests | ✅ | ✅ |
| `web`: generate SDK, lint, typecheck, build | ✅ | ✅ |
| `e2e`: build the image, end-to-end tests | ✅ | ✅ |
| `docker`: build and push the image | – | ✅ (after the other three pass) |

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
     warning (§3.7). Its findings become `::warning` annotations on
     `api/openapi.json`, and the step can't fail the job. It runs the
     `tufin/oasdiff` image at a pinned version, which Dependabot doesn't
     update (it's in a `run` command), so bump it by hand now and then.
2. **`web` job:** in `web/`, run `npm ci`, generate the SDK and route tree
   from `api/openapi.json`, then lint, check formatting, typecheck, test and
   build (§3.12). This runs in parallel with `api`; it only needs the
   committed contract.
3. **`e2e` job:** lint, format-check and typecheck `e2e/`; build the image for
   linux/amd64 (its own GitHub Actions cache scope, so it doesn't evict the
   multi-arch cache); start the stack (`E2E_IMAGE`); install the browsers
   (cached by the lockfile) and run the suite. On failure it prints the app's
   logs and uploads the Playwright report and traces.
4. **`docker` job** (`main` only, after the other jobs pass): log in to Docker Hub
   and build and push the multi-stage image (§3.10) with
   `docker/build-push-action`, tagged:
   - `latest`
   - `vYYYYMMdd.HHmmss`: UTC time, computed once per run, e.g. `v20260927.143005`.

   Image name comes from a repo variable (`DOCKERHUB_IMAGE`). Credentials come
   from secrets `DOCKERHUB_USERNAME` and `DOCKERHUB_TOKEN` (a Docker Hub access
   token, not the account password).

   The image carries standard **OCI labels** (`org.opencontainers.image.revision`
   = git SHA, `.created`, `.source`, `.version`), generated by
   `docker/metadata-action`. Any running image can then be traced back to its
   exact commit (`docker inspect`).

**Concurrency:** one run at a time per PR or branch. On PRs, a new push
cancels the run in progress. On `main`, runs queue instead, so every commit
gets a result and an image push is never cut off halfway. GitHub keeps at
most one waiting run and a newer push replaces it, so the latest commit is
always built last. (Dependabot's own update checks show up as separate
"Dependabot Updates" runs; they aren't part of this workflow.)

**CI never commits to the repo**, so the workflow only needs read access to
the repo contents. Protecting `main` later just means making the `api`, `web`
and `e2e` jobs required checks; nothing else changes.

**Dependency updates:** `.github/dependabot.yml` opens weekly PRs for:
- NuGet packages in `api/` (Dependabot understands `Directory.Packages.props`),
  grouped so related packages (e.g. all `Microsoft.*`, EF Core) arrive together;
- npm packages in `web/` and `e2e/`, grouped similarly;
- GitHub Actions versions;
- the Docker base images in `Dockerfile`, and the images in the e2e stack's
  compose file.

Each Dependabot PR runs the normal PR checks, so an update that breaks the
build or tests is visible before merging.

Major versions that can't be taken yet (because another tool doesn't support
them) are **ignore rules** in `dependabot.yml`, each with a comment saying
why; delete the rule once the blocker is gone. Currently: ESLint 10 (waiting
on `jsx-a11y`), TypeScript 7 (waiting on typescript-eslint) and
`@types/node` above the Node version in `web/.nvmrc`.

### 3.10 Deployment

**Production (decision [0006](docs/decisions/0006-deploy-after-phase-1.md)):**
`https://wasatchwargamers.org`, a Docker Compose stack managed by Komodo on
the home server "roach" (arm64), defined in that server's config repo
(`roach/sync/stacks/wwg`). The image is `mastermel/wwg` on Docker Hub, built
for amd64 and arm64. The shared `traefik` network is pinned to
`172.21.0.0/16` in the Komodo compose, and the stack sets
`ForwardedHeaders__KnownNetworks__0` to it.

- Multi-stage `Dockerfile` at the repo root:
  1. **`web` stage:** `npm ci` + `npm run build` in `web/` → `web/dist/`.
  2. **`api` stage:** `dotnet publish` the API.
  3. **Runtime stage:** `aspnet:10.0-noble-chiseled-extra` (non-root UID
     1654, no shell, ICU and tzdata). It contains the published API with the
     front-end build copied into its `wwwroot/`.
  - The node and sdk stages run on the build machine's platform and
    cross-compile (`dotnet publish -a $TARGETARCH`); the runtime stage only
    copies, so arm64 builds on x64 without emulation. The build-time
    OpenAPI emit is off in the image build (it runs the app, which can't
    run cross-compiled; CI checks the committed contract).
- `.dockerignore` keeps `node_modules`, `bin/`, `obj/`, test output and local
  databases out of the build context.
- One volume mounted at `/data` holding the SQLite DB, its backups and the Data Protection
  keys.
- **Backups** (decision [0008](docs/decisions/0008-backups-in-the-app.md)) go to
  `/data/backups` (`Backup:Path`), made with SQLite's `VACUUM INTO`, a consistent snapshot of
  the live database:
  - **Before migrations:** at startup, if the database has a schema and migrations are
    pending, `wwg-{yyyyMMdd-HHmmss}-before-migration.db` is written first. If that fails,
    startup stops before migrating.
  - **Scheduled:** `wwg-{yyyyMMdd-HHmmss}.db` every `Backup:Interval` (default a day), timed
    from the newest backup on disk, so restarts don't reset the schedule.
  - The newest `Backup:Keep` (default 14) of each kind are kept. A backup is written under a
    temporary name and renamed, so a half-written file never counts as one.
  - No `Backup:Path` means no backups, with a warning at startup (tests switch them off).
  - Restoring, and rolling back a deploy (migrations only go forward), are in
    [`docs/operations.md`](docs/operations.md). Copying backups off the server is the
    server's job.
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
  the shared network's subnet (production's:
  `ForwardedHeaders__KnownNetworks__0=172.21.0.0/16`), not Traefik's IP,
  which can change. Without this, every request appears to come from the
  proxy, which breaks per-IP rate limiting, and generated URLs (`Location`
  headers) use `http://`.
  - Only the last hop is read (forward limit 1), which is right with Traefik
    alone. If another proxy or CDN (e.g. Cloudflare) is ever put in front,
    Traefik's `forwardedHeaders.trustedIPs` and the app's forward limit both
    need adjusting.
- **Health checks:** the runtime image has no `curl`, so the app has a
  check mode: `Wwg.Api --health-check` calls `GET /health` on localhost and
  exits 0 or 1. The image's `HEALTHCHECK` runs it every 30 seconds, and every
  second during the 30-second start period (`--start-interval`), so a new
  container reports healthy as soon as it is.
- The server's Compose file (Traefik labels, the shared network and the
  `/data` volume) lives in its config repo, not here (decision 0006).
  Updating means pulling the new `latest` (or a specific `vYYYYMMdd.HHmmss`)
  and restarting; `docs/operations.md` covers rolling back.
- Production configuration comes from environment variables:
  `ConnectionStrings__Default` (defaults to `Data Source=/data/wwg.db`),
  `Database__MigrateOnStartup` (default `true`), `Auth__DataProtectionKeysPath`
  (defaults to `/data/keys`), `Smtp__*`, `Admin__Emails__0…`,
  `App__PublicUrl`, `ForwardedHeaders__*`, `RateLimits__Auth__*` (defaults
  to 10 per minute), `RateLimits__Refresh__*` (120 per minute),
  `RateLimits__Email__*` (3 per 15 minutes), `RateLimits__Places__*` (30 per
  minute), `Backup__*` (`Path` defaults to `/data/backups`),
  `Geocoding__MapTilerApiKey` (a secret; without it, place search uses Photon).

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
    page load. So do the PWA files at the root (`sw.js`, `registerSW.js`,
    `manifest.webmanifest`); a cached service worker would hold back
    updates.
  - `.webmanifest` is served as `application/manifest+json` (checked, and
    added to the content-type map if missing).
- **Security headers** on SPA responses: a Content-Security-Policy,
  `X-Content-Type-Options: nosniff`, `Referrer-Policy`, and
  `frame-ancestors 'none'`. The SPA holds an access token in memory and can
  act as the user, so preventing XSS matters.
  - CSP starting point: `default-src 'self'`; `script-src 'self'` (strict);
    `style-src 'self'` plus a nonce or `'unsafe-inline'` for the CSS
    variables Mantine injects; `img-src 'self' data:`; `connect-src 'self'`;
    `worker-src 'self'` and `manifest-src 'self'` for the PWA;
    `frame-ancestors 'none'`. Tightened to what the built app actually needs.
  - The campaign map (§3.13) adds the tile, glyph, sprite and elevation hosts
    to `connect-src`, and `blob:` to `worker-src` and `img-src` (MapLibre's
    web workers and images). Place search goes through our API, so it adds
    nothing.
- Serving the SPA is skipped if `wwwroot/index.html` doesn't exist (in
  development and in tests).
- One origin means no CORS, and reset/join links use `App:PublicUrl`.

**Development: two processes, one origin from the browser's view.**
- The API runs on `http://localhost:5102` (`dotnet run` / `dotnet watch`).
- The front-end runs on Vite's dev server (`http://localhost:5173`, with hot
  reload). Vite **proxies** `/api`, `/openapi`, `/swagger` and `/health` to
  the API.
  The browser only ever talks to `:5173`, so there's still no CORS.
- `App:PublicUrl` is `http://localhost:5173` in development, so reset links
  in Mailpit open the Vite app.
- **Editor-agnostic.** Everything runs from the terminal: `scripts/dev.sh`
  starts the API (`dotnet watch`) and Vite together and stops both on
  Ctrl+C, and each piece also has its own documented command.
  [`docs/development.md`](docs/development.md) is the local development
  guide (prerequisites, first-time setup, running, testing, the checks the
  pre-commit hook and CI run, and editor setup, including notes for Neovim).
  VS Code gets optional launch and task configuration (a compound launch for
  API + Vite) and extension recommendations; nothing depends on them.

### 3.12 Front-end application (`web/`)

**Wasatch Wargamers**, a React + TypeScript single-page app, installable as a
PWA (decision [0005](docs/decisions/0005-front-end-stack.md)). That's the name
people see; the project keeps `wwg-campaigner` wherever they don't (decision
[0013](docs/decisions/0013-wasatch-wargamers-branding.md)).

| Area | Choice |
|---|---|
| Build & dev server | **Vite** |
| Components & styling | **Mantine** (CSS Modules + CSS variables), **Tabler** icons |
| Routing | **TanStack Router**: file-based routes, type-safe params and search params |
| Server state | **TanStack Query** hooks generated by **Orval**; cache persisted to IndexedDB |
| Client state | React context (session, UI). No global store |
| Forms | **React Hook Form + Zod**, with Zod schemas generated by Orval from the OpenAPI document |
| Lint & format | **ESLint** (typescript-eslint `strictTypeChecked` + `stylisticTypeChecked`, `react-hooks`, `react-refresh`, `jsx-a11y`, TanStack Query/Router plugins) + **Prettier** |
| TypeScript | `strict` (the Vite template's settings); no extra strictness flags for now |
| Tests | **Vitest** + React Testing Library (jsdom) + **MSW** |
| PWA | `vite-plugin-pwa` (Workbox) |
| Node / packages | Node 24 (pinned: `.nvmrc` + `engines`), npm |

**Look and feel**
- Mostly neutral. Brand colours **navy blue** (primary) and **silver**
  (neutrals), as custom Mantine palettes.
- Light and dark mode **follow the OS** (`defaultColorScheme="auto"`).
- **Desktop-first**, responsive down to phones: Mantine `AppShell` with a
  sidebar on desktop and a bottom tab bar on mobile.
- The mark: a **silhouette of an officer in the uniform of the late 18th
  century** (`web/public/logo.svg`, from a purchased vector image). It's a
  mask, drawn in the text colour (`BrandMark`), its details cut through to
  what's behind.
- The app icon (`web/public/app-icon.svg`): the officer in white on a navy
  rounded square, rendered to the PNG sizes the
  manifest needs. The maskable and Apple icons are the square without padding
  (Android and iOS cut their own shape); the figure keeps to Android's safe
  middle circle.
- Accessibility target **WCAG 2.1 AA**: Mantine's accessible components,
  `jsx-a11y` linting, and keyboard/screen-reader checks on key flows.
- English only, no i18n library. Dates and times use `Intl` in the user's
  locale and time zone.
- Browsers: the last two versions of **Chrome, Safari and iOS Safari**, which
  Vite's default build target (baseline "widely available") covers. Other
  modern browsers (Firefox, Edge) should work but aren't tested.
- **System fonts** only: no web font download, which is faster, works
  offline and keeps the CSP simple.
- Colour contrast of the navy/silver palettes is checked against AA in both
  light and dark mode (Mantine's default greys on dark can fall short).

**Visual design** (the polish pass after step 23)
- **Layered surfaces**, as CSS variables in `web/src/app/theme.ts`: a tinted
  page **canvas** (`--app-canvas`), **panels** on it in
  `--mantine-color-body` with a light shadow, and a **navy header**
  (`--app-header`). Dark mode uses blue-tinted greys (canvas `dark-8`, panels
  `dark-7`), not Mantine's neutral ones.
- **Page anatomy:** `Page` has a back link above the title, a one-line summary
  and the page's actions beside it, then a divider. The page's content is
  **`Section`s**: titled panels (h2) with an optional description and actions,
  a divider under the header, and a padded or edge-to-edge (`flush`, for
  tables) body. Detail pages use two columns on wide screens (the main content,
  and what the thing is beside it), one on phones.
- **Destructive actions** (delete, leave, sign out everywhere) sit in a
  red-edged **danger zone** `Section` at the end, apart from everyday actions.
- **`EmptyState`** for anything empty: an icon, what's missing, what to do.
- Tables: column headings as small, uppercase, dimmed labels; on phones
  secondary columns fold under the first (a unit's type, a member's role)
  rather than scroll sideways.
- Every colour pair is checked against AA with the WCAG formula (the numbers
  are in `theme.ts`), including input borders at 3:1 (WCAG 1.4.11). Links
  inside sentences are underlined (WCAG 1.4.1); status is a word, with colour
  as a dot beside it.

**Layout**

```
web/
├── index.html, vite.config.ts, orval.config.ts, eslint.config.js
├── .prettierrc, .nvmrc, package.json, CLAUDE.md
├── public/                 # icons, manifest assets
└── src/
    ├── main.tsx
    ├── app/                # providers (Mantine, Query, Router), theme, query client
    ├── routes/             # TanStack Router file routes (routeTree.gen.ts is generated)
    ├── features/           # auth, account, admin, campaigns, armies, units:
    │                       #   components, hooks and forms per feature
    ├── components/         # shared UI: layout, empty/error states
    ├── lib/                # API fetcher, session, utilities
    └── api/generated/      # Orval output
```

- Import alias `@/` → `src/`.
- Generated code (Orval output and `routeTree.gen.ts`) is git-ignored and
  regenerated before `dev`, `build`, `typecheck` and `test`.
- Pages are lazy-loaded per route.

**Talking to the API**
- Orval generates from `api/openapi.json`: TanStack Query hooks (fetch
  client) and Zod schemas.
- Every generated call goes through one custom fetch function, which:
  - uses same-origin relative URLs;
  - adds the access token;
  - on a 401, runs one shared refresh and retries once;
  - turns Problem Details into a typed error.
- Validation errors (400, camelCase keys) map onto React Hook Form fields.
  Other errors show as a notification or an inline message, worded by one
  `errorMessage()`: a rate limit and a server error each get their own message;
  otherwise the API's `detail` (e.g. why the Umpire can't leave), else what
  didn't happen.
- **After a change, the whole campaign is refetched** (`refreshCampaign`): its
  details, members, armies, each army's details and the campaign lists
  (including the admin ones). Changes spread: removing a Player unassigns
  their army, and a new Umpire changes the members and armies. Deleting or
  leaving a campaign drops everything cached about it (`forgetCampaign`), so
  it isn't shown offline either.
- **Required strings:** `[Required]` only makes a property required in the
  OpenAPI document; an empty string would still pass the generated Zod
  schema. An OpenAPI schema transformer adds `minLength: 1` to required
  strings, so client and server agree. Trimming stays server-side: a
  whitespace-only value comes back as a validation error on that field.
- Forms set proper `autocomplete` attributes, so password managers and
  autofill work (sign-in, register, account).
- Freshness: refetch on window focus and on reconnect, with a short
  `staleTime` (around 30 seconds). Nothing live in v1.

**Sessions** (API side in §3.4)
- The access token lives **in memory only**; the refresh token is an
  `HttpOnly` cookie that script can't read.
- On start-up the app calls `POST /api/auth/refresh`:
  - 200: signed in.
  - 401: signed out.
  - Network failure: **offline**, not signed out. Cached data is shown and
    the refresh is retried when the connection returns.
- The app refreshes again shortly before the access token expires (from
  `expiresIn`), and on a 401.
- Each tab keeps its own access token. Signing out is broadcast to the other
  tabs (`BroadcastChannel`).
- Signing out calls `POST /api/auth/logout`, drops the access token, and
  clears the query cache and its persisted copy.
- Users always stay signed in; there's no "remember me".
- A signed-out user opening a join link signs in or registers, then returns
  to the join page. The return path is a search param, accepted only if it's
  a same-origin path.
- Admin screens live in the same app and only show when `isAdmin` is true.
  Campaign screens likewise hide actions the user's role can't perform, and a
  page for an action the user can't take (a Player opening the edit page by
  its URL) says so instead of showing a form. The API enforces access
  regardless.
- **Only a 401 from refresh means "signed out"**, at any time. A 429, a 5xx,
  a network error, or a 200 that isn't a token (a captive portal's page) is
  temporary: keep the current state and retry later. Start-up always settles,
  whatever fails.
- **Signed out mid-session** (e.g. a password change elsewhere makes refresh
  return 401): clear the cache, go to sign-in, and keep the return path.
- The signed-in user's profile comes from `GET /api/me` after each
  successful refresh (at start-up, on the timer and after a 401), so a name or
  Admin change shows up without a reload.
- A small **"last user" record** (the `/api/me` response: id, email, names,
  `isAdmin`; no tokens) is kept in `localStorage`, so the app can start
  offline, pick the right cache and draw the shell. It's validated with the
  generated Zod schema when read, and cleared on sign-out.
- **Signing out while offline** can't reach `/api/auth/logout`, so the
  HttpOnly cookie survives. A "sign-out pending" flag keeps the app signed
  out and retries the logout at the next start.
- Both sign-in and sign-out are broadcast to other tabs.

**Offline and PWA**
- **Read-only offline** in v1: actions that change data are disabled while
  offline, with a message saying why.
- The query cache is persisted to IndexedDB (`persistQueryClient`). It's tied
  to the signed-in user's id, so nobody sees another user's cached data, and
  it's cleared on sign-out, when refresh returns 401, and when anyone but the
  last user signs in.
  - `maxAge` is **30 days**, matching the session (the library's 24-hour
    default would drop offline data after a day). `gcTime` is as long as a
    timer allows (about 24.8 days): anything longer overflows and drops data
    at once.
  - Restored data is refetched (if online) as soon as it's restored, however
    recent, so a reload always shows current data.
  - The cache's `buster` is the app version, so **a new deploy clears it**:
    persisted data can never have an older response shape than the code
    reading it. Offline data comes back on the next online visit.
  - **Only campaign data is persisted.** Admin queries (e.g. the user list
    with emails) are marked not to persist.
  - On a shared computer the data stays until someone signs out, which
    clears everything. That's accepted for a club app.
- A page never visited while online has nothing cached; it shows a clear
  "not available offline" state rather than a spinner or an error.
- On iOS, an installed app has its own storage, separate from Safari, so
  users sign in once more there. The install hint says so.
- An offline banner shows when data was last synced. Cached data is shown at
  any age.
- The service worker precaches the **app shell only** (HTML, JS, CSS,
  icons). It never caches API responses. Its navigation fallback excludes
  `/api`, `/openapi`, `/swagger` and `/health`.
- New deploys: the service worker shows an **"Update available, reload"**
  prompt. This is how the app detects a new deploy (§3.7). On a first visit
  no worker controls the page yet, so Reload simply reloads.
- The manifest icons, maskable icon, Apple touch icon and favicon are
  generated from `web/public/app-icon.svg` at build time; no PNGs are
  committed. The precache globs don't include `*.webmanifest`: the plugin
  adds the manifest itself, and listing it twice makes Workbox refuse to
  install.
- TanStack Query assumes it starts online, so the app seeds its online
  state from `navigator.onLine` at start-up. Pages render data through a
  shared `QueryState` component, which shows saved data even when a refetch
  fails, and "not available offline" when there's nothing saved.
- Manifest: name **Wasatch Wargamers**, short name **Wargamers** (launchers
  truncate longer labels), navy theme colour,
  standalone display.
- A small, dismissible install hint. iOS has no install prompt, so there it
  explains Share → Add to Home Screen.
- An **About** page shows the app version (the image tag, passed into the
  Vite build as a build argument by the CI `docker` job; `dev` locally) and
  the API's health. It replaces the Phase 1 "health" page.
- No push notifications, error reporting or analytics in v1.

**Screens, navigation and errors**
- **URLs:** `/campaigns`, `/campaigns/:id`, `/campaigns/:id/armies/:armyId`,
  `/join/:code`, `/sign-in`, `/register`, `/forgot-password`,
  `/reset-password`, `/account`, `/admin/users`, `/admin/users/:id`,
  `/admin/campaigns`, `/about`.
- Signed out, the app opens on the **sign-in page**, which carries the main
  image. Signed in, it opens on the campaign list.
- **Empty states** say what to do next, e.g. no campaigns yet: "Create a
  campaign" or "Ask your Umpire for a join link".
- **Errors:** a route error boundary and a 404 page. API 403 and 404 show a
  "not found / no access" page. 429 and 5xx show as notifications (one of each
  at a time, however many queries fail). Mantine notifications confirm
  successful actions. Deletes ask for confirmation.
- Information (offline, not available offline, not found, "check your email")
  is `role="status"`; only errors are `role="alert"`, which Mantine's `Alert`
  otherwise defaults to.
- **Accessibility:** each route sets `document.title` and moves focus to the
  page heading on navigation (WCAG 2.4.2 and focus order). Animations
  respect `prefers-reduced-motion`.

**Testing**
- Vitest + React Testing Library + MSW (handlers written in each test, with
  shared defaults in `test/server.ts`) for key screens and logic: session handling, forms, and role-dependent UI. Not
  every screen, and no coverage threshold.
- Key screens also get an automated **axe** accessibility check in their
  component tests. jsdom has no layout or computed colours, so those can't
  check contrast: the e2e suite's `accessibility.spec.ts` scans every main page
  in real browsers, light and dark, against WCAG 2.1 A and AA.
- Playwright end-to-end tests run against the production image (§3.8).

**Tooling**
- npm scripts: `dev`, `build`, `preview`, `lint`, `format`, `typecheck`,
  `test`, plus a `generate` step run before `dev`, `build`, `typecheck` and
  `test`.
- Prettier line width **100**, matching the C# code.
- Pre-commit (the Husky.Net hook, §4.1): Prettier and `eslint --fix` on
  staged `web/` files. Type checking and tests are left to CI. The task only
  runs when `web/` files are staged; if `web/node_modules` is missing it
  fails with "run `npm ci` in `web/`".
- Before committing, the full checks are the API's tests plus, in `web/`,
  `npm run lint`, `npm run typecheck` and `npm test` (listed in the root
  `CLAUDE.md`).
- CI `web` job (§3.9): `npm ci`, generate, lint, format check, typecheck,
  test, build.
- Dependabot: weekly npm updates in `web/`, grouped: Mantine, TanStack,
  ESLint/Prettier, Vite/Vitest, and testing libraries.
- Local development: `scripts/dev.sh`, `docs/development.md` and the
  optional VS Code configuration (§3.11).

**Checked during the scaffold**
- ✅ Orval with OpenAPI 3.1 (step 9.3). Nullable type arrays become
  `string | null` (Zod `.nullable()`), `minLength` becomes `.min(1)`, and
  enums without `"type": "string"` become string unions. The document stays
  on 3.1.
- ✅ Mantine injects its CSS variables in a `<style>` tag, so the CSP (§3.11)
  allows `'unsafe-inline'` for styles only. Scripts stay strict.
- ✅ Chrome accepts the `__Secure-` cookie prefix from `http://localhost`
  through the Vite proxy (step 10), so development keeps it.

### 3.13 Campaign map and turns (front-end)

Decisions [0009](docs/decisions/0009-campaign-map-stack.md) (the map) and
[0010](docs/decisions/0010-turns-factions-and-visibility.md) (turns, factions,
visibility); the data is in §5.1. Built in Phase 8 (§7).

**The map**
- **MapLibre GL JS** through `react-map-gl`, on **OpenFreeMap** vector tiles
  (OpenMapTiles schema). The OpenStreetMap and OpenFreeMap attribution is
  always shown.
- **Our own style**, built in code from the campaign's map settings and the
  colour scheme, in light and dark (following the OS, like the rest of the
  app). It's distinctive rather than a period imitation, and every colour pair
  is checked against AA like the rest of the theme (§3.12). Only these layers
  exist, each one the Umpire can switch off:
  - **Roads:** only the main classes (trunk, primary, secondary); motorways and
    smaller roads are left out.
  - **Towns and cities:** cities, towns and villages; not suburbs or
    neighbourhoods.
  - **Rivers and water:** rivers, canals, lakes and the sea.
  - **Forests:** wood and forest land cover.
  - **Hills:** hillshading from **Mapterhorn** elevation tiles (Terrarium
    encoding), with optional contour lines drawn in the browser
    (`maplibre-contour`).

  Never shown, as anachronisms: railways, motorways, modern borders,
  buildings, points of interest and built-up areas.
- **Map layers, for each viewer** (the Map page's **Map layers** button, over the map's top-left
  corner; a popover, or on a phone a sheet from the bottom): every member shows or hides the
  **Real map**'s layers (roads, place names, water, forests, hills, contours) and the **Game
  map**'s (grid, terrain, roads, rivers & waterways, towns & cities, bridges; for the Umpire,
  contact & concentration), of those the campaign's settings show: what the Umpire switches off
  isn't offered. Remembered on that device, per campaign (`localStorage`); "Show everything
  again" puts them back. The game map is drawn only once the view's longest side spans 20 hexes
  or fewer. Units, the reach while moving and ghost moves are always drawn.
- **A hex's card:** a click or tap on a hex (not on a unit, and not while placing or moving)
  opens a card at it with everything the viewer knows of it: its ground and forest, its town,
  city or fortress, the roads, rivers, bridges and waterways on its six sides, its actual terrain
  if they've been shown it, and the depots there they may see; the hex is outlined. With a mouse,
  a label with its ground follows the pointer from hex to hex.
- **The Map page's layout:** on a computer (62em and wider), the map takes the width with the turn
  panel and the legend beside it (340px, as tall as the map, scrolling together; the legend's
  groups open one at a time), and the other panels flow in columns beneath (two, or three on a
  wide screen). On a phone or tablet, one column: the map, the turn panel, the other panels, and
  the legend in full.
- **The legend** samples the unit symbols, the terrain tints, roads, rivers, waterways and
  bridges, towns, cities and fortresses, and the markers, in the current scheme's map colours.
- **Light or dark:** the system's setting, unless the user chooses one in the account menu
  (remembered on the device).
- **Place names** in the language the Umpire chooses (the tiles carry
  `name:en`, `name:de`, `name:fr`…, falling back to the local name). Names are
  modern ones.
- **Bounds:** the Umpire draws the campaign's area as a rectangle on the map
  (**Draw the area**: drag it, or tap one corner and then the opposite one, as
  on a phone), or saves the view as it is (**Use this view**); while the grid's
  shown, the Hex grid section counts the hexes the area and hex size give, as
  they change. Everyone's map opens on it (a computer's on the whole area; a
  phone's filled by it, the rest a pan away) and is held around it (MapLibre's
  `maxBounds`: the area and a margin, widened to the map's shape, so zooming out
  shows all of it at any size or shape). The world outside is faded, and the
  playable area outlined along the grid's outer hexes (its rectangle without a
  grid); the game map is only ever drawn inside it. The Map and Terrain pages
  take the screen's whole width, and on a computer the Map page's map can go
  **full screen** (the browser's too, where it allows it), with what's being
  placed or moved above it; **Exit full screen**, or Esc, brings it back.
  Zooming in is unlimited. Place search (our API, §5.3)
  helps the Umpire find the area; the settings page previews layers and the
  label language as they change.
- **The hex grid** (decision 0014, Phase 11): flat-topped hexes (3 miles
  across by default) over the whole area, drawn as a light line layer that can
  be switched off like the others. Units are in hexes, and a hex's units are one
  stack at its centre. Each hex has a terrain, a forest flag and a settlement,
  and each edge a road, river or bridge (§5.1); the map can shade the terrain.
- **Unit icons:** NATO-style symbols (APP-6), drawn by the app (`UnitSymbol`):
  a frame in the army's colour with the arm's glyph: a cross for infantry, a
  slash for cavalry, a dot for artillery; L marks light infantry, an oval
  (armour) heavy cavalry, a slash (mounted) horse artillery. From Phase 11 the
  types follow the rules' movement classes (§5.1), with symbols for the new
  ones (medium cavalry, scouts, partisans, engineers, supply trains, siege
  artillery). A black frame and white halo keep them clear on any map. Not
  `milsymbol` (decision 0009's choice): none of its codes gave the distinct
  types needed, with nothing for skirmishers or horse artillery. A legend on the
  map says what each means. Each unit on the map is a button named for screen
  readers ("Imperial Guard, Heavy Infantry, Armée du Nord").
- **Stacks:** units close together at the current zoom are drawn as one
  **stack** marker with the count and the armies' colours. Tapping it lists the
  units in it; choosing one selects it. The unit list beside the map selects
  units too (and flies the map to them), which is also the way in for keyboard
  and screen-reader users.
- **Touch screens:** one finger scrolls the page past the map; two pan and zoom
  it (MapLibre's cooperative gestures, which show a hint). Taps still place and
  move units. With a mouse the map pans with a drag and zooms with the wheel.
- **Online only:** offline, the map page says it needs a connection; nothing
  about the map is saved for offline use (`persist: false`).

**Pages**
- `/campaigns/:id/map`: the map, for every member (what's on it follows §5.2).
- `/campaigns/:id/map/settings` (Umpire, Admin): bounds and place search, the
  layer switches, the label language, the distance unit (km or miles), and the
  hex size (while setting up; from Phase 11). Phase 11 adds the terrain editor
  (the inferred terrain of each hex, which the Umpire can change) and the
  movement table.
- `/library` (step 41; everyone signed in, in the navigation): the club's factions and their
  units; Managers and Admins create, edit and delete them there, and only there. In a campaign,
  **Edit army** selects the army's factions, and **Add units** lists only their units.
- The campaign page gains a **Factions** section (the Umpire creates, renames
  and deletes them; from step 41, **Sides**; from step 46a, the campaign's two sides, renamed only); the army page's **Edit army** covers name, faction (side), colour (with
  swatches; a new army is offered the first free one) and nation (with its flag).
- **`ArmyBadge`**, the army's flag in its colour beside its name, everywhere an
  army is named: the armies list, the army page, the members list, the admin
  views and the map. The name is always there, so colour is never the only
  signal. The flags are a library of simplified SVG flags of the period's
  nations (France, Britain, Prussia, Austria, Russia, Spain, Portugal, Sweden,
  the Confederation of the Rhine states and others), drawn for the app from
  public-domain designs, plus a plain one.

**A commander's view** (one army)
- Their units at their current positions (the latest Completed turn), and,
  in a Draft, each ordered move as a **ghost** icon at the destination with a
  line from where the unit is.
- **Turn panel:** the turn number and its status; each unit Moved, Held or
  without an order, with an **undo** button for those with one; the Umpire's
  notes, if the turn was sent back or reverted; how far the turn has got
  ("4 of 6 armies submitted", no names); and **Submit** once every unit has an
  order. While Submitted, nothing can be changed.
- **Unit drawer** (tap a unit): name, type, FF, points, the army, this turn's
  order and any note on it, with **Move** and **Hold**.
- **Move:** the rest of the interface steps aside; the hexes the unit can reach
  this turn are shaded (by the movement table, from its current hex); tapping one
  picks it as the destination, and the cheapest path there is drawn hex by hex,
  with what it costs; **Confirm** or **Cancel**. Each order saves straight away,
  so a draft survives closing the tab. (Phase 8 measured a straight-line range
  instead; decision 0014 replaced it.)
- **History:** their army's turns, newest first; choosing one shows the units
  where they were in that turn. The arrow keys step through them, so moving
  forward and back through the campaign is quick.

**The Umpire's view** (Admins see the same)
- Every army's units at their current positions, in the armies' colours.
- **Armies list:** choosing one highlights its units and fades the rest.
- **Turn list**, from 0 to the open turn, each with its progress ("Turn 4 – 5
  of 6"). Choosing a turn shows everyone's positions in it, and each army's turn
  under it: status, submitted and completed times, and **Approve**, **Send
  back** or **Revert** (the last two with a note for the turn and for units),
  for the open turn only.
- **Start turn N+1**, with confirmation, once every army's turn is Completed.
- **On a commander's behalf** (decision 0011): Move, Hold and Take back in the drawer for any
  army's unit while its turn is a Draft or Submitted (a move past the limit warns first), and
  **Submit for it** for a Draft. The commander's panel marks orders the Umpire set, and the
  history lists what the Umpire changed.
- **Setup (turn 0):** the units not yet placed are listed; the Umpire places
  each one on the map, then presses **Start campaign** (every army needs a
  faction, and every unit a position). A unit or army added later appears in
  that list until it's placed; the next turn can't start until then.

**Testing:** MapLibre needs WebGL, which jsdom lacks. Component tests stub the
map component and test the panels, drawer, lists and move flow around it; the
e2e suite (Chromium) drives the real map.

## 4. Cross-cutting concerns

| Concern | Approach |
|---|---|
| Logging | Built-in `ILogger`. **JSON console logs** in production (`Logging:Console:FormatterName`, UTC timestamps); plain text in development. **One line per API request** (`HttpLogging`: method, path, status, duration; never headers, bodies or query strings); static files and `/health` aren't logged. EF Core's SQL is only logged in development |
| Configuration | `appsettings.{Environment}.json` + env vars; user-secrets in dev. **Every settings section** (`App`, `Smtp`, `Admin`, `Auth`, `Backup`, `Geocoding`, `RateLimits`, `ForwardedHeaders`) is a typed options class with DataAnnotations, `ValidateDataAnnotations()` and `ValidateOnStart()`, so bad config fails at startup with a clear message. Nested objects (each rate limit) need `[ValidateObjectMembers]`, or they aren't checked; lists are checked in `Validate` (`IValidatableObject`), e.g. that every `Admin:Emails` entry is an email. `App:PublicUrl` (the app's public URL, e.g. `https://wwg.example.com`) is required outside development |
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
- `CLAUDE.md` files (the root, `api/`, `web/` and `e2e/`) summarise these
  conventions and the commit rules, so AI-assisted changes follow them too.

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
  Id              Guid
  ArmyId          → Army
  Name            string (required, ≤100)
  Type            (Phase 11, by the rules' movement classes) LineInfantry |
                  FootArtillery | Engineers | LightInfantry | Partisans |
                  LightCavalry | Scouts | MediumCavalry | HeavyCavalry |
                  HorseArtillery | SupplyTrain | SiegeArtillery
                  (before: HeavyInfantry, now LineInfantry; Skirmishers,
                  now LightInfantry)
  FightingFactor  int 1–9 ("FF" in the app)
  Points          int 0–100
  CreatedAt / UpdatedAt
```

**Phase 8** (§7, decisions 0009 and 0010) adds:

```
Faction                       Army (new fields)
  Id           Guid             FactionId    → Faction? (null = "Unassigned")
  CampaignId   → Campaign       Color        ArmyColor (one of 8 palette keys)
  Name         string (≤100)    Nation       Nation (whose flag it flies; None = plain)
  CreatedAt / UpdatedAt

CampaignMap (one per campaign)         MovementLimit
  CampaignId     → Campaign (key)        CampaignId  → Campaign
  West/South/East/North  double (bounds)  UnitType    (as Unit.Type)
  LabelLanguage  string ("local", "en"…)  Metres      int (per turn)
  DistanceUnit   Kilometres | Miles
  ShowRoads / ShowPlaces / ShowWater /
  ShowForests / ShowHills / ShowContours  bool

CampaignTurn                   ArmyTurn
  Id           Guid              Id              Guid
  CampaignId   → Campaign        CampaignTurnId  → CampaignTurn
  Number       int (0 = setup)   ArmyId          → Army
  OpenedAt / ClosedAt?           Status          Draft | Submitted | Completed
                                 SubmittedAt? / CompletedAt?

UnitOrder                      ArmyTurnEvent (the turn's history)
  Id           Guid              Id           Guid
  ArmyTurnId   → ArmyTurn        ArmyTurnId   → ArmyTurn
  UnitId       → Unit            Kind         Submitted | Approved | SentBack | Reverted
  Kind         Move | Hold       At / ByUserId
  Latitude / Longitude  double   Note         string? (≤2000)
  (Hold copies the current
   position, so every turn is    UnitNote
   complete on its own)            EventId → ArmyTurnEvent, UnitId → Unit, Text (≤1000)
```

- **Current position** of a unit: its order's position in its army's latest
  Completed turn. Turn 0's orders are the Umpire's placements.
- Distances are stored in metres; km or miles is only how they're shown. (Phase
  8 checked moves as straight lines; from Phase 11 moves are counted in hexes.)
- Colours and nations are keys, not values (`ArmyColor`: Red, Blue, Green,
  Orange, Purple, Sky, Gold, Magenta; `Nation`: None and 21 states of the
  period), enums in the contract that the API validates. The palette (from
  Okabe–Ito, distinct for colour-blind users, tuned per scheme to 3.2:1 against
  the app's panels and canvas; `army-colors.ts`) and the flags (simplified SVGs,
  `NationFlag.tsx`) live in the front-end. A new army gets the first colour no
  other army has (else the least used). Existing armies were spread over the
  palette by the migration.

**Phase 11** (§7, decision 0014) adds the hex grid:

```
CampaignMap (new fields)          Campaign (new fields)
  HexSize     int (metres across    StartDate      date (the first turn's day)
              the flats; 4828 =     FirstTurnPart  Morning | Afternoon | Night
              3 miles)
                                    UnitOrder (changed)
HexCell (only hexes with data)        Q / R          int (the hex; replaces
  CampaignId  → Campaign                             Latitude / Longitude)
  Q / R       int (axial)             Path           the hexes passed through,
  Terrain     Flat | LowHill |                       in order (Move)
              HighHill | Mountain |   Progress       0–1: part of the way into
              Water                                  the path's last hex, for a
  Forest      bool                                   hex that takes more than a
  SettlementSize None | Town | City                  turn (step 44)
  Walled / Fortress  bool
  Capital     None | Minor | Capital  MovementRate (step 44; the rules' table
  Name        string? (≤100)            by default)
  SetByUmpire bool (inference
              leaves it alone)          CampaignId, MovementClass, Ground
                                        (GoodRoad | PoorRoad | Flat | LowHill
HexEdge (the edge on a hex's N, NE      | HighHill | Mountain), Hexes (per
or SE side; the others belong to        turn; 0 = can't)
its neighbours)
  CampaignId, Q, R, Side  (N | NE | SE)
  Road        None | Poor | Good
  River       bool (along the edge:
              only a bridge crosses it)
  Bridge      bool
  Waterway    None | Out | In (a
              navigable course across
              the edge, flowing out of
              (Q, R) or into it; boats)
  SetByUmpire bool

HexDetail (a hex's actual terrain,     HexDetailReveal
page 57; decision 0016)                  HexDetailId → HexDetail
  CampaignId, Q, R  (unique)             ArmyId → Army (shown to)
  Relief      Flat | Rolling | Hilly | HighHills
  Scrub / Village / Woods / Forest / Farms / Fields / Streams  bool
  Dominant    None | SmallCastle | WeakFarmhouse | StrongFarmhouse
  Favorability  NotRolled | Favorable | Neutral | Unfavorable
  ForArmyId   → Army? (who asked)
  RedDie / WhiteDie / GreenDie  int? (as shaken; red after its modifier)
  ShownToAll  bool
```

- **The grid:** flat-topped hexes laid over the area in a local flat projection
  (east = R·cos φ₀·Δλ, north = R·Δφ, φ₀ the area's middle latitude), hex (0, 0)
  centred on the area's middle, axial coordinates (q east, r south-east). The
  grid is every hex whose centre is inside the bounds. The hex size and bounds
  can change only while setting up; `HexGrid` (C#) and `hex-grid.ts` share the
  arithmetic and are tested against the same figures.
- A hex with no `HexCell` is Flat with nothing on it; an edge with no `HexEdge`
  has no road or river.
- **Two levels of terrain** (decision 0016): the map terrain (`HexCell`, `HexEdge`) is the hex's
  general character, which movement and visibility use; a `HexDetail` is what's actually there,
  found by the Umpire's three dice when a player asks (the rules, p. 57), for the battle. Red
  die: 1d6, +1 for low hills or forest, +2 for high hills or mountains (the larger), −1 on a flat
  hex if the Umpire chooses, kept to 0–7; its row fills the relief and features (0 flat, scrub;
  1 flat, village, woods; 2 flat, farms, streams; 3 flat, forest; 4 rolling, village, woods;
  5 rolling, fields, farms, streams; 6 hilly, fields, farms, streams; 7 high hills, forest,
  streams). White die: 1 small castle, 3 weak farmhouse, 5 strong farmhouse, else none. Green
  die (only when both sides arrive together): 1 favourable, 6 unfavourable, else neutral. The
  Umpire can change any of it; members see a detail once it's shown to their army or to all.
- **Movement classes** (the rules' table, §E.1; hexes per turn):

  | Class | Types | Good road | Poor road | Flat | Low hill | High hill | Mountain |
  |---|---|:-:|:-:|:-:|:-:|:-:|:-:|
  | Infantry | Line infantry, Foot artillery, Engineers | 3 | 2 | 2 | 1 | ½ | – |
  | Light | Light infantry, Partisans | 5 | 4 | 3 | 2 | 1 | ½ |
  | Light cavalry | Light cavalry, Scouts | 6 | 5 | 4 | 3 | 2 | 1 |
  | Cavalry | Medium cavalry, Heavy cavalry, Horse artillery | 5 | 4 | 3 | 2 | 1 | – |
  | Slow | Supply train, Siege artillery | 3 | 2 | 1 | ½ | – | – |

**Step 41** (decision 0015) makes factions and units the club's, shared by every campaign:

```
Faction (global: a collection)   Unit (global)                ArmyUnit (a unit in a campaign)
  Id        Guid                   Id           Guid            Id           Guid
  Name      string (≤100)          FactionId    → Faction       CampaignId   → Campaign
  Nation    Nation (its flag;      Name         string (≤100)   ArmyId       → Army
            None = plain)          Type         (as before)     UnitId       → Unit (the library
  CreatedAt / UpdatedAt            FightingFactor / Points                    unit it came from)
                                   CreatedAt / UpdatedAt        Name, Type, FightingFactor,
Side (was the campaign's                                          Points (copied when it joins;
Faction; unchanged)                                               the campaign's from then on)
  CampaignId, Name                                              CreatedAt / UpdatedAt
Army.SideId (was FactionId; required from step 46a, a campaign having exactly two sides)
ArmyFaction (the library factions an army takes units from)
  ArmyId → Army, FactionId → Faction    (unique together)
```

- `ArmyUnit (CampaignId, UnitId)` is unique: a library unit is in one army per campaign, but can
  be in several campaigns. Orders, turn notes, positions and the visibility rule refer to army
  units (`UnitOrder.UnitId` and `UnitNote.UnitId` point at `ArmyUnits`).
- A `Unit` with army units, or a `Faction` with units or armies selecting it, can't be deleted
  (**NO ACTION**; 409 first). Deleting a campaign deletes its army units and army factions, never
  library items.
- **Manager** is an Identity role beside Admin (§3.5): Managers and Admins edit the library.
  Admins grant and remove it on a user's admin page; it's never granted by config.

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
- **Triggers** back up the commander rules, which span two tables, so no index
  can hold them (migration `AddCommanderRules`):
  - an army's commander must be a **Player in the army's campaign** (on insert,
    and on a change to `CommanderId` or `CampaignId`);
  - a member who commands an army **can't become the Umpire** (Set Umpire
    unassigns the army first).

  The handlers check both first and give clear errors. The triggers only catch
  two requests racing (a Player made Umpire while being given an army), which
  gets a **409**. EF rebuilds a SQLite table for some migrations, which drops
  its triggers, so a test fails if they're missing.
- Phase 8: `CampaignTurn (CampaignId, Number)`, `ArmyTurn (CampaignTurnId,
  ArmyId)` and `UnitOrder (ArmyTurnId, UnitId)` are unique, and
  `MovementLimit (CampaignId, UnitType)`. A `UnitOrder` **restricts** deleting
  its unit, and an `ArmyTurn` its army, so no history can be deleted by
  accident (during setup the handler deletes the turn-0 order first).
- **Cascades:**
  - Deleting a Campaign deletes its members, armies and units (and, from
    Phase 8, its factions, map, turns and orders).
  - Deleting a Faction sets its armies' `FactionId` to NULL.
  - Deleting an Army deletes its units.
  - Deleting a CampaignMember (Player leaves/removed, or user deleted) sets
    `Army.CommanderId` to NULL. The Army and its Units are kept.
  - Deleting a user deletes their CampaignMember rows (and Identity data).
    Their campaigns are **not** deleted.

**Rules enforced in the handlers:**

- A commander must be a member with the **Player** role in the Army's campaign.
  The Umpire can't command an Army.
- The Umpire can't leave or be removed as a normal member.
- **Phase 8:**
  - At most **8 armies** per campaign.
  - **Start campaign** (closing turn 0, opening turn 1) needs every army to have
    a faction, and every unit a position.
  - **Start turn N+1** needs every army's turn N to be Completed and every unit
    placed. An army with no commander can't submit, so it holds the campaign up.
  - Turn statuses only move Draft → Submitted (the commander, once every unit
    has an order) → Completed (the Umpire approves). The Umpire can send a
    Submitted turn back to Draft, and revert a Completed one to Draft, only in
    the open campaign turn. Nothing changes while Submitted.
  - A Move follows a path of adjacent hexes from the unit's current hex, inside
    the grid, that its movement class can afford this turn (Phase 11, below).
    Phase 8's rule, inside the bounds and within a straight-line limit, is gone.
  - After the campaign starts, armies and units can be added but not deleted.
    The Umpire places a new unit before the next turn starts; the placement is
    an order added to its army's turn in the last closed campaign turn (which
    becomes its current position, and which no revert can reach), the only
    change ever made to a Completed turn. A new
    army gets a Completed turn for the last closed campaign turn to hold its
    placements, and a Draft for the open one.
- **Step 41** (decision 0015): adding a library unit copies its name, type, FF and points into
  the army unit; editing either afterwards changes only that one. An army adds units only from
  the factions it has selected (else 409), and a faction can't be deselected while the army has
  units from it (409). An army unit can be removed only while setting up.
- **Phase 11** (decision 0014):
  - A turn gives each unit a budget of one turn's movement. Entering a hex costs
    1 ÷ the class's rate: the road's, when the step crosses an edge with a road
    (a good road into a high-hill or mountain hex counts as poor), else the
    entered hex's terrain (a forest hex moves as low hills). A step across a
    river edge without a bridge, into water, or into terrain the class can't
    cross is closed. A path may end part-way into a hex that costs more than
    what's left, and goes on into it next turn (`Progress`). Until terrain
    exists (step 42) every hex is Flat, and until step 44 roads and rivers
    don't count.
  - Time of day: turn *n* (from 1) falls in `FirstTurnPart` + *n* − 1, cycling
    Morning (06–14), Afternoon (14–22), Night (22–06), from `StartDate`. Every
    turn is played. Morning: the Infantry class of French armies and their
    allies (nations France, Italy, Warsaw, Bavaria, Württemberg, Baden and
    Holland; not Westphalia, Saxony, Spain, Portugal, Naples or Denmark) moves
    one hex further (each rate + 1). Afternoon: Russian and Austrian line
    infantry and foot artillery one hex less (each rate − 1, not below ½). Night
    moves are recorded for forced marches (step 47).
  - The Umpire's moves (decision 0011) may exceed the budget, after a warning,
    but not leave the grid or cross a closed step.
  - Placing a unit puts it in a hex. Positions from before the grid were
    converted to the hex containing them, once.
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

**Phase 8** (decision 0010). One change to the table above: **viewing an
army's units becomes ✅ for every member**, read-only, whatever their faction.
New rows:

| Action | Admin | Umpire | Player (commander) | Player (other) | Non-member |
|---|:-:|:-:|:-:|:-:|:-:|
| List factions; see each army's faction, colour, nation | ✅ | ✅ | ✅ | ✅ | 404 |
| Create / rename / delete faction; set an army's faction, colour, nation | ✅ | ✅ | 403 | 403 | 404 |
| Step 41: list / create / rename / delete sides (the campaign's; as factions above; from step 46a, list and rename only) | ✅ | ✅ | 403 | 403 | 404 |
| Step 41: select an army's factions; add library units to an army, edit or remove an army unit | ✅ | ✅ | 403 | 403 | 404 |

The library (step 41) isn't a campaign's: viewing it is for everyone signed in, and creating,
editing and deleting factions and units is for **Managers** and Admins (403 for others). Admins
make users Managers (`PUT /api/admin/users/{id}/manager`).
| View the map settings (bounds, layers, language, limits) | ✅ | ✅ | ✅ | ✅ | 404 |
| Edit the map settings; search for places | ✅ | ✅ | 403 | 403 | 404 |
| View turn progress (numbers, statuses, counts) | ✅ | ✅ | ✅ | ✅ | 404 |
| View **positions and orders** (the visibility rule) | ✅ all | ✅ all | own army | 403 | 404 |
| Give orders, undo, submit (the open turn; a Draft, or for the Umpire a Draft or Submitted) | ✅ | ✅ | own army | 403 | 404 |
| Place units (turn 0, and units added later) | ✅ | ✅ | 403 | 403 | 404 |
| Approve / send back / revert an army's turn | ✅ | ✅ | 403 | 403 | 404 |
| Start the campaign; start the next turn | ✅ | ✅ | 403 | 403 | 404 |

- **The visibility rule** is one server-side check, "can this user see army
  A's positions in turn N?", that every read of positions or orders goes
  through (queries apply it too). Today: the Umpire and Admins, and the army's
  commander. Later, intelligence sharing (allies' positions for a past turn)
  and scouting (chosen enemy units for a chosen turn) add grants to it.
- The Umpire (and Admins) can also give, change and take back any army's orders,
  and submit a Draft for it, in the open turn only (decision 0011): a Draft or a
  Submitted turn (an approved one is reopened first), inside the area but not
  held to the movement limit. The commander edits only a Draft.

- Any signed-in user can create a campaign, and becomes its Umpire.
- The join link preview is public; anyone with the code can see the campaign
  name. Joining requires signing in. Joining a campaign you're already in does
  nothing (two joins racing: the second gets a 409 from the unique index).
- Players see every Army's name and commander, **including unassigned
  armies**, but only see Units for the Army they command. (Phase 8: every
  army's units, read-only; positions follow the visibility rule.)

**Admin (site-wide):**

| Action | Admin | Everyone else |
|---|:-:|:-:|
| List / search users | ✅ | 403 |
| View a user (incl. their campaigns and roles) | ✅ | 403 |
| Delete a user | ✅ (not themselves; another Admin, yes) | 403 |
| List all campaigns (`/api/admin/campaigns`) | ✅ | 403 |
| Set a campaign's Umpire | ✅ | 403 |

### 5.3 Endpoints

**Auth** (anonymous)

| Method | Route | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Sign up (email, password, first, last) → access token + refresh cookie |
| POST | `/api/auth/login` | Email + password → access token + refresh cookie |
| POST | `/api/auth/refresh` | Refresh cookie → new access token + refresh cookie |
| POST | `/api/auth/logout` | Expire the refresh cookie |
| POST | `/api/auth/masquerade/end` | End a masquerade → the Admin's own tokens (not masquerading: 409) |
| POST | `/api/auth/forgot-password` | Send reset email (always 204) |
| POST | `/api/auth/reset-password` | Email + code + new password |

**Account** (signed in)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/me` | Current user: id, email, names, `isAdmin`, `masquerade` (who, until when) |
| PUT | `/api/me` | Update first/last name |
| PUT | `/api/me/email` | Change email (needs current password) → new tokens; notice to the old address. Rate-limited (`auth`) |
| PUT | `/api/me/password` | Change password (needs current password) → new tokens. Rate-limited (`auth`) |
| POST | `/api/me/sign-out-everywhere` | Rotate security stamp; all tokens stop working, this session's too (its cookie is cleared) |

**Admin** (Admin only)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/admin/users` | Paged list, `?search=` on name/email |
| GET | `/api/admin/users/{id}` | User details, with their campaigns and roles |
| DELETE | `/api/admin/users/{id}` | Delete user (not self: 409) |
| POST | `/api/admin/users/{id}/masquerade` | Masquerade as the user → their tokens, marked (not self or while masquerading: 409) |
| GET | `/api/admin/campaigns` | Paged list of every campaign, incl. umpire-less ones (`?search=`, `?withoutUmpire=`) |
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
| POST | `/api/armies/{id}/units` | Add unit `{ name, type, fightingFactor, points }` |
| PUT | `/api/units/{id}` | Edit unit (the same four fields) |
| DELETE | `/api/units/{id}` | Delete unit |

**Phase 8: factions, the map and turns** (first pass; operationIds settled in
each step)

| Method | Route | Purpose |
|---|---|---|
| GET / POST | `/api/campaigns/{id}/factions` | List / create factions |
| PUT / DELETE | `/api/factions/{id}` | Rename / delete a faction |
| PUT | `/api/armies/{id}` | `UpdateArmy` (was `RenameArmy`): name, faction, colour and nation |
| GET / PUT | `/api/campaigns/{id}/map` | The map settings, with the hex size (the movement limits until Phase 11) |
| GET | `/api/campaigns/{id}/places?search=` | Place search for the bounds (Umpire; server-side geocoder, rate-limited) |
| GET | `/api/campaigns/{id}/turns` | Campaign turns: number, open/closed, each army's status and times, counts; for the Umpire, what stops the start or the next turn |
| POST | `/api/campaigns/{id}/start` | Start the campaign (close turn 0, open turn 1) |
| POST | `/api/campaigns/{id}/turns` | Start the next turn (emails every commander); from step 47b, with the Umpire's confirmed attrition |
| GET | `/api/campaigns/{id}/positions?turn=` | Units' positions, as the caller may see them: now (the default), after a closed turn, or ordered in the open one |
| GET | `/api/armies/{id}/turns` | An army's turns, with their orders, notes and history (visibility rule) |
| PUT / DELETE | `/api/army-turns/{id}/orders/{unitId}` | Give a unit's order `{ kind, path? }` (Move: the hexes it passes through, in order) / undo it |
| POST | `/api/army-turns/{id}/submit` | Submit (every unit on the map has an order; emails the Umpire) |
| POST | `/api/army-turns/{id}/approve` | Approve: Completed (this and the next two email the commander) |
| POST | `/api/army-turns/{id}/send-back` | Back to Draft `{ note?, unitNotes? }` |
| POST | `/api/army-turns/{id}/revert` | Completed back to Draft, open turn only `{ note?, unitNotes? }` |
| GET | `/api/campaigns/{id}/units` | Every unit in the campaign (every member), for the map |
| PUT / DELETE | `/api/units/{id}/placement` | The Umpire places a unit `{ q, r }` (turn 0, or added later) / takes it off again (setup only) |
| GET | `/api/campaigns/{id}/grid` | The grid's cells and edges with data (every member): terrain, forest, settlements, roads, rivers, bridges (Phase 11) |
| PUT | `/api/campaigns/{id}/grid` | Save inferred terrain for the whole grid `{ cells, edges }` (Umpire; 204; no area: 409); hexes and edges the Umpire set are kept |
| PUT | `/api/campaigns/{id}/grid/cells/{q}/{r}` · `/edges/{q}/{r}/{side}` | The Umpire sets one hex or edge |
| GET | `/api/campaigns/{id}/grid/details` | The hexes' actual terrain the caller may see (the Umpire: all, with who asked and who it's shown to; members: shown to their army or to all, without those) (decision 0016) |
| POST | `/api/campaigns/{id}/grid/details/{q}/{r}/roll` | The Umpire shakes the dice for a hex `{ forArmyId?, favorability, flatMinusOne }` (again: replaces the roll, keeping who sees it; one off only on a flat hex). The dice are the `IDice` service, fixed in tests |
| PUT / DELETE | `/api/campaigns/{id}/grid/details/{q}/{r}` | The Umpire changes it, or sets one without dice `{ relief, features, dominant, favorability, forArmyId?, shownToArmyIds, shownToAll }` / forgets it. Changing the grid forgets them all; deleting the army that asked keeps them |

**Step 41: the library and army units** (decision 0015; the campaign's factions become sides:
`/api/campaigns/{id}/sides`, `/api/sides/{id}`; from step 46a, GET the two and PUT to rename
one, as a campaign is made with both)

| Method | Route | Purpose |
|---|---|---|
| GET / POST | `/api/factions` | The library's factions (with how many units) / create one `{ name, nation }` |
| GET / PUT / DELETE | `/api/factions/{id}` | A faction with its units / rename / delete (in use: 409) |
| POST | `/api/factions/{id}/units` | Create a library unit `{ name, type, fightingFactor, points }` |
| PUT / DELETE | `/api/units/{id}` | Edit / delete a library unit (in a campaign: 409) |
| PUT | `/api/armies/{id}` | `UpdateArmy` gains `factionIds`: the library factions it takes units from |
| POST | `/api/armies/{id}/units` | Add library units `{ unitIds }` (from the army's factions; one already in the campaign: 409) |
| PUT | `/api/admin/users/{id}/manager` | Make a user a Manager, or not `{ manager }` (Admins) |
| PUT / DELETE | `/api/army-units/{id}` | Edit the campaign's copy / remove it from the army (setup only) |
| PUT / DELETE | `/api/army-units/{id}/placement` | Replaces `/api/units/{id}/placement` |

Status changes that aren't allowed now (submitting a Submitted turn, reverting
in a closed turn) are **409**s, as is losing a race for the same change; a Move
out of range or bounds is a validation error on the position. Each change is
recorded in the army turn's history (who, when, the notes).

## 6. Open questions

None blocking. Items to revisit later:

- Offline edits that sync later; push notifications.
- More unit details, extra campaign fields.
- Letting users delete their own account.
- After Phase 8 (decision 0010): the Umpire editing closed turns, or anything
  about a unit beyond its order (decision 0011 covers orders in the open turn);
  destroyed units; intelligence sharing and scouting (grants in the visibility
  rule); per-faction visibility of armies and units; turn deadlines and
  reminders; turning emails off; the map offline (a self-hosted Protomaps
  extract); movement along roads.

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
7. ✅ **CI: `api` job**
   - `ci.yml` running on PRs and pushes to `main`: format check, build, tests.
   - `dependabot.yml` (NuGet incl. local tools, Actions). The contract check
     and `oasdiff` steps join the job in step 8.
8. ✅ **Contract pipeline**
   - Build-time `api/openapi.json` emit, with the
     `IsGeneratingOpenApiDocument` guard around startup side effects.
   - CI contract-up-to-date check; `oasdiff breaking` warning on PRs.
9. **Front-end** (§3.12), in four parts:
   1. ✅ **Design** recorded (decisions 0004 and 0005).
   2. ✅ **Scaffold & tooling:** `web/` (Vite + React + TypeScript, npm, Node 24
      pinned), ESLint + Prettier, Vitest + Testing Library, the `@/` alias,
      Husky.Net tasks for `web/`, CI `web` job, Dependabot `npm` entry,
      `web/CLAUDE.md` and the root `CLAUDE.md` commit checks. Local
      development: `scripts/dev.sh`, `docs/development.md` (including Neovim
      notes), VS Code launch/tasks and extension recommendations.
   3. ✅ **SDK & app shell:** Orval (Query hooks, Zod schemas, MSW handlers)
      with the custom fetch function, generation hooked into the npm scripts.
      Check that OpenAPI 3.1 works with Orval (switch to 3.0 if needed). The
      `minLength: 1` schema transformer for required strings.
      TanStack Router; Mantine theme (navy/silver, light/dark from the OS);
      `AppShell` with sidebar / bottom tabs; error boundary and 404 page;
      Vite dev proxy. An About page that shows the API's health through the
      generated hook, with a test.
   4. ✅ **PWA:** `vite-plugin-pwa` (manifest, app-shell service worker, update
      prompt, install hint), the app icon, the persisted query cache and
      offline banner, and the version on the About page.

**Phase 1 is done when:** a PR runs the `api` and `web` jobs in CI (format,
build, contract check, tests, lint, typecheck), Swagger UI shows the health
endpoint, the Vite app's About page shows the API's health through the
generated SDK, and the app installs as a PWA and opens offline.

### Phase 2 — Accounts

10. ✅ **Auth**
   - Identity with bearer tokens; option overrides (unique email, username
     characters, password rules); token lifetimes (30 minutes / 30 days).
   - Register, login (with lockout), refresh (with all the checks in §3.4),
     logout. Refresh token in the `HttpOnly` cookie (decision 0004).
   - `GET /api/me` (the signed-in user, with `isAdmin`), which the session
     handling needs.
   - Per-request security stamp validation; fallback auth policy.
   - Rate limiting (`auth` policy).
   - Persisted Data Protection keys; Admin sync from config at startup.
   - Test auth helpers, cheap password hashing in tests, and the "every
     endpoint declares an access rule" convention test.
   - **Screens:** register, sign in, sign out; session handling (in-memory
     access token, refresh on start-up and before expiry, offline vs signed
     out, cross-tab sign-out).
11. ✅ **Email & password reset:** `IEmailService`, MailKit SMTP + logging
    fallback, fake email service for tests, Mailpit dev compose;
    forgot/reset endpoints with the `email` rate limit. **Screens:** forgot
    and reset password.
12. ✅ **Account:** `PUT /api/me`, change email (+ notice to old address),
    change password, sign out everywhere. **Screens:** account page.
13. ✅ **Admin users:** list/search (paged, `NOCASE`), details, delete (not self).
    **Screens:** admin user list and details.

### Phase 3 — Campaigns

14. ✅ **Campaigns:** entity + membership, CRUD, paged list.
    `RequireCampaignAccess` endpoint filter + `CampaignContext`. Scenario
    builders and data-driven permission tests. **Screens:** campaign list,
    create, details, edit.
    - The filter reads the campaign from the route's `{id}` for now; army and
      unit routes (steps 17 and 18) will add their own lookups.
    - Handlers still bind `Guid id`, though they read the campaign from
      `CampaignContext`: without it, OpenAPI doesn't declare the path
      parameter (a test checks every path parameter is declared).
    - Offline restore was broken since step 9.4 and fixed here: `gcTime` was
      30 days, which overflows timers, so restored data was dropped at once.
15. ✅ **Join flow:** join codes, preview/join/regenerate, member list,
    remove Player, leave (`/members/me`). **Screens:** join page (including
    the sign-in round trip), members, join link sharing.
    - Joining returns 201 when it adds the caller, and 200 with their existing
      role when they're already a member (including the Umpire).
    - The Umpire leaving, or being removed, is a **409**: an Admin sets a new
      Umpire instead (step 16). An Admin who isn't a member gets 404 from
      `/members/me`.
    - The member list doesn't include emails; Players see each other's names
      only. The commanded army joins it in step 17.
    - `/join/:code` is a public page, signed in or not. Signed out, it offers
      sign-in and register, which return to it.
    - The test scenario's Player joins through the join link, not the database.
16. ✅ **Admin campaigns:** `/api/admin/campaigns` list and set Umpire, including
    the umpire-less campaign cases and the user-deletion test. **Screens:**
    admin campaign list, set Umpire.
    - The list searches by name and can keep only campaigns without an Umpire
      (`?withoutUmpire=true`).
    - Setting the Umpire runs in one transaction with two saves: SQLite
      checks the one-Umpire index row by row, so the old Umpire is demoted
      and saved before the new one is promoted. Choosing the current Umpire
      changes nothing; an unknown user is a validation error on `userId`.
    - Step 17 adds "a promoted Player's army becomes unassigned" to it.
    - Admin user details now list the user's campaigns and roles.
    - Screens: "All campaigns" in the admin navigation; "Set Umpire" (or
      "Change Umpire") on the campaign page, for Admins only.
17. ✅ **Armies:** CRUD, separate commander assign/unassign endpoints and rules,
    list with commanders; member list shows commanded army. **Screens:**
    army list and details, commander assignment.
    - `RequireCampaignAccess(access, CampaignRouteId.Army)` finds the campaign
      through the army. `CampaignAccess.Commander` (army routes only) lets in
      the army's commander, the Umpire and Admins; other members get 403.
    - `GET /api/armies/{id}` is Commander access already, because its units
      arrive in step 18.
    - Commander rules: not a Player in this campaign (or the Umpire) is a
      validation error on `memberId` / `commanderMemberId`; a Player who
      already commands another army is a **409** naming it. Setting a new
      Umpire unassigns the promoted Player's army.
    - The test scenario now has two Players: `Role.Commander` commands the army
      "First Corps", `Role.Player` commands nothing. Every §5.2 theory has a
      case for each.
    - Screens: an Armies section on the campaign page (every member sees every
      army; links only where the user can open it), an Army column in the
      member list, and `/campaigns/:id/armies/:armyId`.
18. ✅ **Units:** create/rename/delete; units visible only to Umpire, Admin and
    the commander. **Screens:** units within the army page.
    - Units are listed in the army's details (`GET /api/armies/{id}`, Commander
      access), so there's no separate list endpoint. Unit routes find their
      campaign through the unit's army (`CampaignRouteId.Unit`).
    - The test scenario's army has one unit, "1st Division".
    - Also in this step: token refresh got its own rate limit (§3.4), after
      three people on one network hit the sign-in limit.

### Phase 4 — Release

19. ✅ **Docker & SPA hosting:** API serves the SPA (static files, fallback,
    `/api` 404s, caching and security headers, §3.11); multi-stage
    `Dockerfile` (node → sdk → runtime), `.dockerignore`,
    a Compose file for Traefik (labels, shared network with a fixed subnet,
    no published ports; it later moved to the server's config repo), volume
    layout, health check (§3.10). The
    CSP and PWA caching rules in §3.11; the node stage copies in
    `api/openapi.json` for Orval.
20. ✅ **CD:** Docker Hub push step in CI with `latest` + `vYYYYMMdd.HHmmss` tags and
    OCI labels. The version tag is also passed into the image build, for the
    app's About page.
21. ✅ **First deploy** to the server behind Traefik, with the SMTP values and
    `ForwardedHeaders__KnownNetworks__0` (the shared network's subnet) filled
    in. Register the Admin account, then add it to `Admin:Emails` and restart.

> Steps 19–21 were done right after Phase 1 (decision 0006), so Phases 2–3
> ship to a running deployment.
>
> **Each API step in Phases 2–3 ships with its screens**, so every feature is
> usable end to end before the next begins.

### Before starting: things you'll need to set up

- .NET 10 SDK and Node.js 24 installed locally (and Docker, for Mailpit).
- Rename the GitHub repo to `wwg` (then update the local `origin` remote).
- Docker Hub: a repository for the image and an access token.
- GitHub repo: secrets `DOCKERHUB_USERNAME`, `DOCKERHUB_TOKEN` and variable
  `DOCKERHUB_IMAGE` (only needed by Phase 4).

### Phase 5 — End-to-end tests

22. ✅ **Playwright suite** (`e2e/`, §3.8, decision 0007): the production image
    behind a TLS proxy with Mailpit, Chromium and iPhone WebKit, the main
    flows of Phases 2–3, and the `e2e` CI job gating the image push.

### Phase 6 — Armies and units, fleshed out

23. ✅ **Unit details:** each unit has a **type** (seven kinds of infantry,
    cavalry and artillery), a **Fighting Factor** (1–9, "FF") and **points**
    (0–100), set when it's added and changed with a full edit
    (`PUT /api/units/{id}`, `UpdateUnit`).
    - The type and numbers are `[JsonRequired]`: a request without them is a
      400, rather than silently 0 or Heavy Infantry. A type sent as an
      undefined number is a validation error.
    - Units that existed before got FF 1, 0 points and Heavy Infantry.
    - The army page shows a table (name, type, FF, points, and the points
      total). On phones the type sits under the name.
    - Also fixed: in Development an unreadable request body was a 500, not a
      400 (§3.3).

### Phase 7 — Hardening

From a review of the app, the design and the plan on 2026-09-28. Each step fixes what the review
found in one area; product features come after it.

24. ✅ **Backups:** a SQLite snapshot before every startup migration, and scheduled snapshots with
    a retention limit, on the data volume. How to restore, and how to roll back a deploy, written
    down.
25. ✅ **Logs and health:** JSON console logs in production, one log line per request, retries
    for failed emails, and a health check that reports healthy within a second or two of start-up.
26. ✅ **Web fixes:**
    - Start-up never hangs when the refresh response isn't what it should be.
    - `/api/me` is fetched after every successful refresh.
    - Every change refreshes the data it affects: removing a Player and setting the Umpire
      refresh the armies; a deleted or left campaign is removed from the saved (offline) cache.
    - Information messages aren't announced as alerts.
    - 429 and 5xx errors show as notifications, and other errors show the API's `detail`.
    - A Player can't open the campaign edit page.
27. ✅ **API fixes:**
    - Nested settings (the rate limits) are validated at startup.
    - A wrong current password on change-email or change-password counts towards lockout.
    - Races give 409 or 404, not 500: Identity concurrency failures, rows deleted mid-request,
      and foreign-key failures.
    - A member can't end up as both the Umpire and a commander.
28. ✅ **Docs and cruft:** bring DESIGN up to date with the code, and remove what's unused.
29. ✅ **Test gaps:** 401s and the missing permission and validation cases in the API tests; the
    web's data refreshes and session failure paths; the account flows and 409 messages end to
    end.

### Phase 8 — The campaign map and turns

Decisions [0009](docs/decisions/0009-campaign-map-stack.md) and
[0010](docs/decisions/0010-turns-factions-and-visibility.md); the design is in §3.13, §5.1,
§5.2 and §5.3. Each step ships its API, screens, tests (the permission rows for every role,
including the e2e flows) and DESIGN updates, in commits under 500 lines.

30. ✅ **Factions, army colours and flags:** factions (CRUD, an army's faction, "Unassigned"),
    each army's colour (the 8-colour palette, contrast-checked in both schemes) and nation (the
    SVG flag library), the 8-army limit, and `ArmyBadge` everywhere an army is named. Every
    member now sees every army's units, read-only (the §5.2 change). The test scenario gains
    factions.
    - The migrations add `Armies.FactionId`, `Color` and `Nation` in place (`ALTER TABLE`):
      EF's usual rebuild of `Armies` would drop the commander triggers, and fails outright
      while one refers to it. The trigger SQL is now `CommanderRules`, for any migration that
      can't avoid a rebuild (§3.2).
    - Faction names are unique in a campaign, ignoring case (409). Deleting a faction leaves
      its armies Unassigned.
    - The army's `PUT` became `UpdateArmy` (all four fields, colour and nation
      `[JsonRequired]`). The member list's army carries colour and nation, for the badge.
    - The flags' designs are simplified and some are approximate (Portugal, Westphalia,
      Baden, Naples, Brunswick, Hanover and Württemberg in particular): worth a look by
      someone who knows the period.
31. ✅ **The map:** map settings (bounds, layers, label language, distance unit, movement limits)
    and their page; place search through our API (a `Geocoding` settings section: MapTiler
    with a key, or Photon); the map page on MapLibre and OpenFreeMap with our light and dark
    styles and Mapterhorn hills; the CSP changes; online only.
    - Until the Umpire saves, `GET /map` returns the defaults: no bounds, English names,
      miles, every layer but contours, and a day's march for each type (20 km for heavy
      infantry to 40 km for light cavalry).
    - Place search returns areas and settlements (MapTiler's `types`, Photon's `layer`s), 30 a
      minute per IP; a failing service is a 503. Development keeps the MapTiler key in
      user-secrets; tests blank it and stub the geocoders' HTTP (`StubHttpHandler`).
      HttpClient's request logs (which would show the key in a URL) are off in production.
    - MapLibre 6 looks for its worker beside its own module, which bundling moves:
      `maplibre-worker.ts` has Vite build it as a file and points `setWorkerUrl` at it.
    - Mantine's unselected segmented-control labels (3.2:1 in light mode) are darkened to
      4.9:1, which the e2e axe scan of the settings page caught.
32. ✅ **Setup and turn 0:** campaign and army turns, orders, the visibility rule and the
    positions endpoint; unit icons by type (drawn by the app, with a legend), stacks and the
    unit drawer; the Umpire placing units, and **Start campaign**; units and armies added later
    need placing, and can't be deleted once started.
    - Turn 0 is made the first time it's needed (placing a unit, or starting), so existing
      campaigns need nothing; an army's turn-0 row likewise. `GET /positions` without `turn` is
      where units are now: each unit's latest Completed order (so a unit placed after the start,
      into its army's latest Completed turn, counts).
    - The foreign keys from turns to armies and from orders to units are `NO ACTION`, not
      `RESTRICT`: SQLite checks them at the end of the statement, so deleting a campaign still
      cascades through its armies and turns in either order. The handlers refuse (409) deleting
      an army or unit once started; while setting up, its placements go with it.
    - `GET /api/campaigns/{id}/units` lists every unit for the map (every member sees them all).
    - Placing: the Umpire presses Place (or Move) for a unit, then clicks the map, or a unit
      or stack to put it there too. On a phone, where the list is under the map, the map
      scrolls back into view (after the banner renders, below the header).
    - Stacks are recomputed on zoom: units within 26 px are one marker, at the first one's
      position, showing how many; choosing it lists them in the drawer.
    - Notifications show three at a time (placing a dozen units piled them up); component tests
      clear them after each test, as the store is shared.
33. ✅ **Orders:** Move (with the range circle, bounds and limit checks), Hold and undo; ghost
    moves; the turn panel and Submit; the Umpire's Approve, Send back and Revert with notes;
    **Start turn N+1**; progress counts; the turn emails.
    - The commander's side: the turn panel, Move and Hold, ghosts, the range, undo, Submit.
      The Umpire's: a panel of every army's open turn (status, moves and holds, times) with
      Approve, Send back and Reopen (notes on the turn and on each unit, all optional), every
      army's orders as ghosts, and Start turn N+1 with what's holding it up.
    - The UI says "Reopen" for revert: it's what the Umpire does to an approved turn.
    - Someone who manages the campaign and also commands an army (an Admin who's a Player) gets
      the Umpire's panel, not the commander's; the API lets them give orders all the same.
    - Move: the drawer closes and the map comes into view; a tap outside the range or the area
      says why, and picks nothing; a tap inside shows the ghost and asks to confirm. Tapping a
      unit or stack moves there too.
    - `OwnCommander` access (the army's commander alone, not the Umpire or Admins) guards
      orders and submitting, found through the army turn (`CampaignRouteId.ArmyTurn`).
    - Moves are measured from the unit's current position (haversine, `Geo.Metres`), with a
      metre's grace for rounding between the browser and the API.
    - Status changes are conditional updates in a transaction with their history event, so the
      loser of a race gets a 409. Send back and revert clear the submitted and completed times.
    - Submit needs an order for every unit *on the map*: a unit added since the start and not
      yet placed doesn't hold its army up, but it does hold up the next turn.
    - Units added after the start are placed in their army's turn for the last closed campaign
      turn, not the latest Completed one, which could be the open turn's and then reverted.
    - `GET /turns` gives the Umpire what stops the next turn starting while running, as it does
      the start while setting up.
34. ✅ **History and the Umpire's overview:** stepping through an army's past turns; the Umpire's
    view of every army in its colours, highlighting one; the turn list with its counts and each
    army's status, times and actions.
    - The Turns list (newest first) is for everyone once running: the Umpire sees each turn's
      progress, a commander their army's status. Up and Right step to newer, Down and Left to
      older, moving focus with the choice.
    - A past turn shows where units were after it (`GET /positions?turn=`: each unit's latest
      order at or before it), a banner with Back to now, and each army's part: status and its
      history (who submitted, approved, sent back or reopened, when, and the notes). No ghosts
      or actions. The open turn is "now": positions and ghosts as before.
    - The Umpire's Armies list picks out one army: other stacks fade (opacity 0.3) and only its
      moves show as ghosts; choosing it again shows all alike.
    - The chosen row's detail text isn't dimmed: dimmed text fails contrast on its tint.

### Phase 9 — The Umpire edits orders

Decision [0011](docs/decisions/0011-umpire-edits-orders.md).

35. ✅ **Umpire orders (API):** `GiveOrder`, `UndoOrder` and `SubmitTurn` for the Umpire and
    Admins (`Commander` access; `OwnCommander` goes): a Draft or Submitted turn in the open
    turn, Moves inside the area but not held to the limit. `UnitOrder.ByUmpire` (and on
    `UnitPosition`); an **Edited** history event per run of changes, with a note per unit;
    Approve checks every unit on the map has an order; the approve, send-back and submit
    emails list the orders the Umpire set.
36. ✅ **Umpire orders (UI):** Move, Hold and undo in the drawer for any army's unit (a Draft or
    Submitted turn), warning before a move past the limit; **Submit for the army** in the
    review panel; "Set by the Umpire" beside such orders in the commander's panel; Edited in
    the history. End-to-end: the Umpire moves a commanderless army's unit, submits, approves.
    - An edit's note says what the Umpire did ("Set to move.", "Set to hold.", "Order taken
      back."); a run of changes by the same person is one Edited event, one note per unit.
    - The Umpire gets orders in the drawer from the map's markers (there's no unit list in the
      review panel: the markers are buttons, so keyboards reach them too). Take back is in the
      drawer, for the commander too.
    - Past the limit, the banner adds "That's past its 5 km limit." and Confirm becomes
      **Move anyway**; the range circle still shows the limit.
    - A Draft's row in the review panel says who's giving orders and how many so far, with
      **Submit for it**; the API refuses (409, naming the units) while any unit on the map has
      no order.

### Phase 10 — Admins masquerade as other users

Decision [0012](docs/decisions/0012-admin-masquerade.md).

37. ✅ **Masquerade (API):** `POST /api/admin/users/{id}/masquerade` and
    `POST /api/auth/masquerade/end`; the masquerade claims, capped token lifetimes and the
    checks on refresh; `masquerade` on `GET /api/me`; log lines for start and end.
38. ✅ **Masquerade (UI):** **Masquerade as** on an Admin's user page (with confirmation); the
    session switches, clearing the cache (and other tabs'); the account button in another colour
    naming who you are, with **End masquerade**. End-to-end: an Admin masquerades as a Player,
    sees only what they see, and ends it.
    - The session store's `switchUser` clears the cache and the saved copy, then signs in with
      the new tokens, and tells other tabs ("switched"), which clear theirs and start again.
    - While masquerading the account button is gold (`--app-masquerade`, contrast in
      theme.ts), reads "Bob (masquerade)", and its menu says who and until when, above **End
      masquerade**. Starting lands on the user's campaigns; ending on the Admin's user list.
    - A saved last user from before masquerades is read as not one (`masquerade: null`), so an
      offline start still works after the upgrade.

### Phase 11 — The hex grid and the rules' campaign movement

Decision [0014](docs/decisions/0014-hex-grid-movement.md); the rules are the club's rule book
(`docs/wwc-rules.pdf`), section XII (Campaign). Battles stay off the app. The order follows the
dependencies: the grid, then what's on it, then how units move across it, then the rules that
build on positions.

39. ✅ **The hex grid (API):** the hex size on the map settings (3 miles by default; with the
    bounds, changeable only while setting up); `HexGrid` (axial, flat-topped, local projection);
    unit types by the rules' movement classes (migration: HeavyInfantry → LineInfantry,
    Skirmishers → LightInfantry); orders and placements in hexes (`Q`, `R`, `Path`), existing
    positions converted once at start-up; a Move's path checked against the rules' flat rates
    (every hex Flat until step 42); the movement limits go. `GET /positions` gives each unit's
    hex and its centre.
    - Done with the web side it needs (the contract changed under it): placing by hex, and
      moving by hex with the reachable hexes shaded and the cheapest path drawn (`movement.ts`
      mirrors `Movement.cs`), orders described in hexes ("Moves 2 hexes"). Step 40 adds the grid
      layer on the map page and the rest.
    - The migration converts positions in SQL (SQLite's math functions), checked against
      `testdata/hex_grid.py` both ways; moves from before the grid keep no path, and draw as a
      straight line. `HexGrid.cs` is tested through the endpoints against the same figures
      (placement centres, and re-snapping placements when the grid changes during setup).
    - A path is checked step by step (next to the last, inside the grid) before its cost.
40. ✅ **The hex grid (UI):** the grid layer (on by default, switchable) and the hex size in the
    settings, previewed; placing and moving by hex; the hexes a unit can reach shaded, the
    cheapest path to the chosen one drawn, and its cost; stacks by hex; symbols and legend for
    the new types. End-to-end: set up with the grid, move a unit two hexes.
    - Placing and moving by hex, the reachable hexes and the path came with step 39. The grid is a
      map layer like the others (`MapLayers.Grid`, on by default and for existing maps), drawn
      under the units on the map page; the settings preview draws the size being chosen.
41. **Factions and units for every campaign** (decision
    [0015](docs/decisions/0015-global-factions-and-units.md)); four commits:
    - ✅ **41a. Sides:** Phase 8's campaign factions become sides: `Faction` → `Side`,
      `Army.FactionId` → `SideId` (renamed in place: `ALTER TABLE … RENAME`, no rebuild of
      `Armies`, whose triggers stay), `/api/campaigns/{id}/sides` and `/api/sides/{id}`, the
      campaign page's **Sides** section, "Put X on a side". No change in behaviour.
    - ✅ **41b. Army units:** the campaign's `Unit` becomes `ArmyUnit` (the `Units` table
      renamed in place, so orders and notes keep their foreign keys), its routes
      `/api/army-units/{id}` and `/api/army-units/{id}/placement`. No change in behaviour; it
      frees `Unit` for the library.
    - ✅ **41c. The library and Managers:** global `Faction` (name, nation) and `Unit` (faction,
      name, type, FF, points); `/api/factions`, `/api/factions/{id}`, `/api/factions/{id}/units`,
      `/api/units/{id}`. The `Manager` role (`isManager` on `GET /api/me`, read from the database
      like the library policy, so a change applies at once; the Admin's user page grants it,
      `PUT /api/admin/users/{id}/manager`); Managers and Admins edit, and a faction with units
      can't be deleted (409; a unit in a campaign, from 41d). The **Library** pages (`/library`,
      `/library/{id}`; every signed-in user; in the navigation): factions, each with its units,
      and for Managers and Admins create, edit and delete.
    - ✅ **41d. Army factions, and units from the library:** `ArmyFaction` (an army's selected
      factions, in **New army** and **Edit army**; `factionIds` on `CreateArmy` and `UpdateArmy`,
      where null leaves them as they are; `ArmyResponse.factions`); `ArmyUnit` gains `UnitId` (→
      the library unit) and `CampaignId`, unique together (`ArmyUnitResponse` carries `unitId`
      and its `factionId`). The migration puts a copy of each army unit into the library in a
      faction per army nation ("Unsorted" for none; the library unit takes the army unit's ID),
      links them, and selects that faction for the army; `ArmyUnits` is rebuilt for the foreign
      keys (it has no triggers).
      `POST /api/armies/{id}/units { unitIds }` adds library units from the army's factions (409
      for one from another faction, or already in the campaign); `PUT` / `DELETE
      /api/army-units/{id}` edit the campaign's copy and remove it (setup only). Orders,
      positions, notes and placement use army-unit IDs (`/api/army-units/{id}/placement`). The
      army page's **Add units** lists the units of the army's factions (those already in the
      campaign say which army has them). End-to-end: a Manager builds the library, an Umpire
      sets up two campaigns from it.
42. ✅ **Terrain (API):** `HexCell` and `HexEdge`, `GET` / `PUT /grid` and the single-hex and
    single-edge edits, keeping what the Umpire set when inference runs again. Only hexes and
    edges with something on them are stored (at most 60,000 hexes and 180,000 edges a save);
    an edge is stored on one hex's N, NE or SE side, and on the grid's border when either of its
    hexes is in the grid. A bridge needs a river. Changing the area or hex size while setting
    up clears the terrain, the Umpire's too (it belonged to the old hexes).
    - ✅ **42b. Settlements in parts, and waterways** (decision 0016): `HexCell`'s settlement
      becomes a size, Walled, Fortress, a capital status and a name, sent together as a
      `HexSettlement` (walled and capital need a town or city; a name, a town, city or fortress);
      `HexEdge` gains `Waterway` (a navigable course across the edge, and which way it flows). The
      migration maps an old single settlement onto the parts (and back).
43. **Terrain (UI)**, in three parts:
    - ✅ **43a. The terrain layer and the Umpire's editor:** every member's map draws the stored
      terrain with the grid (ground and forest as tints; rivers along hexsides, roads and
      waterways from hex centre to hex centre, bridges; towns, cities and fortresses at the
      centres, with their names). The Umpire's **Terrain** page (`/campaigns/{id}/map/terrain`,
      from the Map page): choose a hex on the map, set its ground, forest and settlement, and any
      of its six sides (clicking near a side chooses it; S, SW and NW are saved as the
      neighbours' N, NE and SE, with a waterway's flow turned round). End-to-end: set a hex and
      an edge, and find them again.
    - ✅ **43b. Inference** in the Umpire's browser from the tiles the map uses (fetched and
      decoded by `inference/tiles.ts`, vector tiles about five hexes across and heights at about
      25 pixels to a hex, at most 400 of each; `sources.ts` and `infer.ts` are plain arithmetic,
      tested alone; each hex is judged on 19 sample points): relief from
      Mapterhorn elevation (the rise within a hex, roughly: under 50 m flat, under 150 m low
      hills, under 400 m high hills, else mountains), forest from land cover (half the hex or
      more), water, cities and towns (with their names; capitals from the place data), roads
      (trunk and primary good, secondary poor) crossing each edge, and rivers: a waterway where
      a navigable river's line crosses an edge (flowing the way the line runs), and a river
      along the edges nearest its line (snapped to the nearest hex corners and joined along the
      sides between them, so it's unbroken; a road crossing one makes a bridge). Villages and
      streams are left to the hex's detail (43c). On the Terrain page, **Infer terrain** saves
      it for the whole grid, keeping what the Umpire set (asking first when it replaces an earlier
      inference). End-to-end runs it on empty tiles (the flow, not the data).
    - ✅ **43c. A hex's actual terrain** (decision 0016): `HexDetail` and its reveals; the Umpire
      rolls for a hex on the Terrain page (the app shakes the rules' three dice, with the red
      die's modifier from the map terrain), adjusts it, and shows it to armies or to all; members
      see what's been shown to them in the Map page's **Hex details** list. End-to-end: roll,
      reveal to an army, a commander sees it.
44. **Costed movement**, in four parts:
    - ✅ **44a. The movement table and costed steps (API):** `MovementRate` (a campaign's own
      rates, where they differ from the rule book's; `GET` / `PUT` / `DELETE
      /api/campaigns/{id}/movement`, every class on every ground, in halves, 0 for "can't").
      A step costs 1 ÷ the rate for the hex entered (a forest hex moves as low hills, or as its
      own hills if higher), or for the road across the edge when that's quicker (a road never
      slows a unit; a good road into high hills or mountains counts as poor, and a road opens
      mountains). Water, a river along the edge without a bridge, and a 0 in the table close the
      step; the Umpire may take it anyway (decision 0011). `Movement.cs` and `movement.ts` are
      tested against `testdata/movement.json`.
    - ✅ **44b. Costed movement in the browser:** `movement.ts` costs steps the same way from the
      grid's terrain and the table, so the reachable hexes and paths match the API (the Umpire's
      unlimited reach crosses closed steps only as a last resort, and says so); the Map settings
      page edits the table (and puts it back to the rules').
    - ✅ **44c. Hexes that take more than a turn:** `UnitOrder.Progress` (0–1), part of the way
      into the path's last hex when it costs more than a whole turn (only such a hex is entered
      part-way); the unit stays in the hex before it until it's through, carrying its progress to
      the next turn's order if that goes on into the same hex, and losing it otherwise (a Hold, or
      a move elsewhere). Positions carry `progress` too; the map offers such a hex as "half of the
      way into here", and the unit's panel says how far it got. The Umpire's moves always arrive.
    - ✅ **44d. Boats:** a Boat unit type (and movement class) that follows waterways: 4 hexes
      downstream, 2 upstream by the waterway's flow across the edge, 3 across a lake (from one
      Water hex to the next); nowhere else. The table gains boats' three grounds (land classes
      stay on the six land ones: 33 cells), and the Map settings page edits them apart.
45. **Time of day**, in two parts:
    - ✅ **45a. The calendar (API):** `Campaign.StartDate` (optional), `FirstTurnPart` (Morning,
      Afternoon or Night; each turn after is the next, three to a day, a Night belonging to the
      day it starts), and the nations whose infantry (the Infantry class) move a flat hex's worth
      further each Morning (the rules': France, Bavaria, Württemberg, Baden, the Duchy of
      Warsaw, the Kingdom of Italy and Holland) or less each Afternoon (Russia and Austria), the
      rules' until the Umpire changes them (`GET` / `PUT /api/campaigns/{id}/calendar`). Turns
      carry their `part` and `date`; a unit marches as its library faction's nation, or its
      army's when the faction has none (`ArmyUnitResponse.nation`). A move in a Night turn is a
      night move (the turn says so), for forced marches later.
    - ✅ **45b. The calendar in the app:** the Umpire sets it on the campaign's edit page
      (**Calendar**); every turn is labelled with its day and time of day ("17 June 1815,
      Afternoon", under its "Turn 7" heading and in the turn list); the map's reach uses the
      unit's march; in a Night turn the Turn panel says moving counts towards a forced march,
      and its moves read "by night".
46. **Contact and concentration** (decision 0017), in parts:
    - ✅ **46a. Two sides:** every campaign has exactly two sides, made with it ("Side 1" and
      "Side 2", renamable; no adding or deleting), and every army is on one (`Army.SideId`
      required; the army forms choose it). The migration keeps a campaign's first two sides by
      name, merges any others into the second, makes any missing, and puts armies on none on the
      first.
    - ✅ **46b. Concentration settings:** on the campaign's edit page (**Concentration**), the
      unit types that count towards the infantry limit and those towards the cavalry limit (the
      rest are free; a type counts towards one at most), and the two limits (200 and 160 by
      default, 1 to 10,000). `Campaign.InfantryLimitTypes` and `CavalryLimitTypes` (null: the
      usual types, `Concentration` in the API, `concentration.ts` in the app), `InfantryLimit`
      and `CavalryLimit`; `GET` (every member) / `PUT` (the Umpire)
      `/api/campaigns/{id}/concentration`.
    - ✅ **46c. Contact and concentration:** for the Umpire, the hexes holding units of both sides
      (contact), and each side's hexes over a limit (its counted points of those types; doubled in
      a City or a fortress, not a walled town); in the open turn from the orders as given (every
      army's, whatever their status), on past turns from where the units ended up. Outlined on the
      map in the fortress red, and listed in words in the turn panel ("Hex (1, 0): Contact:
      Coalition and French Empire."), as warnings. Worked out in the app (`contact.ts`) from what
      the Umpire's map already loads; commanders aren't shown them.
    - **46d. Battle records (later):** the Umpire records a battle: its hex and turn, the units
      involved, and their points afterwards (which the units then have). Concentration within
      three turns of a battle in that hex is then allowed, as the rules say.
    - Later, for consideration: units whose paths cross during a turn (meeting mid-move).
47. **Forced marches and attrition** (decision 0018), in parts:
    - ✅ **47a. Marches (API):** `UnitOrder.ForceMarch` (`forceMarch` on `GiveOrder`: a flat hex's
      worth of the unit's class further, in a Morning or an Afternoon turn only; 400 by night or
      for a class with no flat rate); each unit's march count worked out from its orders turn by
      turn (`Marches`, `MarchState`: moves in a row, the first forced march at the third move or
      the second force-march order, each Hold working one turn off), and what moving this turn
      would cost (`GET /api/armies/{id}/marches`: its commander, the Umpire, Admins).
    - ✅ **47b. Attrition (API):** the attrition the open turn's orders cost (FF scale × ×1, ×2,
      ×4… × points ÷ 50, `ArmyUnit.AttritionCarry` holding the fraction; `Attrition`), for the
      Umpire (`GET /api/campaigns/{id}/attrition`); `StartNextTurn` takes `{ attrition: [{ unitId,
      points }] }`, a loss for each unit that owes one and no other (400 otherwise), and applies
      it. Each unit's points history (`PointsChange`: attrition, and the Umpire's edits once the
      campaign has started), for every member (`GET /api/army-units/{id}/points`).
    - ✅ **47c. In the app:** "Force march (a hex further)" when moving, by day (the reach goes a
      flat hex further; the order reads "by force march"), with what the move costs; each unit's
      march count in its drawer (its commander and the Umpire) and its points history (everyone);
      starting the next turn lists the attrition due, each loss editable, and sends what the
      Umpire confirms (`StartTurnModal`).
48. **Supply** (decision 0019), in parts:
    - ✅ **48a. Depots:** each army's depots (`Depot`: main or intermediate, a name, a hex), which
      the Umpire places, moves, captures and destroys (`POST /api/armies/{id}/depots`, `PUT` /
      `DELETE /api/depots/{id}`; on the map page's **Depots** panel, placed by clicking the map);
      `GET /api/campaigns/{id}/depots` gives the viewer's (the Umpire's, all; a commander's, their
      army's). Drawn on the map; the Umpire is warned, with contact and concentration, of the other
      side's units in a depot's hex. An army with no depots has its supply untracked.
    - ✅ **48b. Supply settings and living off the land:** on the campaign's edit page (**Supply**;
      `GET` / `PUT /api/campaigns/{id}/supply-settings`), the supply reach (0–3 hexes, 1 by
      default), the exempt unit types, and the nations that may live off the land (France by
      default); `UnitOrder.LivesOffTheLand` (`livesOffTheLand` on `GiveOrder`, 400 for a nation
      that may not), set with a unit's order (the drawer's **Living off the land** switch keeps the
      order and changes only that; each new order keeps the unit's last); a side's hex with a unit
      living off the land held to half the concentration limits.
    - ✅ **48c. Supply (API):** each unit's supply (`SupplyLines`, `SupplyData`): routes by road and
      waterway from its army's depots, cut by 5+ enemy points unless its side has twice as many
      there, within the reach; intermediate depots by their own route to a main one, and for 15
      turns after (`Depot.CutOffTurns`); exempt, living off the land, or untracked (an army with no
      depots). Counted as each turn closes (`ArmyUnit.UnsuppliedTurns`: turns closing reopen
      nothing, so it's stored, not replayed). `GET /api/campaigns/{id}/supply`: each unit's as the
      open turn began and by its orders as given, for its commander and the Umpire.
    - ✅ **48d. Supply's attrition, in the app:** from the 7th unsupplied turn, normal attrition, and
      a forced march's doubled while out of supply, in the list the Umpire confirms (the attrition
      list's `forcedMarchMultiplier` and `unsuppliedTurns`; the history's note says which); a red
      mark on units out of supply on the map, their supply in the unit drawer, and the turn panel's
      **Supply** warnings (units the orders leave out of supply, intermediate depots cut off), for
      the army's commander and the Umpire.
49. **Sightings and intelligence** (decision 0020), in parts:
    - ✅ **49a. Sight (API):** what each army's units can see where a turn leaves them, by the map's
      terrain in elevation steps (flat 1, low hill 2, high hill 3, mountain 4; hills block the view
      beyond unless the observer stands higher): the other side's hexes in sight, per observing
      army, with possible screens flagged (`Sight`; `GET /api/campaigns/{id}/sightings/due`, the
      Umpire's, where the open turn's orders as given leave the units; `Whereabouts` loads them, for
      supply too).
    - ✅ **49b. Sightings:** starting the next turn lists each army's sightings (one per enemy hex),
      prefilled (the hex or roughly where, army and nation, unit types, exact or rough strength);
      the Umpire changes, skips or adds them. Each belongs to its turn and is kept: drawn in full on
      it, faded for the 3 turns after, and again when that turn is chosen in the turn history,
      whose entries mark the turns with sightings (`Sighting`, what was seen copied in; the start-turn
      request's `sightings`; `GET /api/campaigns/{id}/sightings`, the hex left out where it wasn't
      told; in the app `SightingsFields`, `SightingMarkers`, `SightingsPanel`, `sightings.ts`).
    - ✅ **49c. Couriers (API):** a commander sends an ally a report (their units' snapshot, all the
      sightings they've received, a note ≤ 1,000 characters; once it arrives, those sightings are
      the ally's too, on their turns); its courier rides as light cavalry
      from the sender's nearest unit towards the recipient's, turn by turn (within two turns'
      ride, next turn); the Umpire sees couriers, is warned of enemy in their hex, and can stop one
      (`IntelReport`, its content copied in; `CourierRides`, a cheapest-path ride by the movement
      table; `Couriers.RideAsync` as each turn closes; `POST /api/armies/{id}/reports`, `GET
      /api/campaigns/{id}/reports` (a sender isn't told whether it arrived), `GET
      /api/campaigns/{id}/couriers` and `POST /api/reports/{id}/stop` for the Umpire).
    - ✅ **49d. Intelligence in the app:** sending a report (`SendReportModal`: to an ally, our
      units, our sightings, a note), the **Intelligence** panel of reports received (each one's
      units shown on the map on request, faded: `SnapshotMarkers`) and sent, and the Umpire's
      **Couriers** panel (where each is, flagged among the enemy, stopping one).
50. **Towns and victory points** (decision 0021), in parts:
    - ✅ **50a. Values and holders (API):** each settlement's value (the highest of town 10, city 25,
      walled 35, fortress 50, plus 25 or 10 for a capital or minor capital; the Umpire's own where
      set) and its holder (an army, or no one; the Umpire sets who starts with it); as each turn
      closes, an army alone in the hex takes it, with the change kept per turn (`HexSettlement.Value`
      and its `VictoryPoints` override; `Holding`, `HoldingChange`; `PUT
      /api/campaigns/{id}/holdings/{q}/{r}`; `Holdings.CloseTurnAsync`).
    - ✅ **50b. The scoreboard (API):** each side's total for every member, with its armies' parts;
      the settlements and their holders for their own side and the Umpire; the history of totals
      per turn, and the changes a side made or suffered (`GET /api/campaigns/{id}/scoreboard`,
      `Scoreboard`; sides by name, as the sides list).
    - ✅ **50c. In the app:** the value and starting holder in the terrain editor; the scoreboard on
      the campaign and map pages, with its history; a flag in the holder's colour by each
      settlement on the map, and the holder and value in the hex card (`victory.ts`, mirroring the
      API's value; `HolderField`, `ScoreboardPanel`, `HoldingFlags`).
    - ✅ **50d. Which settlements count:** a campaign setting, on the edit page: every settlement
      by the rules (the default), or only those the Umpire gives points, the rest worth nothing
      (`Campaign.VictoryPoints`, `VictoryPointsMode`, `HexSettlement.ValueIn`; `GET` / `PUT
      /api/campaigns/{id}/victory-settings`; `VictorySection`).
51. **Engineering and sieges:** orders that take turns (destroy, repair or build bridges and
    pontoons; boats; earthworks), and the siege clock. Mostly the Umpire's bookkeeping.
