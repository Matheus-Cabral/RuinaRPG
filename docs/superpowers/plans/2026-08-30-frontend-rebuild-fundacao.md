# Front-End Rebuild — Fundação (MudBlazor) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the entire visual layer of `src/RuinaRPG.Client` (`Layout/`, `Shared/`, `wwwroot/css/*`) with a fresh MudBlazor-based foundation — new app shell, theme, and reusable components — keeping only the existing `--rr-*` color tokens and font choices. This plan covers Fase 0 (Fundação) only; Fase 1 (the 3 Ficha pages) is a separate follow-up plan written once this one is merged, since it depends on the components this plan produces.

**Architecture:** MudBlazor supplies structure/interaction (layout, selects, autocomplete, tabs); it never owns color — every `--mud-palette-*` CSS custom property MudBlazor generates is overridden in `theme.css` to point at the existing `--rr-*` tokens, so `theme.css` stays the single source of color truth for both the Sol (light) and Lua (dark) themes. Bootstrap and `components.css` are removed outright, not phased out — pages outside this plan's scope (everything except the shell chrome, which wraps every page) may render unstyled until their own future migration; that's an explicit, accepted tradeoff (see the spec).

**Tech Stack:** Blazor WebAssembly (.NET 8, unchanged), MudBlazor 9.9.0 (new), bUnit 2.9.0 + xunit 2.4.2 (new test project, first Blazor-component tests in this repo).

**Spec:** `docs/superpowers/specs/2026-08-30-frontend-rebuild-fundacao-fichas-design.md` — read it first; this plan implements only its Fase 0 (Fundação) section.

## Global Constraints

- No color/hex value is introduced anywhere outside `wwwroot/css/theme.css`'s existing `--rr-*` tokens. No font is introduced outside Cormorant Garamond (headings) / Inter (body), both already self-hosted under `wwwroot/fonts/`.
- `dotnet build` must stay at 0 warnings / 0 errors after every task.
- Any `Shared/` component still referenced by a page outside this plan's scope (everything except the 3 Fichas, which are a future plan) must keep its existing public API (parameter names/types) so those pages keep compiling. Their *visual* appearance is explicitly not a concern of this plan.
- TDD per `Docs/Requisitos/Requisitos - Técnico.md` R0011: every task with non-trivial logic (cascading selects, search/selection behavior, badge glyph rules) gets a failing bUnit test before the implementation.
- `bootstrap.min.css` and `components.css` are removed in Task 1, not deferred.

---

## Task 1: Add MudBlazor, wire the host page, remove Bootstrap/components.css

**Files:**
- Modify: `src/RuinaRPG.Client/RuinaRPG.Client.csproj`
- Modify: `src/RuinaRPG.Client/Program.cs`
- Modify: `src/RuinaRPG.Client/_Imports.razor`
- Modify: `src/RuinaRPG.Client/wwwroot/index.html`
- Delete: `src/RuinaRPG.Client/wwwroot/css/bootstrap/` (entire directory)
- Delete: `src/RuinaRPG.Client/wwwroot/css/components.css`

**Interfaces:**
- Produces: MudBlazor services registered in DI (`AddMudServices()`), MudBlazor's static assets reachable at `_content/MudBlazor/*` — every later task depends on this.

This is a wiring task with no new logic of its own — verified by build + actually serving the app, not by a unit test.

- [ ] **Step 1: Add the MudBlazor package reference**

Edit `src/RuinaRPG.Client/RuinaRPG.Client.csproj`, inside the existing `<ItemGroup>` that lists `PackageReference`s, add:

```xml
<PackageReference Include="MudBlazor" Version="9.9.0" />
```

- [ ] **Step 2: Restore and confirm the package resolves**

Run: `dotnet restore src/RuinaRPG.Client/RuinaRPG.Client.csproj`
Expected: restores cleanly, no NU1xxx errors.

- [ ] **Step 3: Register MudBlazor services in `Program.cs`**

In `src/RuinaRPG.Client/Program.cs`, add the using and the service registration:

```csharp
using MudBlazor.Services;
```

(add alongside the existing `using` lines at the top)

```csharp
builder.Services.AddMudServices();
```

