# Local development

Everything here runs from a terminal; no particular editor is needed. Commands are run from the
repo root unless they say otherwise.

## Prerequisites

- **.NET SDK 10.** The exact version is pinned in `global.json` (newer feature bands are allowed).
- **Node.js 24** with npm. The version is pinned in `web/.nvmrc`; nvm, fnm and asdf (with
  `legacy_version_file = yes` in `~/.asdfrc`) all pick it up.
- **Docker**, later, for Mailpit (a local mail catcher for password-reset emails, step 11).

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

This starts both halves and stops both on Ctrl+C (or when either one exits):

| What | URL | Started by |
|---|---|---|
| Web app (Vite, hot reload) | http://localhost:5173 | `npm run dev` in `web/` |
| API (`dotnet watch`, reloads on change) | http://localhost:5102 | `dotnet watch --project api/src/Wwg.Api` |
| Swagger UI | http://localhost:5102/swagger (or via :5173) | the API |
| OpenAPI document | http://localhost:5102/openapi/v1.json | the API |

Use the app through **:5173**. Vite forwards `/api`, `/openapi`, `/swagger` and `/health` to the
API, so the browser only ever talks to one origin (like production, where the API serves the
built app).

To run the halves separately, in two terminals:

```sh
dotnet watch --project api/src/Wwg.Api     # or: dotnet run --project api/src/Wwg.Api
npm run dev --prefix web
```

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
for staged `web/` files. It doesn't build or test. Run these before committing (CI runs the same):

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
