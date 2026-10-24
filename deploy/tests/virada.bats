# tools/virada (sub-projeto H): pacote.sh captura o "pacote da 1.5.0" de uma stack em execução e
# restaurar-pacote.sh o restaura num projeto descartável. A "produção" aqui é uma stack descartável com
# o formato da 1.5.0 (serviços db, api, nginx; volume <projeto>_images), com imagens falsas de api e
# nginx (a do nginx usa o nginx.conf real da 1.5.0). Os testes dependem uns dos outros e rodam em ordem.
bats_require_minimum_version 1.5.0
REPO="$(cd "$BATS_TEST_DIRNAME/../.." && pwd)"
PACOTE="$REPO/tools/virada/pacote.sh"
RESTAURAR="$REPO/tools/virada/restaurar-pacote.sh"
ENSAIO="$REPO/tools/virada/ensaio-150.sh"

setup_file() {
  export PROD="ruinarpg-bats-virada-$$"
  export REST="ruinarpg-bats-virada-r-$$"
  export PORTA=18091
  export API_IMG="ruinarpg-bats-virada-api:$$" NGINX_IMG="ruinarpg-bats-virada-nginx:$$"
  export GUARDA="ruinarpg-bats-virada-guarda:$$"
  # ensaio-150.sh: projeto descartável, porta alta, checkout com o nome do projeto, .env de mentira.
  export ENS="ruinarpg-bats-ensaio-$$" ENS_PORTA=18093
  export ENS_DIR="$BATS_FILE_TMPDIR/$ENS" ENS_SEGREDO="segredo-do-ensaio-$$-0123456789abcdef0123456789"
  export RUINA_ENSAIO_PROJECT="$ENS" RUINA_ENSAIO_PORTA="$ENS_PORTA" RUINA_ENSAIO_ENV="$BATS_FILE_TMPDIR/ensaio.env"
  printf '%s\n' 'ASPNETCORE_ENVIRONMENT=Development' 'POSTGRES_USER=ensaio' 'POSTGRES_PASSWORD=senha-ensaio' \
    "JWT_SIGNING_KEY=$ENS_SEGREDO" 'JWT_ISSUER=RuinaRPG' 'JWT_AUDIENCE=RuinaRPG' 'JWT_ACCESS_TOKEN_MINUTES=15' \
    'JWT_REFRESH_TOKEN_DAYS=30' 'CORS_ALLOWED_ORIGINS=http://localhost' 'IMG_MAX_SIZE_MB=10' \
    'INVITE_CODE_EXPIRATION_HOURS=48' > "$RUINA_ENSAIO_ENV"
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
    INSERT INTO "__EFMigrationsHistory" VALUES ($$m1$$), ($$m2$$), ($$m3$$);
    CREATE ROLE leitor_so_na_producao;
    GRANT SELECT ON "Fichas" TO leitor_so_na_producao;'
  head -c 4096 /dev/urandom > "$BATS_FILE_TMPDIR/0001-retrato.png"
  docker run --rm -i -v "${PROD}_images:/images" busybox:1.36 sh -c 'cat > /images/0001-retrato.png && echo zz > /images/zz.png' \
    < "$BATS_FILE_TMPDIR/0001-retrato.png"
  export RUINA_PACOTE_PROJECT="$PROD"
  export RUINA_PACOTE_SEM_ROOT=1
  mkdir -p "$BATS_FILE_TMPDIR/destino"
}

prod_compose() { docker compose -p "$PROD" -f "$BATS_FILE_TMPDIR/prod/docker-compose.yml" "$@"; }