(add anywhere after `builder.Services.AddBlazoredLocalStorage();` — order relative to the other DI registrations doesn't matter)

- [ ] **Step 4: Add the global `@using MudBlazor` import**

Every later task's Razor markup uses bare MudBlazor component tags (`<MudPaper>`, `<MudSelect>`, …) and enums (`Color.Inherit`, `Typo.h6`, …) without a per-file `@using` — that only resolves via a project-wide import. Add one line to `src/RuinaRPG.Client/_Imports.razor`, alongside the existing `@using RuinaRPG.Client.Shared` line:

```razor
@using MudBlazor
```

- [ ] **Step 5: Rewire `wwwroot/index.html`**

Remove these two lines:
```html
<link rel="stylesheet" href="css/bootstrap/bootstrap.min.css" />
<link rel="stylesheet" href="css/components.css" />
```

Replace the remaining `<link>` block (`theme.css`, `app.css`, favicon, `RuinaRPG.Client.styles.css`) with, in this order:

```html
<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
<link rel="stylesheet" href="css/theme.css" />
<link rel="stylesheet" href="css/app.css" />
<link rel="icon" type="image/png" href="favicon.png" />
<link href="RuinaRPG.Client.styles.css" rel="stylesheet" />
```

At the bottom of `<body>`, add the MudBlazor script immediately before `blazor.webassembly.js`:

```html
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
<script src="_framework/blazor.webassembly.js"></script>
```

Leave the inline theme-detection `<script>` in `<head>` and `<script src="js/theme.js"></script>` untouched — that mechanism doesn't change in this task.

- [ ] **Step 6: Delete the Bootstrap and components.css files**

```bash
rm -rf src/RuinaRPG.Client/wwwroot/css/bootstrap
rm src/RuinaRPG.Client/wwwroot/css/components.css
```

- [ ] **Step 7: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors. (Existing pages that used Bootstrap classes still compile fine — those are just string attribute values to Razor, not references MudBlazor's absence can break.)

- [ ] **Step 8: Serve and verify the new assets actually load**

```bash
dotnet run --project src/RuinaRPG.Client --urls http://localhost:5299 &
sleep 8
curl -s http://localhost:5299/ | grep -c "MudBlazor.min.css"
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5299/_content/MudBlazor/MudBlazor.min.css
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5299/_content/MudBlazor/MudBlazor.min.js
kill %1
```

Expected: first `curl` prints `1` (the link is present), both status codes are `200`.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: add MudBlazor, remove Bootstrap and components.css"
```

---

## Task 2: Rebuild the app shell (`MainLayout`, `NavMenu`) on MudBlazor

**Files:**
- Create: `src/RuinaRPG.Client/Theme/RrMudTheme.cs`
- Delete + recreate: `src/RuinaRPG.Client/Layout/MainLayout.razor`
- Delete: `src/RuinaRPG.Client/Layout/MainLayout.razor.css`
- Delete + recreate: `src/RuinaRPG.Client/Layout/NavMenu.razor`
- Delete: `src/RuinaRPG.Client/Layout/NavMenu.razor.css`

**Interfaces:**
- Consumes: `AuthStateService`/`TokenAuthenticationStateProvider` (`src/RuinaRPG.Client/Services/`, unchanged) via the existing `<AuthorizeView>`/`<AuthorizeView Roles="...">` pattern.
- Produces: `RrMudTheme.Instance` (`static MudTheme Instance`) — consumed by this task's own `MainLayout.razor` now, and by nothing else yet (Task 3 extends the same instance, doesn't replace it).
- Produces: `NavMenu`'s `OnLinkClicked` (`EventCallback`) parameter — same contract as before, so `MainLayout` can close the drawer after a nav click.

This task is pure shell/markup with one small piece of real logic (the drawer-open toggle) — verified by build + manual route smoke-check, not bUnit (bUnit setup arrives in Task 7 alongside the first component with real search/selection logic worth testing this way).

- [ ] **Step 1: Create `RrMudTheme`**

Create `src/RuinaRPG.Client/Theme/RrMudTheme.cs`:

```csharp
using MudBlazor;

namespace RuinaRPG.Client.Theme;

/// <summary>
/// The app's single MudTheme instance. Deliberately sets no PaletteLight/PaletteDark — color
/// never lives here. Every --mud-palette-* variable MudBlazor would generate from those defaults
/// is overridden in wwwroot/css/theme.css (see Task 3), which stays the one source of color
/// truth for the Sol/Lua themes. This class only owns structure: typography and border radius.
/// </summary>
public static class RrMudTheme
{
    private static readonly string[] HeadingFontFamily = ["Cormorant Garamond", "Georgia", "Times New Roman", "serif"];
    private static readonly string[] BodyFontFamily = ["Inter", "-apple-system", "Segoe UI", "Roboto", "Helvetica", "Arial", "sans-serif"];

    public static readonly MudTheme Instance = new()
    {
        LayoutProperties = new LayoutProperties
        {
            // Mirrors --rr-radius-sm in theme.css. Kept as a literal here (not a color, so the
            // spec's "no palette duplicated in C#" rule doesn't apply) — if --rr-radius-sm ever
            // changes, update this value to match.
            DefaultBorderRadius = "4px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = BodyFontFamily },
            H1 = new H1Typography { FontFamily = HeadingFontFamily },
            H2 = new H2Typography { FontFamily = HeadingFontFamily },
            H3 = new H3Typography { FontFamily = HeadingFontFamily },
            H4 = new H4Typography { FontFamily = HeadingFontFamily },
            H5 = new H5Typography { FontFamily = HeadingFontFamily },
            H6 = new H6Typography { FontFamily = HeadingFontFamily },
            Body1 = new Body1Typography { FontFamily = BodyFontFamily },
            Body2 = new Body2Typography { FontFamily = BodyFontFamily },
            Button = new ButtonTypography { FontFamily = BodyFontFamily },
            Caption = new CaptionTypography { FontFamily = BodyFontFamily },
            Subtitle1 = new Subtitle1Typography { FontFamily = BodyFontFamily },
            Subtitle2 = new Subtitle2Typography { FontFamily = BodyFontFamily },
        },
    };
}
```

- [ ] **Step 2: Delete the old layout files**

```bash
rm src/RuinaRPG.Client/Layout/MainLayout.razor.css
rm src/RuinaRPG.Client/Layout/NavMenu.razor.css
```

- [ ] **Step 3: Rewrite `NavMenu.razor`**

Same route list, same role-gating, same `OnLinkClicked`/logout behavior as before — only the markup changes, from bare `<nav>`/`<NavLink>` to `MudNavMenu`/`MudNavLink`/`MudNavGroup`:

```razor
@using Microsoft.AspNetCore.Components.Authorization
@inject HttpClient Http
@inject AuthStateService AuthState
@inject NavigationManager Navigation
@inject TokenAuthenticationStateProvider AuthProvider

<MudNavMenu>
    <MudNavLink Href="" Match="NavLinkMatch.All" OnClick="NotifyLinkClicked">🏠 Início</MudNavLink>

    <AuthorizeView>
        <NotAuthorized>
            <MudNavLink Href="login" OnClick="NotifyLinkClicked">🔑 Entrar</MudNavLink>
            <MudNavLink Href="cadastro" OnClick="NotifyLinkClicked">✒️ Cadastrar</MudNavLink>
        </NotAuthorized>
        <Authorized>
            <AuthorizeView Roles="GM" Context="gmContext">
                <Authorized>
                    <MudNavLink Href="painel/convites" OnClick="NotifyLinkClicked">✉️ Convidar Jogador</MudNavLink>
                    <MudNavLink Href="painel/jogadores" OnClick="NotifyLinkClicked">👥 Jogadores</MudNavLink>
                    <MudNavLink Href="catalogo" OnClick="NotifyLinkClicked">🎒 Catálogo de Itens</MudNavLink>
                    <MudNavLink Href="npcs" OnClick="NotifyLinkClicked">🎭 NPCs do GM</MudNavLink>
                    <MudNavLink Href="bestiario" OnClick="NotifyLinkClicked">🐺 Bestiário do GM</MudNavLink>
                    <MudNavLink Href="banco-de-magias" OnClick="NotifyLinkClicked">📜 Banco de Magias e Habilidades</MudNavLink>
                    <MudNavLink Href="campanhas" OnClick="NotifyLinkClicked">🗺️ Campanhas</MudNavLink>
                </Authorized>
            </AuthorizeView>
            <AuthorizeView Roles="Jogador" Context="jogadorContext">
                <Authorized>
                    <MudNavLink Href="minhas-campanhas" OnClick="NotifyLinkClicked">🗺️ Minhas Campanhas</MudNavLink>
                </Authorized>
            </AuthorizeView>
            <MudNavLink Href="compendio" OnClick="NotifyLinkClicked">📖 Compêndio de Regras</MudNavLink>
            <MudNavLink OnClick="LogoutAsync">🚪 Sair</MudNavLink>
        </Authorized>
    </AuthorizeView>
</MudNavMenu>

@code {
    [Parameter] public EventCallback OnLinkClicked { get; set; }

    private Task NotifyLinkClicked() => OnLinkClicked.InvokeAsync();

    private async Task LogoutAsync()
    {
        var refreshToken = await AuthState.GetRefreshTokenAsync();
        if (refreshToken is not null)
            await Http.PostAsJsonAsync("auth/logout", new RuinaRPG.Contracts.Auth.RefreshRequest(refreshToken));

        await AuthState.ClearAsync();
        AuthProvider.NotifyUserChanged();
        Navigation.NavigateTo("/");
        await NotifyLinkClicked();
    }
}
```

Note: `MudNavLink`'s `OnClick` fires on every click regardless of whether it also navigates, so a single handler covers both "close the drawer" (real links) and "log out" (the one non-navigating item) — matching what the old markup did with two different element types (`<NavLink>` vs `<a>`).

- [ ] **Step 4: Rewrite `MainLayout.razor`**

```razor
@inherits LayoutComponentBase
@using RuinaRPG.Client.Theme

<MudThemeProvider Theme="RrMudTheme.Instance" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="0">
        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start"
                        OnClick="@(() => _drawerOpen = !_drawerOpen)" Title="Abrir menu" aria-label="Abrir menu de navegação" />
        <MudText Typo="Typo.h6" Class="ml-2">Ruína RPG</MudText>
        <MudSpacer />
        <ThemeToggle />
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" Breakpoint="Breakpoint.Md" Elevation="0" ClipMode="DrawerClipMode.Always">
        <NavMenu OnLinkClicked="CloseDrawer" />
    </MudDrawer>

    <MudMainContent Class="content">
        @Body
    </MudMainContent>
</MudLayout>

@code {
    private bool _drawerOpen = true;

    private void CloseDrawer() => _drawerOpen = false;
}
```

`_drawerOpen` starts `true` — `MudDrawer`'s `Breakpoint="Breakpoint.Md"` makes it permanently visible above that breakpoint and an overlay below it, matching the old sidebar's desktop-permanent/mobile-collapsible behavior without hand-rolled CSS breakpoints.

- [ ] **Step 5: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors. (`ThemeToggle` is referenced but not yet rewritten — Task 4 rewrites it; until then it still compiles against its current implementation.)

- [ ] **Step 6: Serve and smoke-check the shell renders and nav works**

```bash
dotnet run --project src/RuinaRPG.Client --urls http://localhost:5299 &
sleep 8
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5299/
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5299/login
kill %1
```

Expected: both `200` (this only proves the static shell and routing table serve — the client-rendered nav content itself needs a real browser to see; if one is available in this environment, load `http://localhost:5299/` and confirm the hamburger opens/closes the drawer and the theme toggle button is visible, in both an unauthenticated and authenticated session).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: rebuild MainLayout/NavMenu shell on MudBlazor"
```

---

## Task 3: Theme color bridge — map `--mud-palette-*` to `--rr-*`

**Files:**
- Modify: `src/RuinaRPG.Client/wwwroot/css/theme.css`

**Interfaces:**
- Consumes: the existing `--rr-*` tokens already in `theme.css` (unchanged).
- Produces: every `--mud-palette-*` variable MudBlazor's `MudThemeProvider` generates, overridden to resolve to an `--rr-*` value — every later task's MudBlazor component (`MudPaper`, `MudSelect`, `MudAutocomplete`, `MudAppBar`, `MudDrawer`, etc.) picks these up automatically with zero per-component styling.

**Why `!important` is required, not optional:** `MudThemeProvider` renders its own `<style class='mud-theme-provider'>:root{--mud-palette-primary: ...; ...}</style>` (confirmed by reading MudBlazor 9.9.0's `MudThemeProvider.razor.cs`) as part of `MainLayout`'s render tree — i.e. *after* `theme.css`'s `<link>` in document order, at the same `:root` specificity. Without `!important`, MudBlazor's later, non-important rule would win the cascade and silently discard every mapping below.

- [ ] **Step 1: Add the bridge block to `theme.css`**

Add this new block to `wwwroot/css/theme.css`, right after the existing `:root { ... }` block (the one with `--rr-bg`, `--rr-primary`, etc.) and before `:root[data-theme='dark'] { ... }`:

```css
/* ==========================================================================
   MudBlazor color bridge — every --mud-palette-* MudThemeProvider generates
   is forced (!important — see plan/task notes) to resolve to the existing
   --rr-* tokens above, so MudBlazor never renders its own default Material
   palette. This block never introduces a new color; it only re-points names.
   ========================================================================== */
:root {
  --mud-palette-primary: var(--rr-primary) !important;
  --mud-palette-primary-text: var(--rr-primary-contrast) !important;
  --mud-palette-primary-hover: var(--rr-primary-hover) !important;
  --mud-palette-secondary: var(--rr-accent) !important;
  --mud-palette-secondary-text: var(--rr-primary-contrast) !important;
  --mud-palette-tertiary: var(--rr-accent) !important;
  --mud-palette-tertiary-text: var(--rr-primary-contrast) !important;

  --mud-palette-background: var(--rr-bg) !important;
  --mud-palette-background-gray: var(--rr-surface-alt) !important;
  --mud-palette-surface: var(--rr-surface) !important;
  --mud-palette-drawer-background: var(--rr-sidebar-bg) !important;
  --mud-palette-drawer-text: var(--rr-sidebar-text) !important;
  --mud-palette-drawer-icon: var(--rr-sidebar-text) !important;
  --mud-palette-appbar-background: var(--rr-sidebar-bg) !important;
  --mud-palette-appbar-text: var(--rr-sidebar-text) !important;

  --mud-palette-text-primary: var(--rr-text) !important;
  --mud-palette-text-secondary: var(--rr-text-muted) !important;
  --mud-palette-text-disabled: var(--rr-text-muted) !important;

  --mud-palette-action-default: var(--rr-text-muted) !important;
  --mud-palette-action-disabled: var(--rr-text-muted) !important;
  --mud-palette-action-disabled-background: var(--rr-surface-alt) !important;

  --mud-palette-divider: var(--rr-border) !important;
  --mud-palette-divider-light: var(--rr-border) !important;
  --mud-palette-lines-default: var(--rr-border) !important;
  --mud-palette-lines-inputs: var(--rr-border) !important;
  --mud-palette-table-lines: var(--rr-border) !important;
  --mud-palette-table-striped: var(--rr-surface-alt) !important;
  --mud-palette-table-hover: var(--rr-shadow) !important;

  --mud-palette-error: var(--rr-danger) !important;
  --mud-palette-error-text: #fff !important;
  --mud-palette-success: var(--rr-success) !important;
  --mud-palette-success-text: #fff !important;
  --mud-palette-info: var(--rr-primary) !important;
  --mud-palette-info-text: var(--rr-primary-contrast) !important;
  --mud-palette-warning: var(--rr-accent) !important;
  --mud-palette-warning-text: #fff !important;

  --mud-palette-overlay-dark: var(--rr-shadow) !important;
  --mud-palette-overlay-light: var(--rr-shadow) !important;
}
```

`--mud-palette-error-text`/`-success-text`/`-warning-text` are left as literal white rather than an `--rr-*` token — none of the 6 status colors those pair with (`--rr-danger`, `--rr-success`, `--rr-accent`) has a documented contrast-text token of its own the way `--rr-primary-contrast` does for primary, and white reads correctly against all three in both themes. This is the one place this task adds a raw literal instead of a token reference; call it out in the PR description as a deliberate, reviewed exception, not an oversight.

- [ ] **Step 2: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors (this is CSS-only; the build check just guards against a typo breaking something else).

- [ ] **Step 3: Verify the override actually wins**

```bash
dotnet run --project src/RuinaRPG.Client --urls http://localhost:5299 &
sleep 8
curl -s http://localhost:5299/css/theme.css | grep -c "mud-palette-primary: var(--rr-primary) !important"
kill %1
```

Expected: `1` (the rule ships in the served stylesheet). If a browser/Playwright is available in this environment, additionally load the app and use devtools to confirm `getComputedStyle(document.documentElement).getPropertyValue('--mud-palette-primary')` returns the Sol theme's `#c8932c` (and `#b6b3e0` after toggling to Lua) — this is the only way to empirically confirm the cascade fight was actually won, not just that the rule shipped; if no browser tool is available, say so explicitly rather than silently skipping it.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: bridge MudBlazor's palette CSS variables to existing --rr-* tokens"
```

---

## Task 4: Rebuild `ThemeToggle`

**Files:**
- Delete + recreate: `src/RuinaRPG.Client/Layout/ThemeToggle.razor`
- Delete + recreate: `src/RuinaRPG.Client/Layout/ThemeToggle.razor.css`

**Interfaces:**
- Consumes: `window.ruinaTheme.resolveInitial()`/`window.ruinaTheme.set(theme)` from `wwwroot/js/theme.js` (unchanged — do not touch that file).

The JS-interop contract and the `data-theme` attribute mechanism are correct infrastructure carried over unchanged from the current implementation (see spec) — only the markup/CSS is rebuilt. No new logic, so no bUnit test; `IJSRuntime` interop like this also isn't practically bUnit-testable without a fair amount of JS mocking that isn't worth it for a two-method contract already proven correct in production.

- [ ] **Step 1: Rewrite `ThemeToggle.razor`**

```razor
@inject IJSRuntime JS

<button type="button" class="theme-toggle" @onclick="ToggleAsync"
        title="Alternar entre tema Sol e Lua" aria-label="Alternar entre tema Sol e Lua">
    <span class="theme-toggle-icon @(_theme == "light" ? "active" : "")" aria-hidden="true">☀️</span>
    <span class="theme-toggle-icon @(_theme == "dark" ? "active" : "")" aria-hidden="true">🌙</span>
</button>

@code {
    private string _theme = "light";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _theme = await JS.InvokeAsync<string>("ruinaTheme.resolveInitial");
            StateHasChanged();
        }
    }

    private async Task ToggleAsync()
    {
        _theme = _theme == "dark" ? "light" : "dark";
        await JS.InvokeVoidAsync("ruinaTheme.set", _theme);
    }
}
```

(Same `@code` as before — the rebuild is about confirming this component's markup lives on its own again as a from-scratch file, not gratuitously changing working logic.)

- [ ] **Step 2: Rewrite `ThemeToggle.razor.css`**

```css
.theme-toggle {
    display: flex;
    align-items: center;
    gap: var(--rr-space-1);
    background: var(--rr-surface-alt);
    border: 1px solid var(--rr-border);
    border-radius: var(--rr-radius-lg);
    padding: var(--rr-space-1) var(--rr-space-2);
    cursor: pointer;
}

