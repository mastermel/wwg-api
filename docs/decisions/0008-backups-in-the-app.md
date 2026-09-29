# 0008. Database backups, made by the app

- **Date:** 2026-09-28
- **Status:** Accepted

## Context

Backups were a non-goal. But the app is live with real data, in one SQLite file on the home
server's volume, and every deploy runs migrations at startup. A migration that goes wrong, or a
lost or corrupted file, had no way back. Rolling back to an older image doesn't help after a
migration either: the older app can't read the newer schema.

The options were a sidecar such as Litestream (continuous replication, but another container and
its configuration in the server's repo), a cron job on the server, or the app backing itself up.

## Decision

- The app backs up its own database with SQLite's `VACUUM INTO`. It's a consistent snapshot of
  a live WAL database, it needs nothing beyond the image, and so it works the same everywhere
  the image runs.
- **Before startup migrations:** if the database already has a schema and migrations are
  pending, a `…-before-migration.db` snapshot is made first. If it fails, startup stops before
  migrating.
- **Scheduled:** every `Backup:Interval` (default a day), following the newest backup on disk,
  so restarts don't reset the schedule.
- The newest `Backup:Keep` (default 14) backups of each kind are kept. They're written to
  `Backup:Path` (`/data/backups` in the image). With no path set, there are no backups, and a
  warning is logged at startup.
- Restoring and rolling back are done by hand, following `docs/operations.md`.

## Consequences

- A bad deploy can be undone: restore the pre-migration backup and run the previous image tag.
- Backups sit on the same volume as the database, so they don't survive losing that disk.
  Copying `/data/backups` off the server is the server's job (its own backup tooling), not the
  app's.
- A day's changes can be lost at worst (between scheduled backups). That's fine for a club app.
  If it stops being fine, Litestream remains an option.
