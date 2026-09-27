# wwg-api

Backend REST API for the **Wasatch Wargamers Campaign App**.

Users sign up, create campaigns, and invite other players with a join link.
Each campaign's Umpire builds Armies and Units and assigns Players to command
them. The React front-end uses a TypeScript SDK generated from this API's
OpenAPI document.

## Tech stack

- .NET 10 / ASP.NET Core Minimal APIs
- Entity Framework Core with SQLite
- ASP.NET Core Identity (bearer tokens)
- OpenAPI + Swagger UI; TypeScript SDK generated with Orval in the front-end repo
- xUnit integration tests with a fresh SQLite database per test
- GitHub Actions CI, Docker images published to Docker Hub

## Documentation

- [DESIGN.md](DESIGN.md): architecture, domain model, permissions, endpoints,
  and the implementation plan.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (version pinned in `global.json`)
- Docker (for local email testing with Mailpit, and building the image)

## Development

```sh
dotnet build                                  # build everything (warnings fail the build)
dotnet test                                   # run the integration tests
dotnet run --project src/Wwg.Api              # run the API on http://localhost:5102
```

Tests use xUnit v3 on the Microsoft Testing Platform (enabled in `global.json`).

## Project layout

- `src/Wwg.Api/`: the ASP.NET Core API
- `tests/Wwg.Api.IntegrationTests/`: endpoint-level integration tests

## License

[MIT](LICENSE)
