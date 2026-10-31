# Remover jogador e contas — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** O GM remove um jogador de uma campanha, desativa, reativa e exclui a conta dele; as fichas vão ao cemitério, as concessões voltam ao GM (que pode alterar o dono) e o jogador sai dos encontros ativos.

**Architecture:** Regras puras no Domain (`ContaDeJogador`, `Cemiterio.Motivos` com o estado da conta). Na Api, um único serviço `SaidaDoJogador` faz a saída (vínculos, concessões, encontros) e é usado por remover membro, desativar e excluir; a remoção de participante de encontro é extraída do controller para `RemocaoDeParticipante` e reaproveitada. "Alterar dono" é um controller genérico sobre `ITipoDeFicha<TSheet>` para NPC e Criatura. O estado da conta é uma coluna de `ApplicationUser`, conferida no login, no refresh e em cada token validado.

**Tech Stack:** .NET 8, ASP.NET Core + Identity, EF Core/PostgreSQL, SignalR, Blazor WASM + MudBlazor 9.9, xUnit + bUnit + Testcontainers.

**Spec:** `docs/superpowers/archive/specs/2026-10-29-versao-2-1-0-remover-jogador-e-contas-design.md` (ler inteiro, em especial a seção **Estrutura**). Base: `2026-10-24-versao-2-1-0-cemiterio-design.md` (já implementado).

## Global Constraints

