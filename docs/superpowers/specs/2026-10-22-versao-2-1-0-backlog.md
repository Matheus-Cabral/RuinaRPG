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
