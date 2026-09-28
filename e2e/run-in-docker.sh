#!/usr/bin/env bash
# Runs the tests inside Microsoft's Playwright image, which has every browser and the system
# libraries they need: no `playwright install --with-deps` (and no sudo) on this machine. The image
# version always matches the installed @playwright/test. Arguments go to `playwright test`.
set -euo pipefail
cd "$(dirname "$0")"

version=$(node -p 'require("@playwright/test/package.json").version')
# Host networking, so the stack is at localhost (a secure context) as it is for a local browser.
exec docker run --rm --init --ipc=host --network host \
  --user "$(id -u):$(id -g)" --env HOME=/tmp --env CI \
  --volume "$PWD:/e2e" --workdir /e2e \
  "mcr.microsoft.com/playwright:v${version}-noble" \
  npx playwright test "$@"
