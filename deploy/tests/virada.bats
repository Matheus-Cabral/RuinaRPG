# tools/virada (sub-projeto H): pacote.sh captura o "pacote da 1.5.0" de uma stack em execução e
# restaurar-pacote.sh o restaura num projeto descartável. A "produção" aqui é uma stack descartável com
# o formato da 1.5.0 (serviços db, api, nginx; volume <projeto>_images), com imagens falsas de api e
# nginx (a do nginx usa o nginx.conf real da 1.5.0). Os testes dependem uns dos outros e rodam em ordem.
bats_require_minimum_version 1.5.0
REPO="$(cd "$BATS_TEST_DIRNAME/../.." && pwd)"
PACOTE="$REPO/tools/virada/pacote.sh"
RESTAURAR="$REPO/tools/virada/restaurar-pacote.sh"

setup_file() {
  export PROD="ruinarpg-bats-virada-$$"
  export REST="ruinarpg-bats-virada-r-$$"
  export PORTA=18091
  export API_IMG="ruinarpg-bats-virada-api:$$" NGINX_IMG="ruinarpg-bats-virada-nginx:$$"
  export GUARDA="ruinarpg-bats-virada-guarda:$$"
  local b="$BATS_FILE_TMPDIR/build"
  mkdir -p "$b/api" "$b/nginx" "$BATS_FILE_TMPDIR/prod"
  docker image inspect busybox:1.36 >/dev/null 2>&1 || docker pull -q busybox:1.36 >/dev/null
  printf '%s\n' 'FROM busybox:1.36' \
    'RUN mkdir -p /www/api && printf %s "\"OK\"" > /www/api/health && cp /www/api/health /www/health' \
    'CMD ["httpd", "-f", "-p", "8080", "-h", "/www"]' > "$b/api/Dockerfile"
  git -C "$REPO" show 729dac9:infra/nginx/nginx.conf > "$b/nginx/nginx.conf"
  printf '%s\n' 'FROM nginx:1.27-alpine' 'COPY nginx.conf /etc/nginx/conf.d/default.conf' \
    'RUN printf "<html>ruina 1.5.0</html>" > /usr/share/nginx/html/index.html' > "$b/nginx/Dockerfile"
  docker build -q -t "$API_IMG" "$b/api" >/dev/null
  docker build -q -t "$NGINX_IMG" "$b/nginx" >/dev/null
  cat > "$BATS_FILE_TMPDIR/prod/docker-compose.yml" <<EOF
services:
  db:
    image: postgres:16-alpine
    environment:
      POSTGRES_DB: ruinarpg
      POSTGRES_USER: ruinarpg
      POSTGRES_PASSWORD: segredo
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -h 127.0.0.1 -U ruinarpg -d ruinarpg"]
      interval: 1s
      retries: 30
  api:
    image: $API_IMG
    depends_on:
      db:
        condition: service_healthy
    volumes:
      - images:/images
  nginx:
    image: $NGINX_IMG
    depends_on:
      - api
    volumes:
      - images:/usr/share/nginx/html/images:ro
volumes:
  pgdata:
  images:
EOF
  # O checkout da "produção" é um repositório git: pacote.sh grava o HEAD dele em dump.tar/sha.
  git -C "$BATS_FILE_TMPDIR/prod" init -q
  git -C "$BATS_FILE_TMPDIR/prod" add docker-compose.yml
  git -C "$BATS_FILE_TMPDIR/prod" -c user.name=bats -c user.email=bats@localhost commit -q -m prod
  export PROD_SHA; PROD_SHA="$(git -C "$BATS_FILE_TMPDIR/prod" rev-parse HEAD)"
  prod_compose up -d --wait
  prod_compose exec -T db psql -q -U ruinarpg -d ruinarpg -v ON_ERROR_STOP=1 -c '
    CREATE TABLE "AspNetUsers"("Id" text PRIMARY KEY, "Email" text);
    CREATE TABLE "Fichas"("Id" int PRIMARY KEY, "Nome" text);
    CREATE TABLE "Log"("Msg" text);
    CREATE TABLE "__EFMigrationsHistory"("MigrationId" text PRIMARY KEY);
    INSERT INTO "AspNetUsers" VALUES ($$u1$$, $$a@x$$), ($$u2$$, $$b@x$$);
    INSERT INTO "Fichas" SELECT g, $$f$$ || g FROM generate_series(1, 25) g;
    INSERT INTO "__EFMigrationsHistory" VALUES ($$m1$$), ($$m2$$), ($$m3$$);'
  head -c 4096 /dev/urandom > "$BATS_FILE_TMPDIR/0001-retrato.png"
  docker run --rm -i -v "${PROD}_images:/images" busybox:1.36 sh -c 'cat > /images/0001-retrato.png && echo zz > /images/zz.png' \
    < "$BATS_FILE_TMPDIR/0001-retrato.png"
  export RUINA_PACOTE_PROJECT="$PROD"
  export RUINA_PACOTE_SEM_ROOT=1
  mkdir -p "$BATS_FILE_TMPDIR/destino"
}