.theme-toggle:focus-visible {
    outline: none;
    box-shadow: 0 0 0 0.2rem var(--rr-shadow);
}

.theme-toggle-icon {
    opacity: 0.35;
    font-size: 1rem;
    transition: opacity 0.15s ease;
}

.theme-toggle-icon.active {
    opacity: 1;
}
```

(Adds one thing the old version lacked: a visible `:focus-visible` ring, matching the focus-ring convention already used elsewhere in `components.css`'s predecessor — worth keeping even though that file is gone.)

- [ ] **Step 3: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: rebuild ThemeToggle"
```

---

## Task 5: Rebuild `Breadcrumbs` on `MudBreadcrumbs`

**Files:**
- Delete + recreate: `src/RuinaRPG.Client/Shared/Breadcrumbs.razor`

**Interfaces:**
- Consumes/keeps unchanged: `Shared/BreadcrumbItem.cs` (the `record BreadcrumbItem(string Text, string? Href)` — confirm this file's exact shape before writing the new component; do not modify it).
- Produces: same public API as before — `[Parameter, EditorRequired] public List<BreadcrumbItem> Items { get; set; }` — every page building breadcrumbs today keeps compiling unmodified.

- [ ] **Step 1: Confirm `BreadcrumbItem`'s shape**

Run: `cat src/RuinaRPG.Client/Shared/BreadcrumbItem.cs`
Expected: a record with `Text` (string) and `Href` (string?) — if the actual shape differs, adjust Step 2 to match it exactly rather than the assumption below.

- [ ] **Step 2: Rewrite `Breadcrumbs.razor`**

```razor
@if (Items is { Count: > 0 })
{
    <MudBreadcrumbs Items="_mudItems" Separator="/" />
}

@code {
    [Parameter, EditorRequired] public List<BreadcrumbItem> Items { get; set; } = new();

    private List<MudBlazor.BreadcrumbItem> _mudItems = new();

    protected override void OnParametersSet()
    {
        var lastIndex = Items.Count - 1;
        _mudItems = Items
            .Select((item, i) => new MudBlazor.BreadcrumbItem(item.Text, item.Href, disabled: i == lastIndex || item.Href is null))
            .ToList();
    }
}
```

`MudBlazor.BreadcrumbItem` is the library's own record (name collides with this app's `BreadcrumbItem` — hence the fully-qualified reference); its `disabled` flag is what renders an item as plain text instead of a link, replacing the old markup's `isLast`/`item.Href is not null` branch.

