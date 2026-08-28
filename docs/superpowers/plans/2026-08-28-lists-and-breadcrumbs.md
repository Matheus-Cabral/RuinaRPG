# List Formatting and Breadcrumbs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Format the Campanhas list and the Membros tab's per-member sheet list with the useful data the API already returns but doesn't render, and add a `<Breadcrumbs>` trail to every GM-facing page plus the two shared/role-agnostic pages (`Painel`, `Compendio`).

**Architecture:** A small presentational `Breadcrumbs` component (Bootstrap `nav`/`ol.breadcrumb`) fed an explicit `List<BreadcrumbItem>` that each page builds itself from data it already has — no shared state, no route-to-label registry. Three pages (`CampanhaDetalhe`, `FichaDePersonagem`, `GerenciadorDeEncontros`) additionally need the name of the campaign they belong to; since no `GET /campaigns/{id}` endpoint exists, they reuse the existing GM-only `GET /campaigns` list and find the match client-side, degrading gracefully (skip the campaign hop) if that call fails for a non-GM viewer.

**Tech Stack:** Blazor WebAssembly 8, Bootstrap 5 CSS (already vendored).

**Spec:** `docs/superpowers/specs/2026-08-28-lists-and-breadcrumbs-design.md` — read this first for the full reasoning and the complete per-page trail table.

## Global Constraints

