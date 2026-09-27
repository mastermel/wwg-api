# 0002. One repository for the API and the React front-end

- **Date:** 2026-09-27
- **Status:** Accepted

## Context

The plan had the React front-end in a separate repository, consuming the API
through a pinned copy of `openapi.json`. That needed its own machinery:
- a sync script and pinned contract copy in the React repo;
- CI committing `openapi.json` back to this repo;
- a blocking breaking-change check, because the two could deploy
  independently.

For a small project with one team, the separation costs more than it gives.
Keeping both in one place also gives better combined context, for people and
for AI-assisted work.

## Decision

- One repo, renamed from `wwg-api` to `wwg`, with the .NET solution in `api/`
  and the React app in `web/`. Only truly repo-wide config stays at the root
  (`global.json`, `.editorconfig`, `.gitignore`, dotnet tools, Husky, CI).
- The front-end uses **npm**.
- The SDK is generated in `web/` from the committed `api/openapi.json`. The
  generated code isn't committed.
- CI never commits. It **fails if `openapi.json` is out of date** instead.
  `oasdiff` becomes a warning.
- **The API serves the built SPA** from one Docker image: same origin in
  production, a Vite proxy in development, and no CORS.
  `Frontend:BaseUrl` becomes `App:PublicUrl`.

## Consequences

- One PR can change the API, contract and UI together. A contract change that
  breaks the UI fails the `web` CI job in the same PR.
- The pinned-contract sync, the CI commit-back and its conflict with branch
  protection all go away.
- API and front-end always deploy together. The remaining risk is old
  browser tabs after a deploy, handled with additive changes and a reload
  prompt (front-end design).
- Contributors need both .NET and Node toolchains.
- The Dockerfile gains a Node build stage; the API gains SPA hosting (static
  files, fallback, caching, security headers).
- The front-end stack still needs its own design discussion (plan step 9).
