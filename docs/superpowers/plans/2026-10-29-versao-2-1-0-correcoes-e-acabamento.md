# Correções, divergências do D, sugestões do E e ortografia — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fechar a 2.1.0: cinco correções decididas, as divergências do D que mudam, as duas sugestões do E e a ortografia do livro.

**Architecture:** Mudanças pequenas e localizadas, sempre na peça genérica: acesso e carga no `FichasController`/`FichaStats` genéricos; Maestria PUT promovida da subclasse do Personagem ao `MaestriasDaFichaController` genérico; correções de cliente nas abas compartilhadas e no `AutoSaveCoordinator`; a base comum das páginas e o fato de formato em `TipoDeFichaCliente` como refatorações cobertas pelas redes de segurança.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core/PostgreSQL, Blazor WASM + MudBlazor 9.9, xUnit + bUnit + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-29-versao-2-1-0-correcoes-e-acabamento-design.md` (aprovado; ler a seção Estrutura e as "chamadas minhas").

## Global Constraints

- Branch `release/2.1.0`; o PR #68 continua **fechado** (não reabrir). Commit + push a cada tarefa.
- TDD; `dotnet build` 0 warnings; projetos de teste **um de cada vez**, conferindo `pgrep -fa "dotnet test"` antes.
- **Estrutura 2.0.0:** o que vale para os três tipos fica no genérico (`Controllers/Fichas/`, `FichaStats`, `Shared/Ficha/`, `Contracts/Fichas/`); ao unificar, o código da subclasse sai e a flag de capacidade que existia só para a diferença sai junto; testes genéricos por tipo (`tests/.../Controllers/Fichas/`, `[Theory]` por tipo no cliente). Se algo não couber, parar e reportar.
- 404/403: **só a Ficha de Personagem** muda (decisão do usuário): leitura sem acesso → 404, escrita sem acesso → 403. NPC e Criatura continuam negando tudo com 404.
- Sem migration. `rotas-da-api.txt` muda só pelo PUT de Maestria de NPC e Criatura (Task 3). Redes de segurança do cliente: mudam só na Task 4 (placeholder); nas Tasks 7 e 8 (refatorações) não podem mudar.
- Requisitos em `Docs/Requisitos/` atualizados na tarefa que muda o comportamento; `python3 tools/docs/verificar-wikilinks.py` → 0.

## Review Focus

1. Editar o Nome e logo em seguida o PI, e também um terceiro campo, dentro da janela do auto-save: nenhuma das três edições se perde, e o valor final no servidor é o último digitado. (Task 5)
2. Jogador dono de um NPC concedido tenta `DELETE` → 404 e a ficha continua; o GM consegue apagar. (Task 1)
3. Estranho chamando os cinco GET que mudam na Ficha de Personagem → 404; o dono e o GM continuam com 200; NPC e Criatura sem mudança. (Task 1)
4. Jogador abrindo um NPC concedido: o breadcrumb não tem nenhum link para página só do GM; o GM vê o de hoje. (Task 6)
5. Criatura com arma equipada e outra desequipada: a desequipada pesa, a equipada não — igual ao Personagem. (Task 2)

---

### Task 1: Acesso — só o GM apaga NPC/Criatura; leituras do Personagem negam com 404

**Files:** `src/RuinaRPG.Api/Controllers/Fichas/FichasController.cs` (`ApagarAsync` ~126; os GET `level-up-notice` ~31, `sub-attributes` ~57, `condicoes-ativas` ~71, `equipment-penalties` ~81, `modificador-de-dano` ~95 passam de `FichaEditavelAsync` para `FichaParaLeituraAsync`); comentário de `FichaControllerBase.FichaParaLeituraAsync` (a divergência deixa de existir); `Docs/Requisitos/Requisitos - Campanha.md` R0010 (só o GM apaga, se o texto não for explícito) e onde os requisitos citarem os status; tests em `tests/RuinaRPG.Tests.Integration/Controllers/Fichas/` (genéricos) e `AcessoNegadoPorTipoTests.cs`.

**Regras:** `ApagarAsync` (NPC/Criatura) exige que o chamador seja o GM da ficha (`ITipoDeFicha.MestreAsync`); qualquer outro → a negação do tipo (404). O `Delete` próprio do Personagem não muda.

- [ ] **Step 1: Testes falhando:** `Dono_jogador_nao_apaga_ficha_concedida` (NPC e Criatura, genérico: 404, a ficha continua — Review Focus 2); `GM_apaga_ficha_concedida` (204); `Estranho_le_os_cinco_GET_do_personagem_com_404` (`[Theory]` com as cinco rotas — Review Focus 3); `Dono_e_GM_continuam_lendo` (200); em `AcessoNegadoPorTipoTests`, as expectativas de status dessas cinco rotas do Personagem passam a 404 (é a mudança decidida; explicar no commit).
- [ ] **Step 2:** `dotnet test tests/RuinaRPG.Tests.Integration --filter "AcessoNegadoPorTipo|FichaDeNpcOuCriatura|FichaTestes"` → os novos FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** Mesmo filtro → PASS; Integration completo.
- [ ] **Step 5:** Commit `fix(fichas): só o GM apaga NPC/Criatura; leituras do Personagem negam com 404` e push.

### Task 2: Peso carregado da Criatura pela fórmula atual

**Files:** `src/RuinaRPG.Api/Services/CreatureSheetStats.cs` (sai o override de `CargaAsync`, ~36), `FichaStats.cs`/`NpcSheetStats.cs`/`CharacterSheetStats.cs` (se a fórmula comum de 2026-09-08 — Inventário + Armas/Escudos **desequipados** — ainda estiver repetida em NPC e Personagem, ela sobe para `FichaStats` e os overrides que só a repetem saem; o Bônus de carga da Campanha continua só no Personagem); `Docs/Requisitos/Requisitos - Ficha de Criaturas.md` (2.b, se cita a fórmula antiga); tests genéricos de sub-atributos/carga em `tests/RuinaRPG.Tests.Integration/Controllers/Fichas/`.

- [ ] **Step 1: Teste falhando:** `Arma_equipada_nao_pesa_e_desequipada_pesa` como `[Theory]`/genérico nos três tipos (Review Focus 5) — hoje falha só para Criatura.
- [ ] **Step 2:** `--filter` do teste → FAIL na Criatura.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** PASS; `--filter "SubAtributos|Carga|Peso"` e Integration completo.
- [ ] **Step 5:** Commit `fix(criatura): peso carregado pela fórmula de 2026-09-08` e push.

### Task 3: Maestria editável em NPC e Criatura

**Files:** `src/RuinaRPG.Api/Controllers/CharacterMasteriesController.cs` (o `PUT {id}` ~30 sai daqui) → `src/RuinaRPG.Api/Controllers/Fichas/MaestriasDaFichaController.cs` (genérico, mesmas validações nos três); o request do PUT vira **um** record em `src/RuinaRPG.Contracts/Fichas/` (ex.: `UpdateMasteryRequest`, substituindo `UpdateCharacterMasteryRequest`; JSON igual); cliente: `TipoDeFichaCliente.EditaMaestriaNaLinha` deixa de existir (vale para todos) e `FichaEstado.Combate.cs` (~198-209) usa o record novo; `Docs/Requisitos/` (Ficha de NPCs/Criaturas 4.e citam a edição); `rotas-da-api.txt` (só as linhas novas de NPC/Criatura e o tipo do corpo); tests genéricos `MaestriasDaFichaTestes` e o cliente (`FichaMaestriasNaAbaCombateTests` como `[Theory]` nos três tipos).

- [ ] **Step 1: Testes falhando:** `Editar_maestria_existente` genérico (NPC e Criatura falham hoje com 404/405); cliente: `Edicao_na_linha_da_maestria` nos três tipos.
- [ ] **Step 2:** Filtros (`MaestriasDaFicha` no Integration; `FichaMaestrias` no Client) → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** PASS; `RotasDaApiTests` só com as linhas esperadas; redes de segurança do cliente só pelo que a edição na linha mostra a mais em NPC/Criatura (explicar no commit).
- [ ] **Step 5:** Commit `feat(fichas): Maestria editável nos três tipos` e push.

### Task 4: Correções pequenas do cliente (placeholder, Estrela, recusa de atributo da Perícia)

**Files:** `src/RuinaRPG.Client/Shared/CatalogoItemPicker.razor:13` (`Placeholder="Placeholder"` → o parâmetro `Placeholder` do componente repassado; cada uso em NPC/Criatura passa o texto do campo, igual aos seletores do Personagem); o componente da Estrela em `Shared/Ficha/AbaAntecedentes.razor` (e `Shared/Fields/RequisitosDePassivaEditor.razor`, que também usa `EstrelaSelect`): resposta inválida/erro de `/api/rulebook` → mensagem no alerta de erro da página (`FichaEstado.MostrarErro` ou o padrão do componente), aba de pé; `src/RuinaRPG.Client/Shared/Ficha/FichaEstado.Atributos.cs`: trocar o atributo de uma Perícia recusado pelo servidor → mostra o texto da resposta no alerta, nos três tipos; tests `tests/RuinaRPG.Tests.Client/...` e a atualização intencional de `RedeDeSeguranca/` (só o texto do placeholder).

- [ ] **Step 1: Testes falhando:** `Seletor_de_item_mostra_o_placeholder_do_campo` (NPC e Criatura); `Estrela_com_resposta_invalida_mostra_erro_e_a_aba_continua`; `Recusa_ao_trocar_atributo_da_pericia_mostra_a_mensagem` (`[Theory]` nos três tipos).
- [ ] **Step 2:** Filtros → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** PASS; `--filter RedeDeSeguranca` muda só pelo placeholder; Client completo.
- [ ] **Step 5:** Commit `fix(cliente): placeholder dos seletores, Estrela e recusa de atributo da Perícia` e push.

### Task 5: Nenhuma edição pendente se perde num recarregamento

**Files:** `src/RuinaRPG.Client/Services/AutoSaveCoordinator.cs`, `src/RuinaRPG.Client/Shared/Ficha/FichaEstado*.cs` (o recarregamento `LoadSheetAsync` e o salvamento separado do PI), tests `tests/RuinaRPG.Tests.Client/Services/AutoSaveCoordinatorTests.cs` e `tests/RuinaRPG.Tests.Client/Pages/FichaPontosDeIgnicaoTests.cs`.

**Regra (spec A.4, chamada minha):** um recarregamento nunca sobrescreve no formulário um campo com edição pendente ou em envio; depois que essas edições forem enviadas, o formulário reflete o servidor. Vale no código compartilhado, para os três tipos. O mecanismo é do construtor (fila de pendências, reaplicar sobre o recarregado, adiar o recarregamento até ocioso…).

- [ ] **Step 1: Testes falhando:** `Pi_editado_logo_depois_do_nome_nao_se_perde` (o cenário do backlog: Nome, PI dentro de 400 ms; o servidor termina com os dois); `Tres_edicoes_seguidas_terminam_todas_no_servidor` (Review Focus 1: Nome, PI e mais um campo); `Recarregamento_sem_edicao_pendente_mostra_o_servidor` (não regride o caso normal). Usar tempo controlado como os testes atuais do coordenador (sem esperas reais longas; respeitar a coleção "Tempo real" se usar tempo real).
- [ ] **Step 2:** Filtros → FAIL (o primeiro reproduz a perda).
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** PASS; `--filter "AutoSave|PontosDeIgnicao|RedeDeSeguranca"`; Client completo.
- [ ] **Step 5:** Commit `fix(auto-save): recarregamento não apaga edição pendente` e push.

### Task 6: Breadcrumb da ficha concedida

**Files:** a resposta compartilhada de NPC/Criatura em `src/RuinaRPG.Contracts/Fichas/` (acrescentar a campanha da concessão — id e nome — se ainda não vier; preenchida por `ITipoDeFicha.CampanhaAsync`), o controller genérico que monta essa resposta; cliente: `CabecalhoDaFicha.razor`/`TipoDeFichaCliente` (as trilhas por tipo vivem ali) e as páginas `FichaDeNpc.razor`/`FichaDeCriatura.razor` — o dono jogador vê Painel → Minhas Campanhas → a campanha → a ficha, como no Personagem; o GM vê a trilha de hoje; tests de integração (o campo novo) e do cliente (`[Theory]` NPC e Criatura).

- [ ] **Step 1: Testes falhando:** `Resposta_da_ficha_concedida_traz_a_campanha` (null para ficha só do GM); cliente `Jogador_ve_a_trilha_pela_campanha` (nenhum link para `npcs`/`bestiario` — Review Focus 4) e `GM_ve_a_trilha_de_hoje`.
- [ ] **Step 2:** Filtros → FAIL.
- [ ] **Step 3:** Implementar.
- [ ] **Step 4:** PASS; Integration e Client completos; `RotasDaApiTests` sem mudança (lista só tipos).
- [ ] **Step 5:** Commit `fix(fichas): breadcrumb da ficha concedida pela campanha` e push.

### Task 7: Fato de formato dos dados em `TipoDeFichaCliente`

**Files:** `src/RuinaRPG.Client/Shared/Ficha/TipoDeFichaCliente.cs` (novo fato, ex. `FormatoDosDados`, que diz quais records a Criatura usa), os pontos que hoje escolhem os records da Criatura por flags de interface (`AdaptadorDeCriatura.cs` e onde mais o grep mostrar) passam a usá-lo; tests de `TipoDeFichaCliente`.

- [ ] **Step 1: Teste falhando:** `Formato_dos_dados_por_tipo` (Personagem e NPC compartilhados; Criatura próprio) e um teste que prova que a escolha de record usa o fato (ex.: um tipo de teste com a flag de interface diferente e o mesmo formato).
- [ ] **Step 2:** Filtro → FAIL.
- [ ] **Step 3:** Implementar, sem mudar comportamento.
- [ ] **Step 4:** PASS; `--filter RedeDeSeguranca` **sem nenhuma mudança**; Client completo.
- [ ] **Step 5:** Commit `refactor(cliente): formato dos dados como fato próprio do tipo de ficha` e push.

### Task 8: Base comum das páginas de ficha

**Files:** `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor` → a coordenação repetida (carga, auto-save, avisos em tempo real, alerta de erro, cabeçalho e o que mais for idêntico) vai para uma base em `src/RuinaRPG.Client/Shared/Ficha/` (componente ou classe base: escolha do implementador, sem ramos por tipo — diferenças via `TipoDeFichaCliente`); cada página fica só com o que é dela.

- [ ] **Step 1:** Antes de mexer, registrar no relatório as linhas das três páginas (`wc -l`) e listar o que é idêntico; se a página do Personagem tiver coordenação própria (cemitério, PI separado), ela continua na página do Personagem.
- [ ] **Step 2:** Refatorar em passos pequenos, rodando `--filter "RedeDeSeguranca|FichaEstadoNaPagina|FichaNoCemiterio|FichaPontosDeIgnicao|FichaCondicoes"` a cada passo: **nada** pode mudar nas redes.
- [ ] **Step 3:** Client completo verde; registrar as linhas depois.
- [ ] **Step 4:** Commit `refactor(cliente): base comum das páginas de ficha` (corpo com as linhas antes/depois) e push.

### Task 9: Ortografia do livro de regras

**Files:** `Docs/Sistema RPG/GRAUS & CÍRCULOS.md` (item 12), `Docs/Sistema RPG/Características.md` (item 21: só `conseqüências`, `paranóico`, `pêlo`; **não** `Lingüísta`), `Docs/Sistema RPG/Historico.md` (item 25: "histórico" no texto; arquivo não renomeado), `Docs/Sistema RPG/Tabela de Níveis.md` (item 34: só acentos em texto); snapshots `tests/RuinaRPG.Tests.Integration/Rules/*Congelado*.json`.

- [ ] **Step 1:** Para cada palavra, conferir com grep em `src/` que ela não é chave de seed/parser nem cabeçalho lido por `IRulesDataProvider`; as que forem ficam de fora e vão para o relatório.
- [ ] **Step 2:** Aplicar; `dotnet test tests/RuinaRPG.Tests.Integration --filter "Congelado|LivroDeRegras|Compendio|Rulebook"` → os congelados falham **só** pelos acentos (conferir o diff do JSON); regenerar pelo mecanismo do próprio teste.
- [ ] **Step 3:** Mesmo filtro → PASS; Integration completo; wikilinks → 0.
- [ ] **Step 4:** Commit `docs(livro): correções de ortografia` (lista do que entrou e do que ficou de fora por ser chave) e push.

### Task 10: Verificação final

- [ ] **Step 1:** `dotnet build` → 0 warnings.
- [ ] **Step 2:** Um de cada vez: Unit, Integration, Client → verdes.
- [ ] **Step 3:** wikilinks → 0; `deploy/tests/run-bats.sh deploy/tests/provision.bats deploy/tests/sinais.bats deploy/tests/clone-lock.bats` → verdes.
- [ ] **Step 4:** Ensaio: `gh workflow run release.yml --ref release/2.1.0 -f head=$(git rev-parse HEAD)` → `ci` e `clone` verdes.
- [ ] **Step 5:** Requisitos conferidos contra o construído; commit e push.
