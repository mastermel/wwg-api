# Local development

Everything here runs from a terminal; no particular editor is needed. Commands are run from the
repo root unless they say otherwise.

## Prerequisites

- **.NET SDK 10.** The exact version is pinned in `global.json` (newer feature bands are allowed).
- **Node.js 24** with npm. The version is pinned in `web/.nvmrc`; nvm, fnm and asdf (with
  `legacy_version_file = yes` in `~/.asdfrc`) all pick it up.
- **Docker** (optional), for Mailpit: a local mail catcher for the emails the API sends.

## First-time setup

```sh
git clone git@github.com:mastermel/wwg.git && cd wwg
dotnet tool restore           # CSharpier, Husky.Net, dotnet-ef (versions in .config/dotnet-tools.json)
dotnet build api/Wwg.slnx     # also installs the git pre-commit hook
npm ci --prefix web           # front-end dependencies
```

The first build installs the pre-commit hook (Husky.Net). Set `HUSKY=0` before building to skip
that.

## Running the app

```sh
scripts/dev.sh
```

This starts both halves and stops both on Ctrl+C (or when either one exits). With Docker
available it also starts Mailpit (`docker-compose.dev.yml`), which keeps running in the
background:

| What | URL | Started by |
|---|---|---|
| Web app (Vite, hot reload) | http://localhost:5173 | `npm run dev` in `web/` |
| API (`dotnet watch`, reloads on change) | http://localhost:5102 | `dotnet watch --project api/src/Wwg.Api` |
| Swagger UI | http://localhost:5102/swagger (or via :5173) | the API |
| OpenAPI document | http://localhost:5102/openapi/v1.json | the API |
| Mailpit inbox (emails the API sent) | http://localhost:8025 | `docker compose -f docker-compose.dev.yml up -d` |

Use the app through **:5173**. Vite forwards `/api`, `/openapi`, `/swagger` and `/health` to the
API, so the browser only ever talks to one origin (like production, where the API serves the
built app).

To run the halves separately, in two terminals:

```sh
dotnet watch --project api/src/Wwg.Api     # or: dotnet run --project api/src/Wwg.Api
npm run dev --prefix web
```

### Trying the PWA

The service worker (offline shell, update prompt) only runs in a production build, not under
`npm run dev`:

```sh
npm run build --prefix web && npm run preview --prefix web   # http://localhost:4173
```

`APP_VERSION=v-test npm run build --prefix web` sets the version shown on the About page; building
again with a different value while the preview is open triggers the update prompt. To start
clean, remove the service worker and site data in the browser's dev tools (Application tab in
Chrome).

### Email

In development the API sends email through Mailpit on `localhost:1025` (`Smtp` in
`appsettings.Development.json`); open http://localhost:8025 to read password-reset emails and
follow their links. Without Mailpit running, sending fails and the API logs the error. Stop it
with `docker compose -f docker-compose.dev.yml down`.

With no `Smtp:Host` at all (e.g. production before SMTP is set up), emails are written to the
API's log instead of sent.

### Development database

The API uses SQLite at `api/src/Wwg.Api/wwg.db` (git-ignored), created and migrated on start-up.
To start over, stop the API and delete `wwg.db*`.

After changing an entity or its configuration, add a migration:

```sh
dotnet ef migrations add <Name> --project api/src/Wwg.Api --output-dir Data/Migrations
```

A test fails if the model has changes with no migration.

## Checks

The pre-commit hook formats what you commit: CSharpier for C#, and `eslint --fix` plus Prettier
for staged `web/` and `e2e/` files. It doesn't build or test. Run these before committing (CI runs the same):

```sh
# API
dotnet csharpier check .                  # formatting
dotnet test --solution api/Wwg.slnx       # builds (warnings are errors) and runs all tests

# Web (from web/)
npm run lint                              # ESLint, type-aware
npm run format:check                      # Prettier
npm run typecheck                         # tsc
npm test                                  # Vitest (npm run test:watch while working)
npm run build                             # production build
```

Auto-fix: `dotnet csharpier format .`, and in `web/` `npm run lint:fix` and `npm run format`.

## End-to-end tests

`e2e/` drives the production image in real browsers with Playwright: desktop Chromium and
Safari's engine (WebKit) on an iPhone profile. CI runs it on every PR; run it yourself after
changing a user-facing flow. From `e2e/`, once: `npm ci`.

```sh
./stack.sh up           # build the image, start it behind a TLS proxy with Mailpit (fresh data)
npm run test:docker     # the suite, in Microsoft's Playwright image (no browser setup needed)
npm test                # or on this machine, once browsers are installed (below)
npm run report          # the HTML report of the last run (failures have traces)
./stack.sh down         # stop, and throw the data away
```

- The app is at **https://localhost:8443** (accept the self-signed certificate), and its emails
  at http://localhost:8025. `./stack.sh logs` shows the app's logs.
- Leave the stack up while working on tests: every test signs up its own users, so runs don't
  interfere. Run `./stack.sh up` again after changing the app, to rebuild the image.
- One file, one test, one browser: `npm run test:docker -- tests/join.spec.ts -g "leaves"
  --project iphone`. `npx playwright test --ui` (on this machine) is the interactive runner.
- **Running on this machine** needs the browsers and their system libraries:
  `npx playwright install chromium webkit`, then `sudo npx playwright install-deps chromium webkit`
  once. `test:docker` avoids both.
- Checks for `e2e/` itself: `npm run lint`, `npm run format:check`, `npm run typecheck`.

**The API contract.** Every API build rewrites `api/openapi.json` from the code. Commit it along
with the change that caused it; CI fails if it's out of date, and PRs get a warning for breaking
API changes.

## Editors

The repo's settings are editor-neutral: `.editorconfig` for whitespace and C# style, CSharpier and
Prettier for formatting, ESLint for linting. Point your editor at the repo's own tool versions
(`dotnet csharpier`, `web/node_modules/.bin/…`) rather than global installs, so results match the
hook and CI.

### Neovim

- **C#:** a Roslyn-based language server (e.g. `roslyn.nvim`, the server VS Code's C# Dev Kit
  uses, or OmniSharp). It picks up `.editorconfig`, the analyzers and nullable warnings from the
  build.
- **TypeScript:** `vtsls` or `ts_ls`, using the project's TypeScript in `web/node_modules`.
- **ESLint:** the `eslint` language server (from `vscode-langservers-extracted`) for diagnostics
  and fix-all.
- **Formatting on save:** a formatter plugin such as `conform.nvim`, running CSharpier through
  `dotnet csharpier` for `.cs` files and `web/node_modules/.bin/prettier` for `web/` files.
- `.editorconfig` is supported natively (Neovim 0.9+).
- Run the app with `scripts/dev.sh` in a terminal split or a separate terminal.

### VS Code (optional)

Recommended extensions are listed in `.vscode/extensions.json` (C# Dev Kit, CSharpier, ESLint,
Prettier). `.vscode/launch.json` has an **API + web** compound launch (API under the debugger, web
app in Chrome), and `.vscode/tasks.json` wraps the commands above. Nothing depends on them.

## Troubleshooting

- **The commit hook says `web/node_modules` is missing:** run `npm ci --prefix web`.
- **Port 5102 or 5173 is in use:** an earlier run may still be going; stop it, or find it with
  `lsof -i :5102`.
- **Safari and cookies on localhost:** once sign-in exists (step 10), use Chrome for local
  development. Safari may refuse `Secure` cookies over plain `http://localhost`.
