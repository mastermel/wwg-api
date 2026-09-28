# 0007. End-to-end tests against the production image

- **Date:** 2026-09-28
- **Status:** Accepted

## Context

With the plan done, DESIGN.md §6 listed Playwright end-to-end tests against the real API. Every
step so far was also checked by hand-run browser scripts; they caught real bugs (offline data
dropped at once, a reload showing stale data, the refresh rate limit) that the API and component
tests couldn't. The maintainer chose what the suite runs against, when CI runs it, which browsers
and where it lives.

## Decision

- **The production image**, not dev servers: the same image CI publishes, behind **Caddy** with
  TLS (standing in for Traefik), plus **Mailpit** for emails, in `e2e/compose.yaml`. Every run
  starts from an empty database; `e2e/stack.sh up` also creates the Admin account and restarts the
  app so `Admin:Emails` grants the role.
- **HTTPS is required**, not just faithful: WebKit won't keep the `Secure` refresh cookie over
  plain `http://localhost`, so Safari users could never stay signed in there. Chromium also needs
  `--ignore-certificate-errors` to register the service worker from Caddy's untrusted certificate.
- **Browsers:** desktop Chromium and WebKit with an iPhone profile, the supported Chrome, Safari
  and iOS Safari (§3.12). Playwright supports service workers in Chromium only, so the offline
  test runs there; offline on iOS Safari is checked by hand.
- **CI:** a new `e2e` job on every PR and push to `main`. The image push (`docker` job) now waits
  for it, so an image that fails end to end never reaches Docker Hub.
- **Location:** `e2e/` at the repo root, its own npm package, TypeScript strict, ESLint and
  Prettier like `web/`.
- **Rate limits** are raised in the e2e stack: every test's users come from one IP. The limits
  themselves are covered by the API's integration tests.
- WebKit needs system libraries a plain Linux desktop may not have; `npm run test:docker` runs
  the suite in Microsoft's Playwright image instead (matching the installed version), so no
  `sudo` is needed.

## Consequences

- About 5–8 more minutes per CI run, and the image is built twice on `main` (amd64 for the tests,
  then multi-arch for publishing); the e2e build has its own cache scope.
- The suite already found two app bugs while being written: the phone sidebar left in the tab
  order, and saved data winning over a 403/404 (a removed Player still saw the campaign).
- The Admin account is shared across runs, so no test may fail a sign-in with it: Identity's
  lockout would break every test that uses it.
- Two images (Caddy, Mailpit) are pinned in `e2e/compose.yaml`; Dependabot updates them.
