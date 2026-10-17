# Provisionamento do pipeline (2.0.0)

Passos manuais, feitos **uma vez** pelo dono do projeto. Nenhum script daqui roda sozinho: o pipeline
nao provisiona a si mesmo. Cada passo tem uma linha **Verificacao**; so siga adiante quando ela bater.

## Ordem

1. [`producao.md`](producao.md), parte A (producao `192.168.80.10`): usuario `deploy`, gate, sudoers.
2. [`runner.md`](runner.md) (esta VM, `192.168.80.16`): usuario `ghrunner`, runner, chave de dump, limpeza.
3. [`producao.md`](producao.md), parte B: autorizar a chave de dump; parte C: preparar o primeiro ensaio.
4. [`github.md`](github.md): Environment `production`, segredos, protecao da `main`, label.

## Duas chaves SSH

| Chave | Onde vive | `authorized_keys` em producao | Pode |
|---|---|---|---|
| dump (`~ghrunner/.ssh/ruinarpg_dump`) | disco do runner | `command="sudo /opt/ruinarpg/gate.sh dump-only",...` | `dump`, `status` |
| deploy | **somente** no segredo `DEPLOY_SSH_KEY` do Environment `production` | `command="sudo /opt/ruinarpg/gate.sh",...` | `dump`, `status`, `deploy <sha da main>` |

O codigo de PR roda no disco do runner e por isso so alcanca a chave de dump, que nunca faz deploy.

## Arquivos

- `install-gate.sh`, `sudoers-ruinarpg`: producao (gate em `/opt/ruinarpg/gate.sh`).
- `setup-runner.sh`, `runner-cleanup.sh`: runner.
- Verificacao estatica: `deploy/tests/run-bats.sh deploy/tests/provision.bats`.

## Riscos aceitos

- O codigo de um PR sempre enxerga os dados de producao dentro do clone. O gate do `release.yml` limita isso aos PRs do proprio dono; revise as dependencias novas.
- O job de deploy roda no mesmo runner e no mesmo usuario do SO que o codigo de PR: o que um PR deixou para tras poderia usar o socket do agente durante a janela do deploy. O impacto e limitado pelo gate (`deploy <sha que ja esta na main>`).
- O root de producao executa o `deploy.sh` do repositorio, gravavel por `ruina` (que ja e equivalente a root pelo grupo docker).
- A chave de deploy pode implantar qualquer ancestral da `main` (downgrade); o smoke e o rollback limitam o dano.
- Nesta VM de desenvolvimento o grupo `docker` do `ghrunner` equivale a root. Aceito: o runner precisa dirigir o clone.
