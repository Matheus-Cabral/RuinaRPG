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
sdk_gz=$(find "$fw" -type f -name '*.gz' -printf '%s\n' | awk '{s+=$1} END {print s+0}')
sdk_br=$(find "$fw" -type f -name '*.br' -printf '%s\n' | awk '{s+=$1} END {print s+0}')
record "payload.framework.sdk-gz.kb" "$(( sdk_gz / 1024 ))"
record "payload.framework.sdk-br.kb" "$(( sdk_br / 1024 ))"
