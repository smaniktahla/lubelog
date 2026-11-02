#!/bin/sh
# Runs wizard.mjs in a headless Chromium (Playwright container). Usage: BASE_URL=... TEST_VIN=... ./run.sh
exec docker run --rm --network host -e BASE_URL -e TEST_VIN -v "$(cd "$(dirname "$0")" && pwd)":/work -w /work \
  mcr.microsoft.com/playwright:v1.49.1-noble sh -c 'cd /tmp && npm init -y >/dev/null && npm i playwright@1.49.1 >/dev/null 2>&1 && cp /work/wizard.mjs . && node wizard.mjs'
