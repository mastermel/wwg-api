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
- Page data goes through `QueryState` (loading / error / "not available offline", and saved data
  wins over a failed refetch). Wrap each page in `Page`, which sets the title and focus.
- Queries that must not be saved for offline use (live status, admin data such as the user list)
  pass `meta: { persist: false }`.
- Accessibility (WCAG 2.1 AA) is enforced in part by `jsx-a11y`; also give every page a title and
  a single `h1`.