- [ ] **Step 3: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: rebuild Breadcrumbs on MudBreadcrumbs"
```

---

## Task 6: Rebuild the section/card component (`Section.razor`) on `MudPaper`

**Files:**
- Delete + recreate: `src/RuinaRPG.Client/Shared/Section.razor`
- Delete + recreate: `src/RuinaRPG.Client/Shared/Section.razor.css`

**Interfaces:**
- Produces: same public API as before — `Title` (string?), `Subtitle` (string?), `ChildContent` (RenderFragment?) — every page using `<Section>` today (~8 pages) keeps compiling unmodified.

- [ ] **Step 1: Rewrite `Section.razor`**

```razor
<MudPaper Elevation="0" Class="rr-section">
    @if (!string.IsNullOrWhiteSpace(Title))
    {
        <div class="rr-section-header">
            <MudText Typo="Typo.h6" Class="rr-section-title">@Title</MudText>
            @if (!string.IsNullOrWhiteSpace(Subtitle))
            {
                <MudText Typo="Typo.body2" Class="rr-section-subtitle">@Subtitle</MudText>
            }
        </div>
    }
    <div class="rr-section-body">
        @ChildContent
    </div>
</MudPaper>

@code {
    /// <summary>Rendered as a heading above the body. Omit for a borderless grouping with no heading.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Optional muted line under the title.</summary>
    [Parameter] public string? Subtitle { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
```

- [ ] **Step 2: Rewrite `Section.razor.css`**

Per the spec's "hairline, not shadow" direction — a thin rule at the top instead of `MudPaper`'s (already-neutralized-by-`Elevation="0"`) box-shadow:

```css
::deep .rr-section {
    border-top: 2px solid var(--rr-accent);
    padding: var(--rr-space-4);
    background: var(--rr-surface-alt);
}

::deep .rr-section + .rr-section {
    margin-top: var(--rr-space-4);
}

::deep .rr-section-header {
    margin-bottom: var(--rr-space-3);
}

::deep .rr-section-title {
    font-family: var(--rr-font-heading);
}

::deep .rr-section-subtitle {
    color: var(--rr-text-muted);
    margin-top: var(--rr-space-1);
}

::deep .rr-section-body > :last-child {
    margin-bottom: 0;
}
```

Every selector here uses `::deep`, not just the one targeting `.rr-section` itself. `MudPaper` and `MudText` are child *components*, not elements this component's own markup renders directly — Blazor's CSS isolation only auto-scopes onto directly-rendered elements, so any rule reaching a class applied *through* a child component's `Class` parameter (`.rr-section` on `MudPaper`, `.rr-section-title`/`.rr-section-subtitle` on `MudText`) needs `::deep`, and so does anything chained off them (`.rr-section + .rr-section`). Only `.rr-section-header`/`.rr-section-body` are plain `<div>`s this component renders directly and would scope correctly without it — `::deep` is added there too anyway, since it's harmless on an already-correctly-scoped selector and keeps the file uniform rather than relying on a reader correctly telling apart "component-applied" from "own-element" classes at a glance. This exact class of bug already bit this codebase once before, see `ruina-plan-queue` memory's Round-1 note on `NavMenu`/`NavLink` — worth remembering here too.

- [ ] **Step 3: Build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat: rebuild Section on MudPaper with a hairline top rule"
```

---

## Task 7: bUnit test project + rebuild `EntityPicker` on `MudAutocomplete`

**Files:**
- Create: `tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj`
- Modify: `RuinaRPG.sln` (add the new project)
- Delete + recreate: `src/RuinaRPG.Client/Shared/EntityPicker.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/EntityPickerTests.cs`

**Interfaces:**
- Consumes: `Shared/PickerOption.cs` (`record PickerOption(string Id, string Label)`, unchanged).
- Produces: same public API as before — `Value` (string?), `ValueChanged` (EventCallback<string?>), `SearchItems` (`Func<string, Task<List<PickerOption>>>`), `Placeholder` (string) — every one of the ~10 other consumers keeps compiling unmodified.

This is the first component with real logic worth a real test harness: given a query, show matching results; given a selection, set `Value` and clear the query; given the caller resetting `Value` externally, clear the confirmed-selection display. TDD applies — write the failing tests first.

- [ ] **Step 1: Scaffold the bUnit test project**

```bash
mkdir -p tests/RuinaRPG.Tests.Client
```

Create `tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="bunit" Version="2.9.0" />
    <PackageReference Include="FluentAssertions" Version="6.12.1" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.6.0" />
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\RuinaRPG.Client\RuinaRPG.Client.csproj" />
  </ItemGroup>

</Project>
```

`Sdk="Microsoft.NET.Sdk.Razor"` (not `.Web`) is required here — bUnit renders `.razor` components from the referenced project in-memory; the test project itself has no markup of its own, but the Razor SDK is what lets the test host correctly resolve the Blazor component model bUnit needs.

- [ ] **Step 2: Add the project to the solution**

```bash
dotnet sln RuinaRPG.sln add tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj
```

- [ ] **Step 3: Make `RuinaRPG.Client`'s `internal` members visible to the test project**

This task and Task 8 both add small `internal` test-seam methods to components (e.g. `SearchAsyncForTests` below) — real component logic exercised deterministically, without turning them into public API surface. That requires an explicit `InternalsVisibleTo`. Create `src/RuinaRPG.Client/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RuinaRPG.Tests.Client")]
```

- [ ] **Step 4: Restore and confirm both projects build empty**

```bash
dotnet restore tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj
dotnet build src/RuinaRPG.Client/RuinaRPG.Client.csproj
dotnet build tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj
```

Expected: both build with 0 errors (no test files exist yet, that's fine).

- [ ] **Step 5: Write the failing tests for `EntityPicker`**

Create `tests/RuinaRPG.Tests.Client/Shared/EntityPickerTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using MudBlazor.Services;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class EntityPickerTests : TestContext
{
    public EntityPickerTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task SearchFunc_delegates_to_the_caller_supplied_SearchItems_for_a_non_empty_query()
    {
        // MudAutocomplete owns its own popover/debounce internally — not something bUnit's
        // synchronous TestContext can drive through real elapsed time or a real dropdown click.
        // This test (and the two below) exercise EntityPicker's own logic directly through its
        // test-only seams instead of trying to reproduce MudAutocomplete's internal rendering.
        var cut = RenderComponent<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, query => Task.FromResult(new List<PickerOption>
            {
                new("id-1", $"Espada Longa (matched '{query}')"),
            })));

        var results = await cut.Instance.SearchAsyncForTests("esp");

        results.Should().ContainSingle(o => o.Id == "id-1" && o.Label.Contains("'esp'"));
    }

    [Fact]
    public async Task SearchFunc_returns_no_results_for_a_blank_query_without_calling_SearchItems()
    {
        var searchItemsCalled = false;
        var cut = RenderComponent<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.SearchItems, _ =>
            {
                searchItemsCalled = true;
                return Task.FromResult(new List<PickerOption>());
            }));

        var results = await cut.Instance.SearchAsyncForTests("   ");

        results.Should().BeEmpty();
        searchItemsCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Selecting_a_result_sets_Value_and_switches_off_the_search_input()
    {
        string? boundValue = null;
        var cut = RenderComponent<EntityPicker>(p => p
            .Add(x => x.Value, "")
            .Add(x => x.ValueChanged, v => boundValue = v)
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>())));

        await cut.InvokeAsync(() => cut.Instance.SelectForTests(new PickerOption("id-1", "Espada Longa")));

        boundValue.Should().Be("id-1");
        cut.Markup.Should().Contain("Espada Longa");
        cut.Markup.Should().NotContain("<input");
    }

    [Fact]
    public void Caller_resetting_Value_to_empty_clears_the_confirmed_selection_display()
    {
        var cut = RenderComponent<EntityPicker>(p => p
            .Add(x => x.Value, "id-1")
            .Add(x => x.SearchItems, _ => Task.FromResult(new List<PickerOption>())));
        cut.SetParametersAndRender(p => p.Add(x => x.Value, "id-1"));
        cut.Instance.SetSelectedLabelForTests("Espada Longa"); // simulate a prior confirmed selection

        cut.SetParametersAndRender(p => p.Add(x => x.Value, ""));

        cut.Markup.Should().Contain("<input");
    }
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj`
Expected: FAIL to compile — `EntityPicker` doesn't yet expose `SearchAsyncForTests`/`SetSelectedLabelForTests`, and the current implementation isn't built on `MudAutocomplete` yet.

- [ ] **Step 7: Rewrite `EntityPicker.razor`**

```razor
@*
    Replaces a raw-GUID field with a debounced live-search + click-to-select control, over
    MudAutocomplete, reusing each page's existing search endpoint (via the caller-supplied
    SearchItems delegate).

    Every one of these fields is an "add new X" action, never an edit of an existing selection, so
    the component never needs to resolve an incoming Value back into a label on its own; it always
    starts empty and the caller resets Value to "" after a successful add, which this component
    picks up via OnParametersSet to clear its own confirmed-selection display.
*@
@if (!string.IsNullOrEmpty(Value) && _selectedLabel is not null)
{
    <div class="entity-picker-selected">
        <span>@_selectedLabel</span>
        <MudButton Variant="Variant.Outlined" Size="Size.Small" OnClick="ClearAsync">Trocar</MudButton>
    </div>
}
else
{
    <MudAutocomplete T="PickerOption" Placeholder="@Placeholder" SearchFunc="SearchFuncAsync"
                      ToStringFunc="@(o => o?.Label ?? string.Empty)" ValueChanged="SelectAsync" ResetValueOnEmptyText="true"
                      CoerceText="false" Clearable="true" />
}

@code {
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    [Parameter, EditorRequired] public Func<string, Task<List<PickerOption>>> SearchItems { get; set; } = null!;
    [Parameter] public string Placeholder { get; set; } = "Buscar...";

    private string? _selectedLabel;

    protected override void OnParametersSet()
    {
        // The caller reset Value externally (e.g. after a successful add) — drop our own
        // confirmed-selection display so the search box reappears empty, ready for the next pick.
        if (string.IsNullOrEmpty(Value))
            _selectedLabel = null;
    }

    private async Task<IEnumerable<PickerOption>> SearchFuncAsync(string query, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];
        return await SearchItems(query);
    }

    private async Task SelectAsync(PickerOption? option)
    {
        if (option is null)
            return;
        _selectedLabel = option.Label;
        Value = option.Id;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task ClearAsync()
    {
        _selectedLabel = null;
        Value = "";
        await ValueChanged.InvokeAsync(Value);
    }

    // Test-only seams: MudAutocomplete owns its own debounce/open-state internally, which bUnit's
    // synchronous TestContext can't drive through real elapsed time or a real click-outside event.
    // These let EntityPickerTests exercise the same SearchItems/selection code paths deterministically.
    internal Task<IEnumerable<PickerOption>> SearchAsyncForTests(string query) => SearchFuncAsync(query, CancellationToken.None);
    internal Task SelectForTests(PickerOption option) => SelectAsync(option);
    internal void SetSelectedLabelForTests(string label) => _selectedLabel = label;
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj`
Expected: PASS (4/4).

- [ ] **Step 9: Full-solution build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat: add bUnit test project, rebuild EntityPicker on MudAutocomplete"
```

---

## Task 8: Shared domain fields — Linhagem/Variante, Vocação/Sub-vocação, Afinidade

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Fields/LinhagemVarianteFields.razor`
- Create: `src/RuinaRPG.Client/Shared/Fields/VocacaoSubVocacaoFields.razor`
- Create: `src/RuinaRPG.Client/Shared/Fields/AfinidadeSelect.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/LinhagemVarianteFieldsTests.cs`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/VocacaoSubVocacaoFieldsTests.cs`

**Interfaces:**
- Produces: `LinhagemVarianteFields` — `Linhagem`/`LinhagemChanged` (string?), `Variante`/`VarianteChanged` (string?). `VocacaoSubVocacaoFields` — `Vocacao`/`VocacaoChanged` (string?), `SubVocacao`/`SubVocacaoChanged` (string?). `AfinidadeSelect` — `Value`/`ValueChanged` (string?). All three are consumed by Fase 1 (the Fichas plan, not written yet) — this task's job is just to make them exist and be correct, not to wire any page to them yet.

These three fields are hardcoded and duplicated today across `FichaDePersonagem.razor`/`FichaDeNpc.razor` (Linhagem/Variante, Vocação/Sub-vocação) and all 3 Fichas (Afinidade) — this task centralizes that data into one place each, sourced from the same real values already in the current code (`Requisitos - Ficha de Personagem.md` R0001 1.a, `Relação de Vocacão e Classes.md`).

`LinhagemVarianteFields`/`VocacaoSubVocacaoFields` add one real behavior fix over the current pages: today, changing Linhagem doesn't clear a no-longer-valid Variante (same for Vocação/Sub-vocação). This task's components reset the dependent field to `null` when the independent one changes to a value that invalidates it — call this out explicitly in the PR description as a deliberate small behavior change, not a silent one.

- [ ] **Step 1: Write the failing tests for `LinhagemVarianteFields`**

Create `tests/RuinaRPG.Tests.Client/Shared/Fields/LinhagemVarianteFieldsTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using MudBlazor.Services;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class LinhagemVarianteFieldsTests : TestContext
{
    public LinhagemVarianteFieldsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData("Humano", new[] { "Sinir", "Laonir" })]
    [InlineData("Phylauc", new[] { "PhylacTai", "EsPhylauc" })]
    [InlineData("Nephrytes", new[] { "Koroanos", "Yavos" })]
    [InlineData("Econos", new[] { "Alora" })]
    public void Variante_options_are_scoped_to_the_selected_Linhagem(string linhagem, string[] expectedVariantes)
    {
        var cut = RenderComponent<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, linhagem)
            .Add(x => x.Variante, (string?)null));

        var options = cut.Instance.VarianteOptionsForTests().Select(o => o.Valor);

        options.Should().BeEquivalentTo(expectedVariantes);
    }

    [Fact]
    public async Task Changing_Linhagem_clears_a_now_invalid_Variante()
    {
        string? newVariante = "not-cleared-yet";
        var cut = RenderComponent<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, "Humano")
            .Add(x => x.Variante, "Sinir")
            .Add(x => x.VarianteChanged, v => newVariante = v));

        await cut.InvokeAsync(() => cut.Instance.SetLinhagemForTests("Nephrytes"));

        newVariante.Should().BeNull();
    }

    [Fact]
    public void No_Linhagem_selected_yields_no_Variante_options()
    {
        var cut = RenderComponent<LinhagemVarianteFields>(p => p
            .Add(x => x.Linhagem, (string?)null)
            .Add(x => x.Variante, (string?)null));

        cut.Instance.VarianteOptionsForTests().Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj --filter FullyQualifiedName~LinhagemVarianteFieldsTests`
Expected: FAIL to compile — `LinhagemVarianteFields` doesn't exist yet.

- [ ] **Step 3: Write `LinhagemVarianteFields.razor`**

```razor
<MudSelect T="string" Label="Linhagem" Value="Linhagem" ValueChanged="OnLinhagemChangedAsync" Placeholder="Escolha uma Linhagem">
    @foreach (var linhagem in Linhagens)
    {
        <MudSelectItem Value="@linhagem">@linhagem</MudSelectItem>
    }
</MudSelect>

@if (string.IsNullOrEmpty(Linhagem))
{
    <MudText Typo="Typo.body2" Color="Color.Secondary">Escolha uma Linhagem antes da Variante.</MudText>
}
else
{
    <MudSelect T="string" Label="Variante" Value="Variante" ValueChanged="OnVarianteChangedAsync" Placeholder="Escolha uma Variante">
        @foreach (var (valor, rotulo) in VarianteOptionsForTests())
        {
            <MudSelectItem Value="@valor">@rotulo</MudSelectItem>
        }
    </MudSelect>
}

@code {
    [Parameter] public string? Linhagem { get; set; }
    [Parameter] public EventCallback<string?> LinhagemChanged { get; set; }
    [Parameter] public string? Variante { get; set; }
    [Parameter] public EventCallback<string?> VarianteChanged { get; set; }

    public static readonly string[] Linhagens = ["Humano", "Phylauc", "Nephrytes", "Econos"];

    // From Ruína RPG - Sistema Básico.md §7 — each variant's Sol/Lua polarity is the game's own
    // mythology, not decoration; RrIdentityBadge (Task 9) reads this same table.
    public static readonly Dictionary<string, (string Valor, string Rotulo, string Polaridade)[]> VariantesPorLinhagem = new()
    {
        ["Humano"] = [("Sinir", "Sinir (Sol)", "Sol"), ("Laonir", "Laonir (Lua)", "Lua")],
        ["Phylauc"] = [("PhylacTai", "Phylac'tai (Sol)", "Sol"), ("EsPhylauc", "Es'Phylauc (Lua)", "Lua")],
        ["Nephrytes"] = [("Koroanos", "Koroanos (Sol)", "Sol"), ("Yavos", "Yavos (Lua)", "Lua")],
        ["Econos"] = [("Alora", "Alóra (Lua)", "Lua")],
    };

    internal (string Valor, string Rotulo, string Polaridade)[] VarianteOptionsForTests() =>
        !string.IsNullOrEmpty(Linhagem) && VariantesPorLinhagem.TryGetValue(Linhagem, out var options) ? options : [];

    internal Task SetLinhagemForTests(string? linhagem) => OnLinhagemChangedAsync(linhagem);

    private async Task OnLinhagemChangedAsync(string? novaLinhagem)
    {
        Linhagem = novaLinhagem;
        await LinhagemChanged.InvokeAsync(Linhagem);

        var aindaValida = novaLinhagem is not null
            && VariantesPorLinhagem.TryGetValue(novaLinhagem, out var options)
            && options.Any(o => o.Valor == Variante);
        if (!aindaValida && Variante is not null)
        {
            Variante = null;
            await VarianteChanged.InvokeAsync(null);
        }
    }

    private async Task OnVarianteChangedAsync(string? novaVariante)
    {
        Variante = novaVariante;
        await VarianteChanged.InvokeAsync(Variante);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj --filter FullyQualifiedName~LinhagemVarianteFieldsTests`
Expected: PASS (6/6, counting the 4 `[Theory]` cases as separate).

- [ ] **Step 5: Write the failing tests for `VocacaoSubVocacaoFields`**

Create `tests/RuinaRPG.Tests.Client/Shared/Fields/VocacaoSubVocacaoFieldsTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using MudBlazor.Services;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class VocacaoSubVocacaoFieldsTests : TestContext
{
    public VocacaoSubVocacaoFieldsTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData("Campeao", new[] { "Cavalheiro", "Duelista", "Paladino", "Lamina Holística", "Guardião" })]
    [InlineData("Cacador", new[] { "Arqueiro", "Domador", "Assassino" })]
    [InlineData("Feiticeiro", new[] { "Aeromante", "Biomante", "Fluxomante", "Piromante", "Sábio" })]
    [InlineData("Adepto", new[] { "Paladino", "Arauto", "Trovador", "Eremita" })]
    [InlineData("Bruxo", new[] { "Ecomante", "Hemomante", "Osteomante", "Nexomante", "Cultista" })]
    public void SubVocacao_options_are_scoped_to_the_selected_Vocacao(string vocacao, string[] expected)
    {
        var cut = RenderComponent<VocacaoSubVocacaoFields>(p => p
            .Add(x => x.Vocacao, vocacao)
            .Add(x => x.SubVocacao, (string?)null));

        cut.Instance.SubVocacaoOptionsForTests().Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Changing_Vocacao_clears_a_now_invalid_SubVocacao()
    {
        string? newSubVocacao = "not-cleared-yet";
        var cut = RenderComponent<VocacaoSubVocacaoFields>(p => p
            .Add(x => x.Vocacao, "Campeao")
            .Add(x => x.SubVocacao, "Duelista")
            .Add(x => x.SubVocacaoChanged, v => newSubVocacao = v));

        await cut.InvokeAsync(() => cut.Instance.SetVocacaoForTests("Bruxo"));

        newSubVocacao.Should().BeNull();
    }
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj --filter FullyQualifiedName~VocacaoSubVocacaoFieldsTests`
Expected: FAIL to compile.

- [ ] **Step 7: Write `VocacaoSubVocacaoFields.razor`**

```razor
<MudSelect T="string" Label="Vocação" Value="Vocacao" ValueChanged="OnVocacaoChangedAsync" Placeholder="Escolha uma Vocação">
    @foreach (var (valor, rotulo) in Vocacoes)
    {
        <MudSelectItem Value="@valor">@rotulo</MudSelectItem>
    }
</MudSelect>

@if (string.IsNullOrEmpty(Vocacao))
{
    <MudText Typo="Typo.body2" Color="Color.Secondary">Escolha uma Vocação antes da Sub-vocação.</MudText>
}
else
{
    <MudSelect T="string" Label="Sub-vocação" Value="SubVocacao" ValueChanged="OnSubVocacaoChangedAsync" Placeholder="Escolha uma Sub-vocação">
        @foreach (var subVocacao in SubVocacaoOptionsForTests())
        {
            <MudSelectItem Value="@subVocacao">@subVocacao</MudSelectItem>
        }
    </MudSelect>
}

@code {
    [Parameter] public string? Vocacao { get; set; }
    [Parameter] public EventCallback<string?> VocacaoChanged { get; set; }
    [Parameter] public string? SubVocacao { get; set; }
    [Parameter] public EventCallback<string?> SubVocacaoChanged { get; set; }

    // (valor, rótulo) — value == label server-side today except for the "Cacador"/"Campeao" ascii
    // keys, matching what FichaDePersonagem.razor already sends.
    public static readonly (string Valor, string Rotulo)[] Vocacoes =
    [
        ("Campeao", "Campeão"), ("Cacador", "Caçador"), ("Feiticeiro", "Feiticeiro"), ("Adepto", "Adepto"), ("Bruxo", "Bruxo"),
    ];

    // From "Relação de Vocacão e Classes.md" — Sub-vocação is free text server-side (no enum), so
    // value == label.
    public static readonly Dictionary<string, string[]> SubVocacoesPorVocacao = new()
    {
        ["Campeao"] = ["Cavalheiro", "Duelista", "Paladino", "Lamina Holística", "Guardião"],
        ["Cacador"] = ["Arqueiro", "Domador", "Assassino"],
        ["Feiticeiro"] = ["Aeromante", "Biomante", "Fluxomante", "Piromante", "Sábio"],
        ["Adepto"] = ["Paladino", "Arauto", "Trovador", "Eremita"],
        ["Bruxo"] = ["Ecomante", "Hemomante", "Osteomante", "Nexomante", "Cultista"],
    };

    internal string[] SubVocacaoOptionsForTests() =>
        !string.IsNullOrEmpty(Vocacao) && SubVocacoesPorVocacao.TryGetValue(Vocacao, out var options) ? options : [];

    internal Task SetVocacaoForTests(string? vocacao) => OnVocacaoChangedAsync(vocacao);

    private async Task OnVocacaoChangedAsync(string? novaVocacao)
    {
        Vocacao = novaVocacao;
        await VocacaoChanged.InvokeAsync(Vocacao);

        var aindaValida = novaVocacao is not null
            && SubVocacoesPorVocacao.TryGetValue(novaVocacao, out var options)
            && options.Contains(SubVocacao);
        if (!aindaValida && SubVocacao is not null)
        {
            SubVocacao = null;
            await SubVocacaoChanged.InvokeAsync(null);
        }
    }

    private async Task OnSubVocacaoChangedAsync(string? novaSubVocacao)
    {
        SubVocacao = novaSubVocacao;
        await SubVocacaoChanged.InvokeAsync(SubVocacao);
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj --filter FullyQualifiedName~VocacaoSubVocacaoFieldsTests`
Expected: PASS (6/6).

- [ ] **Step 9: Write `AfinidadeSelect.razor`** (no cascade, no test needed beyond the build — it's a flat 18-item list, same class of triviality as a static lookup table)

```razor
<MudSelect T="string" Label="Afinidade" Value="Value" ValueChanged="OnValueChangedAsync" Placeholder="Escolha uma Afinidade">
    @foreach (var (valor, rotulo) in Afinidades)
    {
        <MudSelectItem Value="@valor">@rotulo</MudSelectItem>
    }
</MudSelect>

@code {
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    // R0001 1.a — 4 Elementos + 14 Sub-Elementos da Matriz Elemental.
    public static readonly (string Valor, string Rotulo)[] Afinidades =
    [
        ("Terra", "Terra"), ("Agua", "Água"), ("Fogo", "Fogo"), ("Ar", "Ar"), ("Gelo", "Gelo"),
        ("Flora", "Flora"), ("Ferro", "Ferro"), ("Raio", "Raio"), ("Prever", "Prever"), ("Alma", "Alma"),
        ("Purificar", "Purificar"), ("Ecomancia", "Ecomancia"), ("Hemomancia", "Hemomancia"), ("Curar", "Curar"),
        ("Vida", "Vida"), ("Aprimorar", "Aprimorar"), ("Necromancia", "Necromancia"), ("Invocacao", "Invocação"),
    ];

    private async Task OnValueChangedAsync(string? novoValor)
    {
        Value = novoValor;
        await ValueChanged.InvokeAsync(Value);
    }
}
```

- [ ] **Step 10: Full-solution build**

Run: `dotnet build RuinaRPG.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "feat: add shared Linhagem/Variante, Vocacao/SubVocacao, Afinidade fields"
```

---

## Task 9: `RrIdentityBadge` — the Selo de Linhagem signature element

**Files:**
- Create: `src/RuinaRPG.Client/Shared/RrIdentityBadge.razor`
- Create: `src/RuinaRPG.Client/Shared/RrIdentityBadge.razor.css`
- Test: `tests/RuinaRPG.Tests.Client/Shared/RrIdentityBadgeTests.cs`

**Interfaces:**
- Consumes: `LinhagemVarianteFields.VariantesPorLinhagem` (Task 8) as the single source of each Variante's `Polaridade` ("Sol"/"Lua") — no data duplicated between the two components.
- Produces: `RrIdentityBadge` — `Linhagem` (string?), `Variante` (string?) parameters, no output events (display-only). Not consumed by any page in this plan — Fase 1 (the Fichas plan) is what mounts it in a sheet header; this task only needs it to exist and be correct.

Per the spec: a small circular emblem, one glyph per Linhagem, a Sol/Lua ring style per Variante — built only from `--rr-primary`/`--rr-accent`/`--rr-border` (no new color). Real logic worth testing: given a Linhagem+Variante pair, resolve the right glyph and polarity; given an unknown/null pair, render a neutral placeholder instead of throwing or showing wrong data.

- [ ] **Step 1: Write the failing tests**

Create `tests/RuinaRPG.Tests.Client/Shared/RrIdentityBadgeTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using MudBlazor.Services;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class RrIdentityBadgeTests : TestContext
{
    public RrIdentityBadgeTests()
    {
        Services.AddMudServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Theory]
    [InlineData("Humano", "Sinir", "Humano", "Sol")]
    [InlineData("Humano", "Laonir", "Humano", "Lua")]
    [InlineData("Phylauc", "EsPhylauc", "Phylauc", "Lua")]
    [InlineData("Nephrytes", "Koroanos", "Nephrytes", "Sol")]
    [InlineData("Econos", "Alora", "Econos", "Lua")]
    public void Renders_the_right_glyph_and_polaridade_for_a_known_pair(
        string linhagem, string variante, string expectedGlyphKey, string expectedPolaridade)
    {
        var cut = RenderComponent<RrIdentityBadge>(p => p
            .Add(x => x.Linhagem, linhagem)
            .Add(x => x.Variante, variante));

        cut.Find(".rr-identity-badge").ClassList.Should().Contain($"rr-glyph-{expectedGlyphKey.ToLowerInvariant()}");
        cut.Find(".rr-identity-badge").ClassList.Should().Contain($"rr-polaridade-{expectedPolaridade.ToLowerInvariant()}");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("Humano", null)]
    [InlineData(null, "Sinir")]
    [InlineData("Humano", "NaoExiste")]
    public void Renders_a_neutral_placeholder_for_an_unknown_or_incomplete_pair(string? linhagem, string? variante)
    {
        var cut = RenderComponent<RrIdentityBadge>(p => p
            .Add(x => x.Linhagem, linhagem)
            .Add(x => x.Variante, variante));

        cut.Find(".rr-identity-badge").ClassList.Should().Contain("rr-glyph-none");
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj --filter FullyQualifiedName~RrIdentityBadgeTests`
Expected: FAIL to compile — `RrIdentityBadge` doesn't exist yet.

- [ ] **Step 3: Write `RrIdentityBadge.razor`**

```razor
@using RuinaRPG.Client.Shared.Fields

<div class="rr-identity-badge rr-glyph-@GlyphKey.ToLowerInvariant() rr-polaridade-@Polaridade.ToLowerInvariant()"
     title="@Title" aria-label="@Title">
    <svg viewBox="0 0 32 32" width="32" height="32" aria-hidden="true">
        <circle class="rr-identity-ring" cx="16" cy="16" r="14" />
        <text class="rr-identity-glyph" x="16" y="21" text-anchor="middle">@GlyphChar</text>
    </svg>
</div>

@code {
    [Parameter] public string? Linhagem { get; set; }
    [Parameter] public string? Variante { get; set; }

    // One character per Linhagem — deliberately abstract/geometric, not a literal illustration.
    private static readonly Dictionary<string, string> GlyphByLinhagem = new()
    {
        ["Humano"] = "H",
        ["Phylauc"] = "Φ",
        ["Nephrytes"] = "N",
        ["Econos"] = "E",
    };

    private string GlyphKey => Linhagem is not null && GlyphByLinhagem.ContainsKey(Linhagem) && HasValidVariante ? Linhagem : "None";

    private string GlyphChar => Linhagem is not null && GlyphByLinhagem.TryGetValue(Linhagem, out var g) && HasValidVariante ? g : "?";

    private string Polaridade =>
        Linhagem is not null
        && Variante is not null
        && LinhagemVarianteFields.VariantesPorLinhagem.TryGetValue(Linhagem, out var options)
        && options.FirstOrDefault(o => o.Valor == Variante) is { Polaridade: var p }
            ? p
            : "Neutra";

    private bool HasValidVariante =>
        Linhagem is not null
        && Variante is not null
        && LinhagemVarianteFields.VariantesPorLinhagem.TryGetValue(Linhagem, out var options)
        && options.Any(o => o.Valor == Variante);

    private string Title => HasValidVariante ? $"{Linhagem} — {Variante}" : "Linhagem não definida";
}
```

- [ ] **Step 4: Write `RrIdentityBadge.razor.css`**

```css
.rr-identity-badge {
    display: inline-flex;
    width: 2rem;
    height: 2rem;
}

.rr-identity-ring {
    fill: var(--rr-surface);
    stroke: var(--rr-border);
    stroke-width: 1.5;
}

.rr-identity-glyph {
    font-family: var(--rr-font-heading);
    font-size: 0.95rem;
    font-weight: 700;
    fill: var(--rr-text-muted);
}

/* Sol variants: solid primary-colored ring. Lua variants: dashed accent-colored ring — the same
   Sol/Lua polarity every Variante already carries (Sistema Básico §7), not invented here. */
.rr-polaridade-sol .rr-identity-ring {
    stroke: var(--rr-primary);
    stroke-width: 2;
}

.rr-polaridade-sol .rr-identity-glyph {
    fill: var(--rr-primary);
}

.rr-polaridade-lua .rr-identity-ring {
    stroke: var(--rr-accent);
    stroke-width: 2;
    stroke-dasharray: 3 2;
}

.rr-polaridade-lua .rr-identity-glyph {
    fill: var(--rr-accent);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj --filter FullyQualifiedName~RrIdentityBadgeTests`
Expected: PASS (9/9, counting both `[Theory]`s' cases).

- [ ] **Step 6: Full-solution build and full test run**

```bash
dotnet build RuinaRPG.sln
dotnet test tests/RuinaRPG.Tests.Client/RuinaRPG.Tests.Client.csproj
dotnet test tests/RuinaRPG.Tests.Unit
```

Expected: build 0/0; both test projects green.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add RrIdentityBadge (Selo de Linhagem)"
```

---

## After this plan

Fase 0 (Fundação) is complete once Task 9 is merged: a new MudBlazor-based shell (layout, nav, theme toggle, breadcrumbs), the color bridge, and the reusable pieces Fase 1 needs (`Section`, `EntityPicker`, the 3 domain field components, `RrIdentityBadge`) all exist, build clean, and (for the ones with real logic) are covered by bUnit. No page besides the shell chrome has been visually migrated yet — that's Fase 1's job, written as its own plan once this one is merged, per the spec's decomposition.
