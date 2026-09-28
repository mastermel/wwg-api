#!/usr/bin/env bash
# Runs the API (dotnet watch, http://localhost:5102) and the Vite dev server
# (http://localhost:5173) together, plus Mailpit if Docker is available. Ctrl+C stops both; so do closing the terminal and either one
# exiting.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ ! -d "$root/web/node_modules" ]; then
  echo "Installing web dependencies (npm ci)..."
  npm ci --prefix "$root/web"
fi

# Mailpit catches the emails the API sends (inbox: http://localhost:8025). Optional: without
# Docker, emails fail to send and the API logs the error.
if command -v docker >/dev/null 2>&1; then
  docker compose -f "$root/docker-compose.dev.yml" up -d --quiet-pull >/dev/null 2>&1 \
    && echo "Mailpit inbox: http://localhost:8025" \
    || echo "Couldn't start Mailpit (docker compose); emails won't be delivered locally."
fi

# Job control: each background job gets its own process group, so it can be stopped whole,
# children included (dotnet watch runs the API as a grandchild).
set -m
groups=()

stop() {
  # Finish stopping even if more signals arrive (e.g. the terminal closing mid-shutdown).
  trap - EXIT
  trap '' INT TERM HUP
  for group in "${groups[@]}"; do kill -TERM -- "-$group" 2>/dev/null || true; done
  # dotnet watch doesn't always exit on SIGTERM; give everything 5 seconds, then SIGKILL.
  for _ in 1 2 3 4 5; do
    alive=false
    for group in "${groups[@]}"; do kill -0 -- "-$group" 2>/dev/null && alive=true; done
    [ "$alive" = false ] && return
    sleep 1
  done
  for group in "${groups[@]}"; do kill -KILL -- "-$group" 2>/dev/null || true; done
}
trap stop EXIT INT TERM HUP

# stdin from /dev/null: a background job that touches the terminal gets suspended.
dotnet watch --project "$root/api/src/Wwg.Api" --non-interactive < /dev/null &
groups+=($!)
npm --prefix "$root/web" run dev < /dev/null &
groups+=($!)

# Return as soon as either process exits; the trap stops the other.
wait -n
