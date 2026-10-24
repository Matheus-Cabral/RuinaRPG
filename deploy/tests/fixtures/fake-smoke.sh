#!/usr/bin/env bash
# Stands in for the smoke image in deploy.bats: records which color deploy.sh smoke-tested.
echo "$RUINA_COLOR" >> "$BATS_FILE_TMPDIR/smoke.log"
exit "${FAKE_SMOKE_EXIT:-0}"
