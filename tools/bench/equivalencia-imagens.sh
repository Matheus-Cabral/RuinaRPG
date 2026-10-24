#!/usr/bin/env bash
# One-off check for 2.0.0 C.2: the deploy images built by the old per-project Dockerfiles (at <old-ref>)
# and by deploy/Dockerfile (working tree) hold the same files with the same bytes and the same image
# config. Prints every difference; exits 1 on any unexpected one. Uses its own tags; never the real stack.
set -euo pipefail
old_ref="${1:?uso: $0 <ref com os Dockerfiles antigos>}"
repo="$(cd "$(dirname "$0")/../.." && pwd)"
tmp="$(mktemp -d)"
tag="eq$$"
sha=equivalencia
cleanup() {
  git -C "$repo" worktree remove --force "$tmp/old" > /dev/null 2>&1 || true
  local t
  for t in api client smoke; do docker rmi "ruinarpg-eq-old-$t:$tag" "ruinarpg-eq-new-$t:$tag" > /dev/null 2>&1 || true; done
  rm -rf "$tmp"
}
trap cleanup EXIT

git -C "$repo" worktree add --quiet --detach "$tmp/old" "$old_ref"
docker build -q -f "$tmp/old/src/RuinaRPG.Api/Dockerfile" --build-arg "GIT_SHA=$sha" -t "ruinarpg-eq-old-api:$tag" "$tmp/old" > /dev/null
docker build -q -f "$tmp/old/infra/client/Dockerfile" --build-arg "GIT_SHA=$sha" -t "ruinarpg-eq-old-client:$tag" "$tmp/old" > /dev/null
docker build -q -f "$tmp/old/tools/RuinaRPG.Smoke/Dockerfile" -t "ruinarpg-eq-old-smoke:$tag" "$tmp/old" > /dev/null
for t in api client smoke; do
  docker build -q -f "$repo/deploy/Dockerfile" --target "$t" --build-arg "GIT_SHA=$sha" -t "ruinarpg-eq-new-$t:$tag" "$repo" > /dev/null
done

# manifest <image> — "<path> <sha256>" per regular file, "<path> -> <target>" per symlink, sorted.
manifest() {
  local c
  c="$(docker create "$1")"
  docker export "$c" | python3 -c '
import hashlib, sys, tarfile
out = []
with tarfile.open(fileobj=sys.stdin.buffer, mode="r|") as t:
    for m in t:
        if m.isfile():
            out.append(f"{m.name} {hashlib.sha256(t.extractfile(m).read()).hexdigest()}")
        elif m.issym():
            out.append(f"{m.name} -> {m.linkname}")
print("\n".join(sorted(out)))'
  docker rm "$c" > /dev/null
}
config() { docker image inspect --format '{{json .Config.Entrypoint}} {{json .Config.Cmd}} {{.Config.WorkingDir}} {{json .Config.ExposedPorts}} {{json .Config.Env}}' "$1"; }

fail=0
for t in api client smoke; do
  manifest "ruinarpg-eq-old-$t:$tag" > "$tmp/old-$t"
  manifest "ruinarpg-eq-new-$t:$tag" > "$tmp/new-$t"
  diff -u "$tmp/old-$t" "$tmp/new-$t" > "$tmp/diff-$t" || true
  # Expected: the old smoke Dockerfile never passed GIT_SHA, so its RuinaRPG assemblies carried no
  # SourceRevisionId; the shared build stamps all three images alike (the .pdb ids change along with the assemblies).
  if [[ "$t" == smoke ]]; then
    grep -E '^[-+]app/RuinaRPG\.[A-Za-z.]+\.(dll|pdb) ' "$tmp/diff-$t" | sed 's/^/esperado: /' || true
    unexpected="$(grep -E '^[-+][^-+]' "$tmp/diff-$t" | grep -vE '^[-+]app/RuinaRPG\.[A-Za-z.]+\.(dll|pdb) ' || true)"
  else
    unexpected="$(grep -E '^[-+][^-+]' "$tmp/diff-$t" || true)"
  fi
  if [[ -n "$unexpected" ]]; then echo "FALHA $t: arquivos diferentes"; echo "$unexpected"; fail=1; else echo "OK    $t: arquivos"; fi
  if [[ "$(config "ruinarpg-eq-old-$t:$tag")" == "$(config "ruinarpg-eq-new-$t:$tag")" ]]; then
    echo "OK    $t: config"
  else
    echo "FALHA $t: config"; config "ruinarpg-eq-old-$t:$tag"; config "ruinarpg-eq-new-$t:$tag"; fail=1
  fi
done
exit "$fail"