- `Breadcrumbs` items are placed directly above the page's `<h1>`.
- Each page builds its own breadcrumb list as a **computed property** (e.g. `private List<BreadcrumbItem> Crumbs => new() { ... }`), not a field assigned once during load — this guarantees the trail is always current (e.g. shows the entity's real name once it loads) without needing to remember to update a cached field, and costs nothing extra since Blazor already re-renders after every state change in this codebase's existing patterns.
- Do **not** extract the "fetch `GET /campaigns`, find by id" logic into a shared service — it's duplicated exactly 3 times (`CampanhaDetalhe`, `FichaDePersonagem`, `GerenciadorDeEncontros`), each occurrence ~5 lines. This matches the deliberate "no shared breadcrumb state" architecture choice from the spec.
- `GET /campaigns` is GM-only. When it returns a non-success response (e.g. a player viewing their own `FichaDePersonagem`), the campaign-name lookup returns `null` and the calling page's breadcrumb list must simply omit the campaign hop (e.g. `Painel > {Nome da Ficha}` instead of `Painel > Campanhas > {Nome da Campanha} > {Nome da Ficha}`) — never show an error, never break the page. This is the same resilient-degradation pattern already used everywhere else in this codebase for a failed fetch (`if (!response.IsSuccessStatusCode) { ...; return; }`).
- `dotnet build` must stay at 0 warnings/0 errors after every task.
- Verification per task: `dotnet build` (0/0) **and** an actual rendered check — Playwright with headless Chromium is available in this environment (confirmed in the prior round's final review). Serve `src/RuinaRPG.Client/wwwroot` over local HTTP (or run `dotnet run --project src/RuinaRPG.Client` standalone) and use Playwright to load the changed route, confirm the breadcrumb/list markup is actually present and styled (e.g. `.breadcrumb-item` elements exist with the expected text, `.list-group-item` elements render), not just that the build is clean. Remember this app is Blazor WASM standalone with no server prerendering — a plain `curl` only ever returns the static shell, never client-rendered markup, so `curl`-only verification proves nothing here; an actual browser (Playwright) is required to see rendered output.

---

### Task 1: `Breadcrumbs` component

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Breadcrumbs.razor`

**Interfaces:**
- Produces: `<Breadcrumbs Items="@someList" />` where `someList` is a `List<BreadcrumbItem>`, `BreadcrumbItem(string Text, string? Href = null)` — the public record every later task's pages construct. The last item in the list is rendered as the current page (no link, `active`/`aria-current="page"`); every earlier item with a non-null `Href` renders as a link.

- [ ] **Step 1: Create `Breadcrumbs.razor`**

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

- [ ] **Step 2: Build and verify rendering**

```bash
dotnet build
```
Expected: 0 warnings/0 errors. No page consumes this component yet, so there's nothing to render-check until Task 2/3 land — this step only confirms the new component compiles.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Shared/Breadcrumbs.razor
git commit -m "feat: add reusable Breadcrumbs component"
```

---

### Task 2: List formatting — `Campanhas.razor` and `CampanhaDetalhe.razor`'s Membros tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/Campanhas.razor`
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`

**Interfaces:**
- Consumes: `CampaignResponse.Descricao` (already fetched, currently unused in `Campanhas.razor`) and `CharacterSheetResponse.Nivel`/`Vocacao`/`Linhagem` (already fetched, currently unused in `CampanhaDetalhe.razor`'s Membros tab).
- No `@code` changes in either file — both are pure markup enrichments of already-loaded data.

- [ ] **Step 1: `Campanhas.razor` — replace the bare list with a Bootstrap list-group**

Replace:
```razor
<ul>
    @foreach (var campaign in _campaigns)
    {
        <li>
            <a href="@($"campanhas/{campaign.Id}")">@campaign.Nome</a>
        </li>
    }
</ul>
```
with:
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

- [ ] **Step 2: `CampanhaDetalhe.razor` — enrich the Membros tab's per-member sheet list**

Inside the Membros `<TabPage>`, replace:
```razor
<ul>
    @foreach (var sheet in _sheets.Where(s => s.OwnerId == member.UserId))
    {
        <li><a href="@($"fichas/{sheet.Id}")">@(sheet.Nome ?? "(sem nome)")</a></li>
    }
</ul>
```
with:
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

- [ ] **Step 3: Build and verify rendering with Playwright**

```bash
dotnet build
```
Then serve `src/RuinaRPG.Client/wwwroot` (or run the dev server standalone) and use Playwright to load `/campanhas` and confirm `.list-group-item` elements render with an `<h3>` and a `<p class="text-muted">` inside. The Membros tab's enriched sheet text can't be checked end-to-end without a logged-in session and real data — visually confirm the markup change is syntactically correct and matches the brief instead (`dotnet build` succeeding on a `.razor` file with C#-in-markup interpolation is a strong signal, but do a final read-through of the committed diff against the brief's exact target).

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/Campanhas.razor src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor
git commit -m "style: format campaign and campaign-member sheet lists with real data"
```

---

### Task 3: Breadcrumbs on static-trail pages

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/Painel.razor`
- Modify: `src/RuinaRPG.Client/Pages/Campanhas.razor`
- Modify: `src/RuinaRPG.Client/Pages/NpcsDoGm.razor`
- Modify: `src/RuinaRPG.Client/Pages/BestiarioDoGm.razor`
- Modify: `src/RuinaRPG.Client/Pages/Catalogo.razor`
- Modify: `src/RuinaRPG.Client/Pages/BancoDeMagias.razor`
- Modify: `src/RuinaRPG.Client/Pages/GmJogadores.razor`
- Modify: `src/RuinaRPG.Client/Pages/GmConvites.razor`
- Modify: `src/RuinaRPG.Client/Pages/Compendio.razor`

**Interfaces:**
- Consumes: `Breadcrumbs`/`BreadcrumbItem` from Task 1 (`src/RuinaRPG.Client/Shared/`).
- No other `@code` changes — every trail here is a compile-time-known literal list, no new fields or fetches.

This is one small, same-shape edit repeated across 9 files: insert `<Breadcrumbs Items="@Crumbs" />` immediately above the existing `<h1>`, and add one computed property to each file's `@code` block. `Painel.razor` gets no breadcrumb at all (it's the root — skip this file's `<h1>` insertion, it needs no change).

- [ ] **Step 1: `Campanhas.razor`** — above `<h1>Campanhas</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; in `@code`, add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Campanhas"),
};
```

- [ ] **Step 2: `NpcsDoGm.razor`** — above `<h1>NPCs do GM</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("NPCs do GM"),
};
```

- [ ] **Step 3: `BestiarioDoGm.razor`** — above `<h1>Bestiário do GM</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Bestiário do GM"),
};
```

- [ ] **Step 4: `Catalogo.razor`** — above `<h1>Catálogo de Itens e Equipamentos</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Catálogo de Itens"),
};
```

- [ ] **Step 5: `BancoDeMagias.razor`** — above `<h1>Banco de Magias e Habilidades</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Banco de Magias e Habilidades"),
};
```

- [ ] **Step 6: `GmJogadores.razor`** — above `<h1>Jogadores</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Jogadores"),
};
```

- [ ] **Step 7: `GmConvites.razor`** — above `<h1>Convidar Jogador</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Convidar Jogador"),
};
```

- [ ] **Step 8: `Compendio.razor`** — above `<h1>Compêndio de Regras</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Compêndio de Regras"),
};
```

- [ ] **Step 9: Build and verify rendering with Playwright**

```bash
dotnet build
```
Then use Playwright against a served build to load at least 3 of the 8 changed routes (e.g. `/campanhas`, `/npcs`, `/compendio`) and confirm each renders a `.breadcrumb` with the expected two `.breadcrumb-item`s, the first a link to `/painel`, the second `active`/text-only.

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Client/Pages/Campanhas.razor src/RuinaRPG.Client/Pages/NpcsDoGm.razor src/RuinaRPG.Client/Pages/BestiarioDoGm.razor src/RuinaRPG.Client/Pages/Catalogo.razor src/RuinaRPG.Client/Pages/BancoDeMagias.razor src/RuinaRPG.Client/Pages/GmJogadores.razor src/RuinaRPG.Client/Pages/GmConvites.razor src/RuinaRPG.Client/Pages/Compendio.razor
git commit -m "feat: add breadcrumbs to static-trail pages"
```

