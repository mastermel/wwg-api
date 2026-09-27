# 0006. Deploy after Phase 1, on Komodo, before building accounts

- **Date:** 2026-09-27
- **Status:** Accepted

## Context

The plan had Docker and deployment in Phase 4, after accounts and campaigns.
The design already noted they could move earlier. The maintainer wanted the
app live after Phase 1, on the existing home server ("roach"): Docker Compose
stacks managed by Komodo, behind Traefik, on an arm64 machine.

## Decision

- Steps 19 (SPA hosting, Dockerfile) and 20 (CD) are done after Phase 1;
  step 21 (first deploy) follows. Phases 2–3 then ship to a running
  deployment.
- One image, `mastermel/wwg` (public, Docker Hub), built for **linux/amd64
  and linux/arm64**. The build stages run on the build machine and
  cross-compile; the runtime stage only copies, so no emulation is needed.
- Runtime image: `aspnet:10.0-noble-chiseled-extra` (non-root, no shell,
  with ICU and tzdata). No curl, so the app has a `--health-check` mode for
  the HEALTHCHECK.
- Production host: **`wasatchwargamers.org`**, through Traefik's
  `websecure` entrypoint and `wasatchwargamersorgresolver`.
- The stack lives in the server's config repo (`roach`,
  `sync/stacks/wwg`). The shared `traefik` network is pinned to
  **172.21.0.0/16** in the Komodo compose, and the app trusts exactly that
  range for forwarded headers. Nothing sits in front of Traefik.
- How Komodo picks up new images (sync + GitHub webhook) is set up
  separately.

## Consequences

- Phase 1 has something users can open. Phase 2's secrets (SMTP) and the
  Data Protection keys are added to the stack when they arrive.
- The subnet is now a fixed value in two repos; changing it means changing
  both.
