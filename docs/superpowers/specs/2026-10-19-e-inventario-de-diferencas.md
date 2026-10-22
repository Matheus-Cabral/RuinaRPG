# Versão 2.0.0 — E: inventory of differences between the three sheet pages

Companion to `2026-10-19-versao-2-0-0-e-cliente-design.md` (E.1, Task 1). Measured on `8ea2500`.

Every visible or behavioral difference between `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (**P**),
`FichaDeNpc.razor` (**N**) and `FichaDeCriatura.razor` (**C**), found by diffing each section with type names
normalized (`Personagem|Character|Npc|NPC|Criatura|Creature` → one token), then checking the API routes
(`tests/RuinaRPG.Tests.Integration/Controllers/rotas-da-api.txt`), the response records and the requirements.

Classes:

- **real**: backed by a different endpoint or record, a capability, or an earlier spec/requirement. Stays.
- **decided**: already in the spec's "Differences decided" table.
- **accidental?**: no reason found. **Open: the user decides** (recommendation given).
- **invisible**: a code difference with no effect on screen or requests. Unified freely.

"—" in a P/N/C column means the kind doesn't have the thing. Line numbers are of `8ea2500`.

Routes that only some kinds have (from the snapshot), which back most **real** rows:

| Only on | Routes |
|---|---|
| P | `GET mine`, `PUT affections/{id}`, `PUT masteries/{id}`, `GET skills/budget`, `diary` (GET/POST/PUT/DELETE) |
| P, N | `historia`, `historia/imagens`, `racial-ability`, `variantes-liberadas`, `affinities` (all), `equipagem/*`, `inventory` (all), `racial-traits/*`, `runes` (all) |
| N, C | `PUT {id}/nivel`, `PUT {id}/experiencia-atual`, list/create at the root (GM lists) |
| C | `spoils` (all) |

## Header (before the tabs)

| Difference | P | N | C | Class | Proposed flag/component |
|---|---|---|---|---|---|
| Title | "Ficha de Personagem" (26) | "Ficha de NPC" (25) | "Ficha de Criatura" (24) | real | `TipoDeFichaCliente.Titulo` |
| Breadcrumbs | "Painel / Campanhas / {campanha} / {nome}" — campaign crumbs only when the campaign name loaded (`Crumbs` 898–910, `LoadCampaignNameAsync` 1128–1135, `GET campaigns`) | "Painel / NPCs do GM / {nome}" (813–818) | "Painel / Bestiário do GM / {nome}" (625–630) | real (different parent pages) | page-level (stays in each page) |
| Breadcrumbs when a **player** opens a granted NPC/Criatura | (n/a) | still "NPCs do GM" → `npcs`, a GM-only page | still "Bestiário do GM" → `bestiario`, a GM-only page | **accidental?** | see A5 |
| "Condições ativas" alert | always (27) | only when granted, `_concedida` (26–29) | only when granted (25–28) | real (Ficha de NPCs R0015, Ficha de Criaturas: a GM's sheet never changes by Condições; no `GET condicoes-ativas` at all) | `CondicoesSoSeConcedida` |
| "Novo Nível!" notice + "Fechar" | 31–43 | 33–45 | 32–44 | identical | shared |
| Racial-trait choice dialog | 45–94: title "Escolha suas Características Raciais"; "Você também recebe automaticamente: **X**." | 47–96: "Escolha a Característica Racial do NPC"; "O NPC também recebe automaticamente: **X**." | — (no `racial-traits` routes) | dialog presence: real; **wording: accidental?** | see A1 |
| Error alert (`DismissibleAlert`) | 29 | 31 | 30 | identical | shared |

## Informações Básicas

| Difference | P | N | C | Class | Proposed flag/component |
|---|---|---|---|---|---|
| Auto-save indicator, avatar, image dialog (upload / gallery / remove) | 100–145 | 102–147 | 50–95 | identical | shared |
| Identity fields | Nome, Linhagem, Variante, Vocação, Classe, Propriedade (147–150) | same (149–152) | Nome, Raça, Arquétipo ("Escolha um Arquétipo", Físico, Arcano), Sub Arquétipo, Afinidade, Rank ("Escolha um Rank", F…S) (97–115) | real (Ficha de Criaturas 1.a; Criatura record has Raca/Arquetipo/SubArquetipo/Rank, no Linhagem/Vocação) | `TemLinhagemEVocacao`; Criatura identity = kind-only component |
| Nível | read-only "Nível: 3" (h6, 154) — follows XP | editable "Nível" field, own endpoint `PUT nivel` (156, `NotifyNivelChangedAsync`) | same as N (119) | real (`PUT nivel` only N, C) | `EditaNivelAManual` |
| Experiência Atual save | in the general `PUT {id}` (161) | own endpoint `PUT experiencia-atual` (163) | same as N (120) | real (route only N, C) | `EditaNivelAManual` |
| "Faltam N XP para o próximo nível" / "Nível máximo atingido" | 162–164 | — | — | real (`XpParaProximoNivel` only on `CharacterSheetResponse`) | Personagem component (XP/PI) |
| "Adicionar/subtrair XP" + "Aplicar" | 165–168 | — | — | real (spec: Personagem-only XP/PI deltas) | Personagem component (XP/PI) |
| "Adicionar/subtrair PI (bônus manual)" + "Aplicar" | 171–174 | — | — | real (same; `PontosDeIgnicaoBonusManual` only on P) | Personagem component (XP/PI) |
| Graduação ("Círculo: 1"/"Grau: 2") and "Possui Coração de Mana?" (magic Vocações only) | 155–160 | 157–162 | — | real (Ficha de Criaturas 1.b: replaced by Rank) | `TemLinhagemEVocacao` |
| "Abate: N" / "Assistência: N" | — | only when the server sends them (GM only), plus an InfoPopup "XP por abate e assistência" … "só o GM vê." (164–171) | always, **no InfoPopup** (121–122) | presence: real (N fields nullable and GM-only per Ficha de NPCs; C fields non-null); **popup only on N: accidental?** | see A2; N+C Nível-manual component |
| "VIS Atual: N" | 169 | 172 | — | real (Criaturas 1.b) | `TemLinhagemEVocacao` |
| Pontos de Ignição label | "Pontos de Ignição Atual" (170) | "Pontos de Ignição Atual" (173) | "Pontos de Ignição" (123) | real (different field `PontosDeIgnicao`; Ficha de Criaturas names it "Pontos de Ignição") | `RotuloDosPontosDeIgnicao` |
| Âmbares Absorvidos (Rank F…S) | 177–185 | 176–184 | — | real (Criaturas 1.b) | `TemLinhagemEVocacao` |
| Características: picker | `traits?nome=` (191) | same (189) | `creature-exclusive-traits?nome=` + "Nova característica de criatura" button for a Rules Auditor (`GET auth/me`) (129–137) | real (Ficha de Criaturas 5.d) | `UrlDeBuscaDeCaracteristicas`; Criatura component for the button |
| Características: budget, lists, Remover | 195–220 | 193–218 | 141–166 | identical | shared |

## Atributos & Perícias

| Difference | P | N | C | Class | Proposed flag/component |
|---|---|---|---|---|---|
| "Progressão do nível" panel (`ProgressaoDoNivelSection`) | 226 | 224 (`Nivel ?? 1`, same output) | — | **accidental?** | see A3 |
| Blank line (`<br/>`) after Atributos and after Perícias | 251, 299 | — | — | **accidental?** | see A4 |
| Pontos de Perícia | "Pontos de Perícia: gasto / disponível" (red when over) (254–259) | "Pontos de Perícia Gastos: N" (250) | same as N (197) | decided (kept per kind) | `TemOrcamentoDePericias` |
| Skill table top margin (`Class="mt-3"`) | yes, below the Acerto Crítico row (265) | no | no | real (spacing belongs to the Personagem-only Acerto Crítico block above it) | goes with `TemOrcamentoDePericias` |
| "Pontos ganhos por Acerto Crítico" + "Adicionar/subtrair pontos de Acerto Crítico" | 260–264 | — | — | real (Personagem-only; `PontosDePericiaBonusCritico` only on P) | `TemOrcamentoDePericias` |
| Skill Modificador in **bold** with "Inclui bônus de Histórico" | 273–282 | same | plain (206) | real (no `ContaHistorico` on `CreatureSkillResponse`) | no flag: render bold when the row says so |
| Afinidades section (VocacaoArcanaResumo + table) | 301–353 | same | — | real (Ficha de Criaturas 2.c; no `affinities` routes) | `TemAfinidades` |
| A failed attribute/skill PUT shows the server's message in the error alert | yes (`UpdateAttributeAsync`/`UpdateSkillAsync`, 1395–1396, 1405–1406) | yes (1303–1304, 1313–1314) | **no**: the response is ignored, nothing is shown (1068, 1077) | **accidental?** | see A6 |

## Combate

| Difference | P | N | C | Class | Proposed flag/component |
|---|---|---|---|---|---|
| "Pontos de Sina" rating | 360–363 | 344–347 | — | real (no `SinaAtual` on Creature) | `TemSinaEEstresse` |
| Order of the resource fields | Vitalidade, Foco, Adrenalina, Estresse (364–367) | Adrenalina, Foco, Estresse, Vitalidade (348–351) | Adrenalina, Arcana, Vitalidade (228–230) | decided (Personagem order for all) | shared |
| Foco label | "Foco (PF)" | "Foco (PF)" | "Arcana (PF)" | real (Ficha de Criaturas: Foco is labeled Arcana) | `RotuloDoFoco` |
| Estresse field | 367 | 350 | — | real (Criatura has no Estresse) | `TemSinaEEstresse` |
| Afinidade inside Sub-Atributos (`ComAfinidade`) | 372–373 | same | — (Afinidade is in Identidade) | real (Ficha de Criaturas 2.b) | `TemAfinidades` |
| Item pickers (Arma, Armadura, Escudo) | `EntityPicker` (392, 556, 569) | `CatalogoItemPicker` with "+" for the GM (376, 521, 534) | same as N | real (2026-09-15 cadastro-inline spec) | `CadastraItemNoPicker` |
| Weapon Origem "Vincular ao Catálogo" / "Preencher manualmente (ataque natural)" + manual fields; row "Ataque natural"; "Adicionar Arma" gets a top margin (`mt-3`, 277) below them | — | — | 254–277, 308–311 | real (Ficha de Criaturas 3.a; `ItemId` nullable) | `TemAtaqueNatural` (or Criatura weapon-form component) |
| Weapon "Peso" column | 405, 424 | same | — | real (no `Peso` on `CreatureWeaponResponse`) | `MostraPesoDasArmas` |
| Maestrias rows | header row "Nome / Perícia / Atributo / Gasto Maestria / Total" (468) + inline edit (Nome, Perícia, Atributo, Gasto Maestria) (469–491) | one line "Nome — Perícia / Atributo — Gasto N — Total N" (456) | same as N (357) | real (`PUT masteries/{id}` only P) | `EditaMaestriasEAfeicoesNaLinha` |
| Durabilidade máxima field name (`DurabilidadeMaxima` vs `DurabilidadeMaximo`) | | | | invisible (wire name; E.2 keeps it) | adapter |

## Magias & Habilidades

| Difference | P | N | C | Class | Proposed flag/component |
|---|---|---|---|---|---|
| Habilidade Racial section (Arca roll for Humano) | 619–635 | same | — | real (no `racial-ability` route) | `TemLinhagemEVocacao` |
| Runas | table: image, Nome, Descrição, Grau, Tipo, Disciplina, Remover (693–716) | one line "Nome (Grau N, Tipo), Disciplina: X — Descrição" (669) | — | table vs line: decided (Personagem table); C: real (no `runes` routes) | `TemRunas` |
| Magias/Habilidades list, add form, Passivas | | | | identical | shared |

## Posses

| Difference | P | N | C | Class | Proposed flag/component |
|---|---|---|---|---|---|
| "Escolher Equipamento Inicial" | 720–724 | same | — | real (no `equipagem` routes) | `TemInventario` |
| Inventário (Peso x / y, Sobrecarregado, Ciclos, items) | 725–768 | same | — ; "Espólios" instead (542–575: Nome, Custo, Qtd, Total, DT) | real (`inventory` only P, N; `spoils` only C; server hides Criatura's carga, `CreatureSheetStats.cs:43`) | `TemInventario`; Espólios = Criatura component |
| "inclui ±N da campanha" under Peso | 729–732 | — | — | invisible (only `CharacterSheetStats` sends a non-zero `BonusDeCargaDaCampanha`; showing it when ≠ 0 on N changes nothing) | shared, no flag |
| Item pickers (Inventário, Artefato) | `EntityPicker` (740, 771) | `CatalogoItemPicker` (695, 726) | same as N (544, 578) | real (cadastro-inline spec) | `CadastraItemNoPicker` |
| Espólios picker searches every item type, not one `Tipo` (GM: `items?nome=`, player: `campaigns/{id}/available-items?nome=`) | — | — | 544, `SearchAllItemsAsync` 687 | real (server `AddSpoil` accepts any catalog item) | Espólios component |
| Afeições rows | header row "Nome / Favorabilidade" (799) + inline edit (Nome, Favorabilidade) (800–805) | one line "Nome — Favorabilidade N" (758) | same as N (610) | real (`PUT affections/{id}` only P) | `EditaMaestriasEAfeicoesNaLinha` |

## Antecedentes and Diário

| Difference | P | N | C | Class | Proposed flag/component |
|---|---|---|---|---|---|
| Antecedentes tab (Estrela, Histórico, História) | 814–831 | 767–784 (identical) | — | real (no `historia` routes) | `TemAntecedentes` |
| Diário tab | 833–886 | — | — | real (`diary` only P) | `TemDiario`; Personagem component |

## Page-level behavior (`@code`)

The `@code` blocks differ mostly by the kind-only methods already listed above (P only: Diário, XP/PI/Acerto
Crítico deltas, `UpdateAffectionAsync`, `UpdateMasteryAsync`, `LoadCampaignNameAsync`; N+C only: `SaveNivelAsync`,
`SaveExperienciaAtualAsync` and their notify hooks; C only: Espólios, `NovaCaracteristicaDeCriaturaAsync`,
`LoadIsRulesAuditorAsync`, `SearchAllItemsAsync`; P+N only: racial choice/ability, Afinidades, Runas, Inventário,
Equipagem, Sina, Ciclos, Arca, Afinidade, variantes liberadas). Differences inside the methods all three share:

| Difference | P | N | C | Class | Proposed flag/component |
|---|---|---|---|---|---|
| Load sequence: `GET /pericias`, the sheet, the image gallery (`images/mine` for the GM, `campaigns/{id}/available-images` for a player), then the rest in parallel | 1066–1082 | 968–984 | 797–813 | identical except for the kind-only loads (pinned by the net's `A_carga_da_pagina_faz_estas_requisicoes`) | page/shared loader + flags |
| `CondicoesAlteradas` filter `Kind` | `character` | `npc` | `creature` | real | `TipoDaNotificacao` |
| Reload on `CondicoesAlteradas` | sheet, tabs 2–3, spell-abilities, **runes**, artifacts (1052) | same (954) | without runes (783) | real (no runes on C) | `TemRunas` |
| Condições ativas are fetched | always | only when `OwnerId` is set (`_concedida`) | same as N | real (see Header) | `CondicoesSoSeConcedida` |
| Reload after the general `PUT {id}` | sheet, racial pending, level-up, tabs 2–3, traits, racial ability | same | without racial pending/ability | real (routes only P, N) | `TemLinhagemEVocacao` |
| Body of the general `PUT {id}` | Character fields (incl. `pontosDeIgnicaoBonusManual`, `pontosDePericiaBonusCritico`, `estresseAtual`) | NPC fields (incl. `nivel`, `pontosDeIgnicaoTotal`) | Creature fields (`raca`, `arquetipo`, `rank`, `pontosDeIgnicao`…) | real (different request records; pinned by the net's `CorpoDaFicha`) | per-kind adapter |
| Error on attribute/skill PUT | shown | shown | ignored | **accidental?** | see A6 |

## Invisible differences (unified without asking)

- N declares `VariantesPorLinhagem` and `SubVocacoesPorVocacao` (794–811) and never uses them (dead code).
- P's `SaveIfValidAsync` clears `_errorMessage` before the PUT (1225); N's doesn't, but `LoadSheetAsync` clears it
  right after a successful save, so the screen is the same.
- `ProgressaoDoNivelSection Nivel="@(_form.Nivel ?? 1)"` (N) vs `"@_form.Nivel"` (P): same output.
- Form-model validation messages follow each kind's fields ("Informe os Pontos de Ignição." on C, "Informe o VIS
  Atual." on N for a field it shows as text); they follow the real rows above.
- `@using`/`@inject` order, `sheet!` vs `sheet`, comments.

## Accidental? — for the user

| # | Difference (quoted) | Where | Recommendation |
|---|---|---|---|
| A1 | Racial-trait dialog: P says "Escolha suas Características Raciais" / "Você também recebe automaticamente: X."; N says "Escolha a Característica Racial do NPC" / "O NPC também recebe automaticamente: X." | P 47, 83; N 49, 85 | **Keep per kind.** The NPC wording is the GM's third-person view (Ficha de NPCs R0006: the GM resolves it). One shared dialog with the two texts as parameters. |
| A2 | Abate/Assistência: N shows an InfoPopup "XP por abate e assistência" ("XP que o NPC concede aos jogadores … só o GM vê."); C shows the same two lines with no popup | N 168–170; C 121–122 | **Keep as is.** The popup text is NPC-specific and says "só o GM vê", which is false for a granted Criatura (its Abate/Assistência are always sent). The N+C Nível-manual component takes the popup text as an optional parameter (none for C). |
| A3 | "Progressão do nível" panel at the top of Atributos & Perícias on P and N, absent on C | P 226; N 224 | **Keep as is** (flag `MostraProgressaoDoNivel`). The panel lists the Tabela de Níveis budgets (Perícia points, Passivas…) that don't apply to a Criatura, whose attribute points come from its Rank (Ficha de Criaturas 2.a). |
| A4 | A blank line (`<br/>`) after the Atributos section and after the Perícias section on P only; N and C have the sections back to back | P 251, 299 | **Unify on Personagem** (N and C gain the same small gap). Like the swapped-order rule. |
| A5 | A player who opens a granted NPC/Criatura sees the breadcrumb "Painel / NPCs do GM" (or "Bestiário do GM") → a GM-only page; a Personagem shows its campaign trail | N 813–818; C 625–630 | **Keep as is in E** (fixing it is a navigation change, out of E's scope); note it as a later suggestion. |
| A6 | When the server refuses an attribute or skill change (e.g. over budget, invalid value), P and N show its message in the error alert; C ignores the response and shows nothing (the value just snaps back on reload) | P 1395–1396, 1405–1406; N 1303–1304, 1313–1314; C 1068, 1077 | **Unify on Personagem** (C shows the server's message too). The shared Atributos & Perícias tab will have one update method; dropping the message for C would need a flag whose only purpose is to hide an error. Visible only when a save fails. |

## Proposed capability flags (`TipoDeFichaCliente`)

Values: P / N / C.

| Member | P | N | C | Used by |
|---|---|---|---|---|
| `Prefixo` | `character-sheets` | `npc-sheets` | `creature-sheets` | every API address |
| `TipoDaNotificacao` | `character` | `npc` | `creature` | CondicoesAlteradas filter |
| `Titulo` | "Ficha de Personagem" | "Ficha de NPC" | "Ficha de Criatura" | h1, last breadcrumb fallback |
| `CondicoesSoSeConcedida` | no | yes | yes | Condições alert |
| `TemLinhagemEVocacao` | yes | yes | no | Identidade, Graduação/Coração/VIS/Âmbares, racial dialog, Habilidade Racial, `variantes-liberadas` |
| `EditaNivelAManual` | no | yes | yes | Nível field, Experiência via own endpoint |
| `TemOrcamentoDePericias` | yes | no | no | "Pontos de Perícia: x / y", Acerto Crítico |
| `TemAfinidades` | yes | yes | no | Afinidades section, `ComAfinidade` |
| `TemSinaEEstresse` | yes | yes | no | Sina rating, Estresse |
| `RotuloDoFoco` | "Foco (PF)" | "Foco (PF)" | "Arcana (PF)" | Combate |
| `RotuloDosPontosDeIgnicao` | "Pontos de Ignição Atual" | "Pontos de Ignição Atual" | "Pontos de Ignição" | Nível e Progressão |
| `MostraProgressaoDoNivel` | yes | yes | no (if A3 is kept) | Atributos & Perícias |
| `CadastraItemNoPicker` | no | yes | yes | Arma/Armadura/Escudo/Inventário/Artefato pickers |
| `MostraPesoDasArmas` | yes | yes | no | weapon table |
| `TemAtaqueNatural` | no | no | yes | weapon form/row |
| `EditaMaestriasEAfeicoesNaLinha` | yes | no | no | Maestrias, Afeições |
| `TemRunas` | yes | yes | no | Runas section |
| `TemInventario` | yes | yes | no | Equipamento Inicial, Inventário |
| `TemAntecedentes` | yes | yes | no | Antecedentes tab |
| `TemDiario` | yes | no | no | Diário tab |
| `UrlDeBuscaDeCaracteristicas` | `traits` | `traits` | `creature-exclusive-traits` | Características picker |

## Proposed kind-only components

- **Personagem**: XP/PI adjustments ("Faltam N XP…", "Adicionar/subtrair XP", "Adicionar/subtrair PI (bônus
  manual)"); Diário tab; campaign breadcrumb loading stays in the page.
- **NPC + Criatura**: Nível and Experiência edited by hand (own endpoints, notify hooks) with Abate/Assistência and
  the optional popup (A2).
- **Criatura**: identity block (Raça, Arquétipo, Sub Arquétipo, Afinidade, Rank); Espólios; the "Nova característica
  de criatura" button with its dialog; the weapon Origem/manual form (unless `TemAtaqueNatural` stays a small branch).
- The Atributos/Perícias update methods are shared for all three kinds (A6 decides whether C shows errors too).
- **Personagem + NPC** (shared between the two, absent on Criatura, so their own components rather than branches):
  racial-trait dialog (A1 texts as parameters), Habilidade Racial, Afinidades, Runas, Equipamento Inicial +
  Inventário, Antecedentes.
- The Criatura Atributos & Perícias tab does **not** need its own body: with `TemOrcamentoDePericias`,
  `TemAfinidades` and `MostraProgressaoDoNivel` it is the shared tab minus three blocks.
