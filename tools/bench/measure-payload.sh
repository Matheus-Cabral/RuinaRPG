#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
out=$(mktemp -d)
trap 'rm -rf "$out"' EXIT
dotnet publish src/RuinaRPG.Client/RuinaRPG.Client.csproj -c Release -o "$out" -v q > /dev/null
fw="$out/wwwroot/_framework"
# Exclude the .gz/.br siblings the SDK already emits; measure the raw files and their gzip -9 size.
mapfile -t files < <(find "$fw" -type f ! -name '*.gz' ! -name '*.br')
raw=0; gz=0
for f in "${files[@]}"; do
  raw=$(( raw + $(stat -c %s "$f") ))
  gz=$(( gz + $(gzip -9 -c "$f" | wc -c) ))
done
record "payload.framework.files" "${#files[@]}"
record "payload.framework.raw.kb" "$(( raw / 1024 ))"
record "payload.framework.gzip.kb" "$(( gz / 1024 ))"
record "payload.served.encoding" "nginx serves raw files today (no gzip/brotli configured)"
