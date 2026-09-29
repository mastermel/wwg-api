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
  Don't drive place search here (it calls a service over the internet).
- Offline: `waitForServiceWorker` and `waitUntilSaved` (tests/support/offline.ts) before going
  offline; the app saves at most once a second. Service workers are Chromium-only in Playwright,
  so offline tests skip WebKit.
- A test that can't run in one browser uses a conditional `test.skip(condition, reason)`.
- Emails: `latestEmailText(to)` and `linkIn(text, prefix)` (tests/support/mailpit.ts).
- A new page or section belongs in `accessibility.spec.ts`, which axe-scans every page in both
  colour schemes (it's the only place colour contrast is checked).
- Long multi-user tests call `test.slow()`: beside the rest of the suite they can pass 30s.
