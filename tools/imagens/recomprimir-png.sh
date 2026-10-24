#!/usr/bin/env bash
# Lossless PNG recompression (2.0.0 C.3): oxipng in a throwaway alpine container; a file is replaced only
# if ImageMagick finds 0 differing pixels (RGBA) between the original and the result.
# --check <a.png> <b.png> only runs the pixel comparison (exit 0 when identical).
set -euo pipefail
run() {
  docker run --rm -v "$1:/w" alpine:3.20 sh -c "apk add --no-cache -q oxipng imagemagick > /dev/null && $2"
}
if [[ "${1:-}" == --check ]]; then
  d="$(mktemp -d)"; trap 'rm -rf "$d"' EXIT
  cp "$2" "$d/a.png"; cp "$3" "$d/b.png"
  out="$(run "$d" 'magick compare -metric AE /w/a.png /w/b.png null: 2>&1 || true')"
  echo "pixels diferentes: $out"
  [[ "$out" == 0 ]]
  exit
fi
(( $# )) || { echo "uso: $0 <arquivo.png>... | --check <a.png> <b.png>" >&2; exit 2; }
for f in "$@"; do
  d="$(mktemp -d)"
  cp "$f" "$d/orig.png"; cp "$f" "$d/new.png"
  run "$d" 'oxipng -q -o max --strip safe /w/new.png && chmod a+r /w/new.png'
  if "$0" --check "$d/orig.png" "$d/new.png"; then
    echo "$f: $(stat -c %s "$d/orig.png") -> $(stat -c %s "$d/new.png") bytes"
    cp "$d/new.png" "$f"
  else
    echo "$f: pixels mudaram; arquivo mantido" >&2; rm -rf "$d"; exit 1
  fi
  rm -rf "$d"
done
