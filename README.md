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
- **Web (`web/`, WWG Campaigner):** React + TypeScript SPA built with Vite,
  Mantine, TanStack Router and Query, and an Orval-generated API client;
  installable as a PWA (coming in step 9)
- GitHub Actions CI; a single Docker image (API serving the built front-end)
  published to Docker Hub

## Documentation

- [DESIGN.md](DESIGN.md): architecture, domain model, permissions, endpoints,
  and the implementation plan.
- [docs/decisions/](docs/decisions/README.md): decision log.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (version pinned in `global.json`)
- Node.js 24 + npm (for `web/`)
- Docker (for local email testing with Mailpit, and building the image)

## Development

From the repo root:

```sh
dotnet build api/Wwg.slnx                     # build the API (warnings fail the build)
dotnet test --solution api/Wwg.slnx           # run the integration tests
dotnet run --project api/src/Wwg.Api          # run the API on http://localhost:5102
```

Or run `dotnet build` / `dotnet test` from inside `api/`. Tests use xUnit v3 on
the Microsoft Testing Platform (enabled in the root `global.json`).

Code is formatted with [CSharpier](https://csharpier.com) (`dotnet csharpier format .`).
The first build restores the local tools and installs a pre-commit hook
(Husky.Net) that formats staged files. Set `HUSKY=0` to skip installing it.

## Repository layout

- `api/`: .NET solution (`Wwg.slnx`) and its shared build config
  - `src/Wwg.Api/`: the ASP.NET Core API
  - `tests/Wwg.Api.IntegrationTests/`: endpoint-level integration tests
- `web/`: React front-end (coming in a later step)
- `docs/`: decision log
- Root: repo-wide config (`global.json`, `.editorconfig`, `.gitignore`,
  `.config/dotnet-tools.json`, `.husky/`)

## License

[MIT](LICENSE)
