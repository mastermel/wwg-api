#!/usr/bin/env bash
# The stack the end-to-end tests run against (compose.yaml), at https://localhost:8443.
#   ./stack.sh up      start from an empty database, with an Admin account (tests/support/accounts.ts)
#   ./stack.sh down    stop it and throw the data away
#   ./stack.sh logs    the app's and proxy's logs
# Set E2E_IMAGE to use an image that's already built (CI does); otherwise it's built from the
# Dockerfile.
set -euo pipefail
cd "$(dirname "$0")"

compose() { docker compose -f compose.yaml "$@"; }

case "${1:-}" in
  up)
    compose down --volumes --remove-orphans
    if [[ -n "${E2E_IMAGE:-}" ]]; then
      compose up --detach --no-build --wait
    else
      compose up --detach --build --wait
    fi
    # Admin:Emails grants the role at startup, to accounts that exist by then.
    curl --fail --silent --show-error --insecure --output /dev/null \
      --header 'Content-Type: application/json' \
      --data '{"email":"admin@e2e.test","password":"e2e admin password","firstName":"Ada","lastName":"Admin"}' \
      https://localhost:8443/api/auth/register
    compose restart app
    compose up --detach --no-build --wait
    echo "Ready: https://localhost:8443 (inbox: http://localhost:8025)"
    ;;
  down)
    compose down --volumes --remove-orphans
    ;;
  logs)
    compose logs app proxy
    ;;
  *)
    echo "usage: $0 up|down|logs" >&2
    exit 2
    ;;
esac
