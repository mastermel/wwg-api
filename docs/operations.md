# Operations

How to look after the production app: backups, restoring one, and rolling back a deploy. How
production runs is in [DESIGN.md §3.10](../DESIGN.md); why backups work this way is in decision
[0008](decisions/0008-backups-in-the-app.md).

The commands use `wwg` for the Compose service and `wwg-data` for its `/data` volume. Use the
real names from the stack in the server's config repo (`roach`, `sync/stacks/wwg`).

## Backups

The app backs itself up into `/data/backups` (`Backup:Path`):

| File | When |
|---|---|
| `wwg-20260928-031500.db` | Scheduled, every `Backup:Interval` (default a day) |
| `wwg-20260928-031500-before-migration.db` | At startup, before migrations are applied |

Times are UTC. The newest `Backup:Keep` (default 14) of each kind are kept. Each file is a
complete SQLite database, with no `-wal` or `-shm` files beside it.

Look for these log lines to check backups are being made: `Backed up the database to …`, and at
startup, not `No backup folder configured`.

The backups are on the same volume as the database. **Copy `/data/backups` somewhere else**
(the server's own backup tooling), or losing that disk loses both.

To list the backups (the app's image has no shell, so use a throwaway container on the same
volume):

```sh
docker run --rm -v wwg-data:/data alpine ls -l /data/backups
```

To take one by hand, for example before changing the server, copy the database with the app
stopped (stopping it writes everything into `wwg.db`). A name that doesn't match the pattern
above is never deleted by the app:

```sh
docker compose stop wwg
docker run --rm -v wwg-data:/data alpine cp /data/wwg.db /data/backups/wwg-manual.db
docker compose start wwg
```

## Restoring a backup

Everything written since the backup is lost, so pick the newest one that's good.

```sh
docker compose stop wwg
docker run --rm -v wwg-data:/data alpine sh -c '
  cd /data &&
  mkdir -p replaced &&
  mv wwg.db wwg.db-wal wwg.db-shm replaced/ 2>/dev/null;
  cp backups/wwg-20260928-031500.db wwg.db &&
  chown 1654:1654 wwg.db'
docker compose start wwg
```

- The `-wal` and `-shm` files belong to the database being replaced and must go with it; a
  leftover `-wal` file would be applied to the restored database.
- `1654` is the app's user in the image. SQLite needs to write to the file and to `/data`.
- The replaced files stay in `/data/replaced` until you delete them.

Then sign in and check the data looks right. Everyone stays signed in: the Data Protection keys
in `/data/keys` aren't touched.

## Rolling back a deploy

Migrations only go forward, and an older image can't use a newer schema. So if a deploy applied
a migration:

1. Point the stack at the previous image tag (`mastermel/wwg:vYYYYMMdd.HHmmss`) instead of
   `latest`, so the new image isn't pulled again.
2. Restore the `…-before-migration.db` backup the new version made at startup (above).
3. Start the app.

If the deploy had no migration (no `…-before-migration.db` for it), only step 1 is needed.

Move the stack back to `latest` once a fixed image is published.
