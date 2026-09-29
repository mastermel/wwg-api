# wwg — Design & Implementation Plan

> **Status:** Phases 1–6 (steps 1–23) are done. Phase 7, hardening (steps 24–29), is in
> progress.
> **Last updated:** 2026-09-28
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
- Extra campaign fields (dates, game system, status…). Unit details beyond name, type, FF and
  points.
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
| Front-end | **WWG Campaigner**: React + TypeScript SPA, **npm**, Vite, **Mantine**, TanStack Router + Query, React Hook Form + Zod; installable **PWA**, read-only offline (§3.12) |
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
    every container restart would log everyone out.
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
    `{id}`: the campaign's own, or an army's (or later a unit's) with one
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
- Covered: sign-up, staying signed in, sign-out, the sign-in redirect,
  password reset by email; campaigns (create, edit, delete, offline); join
  links (the signed-out round trip, a new link, leave, remove); admin (an
  Umpire's account deleted, a new Umpire set; admin screens hidden from
  others); armies and units and who sees them; the image's hosting (security
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
  1. **`node` stage:** `npm ci` + `npm run build` in `web/` → `web/dist/`.
  2. **`sdk` stage:** `dotnet publish` the API.
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
  the shared network's subnet (e.g.
  `ForwardedHeaders__KnownNetworks__0=172.20.0.0/16`), not Traefik's IP,
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
- `docker-compose.yml` for running it on the server, with the Traefik labels,
  the shared network and the `/data` volume. Updating means pulling the new
  `latest` (or a specific `vYYYYMMdd.HHmmss`) and restarting.
- Production configuration comes from environment variables:
  `ConnectionStrings__Default` (defaults to `Data Source=/data/wwg.db`),
  `Database__MigrateOnStartup` (default `true`), `Auth__DataProtectionKeysPath`
  (defaults to `/data/keys`), `Smtp__*`, `Admin__Emails__0…`,
  `App__PublicUrl`, `ForwardedHeaders__*`, `RateLimits__Auth__*` (defaults
  to 10 per minute), `RateLimits__Refresh__*` (120 per minute), `Backup__*` (`Path` defaults
  to `/data/backups`).

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

**WWG Campaigner**, a React + TypeScript single-page app, installable as a
PWA (decision [0005](docs/decisions/0005-front-end-stack.md)).

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
- App icon and main image: a clean, monochrome **silhouette of Napoleon
  Bonaparte on a rearing war horse** (SVG, rendered to the PNG sizes the
  manifest needs, including maskable and Apple touch icons).
- Accessibility target **WCAG 2.1 AA**: Mantine's accessible components,
  `jsx-a11y` linting, and keyboard/screen-reader checks on key flows.
- English only, no i18n library. Dates and times use `Intl` in the user's
  locale and time zone.
- Browsers: the last two versions of **Chrome, Safari and iOS Safari**
  (`browserslist`, which also sets Vite's build target). Other modern
  browsers (Firefox, Edge) should work but aren't tested.
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
  client), Zod schemas, and MSW mock handlers for tests.
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
- Manifest: name **WWG Campaigner**, short name **WWG**, navy theme colour,
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
- Vitest + React Testing Library + MSW (Orval-generated handlers) for key
  screens and logic: session handling, forms, and role-dependent UI. Not
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

**To check during the scaffold**
- ✅ Orval with OpenAPI 3.1: checked in step 9.3. Nullable type arrays become
  `string | null` (Zod `.nullable()`), `minLength` becomes `.min(1)`, and
  enums without `"type": "string"` become string unions. The document stays
  on 3.1.
- Mantine injects its CSS variables in a `<style>` tag, so the CSP (§3.11)
  needs a style nonce or `'unsafe-inline'` for styles. Scripts stay strict.
- Whether Chrome accepts the `__Secure-` cookie prefix from
  `http://localhost` through the Vite proxy (checked in step 10). If not,
  development drops the prefix.

## 4. Cross-cutting concerns

| Concern | Approach |
|---|---|
| Logging | Built-in `ILogger`. **JSON console logs** in production (`Logging:Console:FormatterName`, UTC timestamps); plain text in development. **One line per API request** (`HttpLogging`: method, path, status, duration; never headers, bodies or query strings); static files and `/health` aren't logged. EF Core's SQL is only logged in development |
| Configuration | `appsettings.{Environment}.json` + env vars; user-secrets in dev. **Every settings section** (`App`, `Smtp`, `Admin`, `Auth`, `Backup`, `RateLimits`, `ForwardedHeaders`) is a typed options class with DataAnnotations, `ValidateDataAnnotations()` and `ValidateOnStart()`, so bad config fails at startup with a clear message. Nested objects (each rate limit) need `[ValidateObjectMembers]`, or they aren't checked; lists are checked in `Validate` (`IValidatableObject`), e.g. that every `Admin:Emails` entry is an email. `App:PublicUrl` (the app's public URL, e.g. `https://wwg.example.com`) is required outside development |
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
  Id              Guid
  ArmyId          → Army
  Name            string (required, ≤100)
  Type            HeavyInfantry | LightInfantry | Skirmishers | HeavyCavalry |
                  LightCavalry | FootArtillery | HorseArtillery
  FightingFactor  int 1–9 ("FF" in the app)
  Points          int 0–100
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
| POST | `/api/auth/register` | Sign up (email, password, first, last) → access token + refresh cookie |
| POST | `/api/auth/login` | Email + password → access token + refresh cookie |
| POST | `/api/auth/refresh` | Refresh cookie → new access token + refresh cookie |
| POST | `/api/auth/logout` | Expire the refresh cookie |
| POST | `/api/auth/forgot-password` | Send reset email (always 204) |
| POST | `/api/auth/reset-password` | Email + code + new password |

**Account** (signed in)

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/me` | Current user: id, email, names, `isAdmin` |
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

## 6. Open questions

None blocking. Items to revisit later:

- Offline edits that sync later; push notifications.
- More unit details, extra campaign fields.
- Letting users delete their own account.

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
    - Also in this step: token refresh got its own rate limit (§3.5), after
      three people on one network hit the sign-in limit.

### Phase 4 — Release

19. ✅ **Docker & SPA hosting:** API serves the SPA (static files, fallback,
    `/api` 404s, caching and security headers, §3.11); multi-stage
    `Dockerfile` (node → sdk → runtime), `.dockerignore`,
    `docker-compose.yml` for Traefik (labels, shared network with a fixed
    subnet, no published ports), volume layout, health check (§3.10). The
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
27. **API fixes:**
    - Nested settings (the rate limits) are validated at startup.
    - A wrong current password on change-email or change-password counts towards lockout.
    - Races give 409 or 404, not 500: Identity concurrency failures, rows deleted mid-request,
      and foreign-key failures.
    - A member can't end up as both the Umpire and a commander.
28. **Docs and cruft:** bring DESIGN up to date with the code, and remove what's unused.
29. **Test gaps:** 401s and the missing permission and validation cases in the API tests; the
    web's data refreshes and session failure paths; the account flows and 409 messages end to
    end.
