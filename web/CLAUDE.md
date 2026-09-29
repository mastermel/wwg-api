# web/

WWG Campaigner: React + TypeScript SPA built with Vite. Full design: DESIGN.md §3.12.

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
  `signOut`. Pages inside the app frame live under `routes/_app/` (guarded); sign-in and register
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
- Queries that must not be saved for offline use (live status, admin data such as the user list)
  pass `meta: { persist: false }`.
- Accessibility (WCAG 2.1 AA) is enforced in part by `jsx-a11y`; also give every page a title and
  a single `h1`.
