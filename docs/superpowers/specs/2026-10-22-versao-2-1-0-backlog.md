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