prod_compose() { docker compose -p "$PROD" -f "$BATS_FILE_TMPDIR/prod/docker-compose.yml" "$@"; }

teardown_file() {
  "$RESTAURAR" --remover --projeto "$REST" >/dev/null 2>&1 || true
  "$RESTAURAR" --remover --projeto "$REST-b" >/dev/null 2>&1 || true
  prod_compose down --volumes --remove-orphans >/dev/null 2>&1 || true
  docker rmi -f "$API_IMG" "$NGINX_IMG" "$GUARDA" >/dev/null 2>&1 || true
}

leftovers() {
  local p="$1"
  docker ps -aq --filter "label=com.docker.compose.project=$p"
  docker volume ls -q --filter "label=com.docker.compose.project=$p"
  docker volume ls -q | grep "^${p}_" || true
  docker network ls -q --filter "label=com.docker.compose.project=$p"
}

pacote_dir() { cat "$BATS_FILE_TMPDIR/pacote"; }

@test "pacote.sh exige root" {
  RUINA_PACOTE_SEM_ROOT= run "$PACOTE" "$BATS_TEST_TMPDIR/d"
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"root"* ]]
  [ ! -e "$BATS_TEST_TMPDIR/d" ]
}

@test "pacote.sh falha quando o projeto não está em execução e não deixa nada" {
  RUINA_PACOTE_PROJECT=ruinarpg-bats-inexistente run "$PACOTE" "$BATS_TEST_TMPDIR/d"
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"ruinarpg-bats-inexistente"* ]]
  [ -z "$(ls -A "$BATS_TEST_TMPDIR/d" 2>/dev/null)" ]
}

