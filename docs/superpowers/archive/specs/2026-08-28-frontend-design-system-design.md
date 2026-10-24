# Front-End Design System & Responsive Shell — Design Spec

**Status:** Approved by user 2026-08-28 (section 1 confirmed interactively; sections 2-3 confirmed via "pode executar, tudo bem" after section 1).

## Goal

The Client (`src/RuinaRPG.Client`) is today the untouched Blazor WebAssembly scaffold: default template layout, default Bootstrap styling, default favicon, an "About" link to Microsoft's docs still in `MainLayout.razor`. This spec covers building a real visual identity and a responsive, auth-aware shell around it — the design system and layout that every page will inherit, not a page-by-page redesign (that's an explicit, separate follow-up round).

## Scope (Round 1 — this spec)

- A color/typography design system themed around the game's own mythology (Helônios/Sol, Nyxara/Lua), in light and dark variants.
- Base component re-skinning (buttons, cards, forms, tables, alerts) via CSS custom properties layered on top of the existing Bootstrap foundation — no markup rewrites on existing pages.
- A responsive layout shell: sidebar navigation, top bar, content area, working across desktop/tablet/mobile.
- A light/dark theme toggle: system-preference default, user override, persisted, no flash-of-wrong-theme on load.
- Auth-aware sidebar: built from scratch, since the Client has no client-side auth-state system today (tokens are stored in `localStorage` via `AuthStateService` but nothing decodes them into a usable identity).

**Explicitly out of scope for this spec:** redesigning the internals of any individual page (Ficha de Personagem's attribute grid, Gerenciador de Encontros' participant table, etc.) — those inherit the new base component styles automatically, but their bespoke layouts are untouched. Also out of scope: fixing the pre-existing product gap where a Jogador has no reachable page besides Compêndio (no nav link ever pointed at `MinhaCampanha.razor` or a character sheet in any of the plans that built those pages) — the new nav renders what's *actually* reachable per role today, honestly, rather than linking to something that doesn't work yet.

## 1. Visual identity — colors & type

Grounded directly in `Ruína RPG - Sistema Básico.md` §7 (Linhagens e Variantes): every playable lineage has a Sun-variant (marked by **Helônios**, the Lion archaios — "sangue quente", warm-blooded, courage, leadership, daylight) and a Moon-variant (marked by **Nyxara** — Fae-touched, "mente calma", mystical, night). This is the app's own light/dark mythology, not a generic reskin.

- **Tema Sol (light):** warm parchment/cream background (not stark white), leonine gold as primary accent, deep amber/bronze for emphasis and interactive states, warm brown ink for body text. Evokes daylight on old paper.
- **Tema Lua (dark):** deep indigo-to-near-black background (cool blue-black, not pure black), silver/pale-lilac as primary accent, soft moonlight-white for emphasis, cool grey-lilac body text. Evokes a Fae-touched night.
- Both themes share identical structural tokens (spacing scale, border radii, shadow depth, layout geometry) — only color tokens swap between them, so nothing shifts or reflows when the user switches.
- **Typography:** `Cormorant Garamond` (open-license serif) for all headings/titles — elegant, illuminated-manuscript feel, still legible down to smaller heading sizes — paired with `Inter` (open-license sans) for body text and the dense numeric tables character sheets are full of. Both self-hosted as woff2 under `wwwroot/fonts/` rather than a Google Fonts CDN dependency — this runs on a homelab box behind a Cloudflare Tunnel and shouldn't need external network access to render correctly.
- **Base framework:** keep Bootstrap (already wired into every existing page's markup — `btn`, `form-control`, grid classes). Re-skin through CSS custom properties (`--rr-*` tokens) plus a thin custom stylesheet layer, rather than replacing it — a full replacement would force touching all ~20 existing pages now, which the agreed Round-1 scope explicitly defers.

## 2. Layout & responsive behavior

- **Sidebar:** fixed-width (~260px) persistent sidebar on desktop (Bootstrap's `lg` breakpoint, ≥992px). Below that, it becomes a collapsible off-canvas drawer triggered by a hamburger toggle in the top bar — re-styling the collapse mechanic the Blazor template's `NavMenu.razor` already scaffolds (`collapseNavMenu`/`NavMenuCssClass`) rather than reinventing it.
- **Breakpoints:** reuse Bootstrap's existing scale (`sm` 576 / `md` 768 / `lg` 992 / `xl` 1200) rather than inventing new ones, since grid classes on existing pages already assume it.
- **Top bar:** a slim header, visible at every size, hosting: hamburger toggle (mobile/tablet only), app name/crest, and the theme toggle pinned to the right.
- **Theme toggle:** a compact Sun/Moon sliding switch in the top bar. Default follows `prefers-color-scheme`; an explicit user choice overrides and persists to `localStorage`. A small inline `<script>` in `index.html`'s `<head>` reads that stored preference (falling back to `matchMedia`) and sets a `data-theme` attribute on `<html>` before Blazor/CSS paints, avoiding a flash of the wrong theme.
- **Content width:** the article/content area gets a centered `max-width` (~1100px) with responsive padding, so text- and table-heavy pages stay readable on wide desktop monitors instead of stretching edge-to-edge.

## 3. Auth-aware navigation

The Client has no client-side auth-state system today — `AuthStateService` only stores/retrieves the raw JWT strings. Building real login/role awareness into the sidebar requires adding one.

- New `TokenAuthenticationStateProvider : AuthenticationStateProvider` (`src/RuinaRPG.Client/Services/`): decodes the stored JWT's payload (base64url, no signature verification needed client-side — the server is still the enforcement point) into a `ClaimsPrincipal`, using the same claim types the API already issues (`sub`, `role`, `nickname`). No new API call — purely a client-side decode of the token `AuthStateService` already holds. Exposes a way to force a re-evaluation (`NotifyAuthenticationStateChangedAsync`) so login/logout update the UI immediately.
- Wire it into `Program.cs`: `AddAuthorizationCore()` + register the provider. Wrap `App.razor`'s router in `<CascadingAuthenticationState>`.
- `NavMenu.razor` rewritten around `<AuthorizeView>` with three states:
  - **Logged out:** Início, Entrar, Cadastrar only.
  - **Logged in, GM:** Início, the existing GM tool links (Convidar Jogador, Jogadores, Catálogo de Itens, NPCs do GM, Bestiário do GM, Banco de Magias e Habilidades, Campanhas), Compêndio de Regras, Sair.
  - **Logged in, Jogador:** Início, Compêndio de Regras, Sair. (This is genuinely everything reachable for a Jogador today — see Scope note above.)
- `Login.razor`/`Cadastro.razor` call the provider's notify method right after saving tokens (they currently just save and navigate); `Painel.razor`'s logout does the same right after clearing.

## Testing

This is markup/CSS/client-side-only work with no backend behavior change — verified by `dotnet build` (0 warnings) plus manual verification through the running stack (`make deploy`): confirm both themes render correctly, confirm the sidebar collapses/expands at each breakpoint, confirm nav links change correctly across all three auth states (logged out / GM / Jogador), confirm the theme choice persists across a reload and defaults to system preference on first visit. No new automated test infrastructure exists for Blazor component rendering in this codebase (confirmed in an earlier session) and this spec doesn't introduce one — matching how every other Client-only task in this codebase has been verified.
