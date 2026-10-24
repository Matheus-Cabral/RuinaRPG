# Registro da virada da produção: 1.5.0 → 2.0.0 (sub-projeto H)

Registro de cada passo do roteiro [`2026-10-23-h-roteiro-da-virada.md`](2026-10-23-h-roteiro-da-virada.md) (mesma numeração). Preencha na hora,
um bloco por execução (repita o bloco se o passo rodar de novo; no ensaio, um registro por ensaio, com
"Ensaio n" no título da seção). **Nunca** cole aqui senha, conteúdo do `.env`, pks de amostra, e-mails ou
outro dado pessoal: só comandos, caminhos, shas, contagens e as linhas `OK`/`RESULT`/`Resultado`.

| Campo | Valor |
|---|---|
| Execução | produção / ensaio n |
| Operador | |
| Conta na produção | |
| Pacote (`$PACOTE`) | |
| Dump final (`$BANCO`) | |
| sha do merge (`$MERGE`) | |
| Início da janela (J1) | |
| Fim da janela (V6) | |
| Tempo fora do ar | |

## 0. Acesso

### 0.1 Chave e arquivo da senha (ssh-ok / sudo-ok)

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

## 1. Preflight (O1)

### P1 Docker, compose, git

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### P2 Volumes

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### P3 Contêineres, nomes e imagens

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### P4 Volume de imagens e ambiente do db

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### P5 .env

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### P6 sha implantado e checkout

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### P7 Histórico de migrations

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### P8 Contagens de referência

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### P9 Espaço em disco

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

## 2. Pacote da 1.5.0 (O1)

### K1 Ferramentas na produção

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### K2 Pacote completo

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### K3 Checksums nos dois lados

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### K4 Restauração de teste

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### K5 Desmontar a restauração

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

## 3. Ensaio (O2)

### E1+E2 Rodada 1 do clone + verificar-baseline

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### E1+E2 Rodada 2

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### E1+E2 Rodada 3

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### E3 Stack da 1.5.0 do ensaio

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### E4 Roteiro completo (janela, verificação, volta, janela) — tempo fora do ar

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### E5 Desmontar e devolver a stack de desenvolvimento

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

## 4. Preparação da produção (O3)

### R1 Provisionamento (producao.md 0/A, runner.md, producao.md B/C, github.md)

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### R2 Required reviewers

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### R3 Allowlist vazia e passo 12

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### R4 Texto do ChangelogDialog aprovado

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### R5 Retag das imagens da 1.5.0

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### R6 Pré-build e pré-pull

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### R7 PR da virada (sha do merge)

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

## 5. Janela (O4)

### J1 Parar nginx e api da 1.5.0 (início da janela)

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### J2 Dump final

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### J3 Checksums nos dois lados

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### J4 Restauração do dump final e fingerprint

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### J5 Checkout do merge

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### J6 Reinstalar o gate

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### J7 Deploy à mão

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### J8 Rejeitar a aprovação do deploy.yml

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

## 6. Verificação (O4)

### V1 Estado das cores

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### V2 Versão, saúde, nginx

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### V3 Cabeçalhos pelo Cloudflare

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### V4 Fingerprint: diferença zero

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### V5 Conferência manual do usuário

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### V6 Fim da janela (tempo fora do ar)

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

## 7. Volta para a 1.5.0 (só se houver perda de dados)

### X1 Parar a 2.0.0

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### X2 Restaurar o dump final

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### X3 Checkout da 1.5.0

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### X4 Religar a 1.5.0

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

## 8. Depois (O5)

### D1 Período de observação

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### D2 Remover os Required reviewers

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### D3 Backlog 2.2.0

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### D4 Limpar o aquecimento

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### D5 Recuperação de linhas (se preciso)

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**

### D6 Fim do H: senha apagada, chave revogada

- **Comando:**
- **Saída relevante:**
- **Resultado:** passou / falhou
- **Quem aprovou:**
- **Horário (UTC):**
