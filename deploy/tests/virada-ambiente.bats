# tools/virada/ambiente-{producao,ensaio}.sh (sub-projeto H): os ambientes que cada chamada do roteiro da
# virada carrega. Nada de docker nem de rede: HOME e o diretório do ensaio são temporários, e a produção
# nunca é contatada (sem CONTA, prod recusa antes do ssh).
bats_require_minimum_version 1.5.0
REPO="$(cd "$BATS_TEST_DIRNAME/../.." && pwd)"
AMB="$REPO/tools/virada"

setup() {
  export HOME="$BATS_TEST_TMPDIR/home" RUINA_VIRADA_ENSAIO="$BATS_TEST_TMPDIR/ensaio"
  mkdir -p "$HOME"
  unset ALVO
}

@test "executado sem source: recusa com código 2" {
  run -2 bash "$AMB/ambiente-ensaio.sh"
  [[ "$output" == *"com source"* ]]
  run -2 bash "$AMB/ambiente-producao.sh"
}

@test "ensaio depois da produção no mesmo shell: recusa (e vice-versa)" {
  run -1 bash -c ". '$AMB/ambiente-producao.sh' && . '$AMB/ambiente-ensaio.sh'"
  [[ "$output" == *"recusado: este shell já carregou o ambiente 'producao'"* ]]
  run -1 bash -c ". '$AMB/ambiente-ensaio.sh' && . '$AMB/ambiente-producao.sh'"
  [[ "$output" == *"recusado: este shell já carregou o ambiente 'ensaio'"* ]]
  run -1 bash -c ". '$AMB/ambiente-ensaio.sh' && ALVO=producao"
}

@test "carregar duas vezes o mesmo ambiente é permitido e imprime o alvo" {
  run -0 bash -c ". '$AMB/ambiente-ensaio.sh' && . '$AMB/ambiente-ensaio.sh' && echo \"alvo=\$ALVO url=\$URL\""
  [[ "$output" == *"== ALVO=ensaio"* ]]
  [[ "$output" == *"alvo=ensaio url=http://127.0.0.1"* ]]
}

@test "guardar persiste entre chamadas num arquivo 600; a última linha vale" {
  bash -c ". '$AMB/ambiente-ensaio.sh' && guardar MERGE aaa && guardar MERGE bbb && guardar BANCO 20261024T000000Z-banco-150"
  run -0 stat -c %a "$HOME/.virada/estado-ensaio.env"
  [ "$output" = 600 ]
  run -0 stat -c %a "$HOME/.virada"
  [ "$output" = 700 ]
  run -0 bash -c ". '$AMB/ambiente-ensaio.sh' 2>/dev/null && echo \"\$MERGE \$BANCO\""
  [ "$output" = "bbb 20261024T000000Z-banco-150" ]
  # Estados separados: a produção não vê o do ensaio.
  run -0 bash -c ". '$AMB/ambiente-producao.sh' 2>/dev/null && echo \"[\$MERGE]\""
  [ "$output" = "[]" ]
}

@test "guardar recusa variável não prevista e valor vazio ou com caracteres inesperados" {
  run -2 bash -c ". '$AMB/ambiente-ensaio.sh' && guardar SENHA x"
  run -2 bash -c ". '$AMB/ambiente-ensaio.sh' && guardar MERGE ''"
  run -2 bash -c ". '$AMB/ambiente-ensaio.sh' && guardar MERGE 'a b'"
  run -2 bash -c ". '$AMB/ambiente-ensaio.sh' && guardar MERGE 'a\$(id)'"
  [ ! -e "$HOME/.virada/estado-ensaio.env" ]
}

@test "estado com modo errado ou linha estranha: recusa sem executar nada" {
  mkdir -m 700 "$HOME/.virada"
  (umask 077 && printf 'MERGE=$(touch %s/executou)\n' "$BATS_TEST_TMPDIR" > "$HOME/.virada/estado-ensaio.env")
  run -1 bash -c ". '$AMB/ambiente-ensaio.sh'"
  [[ "$output" == *"linha inesperada"* ]]
  [ ! -e "$BATS_TEST_TMPDIR/executou" ]
  printf 'MERGE=abc\n' > "$HOME/.virada/estado-ensaio.env"
  chmod 644 "$HOME/.virada/estado-ensaio.env"
  run -1 bash -c ". '$AMB/ambiente-ensaio.sh'"
  [[ "$output" == *"modo 600"* ]]
}

@test "prod no ensaio roda nesta VM e mostra o comando; passo_longo grava o log com o código" {
  run -0 bash -c ". '$AMB/ambiente-ensaio.sh' && prod 'echo \$RUINA_PACOTE_SEM_ROOT \$RUINA_BACKUP_DIR'"
  [[ "$output" == *"[ensaio] \$ echo"* ]]
  [[ "$output" == *"1 $RUINA_VIRADA_ENSAIO/deploy-backups"* ]]
  run -3 bash -c ". '$AMB/ambiente-ensaio.sh' && passo_longo T1 prod 'echo saída; exit 3'"
  f="$RUINA_VIRADA_ENSAIO/vm-backups/logs/T1.log"
  run -0 cat "$f"
  [[ "$output" == *"saída"* ]]
  [[ "$output" == *"== T1 código 3"* ]]
  run -0 stat -c %a "$f"
  [ "$output" = 600 ]
}

@test "prod na produção sem CONTA guardada recusa antes do ssh" {
  run -2 bash -c ". '$AMB/ambiente-producao.sh' && prod true"
  [[ "$output" == *"CONTA não guardada"* ]]
}
