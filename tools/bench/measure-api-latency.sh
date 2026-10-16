#!/usr/bin/env bash
source "$(dirname "$0")/lib.sh"
cd "$BENCH_ROOT"
base=http://localhost:8088

bench_compose up -d --build > /dev/null
trap 'bench_compose down --volumes > /dev/null' EXIT
for _ in $(seq 1 60); do curl -fs "$base/api/health" > /dev/null && break; sleep 2; done

json() { curl -fs -H 'Content-Type: application/json' "$@"; }
token=$(json -X POST "$base/api/auth/register/gm" \
  -d '{"nickname":"BenchGm","email":"bench@bench.local","senha":"Senha!123","confirmacaoSenha":"Senha!123"}' | jq -r .accessToken)
auth=(-H "Authorization: Bearer $token")
campaign=$(json "${auth[@]}" -X POST "$base/api/campaigns" -d '{"nome":"Bench","descricao":""}' | jq -r .id)
npc=$(curl -fs "${auth[@]}" -X POST "$base/api/npc-sheets" | jq -r .id)
creature=$(curl -fs "${auth[@]}" -X POST "$base/api/creature-sheets" | jq -r .id)

endpoints=(
  "/api/auth/me" "/api/campaigns" "/api/campaigns/$campaign/members" "/api/npc-sheets/$npc"
  "/api/creature-sheets/$creature" "/api/items" "/api/spell-ability-bank" "/api/traits"
  "/api/rulebook" "/api/compendio/search?q=fogo"
)
for e in "${endpoints[@]}"; do
  curl -fs -o /dev/null "${auth[@]}" "$base$e"   # warm-up, also fails fast on a wrong route
  times=$(for _ in $(seq 1 50); do curl -s -o /dev/null -w '%{time_total}\n' "${auth[@]}" "$base$e"; done | sort -n)
  p50=$(sed -n 25p <<< "$times"); p95=$(sed -n 48p <<< "$times")
  name=$(sed -E 's#/[0-9a-f-]{36}#/{id}#g; s#\?.*##' <<< "$e")
  record "latency.$name" "p50=${p50}s p95=${p95}s"
done
