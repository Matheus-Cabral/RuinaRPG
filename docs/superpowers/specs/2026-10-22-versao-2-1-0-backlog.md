# Versão 2.1.0 — backlog

**Status:** queued by the user on 2026-10-22, during sub-project E of 2.0.0. Nothing here is designed yet:
each item gets its own brainstorm → spec → plan after 2.0.0 ships (H done).

## Items requested by the user

1. **Breadcrumb of a granted NPC/Criatura** (from E's difference A5). A player who opens an NPC or
   Criatura sheet granted to them sees "Painel / NPCs do GM" (or "Bestiário do GM"), which leads to a
   GM-only page. Fix the trail for the player (e.g. through the campaign, as the Personagem does).
2. **Cemitério de fichas.** The GM can move a Ficha de Personagem to a separate list (the "cemitério").
   A sheet there keeps being **visible** but nobody can **edit** it anymore.
3. **Remove a player from a campaign.** The GM can remove a Jogador from one of their campaigns.
4. **Delete a player account.** The GM can delete a Jogador's account.

5. **"Placeholder" in the NPC/Criatura item pickers** (found by E's safety net, 2026-10-22).
   `src/RuinaRPG.Client/Shared/CatalogoItemPicker.razor:13` passes `Placeholder="Placeholder"` as a
   literal, so the weapon/armor/shield pickers on NPC and Criatura sheets show the word "Placeholder".
   User decided to fix it in 2.1.0, not in E. The E safety net pins today's text; update that test with
   the fix.

6. **`EstrelaSelect` crashes on a malformed rulebook response** (found by E's safety net, 2026-10-22).
   The Estrela picker doesn't catch a bad `/api/rulebook` reply, so the tab breaks instead of showing an
   error. Not seen in normal use. User decided to fix it in 2.1.0.

7. **PI edit lost after a quick Nome edit** (found in E, 2026-10-23; pre-existing). Editing Nome and then
   Pontos de Ignição within the 400 ms auto-save window: the general save's reload (`LoadSheetAsync`)
   overwrites the form's PI before the pending PI save runs, so the PI edit is silently lost. Data loss;
   user decided to fix it in 2.1.0. Related to the 1.5.0 parked residual of `AutoSaveCoordinator` keeping
   one pending save.
8. **A refused skill-attribute change shows no message** (found in E, 2026-10-23; pre-existing). Changing
   a Perícia's attribute ignores a server refusal on all three sheet kinds
   (`FichaEstado.Atributos.cs`, kept on purpose in E). User decided to fix it in 2.1.0 (show the server's
   message like the other edits).

Also suggested by E's final review for 2.1.0 (not requested by the user; offer in the brainstorm): a common
page base for the sheet pages' remaining duplicated coordination code, and one dedicated "wire shape" fact
in `TipoDeFichaCliente` instead of reusing UI capability flags to pick Creature records.

## Questions to settle in the brainstorm (not answered yet)

- Cemitério: who sees the sheet there (owner, campaign, GM only)? Can the GM bring it back? Does it
  leave encounters, the campaign list, the player's "minhas fichas"? Is it Personagem-only or also
  NPC/Criatura?
- Removing a player from a campaign: what happens to their Personagem sheets in that campaign (go to the
  cemitério? stay? are deleted?), granted NPC/Criatura sheets, secret notes and diary entries addressed
  to them, and encounter participants.
- Deleting an account: which GM may do it (only the GM the player is linked to through the invite?), what
  happens to the player's sheets, campaigns memberships, images, logs and refresh tokens, and whether it
  is a hard delete or a deactivation. This removes data in production, so the release needs a
  `deploy/data-changes.yml` entry and the window rules of Técnico R0013–R0016 apply.

## Related open item from 2.0.0 D

- "Divergências encontradas" in `2026-10-18-versao-2-0-0-d-backend-design.md`: a player granted an
  NPC/Criatura sheet can delete it, against Campanha R0010. Fits naturally with items 2–4 (permissions
  of GM vs player over sheets) if the user wants it fixed.

## Queued for 2.2.0 (user, 2026-10-23)

- **A robust, complete backup system for production** (scheduled, kept off the production VM, restore
  tested, monitored). Until then, 2.0.0's cutover (H) takes only the backups the cutover needs. Comes after
  the 2.1.0 features.

## Decisões do usuário (2026-10-24)

- **Cemitério:** só o GM e o dono da ficha a veem. É a base das outras funcionalidades e começa primeiro.
- **Remover jogador da campanha:** as fichas dele nessa campanha vão para o cemitério.
- **Conta de jogador:** o GM escolhe entre **desativar** e **excluir**; nos dois casos o jogador sai das
  campanhas e as fichas dele vão para o cemitério.
- **Itens 1, 5–8:** decididos, só implementar.
- **Sugestões da revisão do E:** aceitas (base comum das páginas de ficha; fato próprio de "formato de
  dados" em `TipoDeFichaCliente`).
- **Divergências do D:**
  1. Jogador apagando NPC/Criatura concedido — corrigir, seguir a Campanha R0010 (só o GM apaga).
  2. Maestria PUT — seguir os requisitos (NPC/Criatura também editam, "mesma estrutura do Personagem").
  3. Orçamento de Perícias — só o Personagem; manter.
  4. Afeição PUT — só o Personagem; manter.
  5. Inventário do NPC aceitar qualquer item — correto; manter.
  6. Magias 404 × 403 — todos os tipos devem funcionar do mesmo jeito (padronizar).
  7. Peso carregado da Criatura — atualizar para a fórmula de 2026-09-08.
  8. Leituras 404 × 403 — averiguar caso a caso.
- **Propostas do G (`2026-10-23-g-propostas-sistema-rpg.md`):** só corrigir ortografia; o resto fica como está.
- **Deploy automático:** configurar (provisionar o pipeline) antes de publicar a 2.1.0.

## Cemitério — entregue em 2026-10-29 (branch `release/2.1.0`, até 7736482)

Spec `2026-10-24-versao-2-1-0-cemiterio-design.md`, plano `2026-10-24-versao-2-1-0-cemiterio.md`. Pendências
deixadas para os próximos itens da 2.1.0:

- **Antes da próxima migration:** o `RuinaRpgDbContextModelSnapshot` está com nomes de propriedade anteriores ao
  D (todo `migrations add` regera ~570 linhas de ruído de renomeação) — aceitar o snapshot regenerado num commit
  próprio; e `DatabaseInitializerTests` simula o 1.5.0 apagando `Morto` pelo nome — tornar genérico.
- **Remover jogador:** basta apagar o vínculo de membro (o motivo `RemovidoDaCampanha` já é calculado e o nome do
  dono já vem na resposta). A visão do ex-membro sobre a campanha (nome/imagens) não foi desenhada.
- **Contas:** acrescentar o motivo `ContaInativa` em `Cemiterio.Motivos`/`CemiterioDaFicha`.
- Menores, sem efeito em comportamento: o Painel duplica o cartão da ficha; o filtro do cemitério carrega a ficha
  rastreada + consulta o GM a cada escrita; a regra de template de rota está duplicada entre filtro e teste;
  qualquer 409 ao salvar a ficha recarrega o formulário; ao sair do cemitério o Atual acima do máximo só é cortado
  no próximo salvamento.

## Correções e acabamento — entregue em 2026-10-30 (até eb69bec)

Spec `2026-10-29-versao-2-1-0-correcoes-e-acabamento-design.md`, plano homônimo. Pendências menores, nenhuma bloqueia (para uma próxima rodada):

- (T1) ApagarAsync has two 404 paths (NotFound vs Negado); Delete not covered for outsider GM/Jogador
- (T2) stale XML doc on Carga (FichaStats.cs:77 mentions Exibida); Escudo desequipado for Criatura untested
- (T3) Update repeats Add's validation prelude + Criatura message (shared helper); stale doc comments (RedePartesPorTipoTests Maestrias class, AbaCombate header, AtualizarMaestriaAsync); hand-built TheoryData in AbaCombateTests
- (T4) EstrelaSelect catch misses NotSupportedException (200 with non-JSON content type); ItemFieldsEditor routes Estrela error through OnImageError (misleading name)
- (T5) stale *Carregado values while a reload is deferred (second save in burst can resend old loaded values); duplicate full reloads per burst (coalesce); LoadSheetAsync/RecarregarPontosDeIgnicaoAsync wrappers in three pages → Task 8 absorbs
- (T5) FichaNoCemiterio 409-after-deferred-reload test uses Task.Delay timings (100/1500 ms) — possible flake under load
- (T6) Lista_do_GM_per_kind is a single-InlineData Theory
- (T7) only the Combate load proves Formato-not-flag decoupling; AdicionarArma, Perícias, Características paths lack a decoupling test
- (T8) _concedida/Concedida and Estado.CampaignId assignment repeated in pages; SaveIfValidAsync tail (PUT→FalhaAoSalvar→Recarregar) repeated in three pages (protected SalvarFichaAsync would remove it); English comments in PaginaDeFicha.cs
- Bancos já semeados mantêm "Ultima Passiva" sem acento (a Tabela de Níveis só semeia uma vez).
- Criaturas existentes podem ter a Movimentação aumentada (armas e armaduras equipadas deixaram de pesar) — mencionar no texto de novidades.
