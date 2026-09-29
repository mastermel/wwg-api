# e2e/

Playwright end-to-end tests against the production image. Design: DESIGN.md §3.8, decision 0007.
Commands: docs/development.md ("End-to-end tests").

```sh
./stack.sh up            # image behind Caddy (TLS) + Mailpit, fresh data, Admin account
npm run test:docker      # the suite in Microsoft's Playwright image; args go to playwright test
npm run lint && npm run typecheck && npm run format:check
./stack.sh down
```

## How the stack works

- `compose.yaml`: the app (the image; `E2E_IMAGE` to use a prebuilt one, as CI does), Caddy in
  front with its own certificate at https://localhost:8443, Mailpit at http://localhost:8025. The
  network is pinned so the app trusts Caddy's forwarded headers, as production trusts Traefik.
- HTTPS is required: WebKit drops the `Secure` refresh cookie over http, even on localhost.
- Rate limits are raised: every test's users come from one IP.
- `stack.sh up` registers the Admin (`tests/support/accounts.ts`) and restarts the app, because
  `Admin:Emails` grants the role at startup. Keep the password in both files in step.

## Writing tests

- Each test makes its own users with the `signUp(firstName)` fixture (a new browser context,
  registered through the API, signed in) or signs in with `signIn(email, password)`. Never rely on
  data from another test: tests run in parallel, in both browser projects, against one database.
- The Admin account is shared by every run: sign in to it only with the right password (failed
  sign-ins lock an account out), and give anything that fails sign-in on purpose its own user.
- Find things by role and accessible name, as a person would. Mantine's `Select` is a
  `combobox`; a required field's label ends in " *", so match the textbox's name, not the label.
- Wait on what the user sees (`expect(...).toBeVisible()`), or on state with `expect.poll`. No
  `waitForTimeout`, and not `waitForFunction` with an async predicate: it doesn't wait for the
  promise.
- Debounced searches keep their query in the URL: wait for the URL before using the results.
- Mantine's hidden inputs: click a `Switch` itself (`getByRole("switch")`: its input lies over
  the label), but a `SegmentedControl` option by its label (its input is off-screen). Don't
  `force`; it can click without toggling.
- The campaign map is a MapLibre canvas: `getByRole("region", { name: "Map", exact: true })`.
  `clickMapCentre(page)` and `clickMap(page, right, down)` (tests/support/map.ts) click it where
  it is now (call them after anything that scrolls). After a drawer closes, wait for
  `getByRole("dialog")` to have a count of 0 before clicking the map or scanning: its overlay
  fades out, taking the click (and failing axe while it fades). Units on it are buttons named "Name, Type, Army"; a stack "2 units: A, B". Don't drive
  place search here (it calls a service over the internet).
- Setup the test isn't about (a map's area, armies, units) goes through the API: `apiAs(page)`
  (tests/support/api.ts) calls it as that page's user; `waterlooMap` is a ready area.
  `startedCampaign(umpire, commander, name)` (tests/support/turns.ts) is a campaign at turn 1,
  with the commander's army and two units placed.
- `scan(page, label)` (tests/support/axe.ts) is axe's violations, for pages that need data only a
  flow sets up; the rest belong in `accessibility.spec.ts`.
- Offline: `waitForServiceWorker` and `waitUntilSaved` (tests/support/offline.ts) before going
  offline; the app saves at most once a second. Service workers are Chromium-only in Playwright,
  so offline tests skip WebKit.
- A test that can't run in one browser uses a conditional `test.skip(condition, reason)`.
- Emails: `latestEmailText(to)` and `linkIn(text, prefix)` (tests/support/mailpit.ts).
- A new page or section belongs in `accessibility.spec.ts`, which axe-scans every page in both
  colour schemes (it's the only place colour contrast is checked).
- Long multi-user tests call `test.slow()`: beside the rest of the suite they can pass 30s.
