# List Formatting and Breadcrumbs — Design

**Status:** Approved by user 2026-08-28 ("sim").

## Goal

Two remaining rough edges from the frontend work: (1) the Campanhas list and the Membros tab's per-member character-sheet list are bare, unformatted links carrying no useful information even though the API already returns it; (2) there is no breadcrumb trail anywhere, so a GM navigating between many campaigns/fichas/catalog pages has no sense of where they are or an easy way back up.

## Part 1 — List formatting

**`Campanhas.razor`** (`/campanhas`): replace the bare `<ul><li><a>Nome</a></li></ul>` with a Bootstrap `list-group`, one item per campaign, showing the name as the item's heading and the description (already fetched as `CampaignResponse.Descricao`, currently never rendered) beneath it:

```razor
<div class="list-group">
    @foreach (var campaign in _campaigns)
    {
        <a class="list-group-item list-group-item-action" href="@($"campanhas/{campaign.Id}")">
            <h3 class="mb-1">@campaign.Nome</h3>
            <p class="mb-0 text-muted">@campaign.Descricao</p>
        </a>
    }
</div>
```

**`CampanhaDetalhe.razor`'s Membros tab, per-member sheet list**: enrich each sheet link with data already present on `CharacterSheetResponse` (`Nivel`, `Vocacao`, `Linhagem`) but currently unused here:

```razor
<ul class="list-unstyled">
    @foreach (var sheet in _sheets.Where(s => s.OwnerId == member.UserId))
    {
        <li>
            <a href="@($"fichas/{sheet.Id}")">@(sheet.Nome ?? "(sem nome)")</a>
            <span class="text-muted">— Nível @sheet.Nivel, @sheet.Vocacao (@sheet.Linhagem)</span>
        </li>
    }
</ul>
```

No API/contract changes for either — both fields sets are already fetched.

## Part 2 — Breadcrumbs

### Component

`src/RuinaRPG.Client/Shared/Breadcrumbs.razor` — a small presentational component, Bootstrap's `nav`/`ol.breadcrumb`/`li.breadcrumb-item` markup, fed an explicit list built by the consuming page:

```razor
@if (Items is { Count: > 0 })
{
    <nav aria-label="breadcrumb">
        <ol class="breadcrumb">
            @for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var isLast = i == Items.Count - 1;
                <li class="breadcrumb-item @(isLast ? "active" : "")" aria-current="@(isLast ? "page" : null)">
                    @if (!isLast && item.Href is not null)
                    {
                        <a href="@item.Href">@item.Text</a>
                    }
                    else
                    {
                        @item.Text
                    }
                </li>
            }
        </ol>
    </nav>
}

@code {
    [Parameter, EditorRequired] public List<BreadcrumbItem> Items { get; set; } = new();
}

public record BreadcrumbItem(string Text, string? Href = null);
```

`BreadcrumbItem` lives in this file (a public record beside the component), mirroring how the codebase already declares small local record types beside the component that uses them (e.g. `NpcVisibilityBody` in `CampanhaDetalhe.razor`).

No shared state, no route-to-label registry: each page builds its own `List<BreadcrumbItem>` from data it already has (or, for 3 pages, one extra cheap fetch — see below) and passes it to `<Breadcrumbs Items="@_breadcrumbs" />`, placed directly above the page's `<h1>`.

### Scope

GM-facing pages + the two shared/role-agnostic pages (`Painel`, `Compendio`). Not `MinhaCampanha.razor` (player view — already unreachable in the product today per a prior round's known gap; out of scope here too).

### Per-page trails

**Static** (compile-time-known, no fetch): 
- `Painel`: *(no crumb — it's the root)*
- `Campanhas`: Painel > Campanhas
- `NpcsDoGm`: Painel > NPCs do GM
- `BestiarioDoGm`: Painel > Bestiário do GM
- `Catalogo`: Painel > Catálogo de Itens
- `BancoDeMagias`: Painel > Banco de Magias e Habilidades
- `GmJogadores`: Painel > Jogadores
- `GmConvites`: Painel > Convidar Jogador
- `Compendio`: Painel > Compêndio de Regras

**Dynamic, using data the page already loads (no new fetch)**:
- `FichaDeNpc`: Painel > NPCs do GM > *{Nome, once loaded}*
- `FichaDeCriatura`: Painel > Bestiário do GM > *{Nome, once loaded}*
- `CatalogoItemForm`: Painel > Catálogo de Itens > *{`_form.Nome` once loaded in edit mode, else "Novo Item"}*
- `BancoDeMagiasForm`: Painel > Banco de Magias e Habilidades > *{`_form.Nome` once loaded in edit mode, else "Nova Entrada"}*

**Dynamic, needs the owning campaign's name** — see technical note below:
- `CampanhaDetalhe`: Painel > Campanhas > *{Nome da Campanha}*
- `FichaDePersonagem`: Painel > Campanhas > *{Nome da Campanha}* > *{Nome da Ficha}*
- `GerenciadorDeEncontros`: Painel > Campanhas > *{Nome da Campanha}* > *{Nome do Encontro}*

### Technical note: getting a campaign's name from just its id

There is no `GET /campaigns/{id}` endpoint — only `GET /campaigns` (GM-only, lists all campaigns the caller owns; already used by `Campanhas.razor`). Rather than add a backend endpoint (this stays a frontend-only change, per the user's framing), these three pages call `GET /campaigns` themselves and find the matching one client-side:

```csharp
private async Task<string?> LoadCampaignNameAsync(string campaignId)
{
    var response = await Http.GetAsync("campaigns");
    if (!response.IsSuccessStatusCode) return null;
    var campaigns = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>() ?? new();
    return campaigns.FirstOrDefault(c => c.Id == campaignId)?.Nome;
}
```

This is duplicated three times (once per page) rather than extracted into a shared service — consistent with the "each page owns its own trail" architecture choice (no shared breadcrumb state), and each occurrence is five lines. `GET /campaigns` is GM-only: a non-GM viewer (e.g. a player on their own `FichaDePersonagem`) gets a non-success response, `LoadCampaignNameAsync` returns `null`, and the calling page builds its trail without the campaign hop (`Painel > {Nome da Ficha}` instead of `Painel > Campanhas > {Nome da Campanha} > {Nome da Ficha}`) rather than showing an error or breaking the page — the same resilient-degradation pattern this codebase already uses everywhere else for a failed fetch.

**Bonus, cheap because the data is already being fetched for the breadcrumb**: `CampanhaDetalhe.razor`'s `<h1>Campanha</h1>` (currently always this generic literal, on every campaign) becomes `<h1>@(_campaignName ?? "Campanha")</h1>`.

## Verification

`dotnet build` (0/0) plus an actual rendered check this time — the final review of the previous round discovered Playwright with headless Chromium is available in this environment (earlier rounds incorrectly believed no screenshot tool existed here). Each task's implementer should render its changed page(s) and confirm the breadcrumb/list markup actually appears styled as intended, not just that the build is clean.

## Execution

`docs/superpowers/plans/2026-08-28-lists-and-breadcrumbs.md`, executed via `superpowers:subagent-driven-development` in an isolated worktree.
