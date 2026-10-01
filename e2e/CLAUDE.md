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
- A searchable list in a dialog (the army form's Factions): `chooseFromList(field, name)`
  (tests/support/library.ts). Filled below a phone's fold, its list stays hidden (Mantine hides
  the list of a field out of sight), and clicking an option scrolls the dialog, which flips the
  list, so it never holds still: the helper centres the field and picks with the keyboard. CI's
  fonts are larger than a desktop's, so forms are taller there: a field in view here may not be.
- Mantine's hidden inputs: click a `Switch` itself (`getByRole("switch")`: its input lies over
  the label), but a `SegmentedControl` option by its label (its input is off-screen). Don't
  `force`; it can click without toggling.
- The campaign map is a MapLibre canvas: `getByRole("region", { name: "Map", exact: true })`.
  `clickMapCentre(page)` and `clickMap(page, right, down)` (tests/support/map.ts) scroll it to
  the middle of the screen and click it there (on a phone its middle can be under the tab bar). After a drawer closes, wait for
  `getByRole("dialog")` to have a count of 0 before clicking the map or scanning: its overlay
  fades out, taking the click (and failing axe while it fades). A phone shows the area's full height
  but only part of its width: a unit near the east or west edge is off the map there (its
  marker is clipped, and a click on it lands on the page). `clickMapPart(page, across, downward)`
  clicks a fraction of the map from its middle; `dragOnMap(page, from, to)` drags across it with
  the mouse (drawing the area on the settings page). Units on it are buttons named "Name, Type, Army"; a stack "2 units: A, B". Don't drive
  place search here (it calls a service over the internet).
- Setup the test isn't about (a map's area, armies, units) goes through the API: `apiAs(page)`
  (tests/support/api.ts) calls it as that page's user; `waterlooMap` is a ready area.
  `startedCampaign(umpire, commander, name, hexSize?, { enemyAt? })` (tests/support/turns.ts) is a
  campaign at turn 1, with the commander's army and two units placed in hexes (both move two a
  turn), and with `enemyAt`, a commanderless Prussian army of the other side with one unit there
  (armies get turns only as they open, so it's made before the start), and with `ally`, an army
  of the same side commanded by another user, likewise; `holdForArmy` plays such an army's turn
  as the Umpire.
  Pass small hexes when a test needs several in view on a phone. Army units come from the
  library: `libraryFaction(browserOf(page), name, nation, units)` (tests/support/library.ts)
  makes a faction of the test's own (a stamped name: every run shares the library) as the
  Admin; `chooseFaction` and `addFromLibrary` do the Umpire's side in the UI. `holdAndSubmit`
  and `approveAndStartNext` move a turn on through the API; the latter confirms no attrition, so it
  fails once a unit owes some (a fourth move in a row: decision 0018; the 7th turn out of supply,
  where an army has a depot: decision 0019). A unit out of supply is named "…, out of supply".
- To stand in for an outside service (the map's tiles, say), `page.route` it, and
  `test.use({ serviceWorkers: "block" })`: requests the app's service worker makes don't reach
  `page.route`, so without that some get through (WebKit, often).
- `scan(page, label)` (tests/support/axe.ts) is axe's violations, for pages that need data only a
  flow sets up; the rest belong in `accessibility.spec.ts`.
- Offline: `waitForServiceWorker` and `waitUntilSaved` (tests/support/offline.ts) before going
  offline; the app saves at most once a second. Service workers are Chromium-only in Playwright,
  so offline tests skip WebKit.
- A test that can't run in one browser uses a conditional `test.skip(condition, reason)`.
- Umpires and Admins set up and manage campaigns on a computer; the phone is for Players. A test
  in which only the Umpire or an Admin acts in the app skips the phone:
  `test.skip(isMobile, desktopOnly)` (tests/support/fixtures.ts). One where a Player, commander
  or ally sees or does something runs on both.
- Emails: `latestEmailText(to)` and `linkIn(text, prefix)` (tests/support/mailpit.ts).
- A new page or section belongs in `accessibility.spec.ts`, which axe-scans every page in both
  colour schemes (it's the only place colour contrast is checked).
- Long multi-user tests call `test.slow()`: beside the rest of the suite they can pass 30s.
