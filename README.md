# wwg

The **Wasatch Wargamers Campaign App**: a .NET REST API and a React front-end,
in one repository.

Users sign up, create campaigns, and invite other players with a join link.
Each campaign's Umpire builds Armies and Units and assigns Players to command
them. The front-end talks to the API through a TypeScript SDK generated from
the API's OpenAPI document.

## Tech stack

- **API (`api/`):** .NET 10 / ASP.NET Core Minimal APIs, EF Core with SQLite,
  ASP.NET Core Identity (bearer tokens), OpenAPI + Swagger UI, xUnit
  integration tests with a fresh SQLite database per test
- **Web (`web/`, the Wasatch Wargamers app):** React + TypeScript SPA built with Vite,
  Mantine, TanStack Router and Query, and an Orval-generated API client;
  installable as a PWA
- **End-to-end tests (`e2e/`):** Playwright, in Chromium and iPhone WebKit,
  against the production image behind a TLS proxy
- GitHub Actions CI; a single Docker image (API serving the built front-end)
  published to Docker Hub

## Documentation

- [DESIGN.md](DESIGN.md): architecture, domain model, permissions, endpoints,
  and the implementation plan.
- [docs/decisions/](docs/decisions/README.md): decision log.
- [docs/development.md](docs/development.md): local development guide.
- [docs/operations.md](docs/operations.md): backups, restoring, and rolling back a deploy.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (version pinned in `global.json`)
- Node.js 24 + npm (for `web/` and `e2e/`)
- Docker (for local email testing with Mailpit, building the image, and the
  end-to-end tests)

## Development

Quick start, from the repo root:

```sh
dotnet tool restore && dotnet build api/Wwg.slnx && npm ci --prefix web
scripts/dev.sh          # API on :5102 and the web app on http://localhost:5173
```

See **[docs/development.md](docs/development.md)** for the full guide: running,
the development database, the checks to run before committing, and editor
setup (Neovim, VS Code).

## Deployment

A single image, [`mastermel/wwg`](https://hub.docker.com/r/mastermel/wwg)
(linux/amd64 and linux/arm64), is built and pushed by CI on every push to
`main`: the API serving the built web app on port 8080, with its SQLite
database in the `/data` volume. See [DESIGN.md §3.10](DESIGN.md) for how
production runs it.

```sh
docker build -t wwg .   # build the image locally
docker run --rm -p 8080:8080 -v wwg-data:/data \
  -e App__PublicUrl=http://localhost:8080 wwg   # and run it on http://localhost:8080
```

`App__PublicUrl` is required (reset and join links are built from it). Without `Smtp__Host`,
emails are written to the log instead of sent. Plain http works in Chrome; Safari needs HTTPS
for the sign-in cookie. Backups, restoring and rolling back are in
[docs/operations.md](docs/operations.md).

## Repository layout

- `api/`: .NET solution (`Wwg.slnx`) and its shared build config
  - `src/Wwg.Api/`: the ASP.NET Core API
  - `tests/Wwg.Api.IntegrationTests/`: endpoint-level integration tests
- `web/`: the Wasatch Wargamers app, the React front-end
- `e2e/`: Playwright end-to-end tests and the stack they run against
- `scripts/`: developer scripts (`dev.sh`)
- `docs/`: the decision log, the development guide and operations
- Root: repo-wide config (`global.json`, `.editorconfig`, `.gitignore`,
  `.config/dotnet-tools.json`, `.husky/`)

## License

[MIT](LICENSE)
