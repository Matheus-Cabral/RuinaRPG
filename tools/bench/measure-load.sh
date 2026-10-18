#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
bench_clone "$BENCH_REF"
trap bench_teardown EXIT
bench_deploy > "$RESULTS_DIR/$LABEL-load-deploy.log" 2>&1
# The container joins the host network, so the bench stack's 127.0.0.1:8088 is reachable as-is.
docker run --rm --network host -v "$BENCH_DIR:/bench:ro" mcr.microsoft.com/playwright/python:v1.48.0-jammy \
  bash -c "pip install -q playwright==1.48.0 && python /bench/load_time.py $BENCH_URL/" \
  | grep '^load\.' | while IFS=': ' read -r k v; do record "$k" "$v"; done
