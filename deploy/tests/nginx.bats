# Headers of deploy/nginx/default.conf (Técnico, deploy section): compression and Cache-Control per file
# type. A throwaway nginx with the real config, a fake webroot and a stub upstream; never the real stack.

setup_file() {
  export NGX_NET="ruinarpg-nginx-bats-$$" NGX_WEB="ruinarpg-nginx-bats-web-$$" NGX_STUB="ruinarpg-nginx-bats-stub-$$"
  local repo root="$BATS_FILE_TMPDIR/ngx" big r
  repo="$(cd "$BATS_TEST_DIRNAME/../.." && pwd)"
  # Bodies over gzip_min_length (1024), so size never decides whether a response is compressed.
  big="$(head -c 3000 /dev/zero | tr '\0' 'a')"
  r="$root/webroot/releases/r1"
  mkdir -p "$r/_framework" "$r/css" "$root/images" "$root/stub"
  ln -s releases/r1 "$root/webroot/current"
  printf '<!DOCTYPE html><html><body>shell %s</body></html>' "$big" > "$r/index.html"
  printf '{"x":"%s"}' "$big" > "$r/_framework/blazor.boot.json"
  printf 'wasm%s' "$big" > "$r/_framework/x.wasm"
  gzip -9 -k "$r/_framework/x.wasm"
  printf 'var a="%s";' "$big" > "$r/_framework/dotnet.runtime.8.0.0.abcdefghij.js"
  gzip -9 -k "$r/_framework/dotnet.runtime.8.0.0.abcdefghij.js"
  printf 'var b="%s";' "$big" > "$r/_framework/dotnet.js"
  printf 'body{content:"%s"}' "$big" > "$r/css/app.css"
  printf 'png' > "$root/images/abc.png"
  printf 'server { listen 8080; location / { default_type application/json; return 200 %s; } }\n' "'{\"x\":\"$big\"}'" > "$root/stub/default.conf"
  echo 'upstream app { server stub:8080; }' > "$root/upstream.conf"
  # nginx's workers run as uid 101; bats' temp dirs are private to us.
  # A copy of the repo's config directory, mounted as a directory like docker-compose.yml does.
  cp -r "$repo/deploy/nginx" "$root/conf.d"
  chmod -R a+rX "$BATS_FILE_TMPDIR"
  docker network create "$NGX_NET" > /dev/null
  docker run -d --name "$NGX_STUB" --network "$NGX_NET" --network-alias stub \
    -v "$root/stub/default.conf:/etc/nginx/conf.d/default.conf:ro" nginx:1.27-alpine > /dev/null
  docker run -d --name "$NGX_WEB" --network "$NGX_NET" -p 127.0.0.1::80 \
    -v "$root/conf.d:/etc/nginx/conf.d:ro" \
    -v "$root/upstream.conf:/etc/nginx/upstream.conf:ro" \
    -v "$root/webroot:/webroot:ro" -v "$root/images:/images:ro" nginx:1.27-alpine > /dev/null
  export NGX_URL="http://$(docker port "$NGX_WEB" 80/tcp | head -1)"
  local i
  for i in $(seq 1 50); do curl -fs -o /dev/null "$NGX_URL/index.html" && return 0; sleep 0.2; done
  docker logs "$NGX_WEB" >&2
  return 1
}

teardown_file() {
  docker rm -f "$NGX_WEB" "$NGX_STUB" > /dev/null 2>&1 || true
  docker network rm "$NGX_NET" > /dev/null 2>&1 || true
}

# headers <path> [curl args...] — response headers, lower-cased names, no CR.
headers() { local p="$1"; shift; curl -fsS -o /dev/null -D - "$@" "$NGX_URL$p" | tr -d '\r' | sed -E 's/^([^:]+):/\L\1:/'; }
gz() { headers "$1" -H 'Accept-Encoding: gzip'; }
# Exactly one Cache-Control, with this value.
cache_is() { [ "$(grep -c '^cache-control:' <<< "$1")" -eq 1 ] && grep -qx "cache-control: $2" <<< "$1"; }

@test "index.html: no-cache, gzip, Vary" {
  h="$(gz /index.html)"
  cache_is "$h" "no-cache"
  grep -qx 'content-encoding: gzip' <<< "$h"
  grep -qix 'vary: accept-encoding' <<< "$h"
}

@test "an SPA route falls back to index.html with no-cache" {
  h="$(gz /campanhas/123)"
  cache_is "$h" "no-cache"
  run curl -fsS "$NGX_URL/campanhas/123"
  [[ "$output" == *"shell"* ]]
}

@test "blazor.boot.json: no-cache" {
  cache_is "$(gz /_framework/blazor.boot.json)" "no-cache"
}

@test "a .wasm is served from its .gz with the wasm content type and no-cache" {
  h="$(gz /_framework/x.wasm)"
  grep -qx 'content-encoding: gzip' <<< "$h"
  grep -qx 'content-type: application/wasm' <<< "$h"
  grep -qix 'vary: accept-encoding' <<< "$h"
  cache_is "$h" "no-cache"
}

@test "without Accept-Encoding the plain bytes are served" {
  h="$(headers /_framework/x.wasm)"
  ! grep -q '^content-encoding:' <<< "$h"
  run curl -fsS "$NGX_URL/_framework/x.wasm"
  [ "${#output}" -eq 3004 ]
}

@test "fingerprinted dotnet.*.<hash>.js is immutable for a year" {
  h="$(gz /_framework/dotnet.runtime.8.0.0.abcdefghij.js)"
  cache_is "$h" "public, max-age=31536000, immutable"
  grep -qx 'content-encoding: gzip' <<< "$h"
}

@test "dotnet.js without a hash is no-cache" {
  cache_is "$(gz /_framework/dotnet.js)" "no-cache"
}

@test "CSS without a .gz is compressed on the fly and no-cache" {
  h="$(gz /css/app.css)"
  grep -qx 'content-encoding: gzip' <<< "$h"
  cache_is "$h" "no-cache"
}

@test "uploaded images are private and immutable" {
  cache_is "$(headers /images/abc.png)" "private, max-age=31536000, immutable"
}

@test "API JSON is compressed" {
  grep -qx 'content-encoding: gzip' <<< "$(gz /api/campaigns)"
}

@test "auth responses are never compressed" {
  h="$(gz /api/auth/login)"
  grep -q '^content-type: application/json' <<< "$h"   # the stub answered: not a vacuous pass
  ! grep -q '^content-encoding:' <<< "$h"
}

@test "SignalR is never compressed" {
  h="$(gz /hubs/notifications)"
  grep -q '^content-type: application/json' <<< "$h"   # the stub answered: not a vacuous pass
  ! grep -q '^content-encoding:' <<< "$h"
}

# Runs last: it changes the served config. git checkout replaces a file (new inode); the reload in
# deploy.sh must still pick the change up, which a single-file bind mount would not allow.
@test "a replaced default.conf is picked up by nginx -s reload" {
  local conf="$BATS_FILE_TMPDIR/ngx/conf.d/default.conf"
  sed '/location = \/index.html {/a\        add_header X-Bats-Reload 1 always;' "$conf" > "$conf.new"
  grep -q X-Bats-Reload "$conf.new"
  chmod a+r "$conf.new"
  mv "$conf.new" "$conf"
  docker exec "$NGX_WEB" nginx -t -q
  docker exec "$NGX_WEB" nginx -s reload
  local i
  for i in $(seq 1 25); do
    headers /index.html | grep -qix 'x-bats-reload: 1' && return 0
    sleep 0.2
  done
  return 1
}