---

### Task 4: Breadcrumbs on pages with an already-loaded entity name

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor`
- Modify: `src/RuinaRPG.Client/Pages/CatalogoItemForm.razor`
- Modify: `src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`

**Interfaces:**
- Consumes: `Breadcrumbs`/`BreadcrumbItem` from Task 1. Consumes `_form.Nome`, already populated by each page's existing load logic (`FichaDeNpc`/`FichaDeCriatura`: set in their existing sheet-load method; `CatalogoItemForm`/`BancoDeMagiasForm`: set in `OnInitializedAsync` when editing an existing item/entry) — no new fetch needed.
- No other `@code` changes beyond the one new computed property per file.

- [ ] **Step 1: `FichaDeNpc.razor`** — above `<h1>Ficha de NPC</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("NPCs do GM", "npcs"),
    new(string.IsNullOrWhiteSpace(_form.Nome) ? "Ficha de NPC" : _form.Nome),
};
```

- [ ] **Step 2: `FichaDeCriatura.razor`** — above `<h1>Ficha de Criatura</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Bestiário do GM", "bestiario"),
    new(string.IsNullOrWhiteSpace(_form.Nome) ? "Ficha de Criatura" : _form.Nome),
};
```

- [ ] **Step 3: `CatalogoItemForm.razor`** — above `<h1>@(ItemId is null ? "Novo Item" : "Editar Item")</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Catálogo de Itens", "catalogo"),
    new(ItemId is null ? "Novo Item" : (string.IsNullOrWhiteSpace(_form.Nome) ? "Editar Item" : _form.Nome)),
};
```

- [ ] **Step 4: `BancoDeMagiasForm.razor`** — above `<h1>@(EntryId is null ? "Nova Entrada" : "Editar Entrada")</h1>`, add `<Breadcrumbs Items="@Crumbs" />`; add:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Banco de Magias e Habilidades", "banco-de-magias"),
    new(EntryId is null ? "Nova Entrada" : (string.IsNullOrWhiteSpace(_form.Nome) ? "Editar Entrada" : _form.Nome)),
};
```

- [ ] **Step 5: Build and verify rendering with Playwright**

