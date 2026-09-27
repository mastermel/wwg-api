#!/usr/bin/env bash
# Runs the API (dotnet watch, http://localhost:5102) and the Vite dev server
# (http://localhost:5173) together. Ctrl+C stops both; so does either one exiting.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ ! -d "$root/web/node_modules" ]; then
  echo "Installing web dependencies (npm ci)..."
  npm ci --prefix "$root/web"
fi

pids=()

# Background jobs of a script ignore Ctrl+C, and dotnet watch doesn't always exit on SIGTERM, so
# stop both explicitly: SIGTERM first, then SIGKILL anything still running after a few seconds.
stop() {
  trap - EXIT INT TERM
  kill -TERM "${pids[@]}" 2>/dev/null || true
  for _ in 1 2 3 4 5; do
    kill -0 "${pids[@]}" 2>/dev/null || break
    sleep 1
  done
  kill -KILL "${pids[@]}" 2>/dev/null || true
  # dotnet watch's child (the API itself) and Vite's esbuild share our process group.
  kill -TERM 0 2>/dev/null || true
}
trap stop EXIT INT TERM

dotnet watch --project "$root/api/src/Wwg.Api" --non-interactive &
pids+=($!)
npm --prefix "$root/web" run dev &
pids+=($!)

# Return as soon as either process exits; the trap stops the other.
wait -n
