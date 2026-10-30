# Cemitério de fichas — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fichas de Personagem no "cemitério" (morte marcada pelo GM, ou dono fora da campanha) ficam visíveis só ao GM e ao dono e não podem ser editadas por ninguém.

**Architecture:** A ficha guarda só `Morto`; os motivos são calculados por uma função pura do Domain alimentada por um serviço da Api (`CemiterioDaFicha`). Um filtro MVC global recusa com 409 toda escrita em `/api/character-sheets/{id|sheetId}/…` de ficha no cemitério (exceto ações marcadas `[PermitidoNoCemiterio]`); um teste varre todas as rotas para garantir que nenhuma escapa. O Encontro tem as suas recusas e pulos próprios. No cliente, um único `FichaEstado.SomenteLeitura` desabilita as abas pela cascata de desabilitação do MudBlazor.

**Tech Stack:** .NET 8, ASP.NET Core MVC, EF Core + PostgreSQL (Testcontainers), SignalR, Blazor WebAssembly + MudBlazor 9.9, xUnit + bUnit.

**Spec:** `docs/superpowers/archive/specs/2026-10-24-versao-2-1-0-cemiterio-design.md` (ler inteiro antes de qualquer tarefa).

## Global Constraints

- Branch `release/2.1.0`, criada a partir do HEAD de `release/2.0.0` com `origin/main` mesclado (a 2.0.0 foi mesclada em main pelo PR #67). Empurrar a cada tarefa (o usuário quer o git sempre em dia).
- TDD obrigatório (Técnico R0011): teste falhando primeiro, depois o mínimo de código.
- `dotnet build` termina com 0 warnings, 0 errors.
- Rodar os três projetos de teste **um de cada vez** (`dotnet test tests/<projeto>`), nunca `dotnet test` puro.
- Só Ficha de Personagem. NPC e Criatura não mudam de comportamento.
- Textos exatos (copiar literalmente):
  - recusa de escrita: `Esta ficha está no cemitério e não pode ser editada.`
  - recusa de adicionar ao Encontro: `Esta ficha está no cemitério e não pode entrar em encontros.`
  - motivo de pulo no log do Encontro: `ficha no cemitério`
  - rótulos dos motivos no cliente: `Morte` → `Personagem morto`; `RemovidoDaCampanha` → `Jogador removido da campanha`; `ContaInativa` → `Conta do jogador inativa`
  - aviso no topo da ficha: `Esta ficha está no cemitério` seguido dos rótulos
  - caixa do GM: `Morto`; aba da campanha e seção do painel: `Cemitério`
- Ícones MudBlazor, nunca emoji.
- Redes de segurança: só `rotas-da-api.txt` e as abas de `tests/RuinaRPG.Tests.Client/Pages/Ficha/RedeDeSeguranca/` podem mudar, e só pelo que esta entrega acrescenta; cada mudança explicada no commit. Nenhum outro snapshot muda.
- Migration só acrescenta coluna (`Morto boolean NOT NULL DEFAULT false`), compatível com a 2.0.0 no blue-green.

## Review Focus

1. GM marca "Morto" enquanto o dono está com a ficha aberta e um auto-save pendente → o save recebe 409, a mensagem aparece (não some em silêncio) e a ficha do dono vira só leitura pelo aviso em tempo real. (Task 7)
2. GM desmarca "Morto" → a ficha aberta do dono volta a editável sem recarregar. (Task 7)
3. Um estranho (outro jogador, outro GM) escrevendo numa ficha no cemitério recebe o mesmo 403/404 de sempre, nunca o 409 — o 409 não pode revelar que a ficha existe. (Task 5)
4. Condição aplicada na preparação a uma ficha que já está no cemitério → ao Iniciar, nada é gravado na ficha (nem corte, nem ajuste) e o log do Encontro tem a linha `ficha no cemitério`. (Task 6)
5. Dono deixa de ser membro (linha de `CampaignMembers` apagada direto no banco, já que não há tela de remoção) → a ficha entra no cemitério com motivo `RemovidoDaCampanha`; voltar a ser membro a tira, a menos que esteja `Morto`. (Tasks 3 e 4)

---

### Task 0: Branch

- [ ] **Step 1:** `git switch -c release/2.1.0 && git merge --no-edit origin/main && git push -u origin release/2.1.0`. Expected: merge sem conflito (main só tem o merge commit do PR #67).

### Task 1: Requisitos

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (preâmbulo "Modelo de edição"; novo **R0010**)
- Modify: `Docs/Requisitos/Requisitos - Campanha.md` (R0009, R0016; novo **R0017** aba Cemitério)
- Modify: `Docs/Requisitos/Requisitos - Gerenciador de Encontros.md` (R0002, R0003, R0011, R0013; novo **R0015** participante com ficha no cemitério)
- Modify: `Docs/Requisitos/01 - Visão Geral.md` (PAINEL DO JOGADOR: seção Cemitério; PAINEL DO GM ferramenta 5: aba Cemitério)
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md` (coluna `Morto` de `CharacterSheets`)

- [ ] **Step 1:** Escrever os requisitos com o comportamento da spec (seções "Decisões do usuário" e "Comportamento"), em português, no estilo dos vizinhos, com `[[wikilinks]]` cruzados (Ficha R0010 ↔ Campanha R0017 ↔ Encontros R0015). Números (nenhum novo) não são duplicados. Incluir no R0010 da Ficha: as exceções (caixa "Morto", exclusão pelo GM, dispensar o aviso de subida de nível) e uma frase dizendo que o motivo "conta inativa" chega com a desativação/exclusão de conta (desenho seguinte da 2.1.0). *Dispensar o aviso de subida de nível* é chamada do plano: é estado de interface, não conteúdo da ficha, e sem ela o aviso ficaria preso numa ficha no cemitério.
- [ ] **Step 2:** `python3 tools/docs/verificar-wikilinks.py` → sem links quebrados.
- [ ] **Step 3:** Commit `docs(cemitério): requisitos` e push.

### Task 2: Motivos do cemitério (Domain)

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/MotivoDoCemiterio.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/Cemiterio.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/CemiterioTests.cs`

**Interfaces:**
- Produces: `public enum MotivoDoCemiterio { Morte, RemovidoDaCampanha, ContaInativa }` (`ContaInativa` ainda nunca produzido; reservado para o desenho de contas) e `public static class Cemiterio { public static IReadOnlyList<MotivoDoCemiterio> Motivos(bool morto, bool donoEhMembro); }` — ordem fixa: Morte, RemovidoDaCampanha.

- [ ] **Step 1: Testes falhando** — `Sem_motivo_a_lista_e_vazia` (`Motivos(false, true)` vazio); `Morto_da_Morte` (`[Morte]`); `Dono_fora_da_campanha_da_RemovidoDaCampanha` (`Motivos(false,false)` = `[RemovidoDaCampanha]`); `Os_dois_motivos_vem_na_ordem_fixa` (`Motivos(true,false)` = `[Morte, RemovidoDaCampanha]`).
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Unit --filter CemiterioTests` → FAIL (tipo inexistente).
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** Mesmo comando → PASS.
- [ ] **Step 5:** Commit `feat(cemitério): motivos no domínio` e push.

### Task 3: Coluna `Morto`, serviço `CemiterioDaFicha` e estado na resposta

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs` (`public bool Morto { get; set; }`)
- Create: migration `CemiterioDeFichas` (comando do CLAUDE.md)
- Modify: `src/RuinaRPG.Infrastructure/SheetLogs/SheetLogMap.cs` (`["Morto"] = "Morto"` no mapa da Ficha de Personagem)
- Create: `src/RuinaRPG.Api/Services/CemiterioDaFicha.cs` (registrar no DI em `Program.cs`, scoped)
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs` e `MyCharacterSheetSummaryResponse.cs` (acrescentar `bool NoCemiterio, List<string> MotivosDoCemiterio` no fim)
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs` (`ToResponseAsync`, `ListMine`, `ListForCampaign`)
- Modify: `src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs` (`MinhasFichas` sem fichas no cemitério)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CemiterioTests.cs` (nova classe, `ApiTestBase`)

**Interfaces:**
- Consumes: `Cemiterio.Motivos` (Task 2).
- Produces: `public sealed class CemiterioDaFicha(RuinaRpgDbContext db)` com `Task<IReadOnlyList<MotivoDoCemiterio>> MotivosAsync(CharacterSheet sheet)` e `Task<Dictionary<Guid, IReadOnlyList<MotivoDoCemiterio>>> MotivosAsync(IReadOnlyCollection<CharacterSheet> sheets)` (uma consulta a `CampaignMembers` para o lote). Os motivos vão na resposta como os nomes do enum (`"Morte"`, `"RemovidoDaCampanha"`).

- [ ] **Step 1: Testes falhando** em `CemiterioTests` (montar GM, jogador ligado, campanha, membro e ficha com os helpers de `ApiTestBase`; marcar `Morto` e apagar `CampaignMembers` direto pelo `RuinaRpgDbContext` da fixture, pois o endpoint vem na Task 4):
  - `Ficha_nova_nao_esta_no_cemiterio` — GET `/api/character-sheets/{id}` → `NoCemiterio == false`, `MotivosDoCemiterio` vazio.
  - `Ficha_morta_esta_no_cemiterio_com_motivo_Morte`.
  - `Dono_fora_da_campanha_poe_a_ficha_no_cemiterio` → `["RemovidoDaCampanha"]`; e o dono continua lendo a ficha (200).
  - `Dono_de_volta_a_campanha_tira_a_ficha_do_cemiterio` (reinserir o membro → `NoCemiterio == false`); `Dono_de_volta_mas_morto_continua_no_cemiterio` → `["Morte"]`.
  - `Lista_do_jogador_traz_o_estado_do_cemiterio` (GET `/api/character-sheets/mine`) e `Lista_da_campanha_traz_o_estado_do_cemiterio` (GET `/api/campaigns/{id}/character-sheets`).
  - `Minha_campanha_nao_lista_ficha_no_cemiterio` (`PlayerCampaignViewResponse.MinhasFichas` sem a ficha morta).
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Integration --filter CemiterioTests` → FAIL.
- [ ] **Step 3:** Implementar: coluna, migration (conferir que o `Up` só faz `AddColumn<bool>(... defaultValue: false)`), mapa do log, serviço, respostas, filtro de `MinhasFichas`. Atualizar `Requisitos - Modelo de Dados.md` se a Task 1 não tiver deixado o nome exato da coluna.
- [ ] **Step 4:** Mesmo comando → PASS. Depois `dotnet test tests/RuinaRPG.Tests.Integration --filter "RotasDaApiTests|SchemaTests"` (ou o teste de "no pending model changes"): `RotasDaApiTests` falha só pelas respostas acrescidas → regenerar `rotas-da-api.txt` pelo mecanismo do próprio teste e conferir no diff que só mudaram as linhas das respostas da Ficha de Personagem.
- [ ] **Step 5:** `dotnet build` (0 warnings) e commit `feat(cemitério): estado calculado da ficha` (explicar no corpo a mudança do snapshot) e push.

### Task 4: Caixa "Morto" (endpoint do GM, log, aviso)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs` (ação nova)
- Create: `src/RuinaRPG.Contracts/CharacterSheets/DefinirMorteRequest.cs` — `public record DefinirMorteRequest(bool Morto);`
- Modify: `src/RuinaRPG.Contracts/Notifications/NotificationEvents.cs` — `public const string FichaNoCemiterioAlterada = "FichaNoCemiterioAlterada";`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CemiterioTests.cs`; `tests/RuinaRPG.Tests.Integration/Hubs/` (seguir o teste existente de `CondicoesAlteradas`)

**Interfaces:**
- Produces: `PUT /api/character-sheets/{id}/morte` (`[Authorize(Roles = "GM")]`, corpo `DefinirMorteRequest`), 204; 404 se a ficha não existe ou o chamador não é o GM da campanha. Marcada `[PermitidoNoCemiterio]` (atributo criado na Task 5 — se a Task 5 ainda não existe, esta tarefa cria o atributo vazio em `src/RuinaRPG.Api/Controllers/Fichas/PermitidoNoCemiterioAttribute.cs`). Envia `FichaNoCemiterioAlterada` com o id da ficha (string) ao dono via `NotificationHub` **só quando `NoCemiterio` muda**, e `ParticipantsChanged` aos grupos `encounter-{id}` dos Encontros com a ficha (`TipoPersonagem.EncontrosAsync`).

- [ ] **Step 1: Testes falhando:** `GM_marca_e_desmarca_morto` (204 e GET reflete); `Dono_nao_pode_marcar_morto` (403 pela política de papel); `Outro_GM_recebe_404`; `Marcar_morto_entra_no_log_da_ficha` (GET do log da ficha tem a linha campo "Morto", autor GM); `Dono_e_avisado_quando_a_ficha_entra_e_sai_do_cemiterio` (hub); `Desmarcar_morto_com_dono_fora_da_campanha_nao_avisa` (o estado não mudou).
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Integration --filter "CemiterioTests|<classe do hub>"` → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** Mesmo comando → PASS; atualizar `rotas-da-api.txt` (só a linha nova da rota) e conferir o diff.
- [ ] **Step 5:** Commit `feat(cemitério): caixa Morto do GM` e push.

### Task 5: Nenhuma escrita numa ficha no cemitério

**Files:**
- Create: `src/RuinaRPG.Api/Controllers/Fichas/PermitidoNoCemiterioAttribute.cs` (se a Task 4 não criou) — `[AttributeUsage(AttributeTargets.Method)] public sealed class PermitidoNoCemiterioAttribute : Attribute;`
- Create: `src/RuinaRPG.Api/Controllers/Fichas/FichaNoCemiterioFilter.cs` — `IAsyncActionFilter`, registrado globalmente em `Program.cs` (`options.Filters.Add<FichaNoCemiterioFilter>()`)
- Modify: `CharacterSheetsController.Delete` e a ação de `dismiss-level-up-notice` → `[PermitidoNoCemiterio]`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CemiterioEscritaTests.cs`

**Regra do filtro (a ordem importa — Review Focus 3):** age só quando o método HTTP não é GET **e** o template de rota do `ControllerActionDescriptor` começa com `api/character-sheets/{id}` ou `api/character-sheets/{sheetId}` **e** a ação não tem `[PermitidoNoCemiterio]`. Lê o id do route value (`id` ou `sheetId`); ficha inexistente → segue (a ação dá 404); chamador sem acesso (`CharacterSheetAuthorization.CanEdit` falso) → segue (a ação dá 403/404 como hoje); `CemiterioDaFicha.MotivosAsync` não vazio → `409 Conflict` com o texto exato de recusa; senão segue.

- [ ] **Step 1: Testes falhando:**
  - `Toda_escrita_da_ficha_de_personagem_e_recusada_no_cemiterio` — `[Theory]` com `MemberData` gerado de `IActionDescriptorCollectionProvider` (como `RotasDaApiTests`): toda ação não-GET com template `api/character-sheets/{id|sheetId}…` sem `[PermitidoNoCemiterio]`. Para cada uma, montar a URL com o id da ficha morta e Guids novos nos outros parâmetros de rota (valores válidos para enums de rota: o primeiro valor do enum; `{pericia}`: uma Perícia existente), enviar corpo `{}` e esperar **409** com o texto exato. O teste também falha se a lista gerada vier vazia ou com menos de 40 ações (proteção contra template errado).
  - `As_excecoes_continuam_funcionando` — `PUT …/morte`, `DELETE /api/character-sheets/{id}` (GM) e `POST …/dismiss-level-up-notice` não dão 409.
  - `Estranho_recebe_o_mesmo_erro_de_antes` — outro jogador fazendo `PUT /api/character-sheets/{id}` e `POST …/inventory` na ficha morta recebe o mesmo status que recebe numa ficha viva (comparar os dois na mesma execução).
  - `Ficha_viva_continua_editavel` — `PUT /api/character-sheets/{id}` do dono → 2xx.
  - `NPC_e_Criatura_nao_sao_afetados` — um PUT numa ficha de NPC concedida continua 2xx.
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Integration --filter CemiterioEscritaTests` → FAIL.
- [ ] **Step 3:** Implementar o filtro e as marcações.
- [ ] **Step 4:** Mesmo comando → PASS; `RotasDaApiTests` continua verde (o snapshot não mostra filtros; se mostrar atributos, conferir que só aparecem os `[PermitidoNoCemiterio]`).
- [ ] **Step 5:** Commit `feat(cemitério): ficha no cemitério não é editada` e push.

### Task 6: Encontros no servidor

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/EncounterParticipantsController.cs` (`Add`, `Update`, resposta)
- Modify: `src/RuinaRPG.Api/Controllers/EncounterParticipantCondicoesController.cs` (`Aplicar`, `Remover`)
- Modify: `src/RuinaRPG.Api/Services/CondicoesDoEncontro.cs` (`AoMudarAtivacaoAsync`/`CortarAsync`, `AjustarAsync`)
- Modify: `src/RuinaRPG.Contracts/Encounters/EncounterParticipantResponse.cs` (acrescentar `bool NoCemiterio` no fim; `false` para NPC/Criatura)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CemiterioNoEncontroTests.cs`

**Interfaces:**
- Consumes: `CemiterioDaFicha.MotivosAsync` (Task 3).

- [ ] **Step 1: Testes falhando:**
  - `Ficha_no_cemiterio_nao_entra_no_encontro` — `POST /api/encounters/{id}/participants` com a ficha morta → 409, texto exato de recusa de Encontro.
  - `Participante_continua_no_encontro_ao_entrar_no_cemiterio` — lista de participantes ainda o tem, com `NoCemiterio == true`; vez e Iniciativa intactas.
  - `GM_nao_edita_recursos_do_participante_no_cemiterio` — `PUT …/participants/{id}` com PV novo → 409, PV da ficha igual.
  - `GM_nao_aplica_nem_remove_condicao_no_participante_no_cemiterio` → 409 nos dois.
  - `GM_remove_o_participante_no_cemiterio` — `DELETE …/participants/{id}` → 2xx.
  - `Ajuste_de_condicao_e_pulado_com_linha_no_log` — Condição com ajuste "a cada vez" aplicada antes da morte; avançar a vez até ele → Vitalidade da ficha inalterada; log do Encontro tem `AjusteDeCondicao` com motivo terminando em `ficha no cemitério`.
  - `Corte_ao_iniciar_e_pulado` (Review Focus 4) — Condição que baixa o máximo aplicada na preparação; marcar morto; Iniciar → Atual da ficha inalterado, nenhuma linha automática nova no log da ficha, linha `ficha no cemitério` no log do Encontro.
  - `Expiracao_continua_valendo` — a Condição com prazo expira; log da ficha tem "expirada"; dono avisado (`CondicoesAlteradas`).
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Integration --filter CemiterioNoEncontroTests` → FAIL.
- [ ] **Step 3:** Implementar. Em `AjustarAsync`, o pulo vem antes de `RecursosAsync`, pelo mesmo `Registrar(campo, null, null, "ficha no cemitério")`. Em `AoMudarAtivacaoAsync`, para ficha de Personagem no cemitério, não chamar `CortarAsync`; registrar uma linha `AjusteDeCondicao` com motivo `ficha no cemitério` por Encontro do participante cujo corte foi pulado (só quando o corte mudaria algum valor — comparar o Atual com o máximo novo; sem mudança, sem linha). `Registrar` (log da ficha "aplicada/removida/expirada") e `Avisar` continuam.
- [ ] **Step 4:** Mesmo comando → PASS; rodar também `--filter "EncounterParticipants|CondicoesDoEncontro|EncounterLog"` → PASS; `rotas-da-api.txt` atualizado só pela resposta do participante.
- [ ] **Step 5:** Commit `feat(cemitério): ficha no cemitério no encontro` e push.

### Task 7: Ficha em modo leitura (cliente)

**Files:**
- Modify: `src/RuinaRPG.Client/Shared/Ficha/FichaEstado.cs` (`public bool SomenteLeitura { get; private set; }`, `public IReadOnlyList<string> MotivosDoCemiterio`, preenchidos a cada carga da ficha; NPC/Criatura sempre falso)
- Modify: `src/RuinaRPG.Client/Shared/Ficha/TipoDeFichaCliente.cs` (`public bool TemCemiterio` — só Personagem)
- Modify: `src/RuinaRPG.Client/Shared/Ficha/CabecalhoDaFicha.razor` (aviso; caixa "Morto" para o GM quando `TemCemiterio`)
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (envolver `<MudTabs>` na desabilitação em cascata; breadcrumb; reação a `FichaNoCemiterioAlterada`)
- Test: `tests/RuinaRPG.Tests.Client/Pages/Ficha/FichaNoCemiterioTests.cs`; atualizar `RedeDeSeguranca/` se mudar

**Interfaces:**
- Consumes: `CharacterSheetResponse.NoCemiterio/MotivosDoCemiterio` (Task 3), `PUT character-sheets/{id}/morte` + `DefinirMorteRequest` + `NotificationEvents.FichaNoCemiterioAlterada` (Task 4).

- [ ] **Step 1: Testes falhando (bUnit):**
  - `Ficha_no_cemiterio_mostra_o_aviso_com_os_motivos` — texto `Esta ficha está no cemitério` e `Personagem morto`.
  - `Ficha_no_cemiterio_nao_tem_controle_habilitado` — em cada uma das abas, nenhum `input`, `textarea`, `button` ou `.mud-select` sem `disabled`/estado desabilitado (exceto as abas em si e a caixa "Morto"); o editor da História e o Diário também (se o editor da História não respeitar a cascata, passar `SomenteLeitura` a ele explicitamente).
  - `GM_ve_a_caixa_Morto_e_jogador_nao`.
  - `Marcar_Morto_chama_o_endpoint_e_recarrega` — PUT `character-sheets/{id}/morte` com `{"morto":true}`.
  - `Aviso_em_tempo_real_poe_e_tira_do_modo_leitura` (Review Focus 1 e 2) — disparar `FichaNoCemiterioAlterada` com o id da ficha → recarrega e alterna `SomenteLeitura`, sem navegar.
  - `Recusa_409_do_auto_save_aparece_na_pagina` — o handler de salvamento devolve 409 com o texto → o alerta de erro mostra o texto.
  - `Breadcrumb_da_ficha_no_cemiterio` — GM: campanha com `?aba=cemiterio`; jogador: Painel.
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Client --filter FichaNoCemiterioTests` → FAIL.
- [ ] **Step 3:** Implementar. Para a cascata use o mecanismo do MudBlazor 9.9 (o `CascadingValue` `ParentDisabled` que `MudForm` propaga a componentes e botões, ou um `MudForm Disabled` ao redor); confirmar no teste, não por suposição. Controles HTML próprios das abas que não leem a cascata recebem `Disabled="@Estado.SomenteLeitura"`.
- [ ] **Step 4:** Mesmo comando → PASS; rodar `--filter RedeDeSeguranca`: se só o cabeçalho do GM mudou (a caixa), atualizar o snapshot e explicar no commit; qualquer outra diferença é bug.
- [ ] **Step 5:** Commit `feat(cemitério): ficha só leitura no cliente` e push.

### Task 8: Listas no cliente (aba da campanha e painel)

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor` (Membros sem fichas no cemitério; `MudTabPanel Text="Cemitério"` logo depois de Membros; `?aba=cemiterio` abre essa aba)
- Modify: `src/RuinaRPG.Client/Pages/Painel.razor` (lista sem as do cemitério; seção `Cemitério` abaixo, só quando há alguma)
- Test: `tests/RuinaRPG.Tests.Client/Pages/CampanhaDetalheTests.cs`; Create `tests/RuinaRPG.Tests.Client/Pages/PainelTests.cs`

- [ ] **Step 1: Testes falhando:** `Membros_nao_lista_ficha_no_cemiterio`; `Aba_Cemiterio_lista_imagem_nome_nivel_dono_e_motivos` (com o ícone de log da ficha); `Aba_Cemiterio_vazia_mostra_mensagem`; `Query_aba_cemiterio_abre_a_aba`; `Painel_separa_as_fichas_do_cemiterio` (nome, nível, campanha); `Painel_sem_ficha_no_cemiterio_nao_mostra_a_secao`; `Mensagem_de_nenhuma_ficha_considera_so_as_vivas` (decisão: se todas estão no cemitério, a lista principal mostra a mensagem atual de "nenhuma ficha" e a seção Cemitério aparece).
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Client --filter "CampanhaDetalheTests|PainelTests"` → FAIL.
- [ ] **Step 3:** Implementar, reaproveitando a marcação das listas existentes (cartões do Painel, linhas de Membros).
- [ ] **Step 4:** Mesmo comando → PASS.
- [ ] **Step 5:** Commit `feat(cemitério): aba da campanha e seção do painel` e push.

### Task 9: Encontro no cliente

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/GerenciadorDeEncontros.razor` (participante com `NoCemiterio`: ícone MudBlazor com tooltip `Ficha no cemitério`, edição de PV/PF/PA e Condições desabilitadas, remover habilitado; seletor de adicionar participante sem fichas no cemitério)
- Test: `tests/RuinaRPG.Tests.Client/Pages/GerenciadorDeEncontrosTests.cs`

- [ ] **Step 1: Testes falhando:** `Participante_no_cemiterio_tem_icone_e_edicao_desabilitada`; `Participante_no_cemiterio_pode_ser_removido`; `Seletor_nao_oferece_ficha_no_cemiterio` (a lista de fichas da campanha vem com `NoCemiterio`).
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Client --filter GerenciadorDeEncontrosTests` → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** Mesmo comando → PASS.
- [ ] **Step 5:** Commit `feat(cemitério): encontro no cliente` e push.

### Task 10: Verificação final

- [ ] **Step 1:** `dotnet build` → 0 warnings, 0 errors.
- [ ] **Step 2:** Um de cada vez: `dotnet test tests/RuinaRPG.Tests.Unit`, `dotnet test tests/RuinaRPG.Tests.Integration`, `dotnet test tests/RuinaRPG.Tests.Client` → todos verdes.
- [ ] **Step 3:** `python3 tools/docs/verificar-wikilinks.py` → limpo; `git diff origin/main -- tests/**/*Congelado*` vazio (os snapshots do livro de regras não mudaram).
- [ ] **Step 4:** Conferir os requisitos (Task 1) contra o que foi construído; corrigir divergências no doc.
- [ ] **Step 5:** Commit final se houver e push.
