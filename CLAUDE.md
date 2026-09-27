# wwg

The Wasatch Wargamers Campaign App: a .NET 10 REST API (`api/`) and a React + TypeScript
front-end, WWG Campaigner (`web/`), in one repo. The front-end calls the API through an SDK
generated from the API's OpenAPI document.

- **[DESIGN.md](DESIGN.md)** is the source of truth: architecture, domain model, permissions,
  endpoints, conventions (§4.1) and the step-by-step implementation plan (§7). Read the relevant
  section before starting work, and keep it up to date when the design changes.
- Significant design changes get a short entry in [docs/decisions/](docs/decisions/README.md).
- Area-specific conventions live in `api/CLAUDE.md` and `web/CLAUDE.md`.

## Commits

- [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `chore:`,
  `docs:`, `build:`, `ci:`, `test:`, `refactor:`.
- One commit per task or step, each under ~500 changed lines; split larger work.
- Run the full checks before every commit and only commit when they pass:
  - API: `dotnet test --solution api/Wwg.slnx` (builds too, and refreshes `api/openapi.json`).
  - Web (from `web/`): `npm run lint`, `npm run typecheck` and `npm test`.
- Never push. The maintainer pushes.
- The pre-commit hook (Husky.Net, `.husky/`) formats staged C# with CSharpier, and runs
  `eslint --fix` and Prettier on staged `web/` files. Don't bypass it with `--no-verify`.

## Repo-wide tooling

- `global.json` pins the .NET SDK and enables Microsoft Testing Platform.
- `.config/dotnet-tools.json` holds local tools (`dotnet tool restore`): CSharpier, Husky.Net.
- `.editorconfig` holds style rules and analyzer tuning. Every suppressed or lowered rule gets a
  comment saying why.