- Branch `release/2.1.0` (o PR #68 está **fechado** de propósito: não reabrir). Commit + push a cada tarefa.
- TDD obrigatório; `dotnet build` com 0 warnings; projetos de teste **um de cada vez**, conferindo antes com `pgrep -fa "dotnet test"` que nada mais está rodando.
- **Estrutura da 2.0.0 (pedido do usuário, ver seção Estrutura da spec):** regras puras em `src/RuinaRPG.Domain`; a saída do jogador num só serviço; remoção de participante extraída, nunca copiada; "alterar dono" genérico em `Controllers/Fichas/` com um record em `Contracts/Fichas/` e testes genéricos por tipo; cliente sem ramos "se NPC/se Criatura" (diferenças via `TipoDeFichaCliente`); textos compartilhados servidor/cliente em `Contracts`. Se algo não couber, **parar e reportar**.
- Só contas de papel Jogador têm estado; quem gere a conta é só o GM de `InvitedByGmId` (outro GM → 404).
- Textos exatos:
  - login de conta desativada: `Esta conta está desativada. Fale com o seu GM.`
  - motivos (enum → rótulo): `ContaDesativada` → `Conta do jogador desativada`; `ContaExcluida` → `Conta do jogador excluída` (o antigo `ContaInativa`, nunca produzido, deixa de existir)
  - ações: `Remover da campanha`, `Alterar dono`, `Desativar`, `Reativar`, `Excluir`; seção `Desativados`
- Migration só acrescenta (coluna de estado em `AspNetUsers`, padrão Ativa); `rotas-da-api.txt` muda só pelas rotas novas, explicado no commit.
- Remover/desativar/excluir: uma transação; tudo ou nada.
- Ícones MudBlazor, nunca emoji.

## Review Focus

1. Remover um jogador cujo participante **tem a vez** num encontro em andamento, e que está em dois encontros: a vez passa como no R0008, cada encontro registra a saída, nada quebra. (Task 4)
2. Conta desativada com um token de acesso ainda válido (até 15 min) chamando a API e o hub → 401 já na próxima requisição. (Task 6)
3. Senha errada numa conta desativada → `Credenciais inválidas.` (não revela o estado); senha certa → texto de conta desativada. (Task 6)
4. Conta excluída: o mesmo e-mail se cadastra de novo com sucesso; o mesmo apelido é recusado (reservado). (Task 5)
5. Alterar dono para quem não é membro ativo da campanha, para jogador de outro GM, ou feito por um jogador → recusado; para `null` → o jogador perde o acesso à ficha. (Task 3)

---

### Task 1: Requisitos

**Files:** `Docs/Requisitos/` — Campanha (novos: remover membro; alterar dono; R0003 só ativos; R0010; R0011 notas ficam; R0017 motivos de conta), Convite de Jogador (novos: desativar, reativar, excluir; R0006/R0007 com estados), Login e Cadastro (login recusado; e-mail livre; apelido reservado), Ficha de Personagem R0010 (motivos de conta), Gerenciador de Encontros (remoção de participantes pela saída do jogador), Modelo de Dados (estado da conta), 01 - Visão Geral (página de jogadores com Desativados).

- [ ] **Step 1:** Escrever conforme a spec (seções Decisões e Comportamento), continuando a numeração de cada arquivo, `[[wikilinks]]` cruzados, textos exatos das Global Constraints.
- [ ] **Step 2:** `python3 tools/docs/verificar-wikilinks.py` → `0 wikilink(s) sem destino`.
- [ ] **Step 3:** Commit `docs(contas): requisitos` e push.

### Task 2: Regras no Domain

**Files:**
- Create: `src/RuinaRPG.Domain/Auth/EstadoDaConta.cs`, `src/RuinaRPG.Domain/Auth/ContaDeJogador.cs`
- Modify: `src/RuinaRPG.Domain/CharacterSheets/MotivoDoCemiterio.cs`, `Cemiterio.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Auth/ContaDeJogadorTests.cs`, `tests/RuinaRPG.Tests.Unit/CharacterSheets/CemiterioTests.cs`

**Interfaces (Produces):**
- `public enum EstadoDaConta { Ativa, Desativada, Excluida }`
- `public enum AcaoDeConta { Desativar, Reativar, Excluir }`
- `public static class ContaDeJogador { public static bool PodeGerir(Guid gmId, Guid? invitedByGmId); public static EstadoDaConta? Aplicar(EstadoDaConta atual, AcaoDeConta acao); }` — `Aplicar` devolve `null` para transição inválida. Válidas: Ativa+Desativar→Desativada; Desativada+Reativar→Ativa; Ativa|Desativada+Excluir→Excluida.
- `MotivoDoCemiterio { Morte, RemovidoDaCampanha, ContaDesativada, ContaExcluida }`
- `Cemiterio.Motivos(bool morto, bool donoEhMembro, EstadoDaConta estadoDoDono)` — ordem fixa: Morte, RemovidoDaCampanha, ContaDesativada/ContaExcluida.

- [ ] **Step 1: Testes falhando:** `PodeGerir_so_o_GM_que_convidou` (igual → true; outro GM → false; `null` → false); `Transicoes_validas` (as três); `Transicoes_invalidas_devolvem_null` (Ativa+Reativar, Desativada+Desativar, Excluida + qualquer); `Conta_desativada_da_motivo` e `Conta_excluida_da_motivo`; `Ordem_fixa_com_todos_os_motivos` (`Motivos(true,false,Desativada)` = `[Morte, RemovidoDaCampanha, ContaDesativada]`); os testes antigos de `Cemiterio` passam com `EstadoDaConta.Ativa`.
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Unit --filter "ContaDeJogadorTests|CemiterioTests"` → FAIL.
- [ ] **Step 3:** Implementar; atualizar os chamadores de `Cemiterio.Motivos` (por ora com `EstadoDaConta.Ativa` onde o estado ainda não é lido — a Task 3 liga o estado real) para o build ficar verde.
- [ ] **Step 4:** Mesmo filtro → PASS; `dotnet build` → 0 warnings.
- [ ] **Step 5:** Commit `feat(contas): regras de conta no domínio` e push.

### Task 3: Estado da conta no banco, motivos de conta no cemitério e "Alterar dono"

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs` (`public EstadoDaConta Estado { get; set; } = EstadoDaConta.Ativa;`); migration `EstadoDaConta` (só `AddColumn`, padrão Ativa; mapeamento no mesmo estilo dos outros enums do `RuinaRpgDbContext`). Lembrar o aviso do ledger anterior: o `ModelSnapshot` tem nomes antigos — **antes** de gerar a migration, aceitar o snapshot regenerado num commit próprio (`chore(migrations): snapshot alinhado ao modelo atual`, sem operações; `MigrationTests` deve continuar verde) e tornar genérico o `DROP COLUMN "Morto"` de `DatabaseInitializerTests` (desfazer todas as colunas posteriores ao baseline, não só `Morto`).
- Modify: `src/RuinaRPG.Api/Services/CemiterioDaFicha.cs` (lê o `Estado` do dono, no lote também)
- Modify: `src/RuinaRPG.Client/Shared/Ficha/FichaEstado.cs` (`RotuloDoMotivo` com os dois rótulos novos)
- Create: `src/RuinaRPG.Infrastructure/Fichas/IFichaConcedivel.cs` — `Guid GmId { get; }`, `Guid? OwnerId { get; set; }`, implementada por `NpcSheet` e `CreatureSheet`
- Create: `src/RuinaRPG.Contracts/Fichas/AlterarDonoRequest.cs` — `public record AlterarDonoRequest(string? DonoId);`
- Create: `src/RuinaRPG.Api/Controllers/Fichas/DonoDaFichaController.cs` (abstrato, `TSheet : class, IFicha, IFichaConcedivel`) + subclasses por tipo com `[Route("api/npc-sheets/{sheetId}")]` e `[Route("api/creature-sheets/{sheetId}")]`, no padrão de `AfeicoesDaFichaController`/`NpcAffectionsController`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CemiterioTests.cs` (motivos de conta); `tests/RuinaRPG.Tests.Integration/Controllers/Fichas/DonoDaFichaTestes.cs` (abstrata + uma subclasse por tipo, padrão de `FichaDeNpcOuCriaturaTestes`); `tests/RuinaRPG.Tests.Client/...` (rótulos)

**Interfaces (Produces):** `PUT /api/{npc-sheets|creature-sheets}/{sheetId}/dono` (`[Authorize(Roles="GM")]`, corpo `AlterarDonoRequest`), 204. Regras: ficha do GM chamador (senão 404); `DonoId` `null` → `OwnerId = null`; senão o jogador deve ser **membro** de uma campanha do GM à qual a ficha está anexada **e** ter `Estado == Ativa` (senão 400 `O jogador informado não é membro ativo de uma campanha desta ficha.`).

- [ ] **Step 1: Testes falhando:** `Ficha_de_conta_desativada_mostra_o_motivo` (marcar `Estado` direto no banco → `MotivosDoCemiterio` contém `ContaDesativada`); idem `ContaExcluida`; em `DonoDaFichaTestes`: `GM_passa_a_ficha_para_outro_membro` (novo dono lê a ficha 200; antigo recebe 404); `GM_devolve_a_ficha_a_si_mesmo` (`DonoId=null` → antigo dono 404, ficha some de "Meus Companheiros" dele); `Dono_que_nao_e_membro_ativo_e_recusado` (não-membro; membro com conta desativada; jogador de outro GM) → 400 com o texto; `Jogador_nao_altera_dono` → 403; `Outro_GM_recebe_404`; cliente: `RotuloDoMotivo` dos dois novos.
- [ ] **Step 2:** Rodar os filtros (`CemiterioTests|DonoDaFicha` no Integration; o teste de rótulo no Client) → FAIL.
- [ ] **Step 3:** Implementar na ordem: commit do snapshot; migration; `CemiterioDaFicha`; interface + controller genérico; rótulos.
- [ ] **Step 4:** Filtros → PASS; `MigrationTests`, `DatabaseInitializerTests`, `ConsolidacaoDoHistoricoTests`, `RotasDaApiTests` (só as duas linhas novas de rota) → PASS.
- [ ] **Step 5:** Commits `chore(migrations): …` e `feat(contas): estado da conta e Alterar dono`; push.

### Task 4: Saída do jogador e remover da campanha

**Files:**
- Create: `src/RuinaRPG.Api/Services/RemocaoDeParticipante.cs` — extraído do corpo de `EncounterParticipantsController.Delete` (log `ParticipanteRemovido`, passagem da vez com `TurnAdvanceCalculator.AfterRemoval`/`EncounterTurnos.DarAVezAsync`, Condições, `AoMudarAVezAsync`, `AoMudarAtivacaoAsync`). Assinatura: `Task RemoverAsync(Encounter encounter, Guid participantId)` — **não** chama `SaveChanges` nem `NotificarAsync` (quem chama faz). O controller passa a usá-lo; os testes existentes de remover participante continuam verdes sem mudança.
- Create: `src/RuinaRPG.Api/Services/SaidaDoJogador.cs` — `Task SairAsync(Guid jogadorId, IReadOnlyCollection<Guid> campanhaIds)`: para cada campanha, apaga o `CampaignMember`; concessões do jogador anexadas a ela e **não** anexadas a outra campanha em que ele continua membro → `OwnerId = null`; remove dos encontros **em preparação ou em andamento** dessas campanhas os participantes vindos de fichas de Personagem dele ou dessas concessões (via `RemocaoDeParticipante`). Sem `SaveChanges` (quem chama faz, numa transação).
- Modify: `src/RuinaRPG.Api/Controllers/CampaignsController.cs` — `DELETE /api/campaigns/{campaignId}/members/{userId}` (GM dono da campanha; não-membro → 404); transação; `SaveChanges`; depois `NotificarAsync` dos encontros, `ParticipantsChanged` aos grupos afetados e `NotificationEvents.CampanhasDoJogadorAlteradas` ao jogador.
- Modify: `src/RuinaRPG.Contracts/Notifications/NotificationEvents.cs` (`CampanhasDoJogadorAlteradas`)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/RemoverMembroTests.cs`; hub em `tests/RuinaRPG.Tests.Integration/Hubs/`

- [ ] **Step 1: Testes falhando:** `Remover_membro_poe_as_fichas_no_cemiterio` (motivo `RemovidoDaCampanha`; dono ainda lê a ficha); `Concessoes_voltam_ao_GM` (NPC e Criatura concedidos → `OwnerId` nulo; seguem anexados e privados; jogador recebe 404 nelas); `Concessao_anexada_a_outra_campanha_dele_nao_muda`; `Sai_dos_encontros_ativos_e_fica_nos_finalizados` (preparação e andamento perdem os participantes dele e das concessões; finalizado intacto); `Remover_quem_tem_a_vez_passa_a_vez` (Review Focus 1, dois encontros, linhas `ParticipanteRemovido` em ambos); `Notas_secretas_ficam` (a nota continua na lista do GM; o jogador perde o acesso); `Readicionar_tira_do_cemiterio_e_nao_devolve_concessoes`; `Outro_GM_recebe_404`; `Jogador_nao_remove` → 403; hub: `Jogador_removido_recebe_CampanhasDoJogadorAlteradas`.
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Integration --filter "RemoverMembroTests|EncounterParticipants"` → os novos FAIL.
- [ ] **Step 3:** Implementar: primeiro a extração (`EncounterParticipantsControllerTests` verdes antes de seguir), depois `SaidaDoJogador`, depois o endpoint.
- [ ] **Step 4:** Filtros → PASS; `RotasDaApiTests` com só a rota nova.
- [ ] **Step 5:** Commits (`refactor(encontros): remoção de participante como serviço`, `feat(contas): remover jogador da campanha`); push.

### Task 5: Desativar, reativar e excluir conta

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/PlayersController.cs` — `POST api/players/{playerId}/desativar`, `POST …/reativar`, `POST …/excluir` (corpo `ExcluirContaRequest(string Nickname)`); `Search` devolve o estado e omite `Excluida`; `ResetPassword` recusa `Excluida` (404)
- Create: `src/RuinaRPG.Contracts/Players/ExcluirContaRequest.cs`; Modify `PlayerSearchResultResponse` (acrescentar `string Estado` no fim)
- Modify: `CampaignsController.AddMember` (jogador não `Ativa` → 400 `O jogador informado não está vinculado à sua conta.` — mesmo texto de hoje)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/ContasDeJogadorTests.cs`

**Regras:** gestão só pelo GM do convite (`ContaDeJogador.PodeGerir`, senão 404); transição por `ContaDeJogador.Aplicar` (inválida → 409); desativar e excluir chamam `SaidaDoJogador.SairAsync` com **todas** as campanhas do GM em que ele é membro e revogam os refresh tokens dele, numa transação; excluir exige `Nickname` igual ao do jogador (senão 400 `O apelido não confere.`) e apaga Email/NormalizedEmail/PasswordHash, troca `UserName`/`NormalizedUserName` por um valor único derivado do Id e renova o `SecurityStamp`; `Nickname` fica.

- [ ] **Step 1: Testes falhando:** `Desativar_tira_de_todas_as_campanhas_e_revoga_tokens` (refresh antigo → 401); `Reativar_volta_a_ativa_sem_campanhas`; `Excluir_exige_o_apelido`; `Excluir_libera_o_email_e_reserva_o_apelido` (Review Focus 4: cadastro com o mesmo e-mail e outro apelido → sucesso; com o mesmo apelido → recusa de apelido em uso); `Transicao_invalida_da_409` (reativar ativa; desativar excluída); `Outro_GM_recebe_404`; `Busca_mostra_estado_e_omite_excluidas`; `Nao_adiciona_membro_desativado`; `Nao_redefine_senha_de_excluida`.
- [ ] **Step 2:** `--filter ContasDeJogadorTests` → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** → PASS; `RotasDaApiTests` só com as rotas novas.
- [ ] **Step 5:** Commit `feat(contas): desativar, reativar e excluir` e push.

### Task 6: Conta inativa não entra

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/AuthController.cs` (`Login`: depois de conferir a senha, conta não `Ativa` → 401 com o texto de conta desativada; `Refresh`: dono não `Ativa` → 401)
- Modify: `src/RuinaRPG.Api/Program.cs` (`JwtBearerEvents.OnTokenValidated`: lê o `Estado` do `sub` sem rastrear; não `Ativa` → `context.Fail(...)`; vale para HTTP e para os hubs)
- Create: `src/RuinaRPG.Contracts/Auth/TextosDaConta.cs` (`ContaDesativada = "Esta conta está desativada. Fale com o seu GM."`)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerContaInativaTests.cs`

- [ ] **Step 1: Testes falhando:** `Login_de_conta_desativada_mostra_o_texto` (senha certa); `Senha_errada_nao_revela_o_estado` (Review Focus 3 → `Credenciais inválidas.`); `Token_de_acesso_ainda_valido_e_recusado_apos_desativar` (Review Focus 2: GET `/api/auth/me` → 401); `Hub_recusa_conta_desativada` (conexão ao `NotificationHub` falha); `Refresh_de_conta_desativada_e_recusado`; `Conta_reativada_volta_a_entrar`; `Conta_excluida_nao_entra` (login pelo apelido → `Credenciais inválidas.`).
- [ ] **Step 2:** `--filter AuthControllerContaInativaTests` → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** → PASS; rodar também `--filter AuthController` (os existentes verdes).
- [ ] **Step 5:** Commit `feat(contas): conta inativa não entra` e push.

### Task 7: Cliente — campanha (remover membro, alterar dono) e atualização do painel

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor` — aba Membros: `Remover da campanha` por membro, com diálogo de confirmação que lista os efeitos (fichas ao cemitério, concessões ao GM, saída dos encontros ativos); `Alterar dono` em Concessões existentes e nas linhas de NPC/Criatura de Anexos — **um** diálogo compartilhado (Create `src/RuinaRPG.Client/Shared/AlterarDonoDialog.razor`) com os membros + "GM (sem dono)", chamando `PUT {rota do tipo}/{id}/dono`; a rota vem do tipo da ficha (sem `if` por tipo no diálogo: recebe a URL pronta).
- Modify: `src/RuinaRPG.Client/Pages/Painel.razor`, `MinhasCampanhas.razor`, `MinhaCampanha.razor` — recarregam ao receber `CampanhasDoJogadorAlteradas` (padrão dos avisos existentes); `MinhaCampanha` de uma campanha da qual saiu volta ao painel.
- Test: `tests/RuinaRPG.Tests.Client/Pages/CampanhaDetalheTests.cs`, `MinhasCampanhasTests.cs`, `PainelTests.cs`, `MinhaCampanhaTests.cs`

- [ ] **Step 1: Testes falhando:** `Remover_membro_confirma_e_chama_DELETE`; `Cancelar_nao_chama`; `Alterar_dono_lista_membros_e_GM` e `Alterar_dono_envia_DonoId_ou_null` (NPC e Criatura, mesma asserção com URLs diferentes); `Anexo_de_NPC_sem_dono_oferece_Alterar_dono`; `Aviso_de_campanhas_recarrega_o_painel` / `…_minhas_campanhas`; `Minha_campanha_de_onde_saiu_volta_ao_painel`.
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Client --filter "CampanhaDetalheTests|PainelTests|MinhasCampanhasTests|MinhaCampanhaTests"` → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** → PASS; Client completo uma vez.
- [ ] **Step 5:** Commit `feat(contas): remover membro e alterar dono no cliente` e push.

### Task 8: Cliente — página de jogadores e seleção de membros

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/GmJogadores.razor` — lista de ativos com `Desativar` e `Excluir`; seção `Desativados` (só quando houver) com `Reativar` e `Excluir`; `Excluir` abre diálogo que só habilita o botão quando o apelido digitado é igual ao do jogador; redefinir senha continua nos ativos e desativados.
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor` (busca de jogador para adicionar membro mostra só `Ativa`)
- Test: Create `tests/RuinaRPG.Tests.Client/Pages/GmJogadoresTests.cs`; `CampanhaDetalheTests.cs`

- [ ] **Step 1: Testes falhando:** `Ativos_e_desativados_em_secoes_separadas`; `Secao_desativados_some_sem_desativados`; `Desativar_chama_o_endpoint_e_move_o_jogador`; `Reativar_…`; `Excluir_so_habilita_com_o_apelido_certo` e `Excluir_envia_o_apelido`; `Busca_de_membro_mostra_so_ativos`.
- [ ] **Step 2:** `--filter "GmJogadoresTests|CampanhaDetalheTests"` → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** → PASS; Client completo uma vez.
- [ ] **Step 5:** Commit `feat(contas): página de jogadores` e push.

### Task 9: Verificação final

- [ ] **Step 1:** `dotnet build` → 0 warnings.
- [ ] **Step 2:** Um de cada vez: Unit, Integration, Client → verdes.
- [ ] **Step 3:** `python3 tools/docs/verificar-wikilinks.py` → 0; `deploy/tests/run-bats.sh deploy/tests/provision.bats deploy/tests/sinais.bats` → verdes.
- [ ] **Step 4:** Ensaio do pipeline: `gh workflow run release.yml --ref release/2.1.0 -f head=$(git rev-parse HEAD)` → `ci` e `clone` verdes (o `clone` aplica as duas migrations da 2.1.0 sobre um dump de produção).
- [ ] **Step 5:** Conferir os requisitos (Task 1) contra o construído; corrigir o doc se divergir; commit e push.