teardown_file() {
  "$ENSAIO" desfazer "$ENS_DIR" --remover-stack-atual >/dev/null 2>&1 || true
  docker compose -p "$ENS" -f "$BATS_FILE_TMPDIR/atual.yml" down --volumes >/dev/null 2>&1 || true
  git -C "$REPO" worktree remove --force "$ENS_DIR" >/dev/null 2>&1 || true
  git -C "$REPO" worktree prune || true
  docker rmi "$ENS-api" "$ENS-nginx" >/dev/null 2>&1 || true
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

@test "pacote.sh morto por SIGPIPE (stderr fechado) não deixa .partial" {
  mkdir -p "$BATS_TEST_TMPDIR/d"
  # O leitor do stderr sai depois de "gerando" (o .partial já existe): a próxima mensagem de progresso
  # recebe SIGPIPE.
  run bash -c '"$1" "$2" 2>&1 >/dev/null | grep -q -m1 gerando; exit "${PIPESTATUS[0]}"' _ "$PACOTE" "$BATS_TEST_TMPDIR/d"
  echo "status $status"
  [ "$status" -eq 141 ]
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
  # Sem "_": o prefixo "<projeto>_" dos volumes não pode casar com outro projeto.
  run "$RESTAURAR" --remover --projeto ruinarpg-bats_x
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

# Uma "stack atual" qualquer no projeto do ensaio (como a 2.0.0 de desenvolvimento em ruinarpg).
atual_compose() { docker compose -p "$ENS" -f "$BATS_FILE_TMPDIR/atual.yml" "$@"; }

@test "ensaio-150.sh recusa nome de projeto, diretório e pacote errados" {
  RUINA_ENSAIO_PROJECT=ruinarpg-bats_x run "$ENSAIO" preparar "$(pacote_dir)" "$BATS_TEST_TMPDIR/ruinarpg-bats_x"
  [ "$status" -eq 2 ]
  RUINA_ENSAIO_PROJECT=outra run "$ENSAIO" desfazer "$BATS_TEST_TMPDIR/outra"
  [ "$status" -eq 2 ]
  # O diretório tem de se chamar como o projeto (o Makefile da 1.5.0 roda "docker compose" sem -p).
  run "$ENSAIO" preparar "$(pacote_dir)" "$BATS_TEST_TMPDIR/RuinaRPG"
  echo "$output"
  [ "$status" -eq 2 ]
  [[ "$output" == *"$ENS"* ]]
  run "$ENSAIO" preparar "$(cat "$BATS_FILE_TMPDIR/banco")" "$ENS_DIR"
  echo "$output"
  [ "$status" -eq 2 ]
  [[ "$output" == *"completo"* ]]
  [ ! -e "$ENS_DIR" ]
  [ -z "$(leftovers "$ENS")" ]
}

@test "ensaio-150.sh recusa uma stack existente com o mesmo nome sem --remover-stack-atual" {
  printf '%s\n' 'services:' '  web:' '    image: busybox:1.36' '    command: ["sleep", "3600"]' \
    '    volumes: ["velho:/dados"]' 'volumes:' '  velho:' > "$BATS_FILE_TMPDIR/atual.yml"
  atual_compose up -d
  run "$ENSAIO" preparar "$(pacote_dir)" "$ENS_DIR"
  echo "$output"
  [ "$status" -eq 2 ]
  [[ "$output" == *"--remover-stack-atual"* ]]
  [ "$(atual_compose ps -q --status running | wc -l)" -eq 1 ]
  docker volume inspect "${ENS}_velho" >/dev/null
  [ ! -e "$ENS_DIR" ]
}

@test "ensaio-150.sh preparar com --remover-stack-atual sobe a 1.5.0 no formato da produção" {
  d="$(pacote_dir)"
  run --separate-stderr "$ENSAIO" preparar "$d" "$ENS_DIR" --remover-stack-atual
  echo "$output"; echo "$stderr"
  [ "$status" -eq 0 ]
  [ "$output" = "OK ensaio $ENS em $ENS_DIR (porta $ENS_PORTA)" ]
  # O segredo do .env nunca aparece.
  [[ "$output$stderr" != *"$ENS_SEGREDO"* ]]
  # A stack atual sumiu, com o volume.
  [ -z "$(docker ps -aq --filter "label=com.docker.compose.project=$ENS" --filter "label=com.docker.compose.service=web")" ]
  ! docker volume inspect "${ENS}_velho" >/dev/null 2>&1
  # Checkout: worktree da 1.5.0, .env 600 em Production com os valores do .env de origem.
  [ "$(git -C "$ENS_DIR" rev-parse HEAD)" = "$(git -C "$REPO" rev-parse 729dac9)" ]
  git -C "$REPO" worktree list --porcelain | grep -qxF "worktree $ENS_DIR"
  [ "$(stat -c %a "$ENS_DIR/.env")" = 600 ]
  [ "$(grep -c '^ASPNETCORE_ENVIRONMENT=' "$ENS_DIR/.env")" -eq 1 ]
  grep -qx 'ASPNETCORE_ENVIRONMENT=Production' "$ENS_DIR/.env"
  grep -qx "JWT_SIGNING_KEY=$ENS_SEGREDO" "$ENS_DIR/.env"
  [ "$(cat "$ENS_DIR/.ensaio-150")" = "$ENS" ]
  # Os comandos do runbook funcionam ao pé da letra no checkout: compose sem -p acha o projeto.
  [ "$(cd "$ENS_DIR" && docker compose ps --services --status running | sort | tr '\n' ' ')" = "api db nginx " ]
  [ "$(docker inspect -f '{{.State.Status}}' "$ENS-nginx-1" "$ENS-api-1" "$ENS-db-1" | sort -u)" = running ]
  docker inspect -f '{{range .Config.Env}}{{println .}}{{end}}' "$ENS-api-1" | grep -qx 'ASPNETCORE_ENVIRONMENT=Production'
  # Imagens com os nomes que o compose da 1.5.0 deriva, apontando para as do pacote.
  [ "$(docker image inspect -f '{{.Id}}' "$ENS-api")" = "$(awk -F'\t' '$1=="api"{print $3}' "$d/imagens.tsv")" ]
  [ "$(docker image inspect -f '{{.Id}}' "$ENS-nginx")" = "$(awk -F'\t' '$1=="nginx"{print $3}' "$d/imagens.tsv")" ]
  # Porta, api e volume de imagens.
  [ "$(curl -fsS "http://127.0.0.1:$ENS_PORTA/api/health")" = '"OK"' ]
  curl -fsS -o "$BATS_TEST_TMPDIR/img" "http://127.0.0.1:$ENS_PORTA/images/0001-retrato.png"
  cmp "$BATS_TEST_TMPDIR/img" "$BATS_FILE_TMPDIR/0001-retrato.png"
  # Banco: usuário do .env, banco ruinarpg, contagens do pacote.
  docker exec -i "$ENS-db-1" psql -X -q -At -U ensaio -d ruinarpg < "$REPO/tools/virada/contagens.sql" > "$BATS_TEST_TMPDIR/c.tsv"
  cmp "$BATS_TEST_TMPDIR/c.tsv" "$d/contagens.tsv"
  # O pacote continua íntegro e nada temporário ficou nele.
  (cd "$d" && sha256sum -c --strict --quiet SHA256SUMS)
  [ -z "$(find "$d" -name '.ensaio.*')" ]
  # Um checkout que já existe é recusado.
  run "$ENSAIO" preparar "$d" "$ENS_DIR" --remover-stack-atual
  [ "$status" -eq 2 ]
  [[ "$output" == *"desfazer"* ]]
}

@test "ensaio-150.sh desfazer remove a stack e o worktree e mantém as imagens" {
  run "$ENSAIO" desfazer "$ENS_DIR"
  echo "$output"
  [ "$status" -eq 0 ]
  [ "${lines[-1]}" = "OK desfeito $ENS" ]
  [ -z "$(leftovers "$ENS")" ]
  [ ! -e "$ENS_DIR" ]
  ! git -C "$REPO" worktree list --porcelain | grep -qxF "worktree $ENS_DIR"
  docker image inspect "$ENS-api" "$ENS-nginx" >/dev/null
  # Um segundo desfazer recusa e não apaga nada (um volume que aparecer no projeto sobrevive).
  docker volume create "${ENS}_pgdata" >/dev/null
  run "$ENSAIO" desfazer "$ENS_DIR"
  echo "$output"
  [ "$status" -eq 2 ]
  docker volume inspect "${ENS}_pgdata" >/dev/null
  docker volume rm "${ENS}_pgdata" >/dev/null
}

@test "ensaio-150.sh desfazer sem a marca do preparar recusa e não apaga os volumes do projeto" {
  # Como a stack de desenvolvimento derrubada: só volumes, nenhum contêiner.
  docker volume create "${ENS}_pgdata" >/dev/null
  mkdir -p "$BATS_TEST_TMPDIR/outro"
  run "$ENSAIO" desfazer "$BATS_TEST_TMPDIR/outro/$ENS"
  echo "$output"
  [ "$status" -eq 2 ]
  docker volume inspect "${ENS}_pgdata" >/dev/null
  # Um worktree deste repositório que não foi criado por preparar (sem a marca) também é recusado.
  git -C "$REPO" worktree add -q --detach "$ENS_DIR" 729dac9
  run "$ENSAIO" desfazer "$ENS_DIR"
  echo "$output"
  [ "$status" -eq 2 ]
  [[ "$output" == *"--remover-stack-atual"* ]]
  docker volume inspect "${ENS}_pgdata" >/dev/null
  [ -d "$ENS_DIR" ]
  git -C "$REPO" worktree remove --force "$ENS_DIR"
  docker volume rm "${ENS}_pgdata" >/dev/null
}

@test "ensaio-150.sh preparar que falha indica o desfazer e o desfazer limpa" {
  # Um pacote cujas contagens não batem com o banco: falha depois de criar checkout e stack.
  c="$BATS_TEST_TMPDIR/contagem"
  cp -a "$(pacote_dir)" "$c"
  sed -i 's/^Fichas\t25$/Fichas\t26/' "$c/contagens.tsv"
  (cd "$c" && sha256sum contagens.tsv dump.tar imagens-docker.tar imagens.tsv > SHA256SUMS)
  run "$ENSAIO" preparar "$c" "$ENS_DIR"
  echo "$output"
  [ "$status" -eq 1 ]
  [[ "$output" == *"Fichas"* ]]
  [[ "$output" == *"desfazer $ENS_DIR"* ]]
  [ -z "$(find "$c" -name '.ensaio.*')" ]
  run "$ENSAIO" desfazer "$ENS_DIR"
  echo "$output"
  [ "$status" -eq 0 ]
  [ -z "$(leftovers "$ENS")" ]
  [ ! -e "$ENS_DIR" ]
}

@test "ensaio-150.sh desfazer recusa um projeto que não veio do checkout sem --remover-stack-atual" {
  atual_compose up -d
  run "$ENSAIO" desfazer "$ENS_DIR"
  echo "$output"
  [ "$status" -eq 2 ]
  [[ "$output" == *"--remover-stack-atual"* ]]
  [ "$(atual_compose ps -q --status running | wc -l)" -eq 1 ]
  run "$ENSAIO" desfazer "$ENS_DIR" --remover-stack-atual
  [ "$status" -eq 0 ]
  [ -z "$(leftovers "$ENS")" ]
}
