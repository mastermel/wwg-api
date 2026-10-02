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

# Confirms the Admin's email with the welcome's link, waiting for it to reach Mailpit (the app
# sends emails from a background queue).
confirm_admin() {
  local link=""
  for _ in $(seq 1 30); do
    local id
    id=$(curl --fail --silent 'http://localhost:8025/api/v1/search?query=to:%22admin@e2e.test%22' |
      jq -r '.messages[0].ID // empty')
    if [[ -n "$id" ]]; then
      link=$(curl --fail --silent "http://localhost:8025/api/v1/message/$id" |
        jq -r '.Text' | grep -oE 'confirm-email\?user=[0-9a-f-]+&code=[A-Za-z0-9_-]+' | head -n 1)
      [[ -n "$link" ]] && break
    fi
    sleep 1
  done
  if [[ -z "$link" ]]; then
    echo "The Admin's welcome email never came." >&2
    exit 1
  fi
  local user="${link#*user=}"
  user="${user%%&*}"
  local code="${link#*code=}"
  curl --fail --silent --show-error --insecure --output /dev/null \
    --header 'Content-Type: application/json' \
    --data "{\"userId\":\"$user\",\"code\":\"$code\"}" \
    https://localhost:8443/api/auth/confirm-email
}

case "${1:-}" in
  up)
    compose down --volumes --remove-orphans
    if [[ -n "${E2E_IMAGE:-}" ]]; then
      compose up --detach --no-build --wait
    else
      compose up --detach --build --wait
    fi
    # Admin:Emails grants the role at startup, to accounts that exist and have confirmed their
    # email by then (decision 0023): register, then follow the welcome's link from Mailpit.
    curl --fail --silent --show-error --insecure --output /dev/null \
      --header 'Content-Type: application/json' \
      --data '{"email":"admin@e2e.test","password":"e2e admin password","firstName":"Ada","lastName":"Admin"}' \
      https://localhost:8443/api/auth/register
    confirm_admin
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
