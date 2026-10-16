#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
bench_compose up -d --build > /dev/null
trap 'bench_compose down --volumes > /dev/null' EXIT
for _ in $(seq 1 60); do curl -fs http://localhost:8088/api/health > /dev/null && break; sleep 2; done
docker run --rm --network host -v "$BENCH_DIR:/bench:ro" mcr.microsoft.com/playwright/python:v1.48.0-jammy \
  bash -c "pip install -q playwright==1.48.0 && python /bench/load_time.py http://localhost:8088/" \
  | grep '^load\.' | while IFS=': ' read -r k v; do record "$k" "$v"; done