```bash
dotnet build
```
Then use Playwright against a served build to load `/catalogo/novo` and confirm the breadcrumb shows `Painel > Catálogo de Itens > Novo Item` (the create route needs no auth/data to render correctly, unlike the edit routes and the two Ficha pages which need a real logged-in session with existing data — for those, a read-through of the committed diff against the brief's exact target is the verification, same as Task 2 Step 3).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDeNpc.razor src/RuinaRPG.Client/Pages/FichaDeCriatura.razor src/RuinaRPG.Client/Pages/CatalogoItemForm.razor src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor
git commit -m "feat: add breadcrumbs to pages with an already-loaded entity name"
```

---

### Task 5: Breadcrumbs on pages needing the owning campaign's name

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/GerenciadorDeEncontros.razor`

**Interfaces:**
- Consumes: `Breadcrumbs`/`BreadcrumbItem` from Task 1, `CampaignResponse` (`RuinaRPG.Contracts.Campaigns`, already `@using`'d in all three files) from the reused `GET /campaigns` list endpoint.
- Produces (for reference, not consumed elsewhere): each file gets a private `LoadCampaignNameAsync(string campaignId)` helper, duplicated three times per the Global Constraints (no shared service).

- [ ] **Step 1: `CampanhaDetalhe.razor`**

`CampaignId` is already a route parameter here, so the campaign-name fetch can join the page's existing parallel load. Add a field and helper method:
```csharp
private string? _campaignName;

private async Task LoadCampaignNameAsync()
{
    var response = await Http.GetAsync("campaigns");
    if (!response.IsSuccessStatusCode) return;
    var campaigns = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>() ?? new();
    _campaignName = campaigns.FirstOrDefault(c => c.Id == CampaignId)?.Nome;
}
```
Add `LoadCampaignNameAsync()` to the existing `OnInitializedAsync`'s `Task.WhenAll(...)` call (currently `Task.WhenAll(LoadMembersAsync(), LoadSheetsAsync(), LoadDiaryAsync(), LoadEncountersAsync(), LoadAttachmentsAsync(), LoadSecretNotesAsync())` — add `LoadCampaignNameAsync()` as one more argument to that same `Task.WhenAll`).

Add the breadcrumb property:
```csharp
private List<BreadcrumbItem> Crumbs => new()
{
    new("Painel", "painel"),
    new("Campanhas", "campanhas"),
    new(_campaignName ?? "Campanha"),
};
```

Change:
```razor
<h1>Campanha</h1>
```
to:
```razor
<h1>@(_campaignName ?? "Campanha")</h1>
```
and add `<Breadcrumbs Items="@Crumbs" />` directly above it.

- [ ] **Step 2: `FichaDePersonagem.razor`**

Here `CampaignId` is **not** a route parameter (only `SheetId` is) — it only becomes known after `LoadSheetAsync()` fetches the `CharacterSheetResponse`, which has a `CampaignId` field. This requires two changes beyond just adding the fetch:

1. Add a field to capture it, and set it inside the existing `LoadSheetAsync()` method (find the line `_form.Nome = sheet!.Nome;` and add a line near it):
```csharp
private string? _campaignId;
private string? _campaignName;
```
Inside `LoadSheetAsync()`, right after `var sheet = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();`, add:
```csharp
_campaignId = sheet!.CampaignId;
```

2. Add the same-shaped helper as Step 1, but reading `_campaignId` instead of a route parameter:
```csharp
private async Task LoadCampaignNameAsync()
{
    if (_campaignId is null) return;
    var response = await Http.GetAsync("campaigns");
    if (!response.IsSuccessStatusCode) return;
    var campaigns = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>() ?? new();
    _campaignName = campaigns.FirstOrDefault(c => c.Id == _campaignId)?.Nome;
}
```

3. **Resequence `OnInitializedAsync`** — it currently runs everything in one `Task.WhenAll(LoadSheetAsync(), LoadLevelUpNoticeAsync(), LoadTabs2And3Async(), LoadRacialAbilityAsync(), LoadSpellAbilitiesAsync(), LoadRunesAsync(), LoadMasteriesAsync(), LoadInventoryAsync(), LoadArtifactsAsync(), LoadAffectionsAsync(), LoadTraitsAsync(), LoadDiaryAsync())`, but `LoadCampaignNameAsync` needs `_campaignId`, which only exists after `LoadSheetAsync` completes. Change it to:
```csharp
protected override async Task OnInitializedAsync()
{
    await LoadSheetAsync();
    await Task.WhenAll(LoadLevelUpNoticeAsync(), LoadTabs2And3Async(),
        LoadRacialAbilityAsync(), LoadSpellAbilitiesAsync(), LoadRunesAsync(), LoadMasteriesAsync(),
        LoadInventoryAsync(), LoadArtifactsAsync(), LoadAffectionsAsync(), LoadTraitsAsync(), LoadDiaryAsync(),
        LoadCampaignNameAsync());
}
```
This is a deliberate, minimal behavior change (not pure markup): `LoadSheetAsync` now runs to completion before the rest start, instead of all in one parallel batch — required because nothing else needs `CampaignId`, only this new breadcrumb fetch does, and it can't start without it. The added latency is one sequential round-trip (`LoadSheetAsync`'s own request) instead of full parallelism; acceptable and necessary, not a regression to flag as a concern.

4. Add the breadcrumb property:
```csharp
private List<BreadcrumbItem> Crumbs
{
    get
    {
        var crumbs = new List<BreadcrumbItem> { new("Painel", "painel") };
        if (_campaignName is not null && _campaignId is not null)
            crumbs.Add(new("Campanhas", "campanhas"));
        if (_campaignName is not null && _campaignId is not null)
            crumbs.Add(new(_campaignName, $"campanhas/{_campaignId}"));
        crumbs.Add(new(string.IsNullOrWhiteSpace(_form.Nome) ? "Ficha de Personagem" : _form.Nome));
        return crumbs;
    }
}
```
(This degrades to `Painel > {Nome da Ficha}` when `_campaignName` is null — e.g. a player viewing their own sheet, where `GET /campaigns` 403s — per the Global Constraints.)

Add `<Breadcrumbs Items="@Crumbs" />` directly above `<h1>Ficha de Personagem</h1>` (the `<h1>` text itself is unchanged here, unlike `CampanhaDetalhe`).

- [ ] **Step 3: `GerenciadorDeEncontros.razor`**

`CampaignId` is already a route parameter here too. Add:
```csharp
private string? _campaignName;

private async Task LoadCampaignNameAsync()
{
    var response = await Http.GetAsync("campaigns");
    if (!response.IsSuccessStatusCode) return;
    var campaigns = await response.Content.ReadFromJsonAsync<List<CampaignResponse>>() ?? new();
    _campaignName = campaigns.FirstOrDefault(c => c.Id == CampaignId)?.Nome;
}
```
This file also needs `@using RuinaRPG.Contracts.Campaigns` added (it currently only has `@using RuinaRPG.Contracts.Encounters`).

Change the existing `OnInitializedAsync`'s first line from `await LoadAsync();` to `await Task.WhenAll(LoadAsync(), LoadCampaignNameAsync());` (both are independent — `CampaignId` is already available from the route, no sequencing needed, unlike `FichaDePersonagem`).

Add the breadcrumb property:
```csharp
private List<BreadcrumbItem> Crumbs
{
    get
    {
        var crumbs = new List<BreadcrumbItem> { new("Painel", "painel") };
        if (_campaignName is not null)
        {
            crumbs.Add(new("Campanhas", "campanhas"));
            crumbs.Add(new(_campaignName, $"campanhas/{CampaignId}"));
        }
        crumbs.Add(new(_encounter?.Nome ?? "Encontro"));
        return crumbs;
    }
}
```
Add `<Breadcrumbs Items="@Crumbs" />` directly above `<h1>Encontro — Rodada @_encounter?.CurrentRound</h1>`.

- [ ] **Step 4: Verify no unintended `@code` drift beyond what this task specifies**

For all three files, confirm every pre-existing field/method/binding is untouched except the specific additions listed above — this task adds new code, it does not refactor or rename anything that already exists.

- [ ] **Step 5: Build and verify rendering with Playwright**

```bash
dotnet build
```
Confirm 0 warnings/0 errors (in particular, the new `@using RuinaRPG.Contracts.Campaigns` in `GerenciadorDeEncontros.razor` must not collide with anything already there). A full end-to-end render check of these three pages needs a real logged-in GM session with an existing campaign/sheet/encounter — not achievable standalone the way the static-trail pages in Task 3 were. Instead: read through the committed diff for all three files against this task's exact target code, and reason explicitly about the `FichaDePersonagem.razor` resequencing (`LoadSheetAsync` now awaited before the rest) — confirm nothing that used to run in the first `Task.WhenAll` depended on anything `LoadSheetAsync` doesn't set, i.e. confirm the resequencing genuinely doesn't change what any of `LoadLevelUpNoticeAsync`/`LoadTabs2And3Async`/etc. do or depend on, only when `LoadSheetAsync` itself finishes relative to them.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/GerenciadorDeEncontros.razor
git commit -m "feat: add breadcrumbs to pages that need their owning campaign's name"
```
