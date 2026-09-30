# web/

The Wasatch Wargamers app (`wwg-campaigner` in code): React + TypeScript SPA built with Vite. Full design: DESIGN.md §3.12.

## Commands (from web/)

```sh
npm ci                  # install (Node version in .nvmrc)
npm run dev             # Vite dev server on http://localhost:5173
npm run lint            # ESLint (type-aware); npm run lint:fix to auto-fix
npm run format          # Prettier; npm run format:check is what CI runs
npm run typecheck       # tsc -b
npm test                # Vitest, once; npm run test:watch while working
npm run build           # typecheck + production build into dist/
```

Before committing a `web/` change: `npm run lint`, `npm run typecheck` and `npm test` must pass.
The pre-commit hook runs `eslint --fix` and Prettier on staged files.

## Conventions

- TypeScript `strict`. No `any`; lint errors are fixed, not disabled. A rare `eslint-disable`
  comment needs a reason on the same line.
- Import from `src/` with the `@/` alias (`import { App } from "@/App"`), not long relative paths.
- Named exports, not default exports (except where a tool requires one, e.g. config files).
- Components are function components in `PascalCase.tsx` files; one exported component per file.
- Tests sit next to the code as `*.test.ts(x)`, using Testing Library queries by role/label (how a
  user finds things), not CSS selectors or test IDs.
- Prettier owns formatting (line width 100). Don't hand-format.
- Session: `useSession()` for the state (status, user), `useSessionStore()` for `signIn` /
  `signOut`, and `switchUser` for a masquerade (decision 0012; `useMasqueradeSession`). Pages inside the app frame live under `routes/_app/` (guarded); sign-in and register
  under `routes/_public/`. Never store tokens yourself: the access token is in memory in
  `lib/access-token`, the refresh token is an HttpOnly cookie.
- Forms: React Hook Form + `zodResolver` with the generated schema (`@/api/generated/zod/...`);
  trim name/email inputs with `setValueAs`; put API errors on fields with `applyServerErrors`.
- Tests: `renderApp(path, { session })` renders the real app with a mocked session
  ("signed-in" by default, "signed-out" or "offline").
  `test/server.ts` answers requests many pages make (the campaign list, a campaign's armies,
  members and join code) with empty defaults; a test's own `server.use(...)` wins.
- Page data goes through `QueryState` (loading / error / "not available offline", and saved data
  wins over a failed refetch). Wrap each page in `Page`, which sets the title and focus.
- Admin screens live under `routes/_app/admin/` (the layout gives non-admins the not-found page).
  Lists keep their search and page in the URL (`validateSearch`), so they can be shared.
- Page layout: `Page` takes `back` (a `BackLink`), `summary` and `actions`; its content is
  `Section`s (a titled panel; `flush` for a table, `tone="danger"` for deleting and leaving,
  which go last). Empty lists use `EmptyState`. Don't hand-roll headings and panels.
- Colours come from the theme's variables (`--app-canvas`, `--app-header`, Mantine's); a new
  colour pair gets its contrast checked and noted in `theme.ts`. Component styles that need more
  than props go in a `*.module.css` next to the component.
- Anything that deletes, removes or can't be undone asks first with `ConfirmModal`.
- A button that goes to another page is a `LinkButton` (`renderLink`), not a `Button` with
  `renderRoot`: disabled, it becomes a real disabled button (a link could still be followed).
- Every successful change confirms itself with a green notification ("Saved …", "Deleted …").
- Mantine's `Select` is a `combobox` to Testing Library, and its options need `hidden: true`
  (the dropdown's transition leaves it `display: none` in jsdom).
  A `Menu`'s items can too, just after something else re-rendered the page.
- Show dates with `formatDate` / `formatDateTime` (`lib/format`): the API sends UTC.
- Don't set a `gcTime` above 2^31 - 1 ms (about 24.8 days): timers overflow and fire at once,
  dropping the data before it can be saved or restored.
- After a change inside a campaign, refetch it with `refreshCampaign(queryClient, id)`
  (`features/campaigns/campaign-cache`), not query by query: changes spread (removing a Player
  unassigns their army). After deleting or leaving one, `forgetCampaign` (once navigated away).
- Failed calls: `errorMessage(error, "What didn't happen. Try again.")` (`lib/errors`) for the
  message; forms use `applyServerErrors`, which does the same for non-field errors.
- A Mantine `Alert` that informs rather than warns of an error gets `role="status"`.
- Name an army with `ArmyBadge` (`features/armies/identity`): its flag, framed in its colour,
  beside the name. Army colours are the theme's `--army-*` variables (`armyColorVar`), checked
  in `army-colors.ts`; flags come from `NationFlag`, names from `nationLabel`.
- The campaign map (`features/maps`): `CampaignMap` draws our style (`map-style.ts`, colours
  contrast-checked there) with MapLibre. jsdom has no WebGL, so tests `vi.mock` `CampaignMap`
  with a stand-in (`useImperativeHandle` for its `mapRef`, a button calling `onMapClick`); the
  stand-in can't draw its children (markers need a real map), so marker behaviour is e2e's.
  Stacking (`stacks.ts`) is a pure function, tested alone.
  Import it only through `CampaignMap`, which sets MapLibre's worker URL (`maplibre-worker.ts`).
  Distances and the range circle come from `geo.ts` (haversine, the same Earth radius as the
  API's `Geo.cs`); orders in words from `orders.ts`. Ghost moves and the range are
  `OrderOverlay`, inside the map: sight only, as the turn panel lists the same orders.
  A commander's turn changes go through `useOrders` (`use-orders.ts`), which also refetches the
  army's turns: they're keyed by the army, so `refreshCampaign` doesn't reach them.
  The hex grid (decision 0014) is `hex-grid.ts`: its arithmetic matches the API's `HexGrid.cs`,
  and both are tested against `testdata/hex-grid.json` (from an independent Python reference;
  the Dockerfile copies it into the web build). `HexGridLayer` draws it, and nothing past
  `maxDrawnHexes`. Movement is `movement.ts` (classes, the rules' rates, `reach` and `pathTo`),
  mirroring the API's `Movement.cs`. Terrain (decisions 0014, 0016) is `terrain.ts` (labels,
  and where each of a hex's six sides is stored: `storedEdge`, `flowFor`), drawn by
  `TerrainLayer` and edited on `TerrainPage`. Inference (`inference/`) reads the map's tiles in
  the browser: `tiles.ts` fetches and decodes them (browser only), `sources.ts` and `infer.ts`
  are plain arithmetic, tested with made-up data. The Umpire's
  (approve, send back, reopen, the next turn) go through `useReview` (`use-review.ts`).
- The library (decision 0015) is `features/library`: every signed-in user views it; edits are
  shown only to `canEditLibrary(user)` (Managers and Admins). `UnitFormModal` (`features/units`)
  is shared by library units and the army's copies of them. An army takes units only from its
  factions (the army form's **Factions**), through `AddUnitsModal`; both fetch the library afresh
  each time they open (`refetchOnMount: "always"`), as a Manager may have changed it.
- Queries that must not be saved for offline use (live status, admin data such as the user list)
  pass `meta: { persist: false }`.
- Accessibility (WCAG 2.1 AA) is enforced in part by `jsx-a11y`; also give every page a title and
  a single `h1`.