@test "pacote.sh gera o pacote completo: cinco arquivos, 700/600, SHA256SUMS válido" {
  run --separate-stderr "$PACOTE" "$BATS_FILE_TMPDIR/destino"
  echo "$output"; echo "$stderr"
  [ "$status" -eq 0 ]
  d="$output"
  [[ "$(basename "$d")" =~ ^[0-9]{8}T[0-9]{6}Z-pacote-150$ ]]
  [ "$(dirname "$d")" = "$BATS_FILE_TMPDIR/destino" ]
  echo "$d" > "$BATS_FILE_TMPDIR/pacote"
  [ "$(LC_ALL=C ls -1 "$d" | tr '\n' ' ')" = "SHA256SUMS contagens.tsv dump.tar imagens-docker.tar imagens.tsv " ]
  [ "$(stat -c %a "$d")" = 700 ]
  for f in "$d"/*; do [ "$(stat -c %a "$f")" = 600 ]; done
  (cd "$d" && sha256sum -c --strict SHA256SUMS)
  [ "$(awk '{print $2}' "$d/SHA256SUMS" | tr '\n' ' ')" = "contagens.tsv dump.tar imagens-docker.tar imagens.tsv " ]
  # Nada escondido sobrou no destino.
  [ "$(ls -A "$BATS_FILE_TMPDIR/destino" | wc -l)" -eq 1 ]
  # dump.tar no formato do clone.sh.
  [ "$(tar -tf "$d/dump.tar" | sort | tr '\n' ' ')" = "db.dump images.tar sha " ]
  mkdir "$BATS_TEST_TMPDIR/x" && tar -C "$BATS_TEST_TMPDIR/x" -xf "$d/dump.tar"
  [ "$(head -c 5 "$BATS_TEST_TMPDIR/x/db.dump")" = PGDMP ]
  [ "$(cat "$BATS_TEST_TMPDIR/x/sha")" = "$PROD_SHA" ]
  tar -tf "$BATS_TEST_TMPDIR/x/images.tar" | grep -qx './0001-retrato.png'
  # Contagens exatas, ordenadas pelo nome da tabela.
  [ "$(cat "$d/contagens.tsv")" = "$(printf 'AspNetUsers\t2\nFichas\t25\nLog\t0\n__EFMigrationsHistory\t3')" ]
  # As imagens salvas são as dos contêineres em execução.
  api_id="$(docker inspect -f '{{.Image}}' "$(prod_compose ps -q api)")"
  nginx_id="$(docker inspect -f '{{.Image}}' "$(prod_compose ps -q nginx)")"
  grep -qx "api	$API_IMG	$api_id" "$d/imagens.tsv"
  grep -qx "nginx	$NGINX_IMG	$nginx_id" "$d/imagens.tsv"
  tar -xOf "$d/imagens-docker.tar" manifest.json | grep -qF "$API_IMG"
  tar -xOf "$d/imagens-docker.tar" manifest.json | grep -qF "$NGINX_IMG"
}

@test "pacote.sh --so-banco gera três arquivos, com api e nginx parados" {
  prod_compose stop api nginx
  run --separate-stderr "$PACOTE" --so-banco "$BATS_FILE_TMPDIR/destino"
  prod_compose start api nginx
  echo "$output"; echo "$stderr"
  [ "$status" -eq 0 ]
  d="$output"
  [[ "$(basename "$d")" =~ ^[0-9]{8}T[0-9]{6}Z-banco-150$ ]]
  echo "$d" > "$BATS_FILE_TMPDIR/banco"
  [ "$(LC_ALL=C ls -1 "$d" | tr '\n' ' ')" = "SHA256SUMS contagens.tsv dump.tar " ]
  [ "$(stat -c %a "$d")" = 700 ]
  for f in "$d"/*; do [ "$(stat -c %a "$f")" = 600 ]; done
  (cd "$d" && sha256sum -c --strict SHA256SUMS)
  [ "$(cat "$d/contagens.tsv")" = "$(cat "$(pacote_dir)/contagens.tsv")" ]
}

@test "pacote.sh falha no meio (docker save) sem deixar diretório completo nem .partial" {
  mkdir -p "$BATS_TEST_TMPDIR/bin" "$BATS_TEST_TMPDIR/d"
  real="$(command -v docker)"
  printf '#!/usr/bin/env bash\n[[ "$1" == save ]] && { echo "save quebrado" >&2; exit 1; }\nexec %q "$@"\n' "$real" > "$BATS_TEST_TMPDIR/bin/docker"
  chmod +x "$BATS_TEST_TMPDIR/bin/docker"
  PATH="$BATS_TEST_TMPDIR/bin:$PATH" run "$PACOTE" "$BATS_TEST_TMPDIR/d"
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"save quebrado"* ]]
  [ -z "$(ls -A "$BATS_TEST_TMPDIR/d")" ]
}

@test "pacote.sh recusa quando a imagem em execução não é mais a da tag" {
  old="$(docker image inspect -f '{{.Id}}' "$NGINX_IMG")"
  # Um segundo nome segura a imagem antiga enquanto a tag aponta para a nova (no containerd image store,
  # tirar o último nome apaga o registro da imagem).
  docker tag "$NGINX_IMG" "$GUARDA"
  printf 'FROM %s\nRUN echo outra > /tmp/outra\n' "$NGINX_IMG" | docker build -q -t "$NGINX_IMG" - >/dev/null
  run "$PACOTE" "$BATS_TEST_TMPDIR/d"
  new="$(docker image inspect -f '{{.Id}}' "$NGINX_IMG")"
  docker tag "$GUARDA" "$NGINX_IMG"
  docker rmi "$GUARDA" "$new" >/dev/null
  [ "$(docker image inspect -f '{{.Id}}' "$NGINX_IMG")" = "$old" ]
  echo "$output"
  [ "$status" -ne 0 ]
  [[ "$output" == *"$NGINX_IMG"* ]]
  [ -z "$(ls -A "$BATS_TEST_TMPDIR/d" 2>/dev/null)" ]
}

@test "restaurar-pacote.sh recusa projetos que não são descartáveis" {
  run "$RESTAURAR" "$(pacote_dir)" --projeto ruinarpg --porta "$PORTA"
  echo "$output"
  [ "$status" -eq 2 ]
  [[ "$output" == *"ruinarpg-pacote"* ]]
  run "$RESTAURAR" --remover --projeto ruinarpg
  [ "$status" -eq 2 ]
  run "$RESTAURAR" --remover --projeto ruinarpg-clone
  [ "$status" -eq 2 ]
}

@test "restaurar-pacote.sh falha no checksum de um arquivo corrompido, antes de subir qualquer coisa" {
  c="$BATS_TEST_TMPDIR/corrompido"
  cp -a "$(pacote_dir)" "$c"
  printf 'X' | dd of="$c/dump.tar" bs=1 seek=2000 conv=notrunc status=none
  run "$RESTAURAR" "$c" --projeto "$REST" --porta "$PORTA"
  echo "$output"
  [ "$status" -eq 1 ]
  [[ "$output" == *"SHA256SUMS"* ]]
  [[ "$output" == *"dump.tar"* ]]
  [ -z "$(leftovers "$REST")" ]
  [ ! -e "$c/fingerprint.jsonl" ]
}

@test "restaurar-pacote.sh falha nomeando a tabela quando as contagens divergem" {
  c="$BATS_TEST_TMPDIR/contagem"
  cp -a "$(pacote_dir)" "$c"
  sed -i 's/^Fichas\t25$/Fichas\t26/' "$c/contagens.tsv"
  (cd "$c" && sha256sum contagens.tsv dump.tar imagens-docker.tar imagens.tsv > SHA256SUMS)
  run "$RESTAURAR" "$c" --projeto "$REST" --porta "$PORTA"
  echo "$output"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Fichas"* ]]
  [[ "$output" != *"AspNetUsers"* ]]
  [[ "$output" != *"OK pacote"* ]]
  # Um projeto que já existe é recusado.
  run "$RESTAURAR" "$c" --projeto "$REST" --porta "$PORTA"
  [ "$status" -eq 2 ]
  [[ "$output" == *"--remover"* ]]
  run "$RESTAURAR" --remover --projeto "$REST"
  [ "$status" -eq 0 ]
  [ -z "$(leftovers "$REST")" ]
}

@test "restaurar-pacote.sh restaura um pacote bom e imprime OK pacote" {
  d="$(pacote_dir)"
  run "$RESTAURAR" "$d" --projeto "$REST" --porta "$PORTA"
  echo "$output"
  [ "$status" -eq 0 ]
  [ "${lines[-1]}" = "OK pacote $d" ]
  [ "$(curl -fsS "http://127.0.0.1:$PORTA/api/health")" = '"OK"' ]
  curl -fsS -o "$BATS_TEST_TMPDIR/img" "http://127.0.0.1:$PORTA/images/0001-retrato.png"
  cmp "$BATS_TEST_TMPDIR/img" "$BATS_FILE_TMPDIR/0001-retrato.png"
  [ "$(stat -c %a "$d/fingerprint.jsonl")" = 600 ]
  [ "$(grep -c '"table" : "Fichas"' "$d/fingerprint.jsonl")" -eq 25 ]
  [ "$(grep -c '"table" : "AspNetUsers"' "$d/fingerprint.jsonl")" -eq 2 ]
  # O pacote continua íntegro (o fingerprint não entra no SHA256SUMS).
  (cd "$d" && sha256sum -c --strict --quiet SHA256SUMS)
  # A produção de mentira não foi tocada.
  [ "$(prod_compose ps -q --status running | wc -l)" -eq 3 ]
  run "$RESTAURAR" --remover --projeto "$REST"
  [ "$status" -eq 0 ]
  [ -z "$(leftovers "$REST")" ]
  [ "$(prod_compose ps -q --status running | wc -l)" -eq 3 ]
}

@test "restaurar-pacote.sh restaura um pacote --so-banco (só banco e volume)" {
  d="$(cat "$BATS_FILE_TMPDIR/banco")"
  run "$RESTAURAR" "$d" --projeto "$REST-b" --porta "$PORTA"
  echo "$output"
  [ "$status" -eq 0 ]
  [[ "${lines[-1]}" == "OK pacote $d (só banco"* ]]
  [ "$(grep -c '"table" : "Fichas"' "$d/fingerprint.jsonl")" -eq 25 ]
  run "$RESTAURAR" --remover --projeto "$REST-b"
  [ "$status" -eq 0 ]
  [ -z "$(leftovers "$REST-b")" ]
}
