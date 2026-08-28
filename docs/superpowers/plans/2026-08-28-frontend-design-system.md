# Front-End Design System & Responsive Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the untouched Blazor scaffold shell (default layout, default styling, no auth awareness) with a responsive, Sun/Moon-themed shell — design tokens, base component skin, a collapsible sidebar, a light/dark theme toggle, and an auth-aware nav — that every existing page inherits without markup changes.

**Architecture:** Keep Bootstrap as the structural/utility layer already used throughout the app; layer a custom design-token stylesheet (`theme.css`) and a base-component re-skin (`components.css`) on top via CSS custom properties, switched by a `data-theme` attribute on `<html>`. Add a from-scratch client-side auth-state system (none exists today) — a pure-C# JWT payload decoder in `RuinaRPG.Domain` (TDD'd) feeding a `TokenAuthenticationStateProvider` that Blazor's built-in `<AuthorizeView>` consumes — so the sidebar can honestly show only what's reachable per role.

**Tech Stack:** Same as established (Blazor WebAssembly 8, Bootstrap 5), plus self-hosted `Cormorant Garamond`/`Inter` woff2 fonts and vanilla JS interop for theme persistence (no new npm/JS tooling).

**Spec:** `docs/superpowers/specs/2026-08-28-frontend-design-system-design.md`

## Global Constraints

- TDD is mandatory (Técnico R0011) — applies to the one piece of this plan that's actual logic (JWT payload parsing in Task 5); the rest is markup/CSS verified by `dotnet build` and manual checks, matching how every other Client-only task in this codebase has been done.
- Round-1 scope only: the design system + shell. No individual page's internal content (Ficha de Personagem's grids, Gerenciador de Encontros' table, etc.) is redesigned in this plan.
- Both themes share identical structural tokens (spacing, radii, sidebar width, breakpoints) — only color tokens differ between Tema Sol and Tema Lua.
- The sidebar renders only what's genuinely reachable today per auth state (logged out / GM / Jogador) — it must not link to a page that doesn't actually work for that role yet.
- No secret ever hardcoded — unaffected by this plan (pure front-end, no new backend surface).

---

### Task 1: Font assets and design tokens

**Files:**
- Create: `src/RuinaRPG.Client/wwwroot/fonts/cormorant-garamond-latin.woff2`
- Create: `src/RuinaRPG.Client/wwwroot/fonts/cormorant-garamond-latin-ext.woff2`
- Create: `src/RuinaRPG.Client/wwwroot/fonts/inter-latin.woff2`
- Create: `src/RuinaRPG.Client/wwwroot/fonts/inter-latin-ext.woff2`
- Create: `src/RuinaRPG.Client/wwwroot/css/theme.css`

**Interfaces:**
- Produces: CSS custom properties on `:root` (Tema Sol, default) and `:root[data-theme='dark']` (Tema Lua) — `--rr-bg`, `--rr-surface`, `--rr-surface-alt`, `--rr-text`, `--rr-text-muted`, `--rr-primary`, `--rr-primary-hover`, `--rr-primary-contrast`, `--rr-accent`, `--rr-border`, `--rr-shadow`, `--rr-danger`, `--rr-success`, `--rr-sidebar-bg`, `--rr-sidebar-text`, `--rr-sidebar-text-muted`, `--rr-sidebar-active-bg`; structural tokens `--rr-font-heading`, `--rr-font-body`, `--rr-radius-sm/md/lg`, `--rr-space-1..5`, `--rr-sidebar-width`, `--rr-topbar-height`, `--rr-content-max-width`. Every later task's CSS reads these — do not rename any of them without updating every consumer.

- [ ] **Step 1: Download the font files**

These are the real, verified woff2 files for `Cormorant Garamond` (weights 400-700, variable) and `Inter` (weights 400-600, variable), Latin + Latin Extended subsets only (sufficient for Portuguese diacritics) — resolved from Google Fonts' CSS2 API and confirmed as valid WOFF2 binaries before writing this plan.

```bash
mkdir -p src/RuinaRPG.Client/wwwroot/fonts
curl -sL "https://fonts.gstatic.com/s/cormorantgaramond/v21/co3bmX5slCNuHLi8bLeY9MK7whWMhyjYp3tKgS4.woff2" -o src/RuinaRPG.Client/wwwroot/fonts/cormorant-garamond-latin-ext.woff2
curl -sL "https://fonts.gstatic.com/s/cormorantgaramond/v21/co3bmX5slCNuHLi8bLeY9MK7whWMhyjYqXtK.woff2" -o src/RuinaRPG.Client/wwwroot/fonts/cormorant-garamond-latin.woff2
curl -sL "https://fonts.gstatic.com/s/inter/v20/UcC73FwrK3iLTeHuS_nVMrMxCp50SjIa25L7SUc.woff2" -o src/RuinaRPG.Client/wwwroot/fonts/inter-latin-ext.woff2
curl -sL "https://fonts.gstatic.com/s/inter/v20/UcC73FwrK3iLTeHuS_nVMrMxCp50SjIa1ZL7.woff2" -o src/RuinaRPG.Client/wwwroot/fonts/inter-latin.woff2
file src/RuinaRPG.Client/wwwroot/fonts/*.woff2
```

Expected: all 4 lines report `Web Open Font Format (Version 2)`. If any download fails or `file` doesn't report WOFF2 (e.g. because the CDN URL rotated), stop and ask — do not substitute a placeholder or a different font family.

- [ ] **Step 2: Write `theme.css`**

`src/RuinaRPG.Client/wwwroot/css/theme.css`:

```css
/* ==========================================================================
   Ruína RPG — Design Tokens
   Tema Sol (Helônios) = claro · Tema Lua (Nyxara) = escuro
   Toggled via [data-theme="light"|"dark"] on <html>, set before first paint
   by the inline script in index.html — see theme.js for the runtime API
   the ThemeToggle component uses after load.
   ========================================================================== */

@font-face {
  font-family: 'Cormorant Garamond';
  font-style: normal;
  font-weight: 400 700;
  font-display: swap;
  src: url('/fonts/cormorant-garamond-latin-ext.woff2') format('woff2');
  unicode-range: U+0100-02BA, U+02BD-02C5, U+02C7-02CC, U+02CE-02D7, U+02DD-02FF, U+0304, U+0308, U+0329, U+1D00-1DBF, U+1E00-1E9F, U+1EF2-1EFF, U+2020, U+20A0-20AB, U+20AD-20C0, U+2113, U+2C60-2C7F, U+A720-A7FF;
}
@font-face {
  font-family: 'Cormorant Garamond';
  font-style: normal;
  font-weight: 400 700;
  font-display: swap;
  src: url('/fonts/cormorant-garamond-latin.woff2') format('woff2');
  unicode-range: U+0000-00FF, U+0131, U+0152-0153, U+02BB-02BC, U+02C6, U+02DA, U+02DC, U+0304, U+0308, U+0329, U+2000-206F, U+20AC, U+2122, U+2191, U+2193, U+2212, U+2215, U+FEFF, U+FFFD;
}
@font-face {
  font-family: 'Inter';
  font-style: normal;
  font-weight: 400 600;
  font-display: swap;
  src: url('/fonts/inter-latin-ext.woff2') format('woff2');
  unicode-range: U+0100-02BA, U+02BD-02C5, U+02C7-02CC, U+02CE-02D7, U+02DD-02FF, U+0304, U+0308, U+0329, U+1D00-1DBF, U+1E00-1E9F, U+1EF2-1EFF, U+2020, U+20A0-20AB, U+20AD-20C0, U+2113, U+2C60-2C7F, U+A720-A7FF;
}
@font-face {
  font-family: 'Inter';
  font-style: normal;
  font-weight: 400 600;
  font-display: swap;
  src: url('/fonts/inter-latin.woff2') format('woff2');
  unicode-range: U+0000-00FF, U+0131, U+0152-0153, U+02BB-02BC, U+02C6, U+02DA, U+02DC, U+0304, U+0308, U+0329, U+2000-206F, U+20AC, U+2122, U+2191, U+2193, U+2212, U+2215, U+FEFF, U+FFFD;
}

:root {
  /* Estrutura — compartilhada pelos dois temas */
  --rr-font-heading: 'Cormorant Garamond', Georgia, 'Times New Roman', serif;
  --rr-font-body: 'Inter', -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;

  --rr-radius-sm: 4px;
  --rr-radius-md: 8px;
  --rr-radius-lg: 16px;

  --rr-space-1: 4px;
  --rr-space-2: 8px;
  --rr-space-3: 16px;
  --rr-space-4: 24px;
  --rr-space-5: 40px;

  --rr-sidebar-width: 260px;
  --rr-topbar-height: 3.5rem;
  --rr-content-max-width: 1100px;

  /* Tema Sol (Helônios) — padrão */
  --rr-bg: #f6ecd9;
  --rr-surface: #fbf5e9;
  --rr-surface-alt: #efe1c4;
  --rr-text: #3b2a1a;
  --rr-text-muted: #6b5842;
  --rr-primary: #c8932c;
  --rr-primary-hover: #a97a20;
  --rr-primary-contrast: #2a1c0c;
  --rr-accent: #8a4a1f;
  --rr-border: #d9c39a;
  --rr-shadow: rgba(59, 42, 26, 0.18);
  --rr-danger: #a3312a;
  --rr-success: #4f7942;
  --rr-sidebar-bg: linear-gradient(180deg, #7a5424 0%, #4d3315 100%);
  --rr-sidebar-text: #f3e3c2;
  --rr-sidebar-text-muted: #d9c39a;
  --rr-sidebar-active-bg: rgba(255, 223, 158, 0.28);
}

:root[data-theme='dark'] {
  /* Tema Lua (Nyxara) */
  --rr-bg: #14172b;
  --rr-surface: #1d2140;
  --rr-surface-alt: #262b4d;
  --rr-text: #dcd6f0;
  --rr-text-muted: #9b93bd;
  --rr-primary: #b6b3e0;
  --rr-primary-hover: #cac7ee;
  --rr-primary-contrast: #14172b;
  --rr-accent: #eef0ff;
  --rr-border: #33375c;
  --rr-shadow: rgba(0, 0, 0, 0.45);
  --rr-danger: #e0685f;
  --rr-success: #7fc98f;
  --rr-sidebar-bg: linear-gradient(180deg, #0d0f21 0%, #1d1938 100%);
  --rr-sidebar-text: #e5e2fb;
  --rr-sidebar-text-muted: #9b93bd;
  --rr-sidebar-active-bg: rgba(182, 179, 224, 0.22);
}

html, body {
  background-color: var(--rr-bg);
  color: var(--rr-text);
  font-family: var(--rr-font-body);
  transition: background-color 0.2s ease, color 0.2s ease;
}

h1, h2, h3, h4, h5, h6 {
  font-family: var(--rr-font-heading);
  font-weight: 600;
  color: var(--rr-text);
}

a, .btn-link {
  color: var(--rr-primary);
}

a:hover, .btn-link:hover {
  color: var(--rr-primary-hover);
}
```

- [ ] **Step 3: Verify the build picks up the new static assets**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

Run: `ls src/RuinaRPG.Client/bin/Debug/net8.0/wwwroot/fonts/ src/RuinaRPG.Client/bin/Debug/net8.0/wwwroot/css/theme.css`
Expected: all 4 font files and `theme.css` are listed (Blazor's static web assets pipeline copies everything under `wwwroot/` automatically — no `.csproj` change needed).

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/wwwroot/fonts src/RuinaRPG.Client/wwwroot/css/theme.css
git commit -m "feat: add self-hosted fonts and Sol/Lua design tokens"
```

---

### Task 2: Base component skin

**Files:**
- Create: `src/RuinaRPG.Client/wwwroot/css/components.css`
- Modify: `src/RuinaRPG.Client/wwwroot/css/app.css`

**Interfaces:**
- Consumes: the `--rr-*` tokens from Task 1's `theme.css`.
- Produces: re-skinned Bootstrap primitives (`.btn`, `.btn-primary`, `.btn-danger`, `.card`, `.list-group-item`, `.table`, `.form-control`, `.form-select`, `label`, `.alert-danger`/`.validation-message`/`.error`, `.alert-success`, `hr`) that every existing page's markup already uses, so they re-skin automatically with no page changes.

- [ ] **Step 1: Write `components.css`**

`src/RuinaRPG.Client/wwwroot/css/components.css`:

```css
/* ==========================================================================
   Ruína RPG — Base component skin
   Re-styles Bootstrap's default elements using the tokens from theme.css.
   Every existing page inherits this without markup changes.
   ========================================================================== */

.btn {
  border-radius: var(--rr-radius-sm);
  font-family: var(--rr-font-body);
  font-weight: 600;
}

.btn-primary {
  background-color: var(--rr-primary);
  border-color: var(--rr-primary);
  color: var(--rr-primary-contrast);
}

.btn-primary:hover, .btn-primary:focus {
  background-color: var(--rr-primary-hover);
  border-color: var(--rr-primary-hover);
  color: var(--rr-primary-contrast);
}

.btn-danger {
  background-color: var(--rr-danger);
  border-color: var(--rr-danger);
}

.btn:focus, .btn:active:focus, .btn-link.nav-link:focus, .form-check-input:focus {
  box-shadow: 0 0 0 0.2rem var(--rr-shadow);
}

.card, .list-group-item {
  background-color: var(--rr-surface);
  border-color: var(--rr-border);
  color: var(--rr-text);
}

.table {
  --bs-table-bg: var(--rr-surface);
  --bs-table-color: var(--rr-text);
  --bs-table-border-color: var(--rr-border);
  --bs-table-striped-bg: var(--rr-surface-alt);
}

thead th {
  font-family: var(--rr-font-heading);
  font-weight: 600;
  border-bottom-color: var(--rr-border);
}

.form-control, .form-select {
  background-color: var(--rr-surface);
  border-color: var(--rr-border);
  color: var(--rr-text);
  border-radius: var(--rr-radius-sm);
}

.form-control:focus, .form-select:focus {
  background-color: var(--rr-surface);
  color: var(--rr-text);
  border-color: var(--rr-primary);
  box-shadow: 0 0 0 0.2rem var(--rr-shadow);
}

label {
  color: var(--rr-text-muted);
  font-weight: 600;
}

.alert-danger, .validation-message, .error {
  color: var(--rr-danger);
}

.alert-success {
  color: var(--rr-success);
}

hr {
  border-color: var(--rr-border);
}
```

- [ ] **Step 2: Rewrite `app.css`, dropping the hardcoded colors/fonts now superseded by `theme.css`/`components.css`**

`src/RuinaRPG.Client/wwwroot/css/app.css` (replace the whole file):

```css
h1:focus {
    outline: none;
}

.content {
    padding-top: 1.1rem;
}

.valid.modified:not([type=checkbox]) {
    outline: 1px solid var(--rr-success);
}

.invalid {
    outline: 1px solid var(--rr-danger);
}

.validation-message {
    color: var(--rr-danger);
}

#blazor-error-ui {
    background: var(--rr-surface-alt);
    color: var(--rr-text);
    bottom: 0;
    box-shadow: 0 -1px 2px var(--rr-shadow);
    display: none;
    left: 0;
    padding: 0.6rem 1.25rem 0.7rem 1.25rem;
    position: fixed;
    width: 100%;
    z-index: 1000;
}

    #blazor-error-ui .dismiss {
        cursor: pointer;
        position: absolute;
        right: 0.75rem;
        top: 0.5rem;
    }

.blazor-error-boundary {
    background: var(--rr-danger);
    padding: 1rem;
    color: white;
    border-radius: var(--rr-radius-sm);
}

    .blazor-error-boundary::after {
        content: "Ocorreu um erro."
    }

.loading-progress {
    position: relative;
    display: block;
    width: 8rem;
    height: 8rem;
    margin: 20vh auto 1rem auto;
}

    .loading-progress circle {
        fill: none;
        stroke: var(--rr-border);
        stroke-width: 0.6rem;
        transform-origin: 50% 50%;
        transform: rotate(-90deg);
    }

        .loading-progress circle:last-child {
            stroke: var(--rr-primary);
            stroke-dasharray: calc(3.141 * var(--blazor-load-percentage, 0%) * 0.8), 500%;
            transition: stroke-dasharray 0.05s ease-in-out;
        }

.loading-progress-text {
    position: absolute;
    text-align: center;
    font-weight: bold;
    inset: calc(20vh + 3.25rem) 0 auto 0.2rem;
    color: var(--rr-text);
}

    .loading-progress-text:after {
        content: var(--blazor-load-percentage-text, "Carregando");
    }

code {
    color: var(--rr-accent);
}
```

- [ ] **Step 3: Verify the build**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/wwwroot/css/components.css src/RuinaRPG.Client/wwwroot/css/app.css
git commit -m "feat: re-skin base bootstrap components with the design tokens"
```

---

### Task 3: No-flash theme bootstrap

**Files:**
- Create: `src/RuinaRPG.Client/wwwroot/js/theme.js`
- Modify: `src/RuinaRPG.Client/wwwroot/index.html`

**Interfaces:**
- Produces: `window.ruinaTheme.resolveInitial() : string` ("light"|"dark") and `window.ruinaTheme.set(theme: string) : void`, both called via Blazor `IJSRuntime` interop by Task 4's `ThemeToggle` component. Also sets `data-theme` on `<html>` synchronously before first paint via an inline script.

- [ ] **Step 1: Write `theme.js`**

`src/RuinaRPG.Client/wwwroot/js/theme.js`:

```javascript
window.ruinaTheme = {
    KEY: 'rr-theme',

    resolveInitial: function () {
        var stored = localStorage.getItem(this.KEY);
        if (stored === 'light' || stored === 'dark') return stored;
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    },

    set: function (theme) {
        localStorage.setItem(this.KEY, theme);
        document.documentElement.setAttribute('data-theme', theme);
    }
};
```

- [ ] **Step 2: Wire it into `index.html`**

`src/RuinaRPG.Client/wwwroot/index.html` (replace the whole file):

```html
<!DOCTYPE html>
<html lang="pt-BR">

<head>
    <meta charset="utf-8" />
    <script>
        (function () {
            var stored = localStorage.getItem('rr-theme');
            var theme = (stored === 'light' || stored === 'dark')
                ? stored
                : (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
            document.documentElement.setAttribute('data-theme', theme);
        })();
    </script>
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>Ruína RPG</title>
    <base href="/" />
    <link rel="stylesheet" href="css/bootstrap/bootstrap.min.css" />
    <link rel="stylesheet" href="css/theme.css" />
    <link rel="stylesheet" href="css/components.css" />
    <link rel="stylesheet" href="css/app.css" />
    <link rel="icon" type="image/png" href="favicon.png" />
    <link href="RuinaRPG.Client.styles.css" rel="stylesheet" />
    <script src="js/theme.js"></script>
</head>

<body>
    <div id="app">
        <svg class="loading-progress">
            <circle r="40%" cx="50%" cy="50%" />
            <circle r="40%" cx="50%" cy="50%" />
        </svg>
        <div class="loading-progress-text"></div>
    </div>

    <div id="blazor-error-ui">
        Ocorreu um erro inesperado.
        <a href="" class="reload">Recarregar</a>
        <a class="dismiss">🗙</a>
    </div>
    <script src="_framework/blazor.webassembly.js"></script>
</body>

</html>
```

The inline script is placed as the very first thing in `<head>`, before the (render-blocking) stylesheet `<link>` tags, so `data-theme` exists on `<html>` before the browser starts applying `theme.css`'s `:root[data-theme='dark']` rules — this is what avoids a flash of the wrong theme. `lang="pt-BR"` and the page `<title>`/error-UI text were also fixed from the template's English/generic defaults while touching this file.

- [ ] **Step 3: Verify the build**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/wwwroot/js/theme.js src/RuinaRPG.Client/wwwroot/index.html
git commit -m "feat: add no-flash theme bootstrap and persistence"
```

---

### Task 4: Responsive shell — MainLayout, restructured NavMenu, ThemeToggle

**Files:**
- Modify: `src/RuinaRPG.Client/Layout/MainLayout.razor`
- Modify: `src/RuinaRPG.Client/Layout/MainLayout.razor.css`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor.css`
- Create: `src/RuinaRPG.Client/Layout/ThemeToggle.razor`
- Create: `src/RuinaRPG.Client/Layout/ThemeToggle.razor.css`

**Interfaces:**
- Consumes: `theme.css`/`components.css` tokens (Tasks 1-2), `window.ruinaTheme.resolveInitial()`/`.set()` (Task 3).
- Produces: `NavMenu` gains `[Parameter] public EventCallback OnLinkClicked { get; set; }` — Task 8 (auth-aware rewrite) and any later consumer must keep this parameter. This task's `NavMenu` still shows every link unconditionally (no auth logic yet — that's Task 8, layered on top without further structural changes) and has no logout capability yet.

- [ ] **Step 1: Rewrite `MainLayout.razor`**

`src/RuinaRPG.Client/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase

<div class="page">
    <aside class="sidebar @(_sidebarOpen ? "sidebar-open" : "")">
        <NavMenu OnLinkClicked="CloseSidebar" />
    </aside>

    @if (_sidebarOpen)
    {
        <div class="sidebar-backdrop" @onclick="CloseSidebar"></div>
    }

    <div class="main-column">
        <header class="top-bar">
            <button class="hamburger" @onclick="ToggleSidebar" title="Abrir menu" aria-label="Abrir menu de navegação">
                <span></span>
                <span></span>
                <span></span>
            </button>
            <span class="top-bar-title">Ruína RPG</span>
            <ThemeToggle />
        </header>

        <main class="content">
            @Body
        </main>
    </div>
</div>

@code {
    private bool _sidebarOpen;

    private void ToggleSidebar() => _sidebarOpen = !_sidebarOpen;

    private void CloseSidebar() => _sidebarOpen = false;
}
```

- [ ] **Step 2: Rewrite `MainLayout.razor.css`**

`src/RuinaRPG.Client/Layout/MainLayout.razor.css` (replace the whole file):

```css
.page {
    display: flex;
    min-height: 100vh;
}

.sidebar {
    background: var(--rr-sidebar-bg);
    width: var(--rr-sidebar-width);
    flex-shrink: 0;
    position: fixed;
    top: 0;
    bottom: 0;
    left: 0;
    z-index: 20;
    overflow-y: auto;
    transform: translateX(-100%);
    transition: transform 0.2s ease;
}

.sidebar.sidebar-open {
    transform: translateX(0);
}

.sidebar-backdrop {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    z-index: 15;
}

.main-column {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-width: 0;
}

.top-bar {
    height: var(--rr-topbar-height);
    background-color: var(--rr-surface);
    border-bottom: 1px solid var(--rr-border);
    display: flex;
    align-items: center;
    gap: var(--rr-space-3);
    padding: 0 var(--rr-space-3);
    position: sticky;
    top: 0;
    z-index: 10;
}

.top-bar-title {
    font-family: var(--rr-font-heading);
    font-size: 1.25rem;
    font-weight: 600;
    color: var(--rr-text);
    flex: 1;
}

.hamburger {
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 4px;
    width: 2rem;
    height: 2rem;
    background: none;
    border: none;
    cursor: pointer;
}

.hamburger span {
    display: block;
    height: 2px;
    background-color: var(--rr-text);
    border-radius: 2px;
}

.content {
    flex: 1;
    max-width: var(--rr-content-max-width);
    width: 100%;
    margin: 0 auto;
    padding: var(--rr-space-4);
}

@media (min-width: 992px) {
    .sidebar {
        position: sticky;
        transform: none;
    }

    .sidebar-backdrop {
        display: none;
    }

    .hamburger {
        display: none;
    }
}
```

- [ ] **Step 3: Restructure `NavMenu.razor`** (still unconditional — auth-awareness is Task 8)

`src/RuinaRPG.Client/Layout/NavMenu.razor` (replace the whole file):

```razor
<nav class="nav-menu">
    <div class="nav-item">
        <NavLink class="nav-link" href="" Match="NavLinkMatch.All" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">🏠</span> Início
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="login" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">🔑</span> Entrar
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="cadastro" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">✒️</span> Cadastrar
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="painel/convites" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">✉️</span> Convidar Jogador
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="painel/jogadores" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">👥</span> Jogadores
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="catalogo" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">🎒</span> Catálogo de Itens
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="npcs" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">🎭</span> NPCs do GM
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="bestiario" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">🐺</span> Bestiário do GM
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="banco-de-magias" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">📜</span> Banco de Magias e Habilidades
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="campanhas" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">🗺️</span> Campanhas
        </NavLink>
    </div>
    <div class="nav-item">
        <NavLink class="nav-link" href="compendio" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">📖</span> Compêndio de Regras
        </NavLink>
    </div>
</nav>

@code {
    [Parameter] public EventCallback OnLinkClicked { get; set; }

    private Task NotifyLinkClicked() => OnLinkClicked.InvokeAsync();
}
```

- [ ] **Step 4: Rewrite `NavMenu.razor.css`**

`src/RuinaRPG.Client/Layout/NavMenu.razor.css` (replace the whole file):

```css
.nav-menu {
    display: flex;
    flex-direction: column;
    padding: var(--rr-space-3) 0;
}

.nav-item {
    padding: 0 var(--rr-space-2);
    margin-bottom: var(--rr-space-1);
}

.nav-link {
    display: flex;
    align-items: center;
    gap: var(--rr-space-2);
    padding: var(--rr-space-2) var(--rr-space-3);
    border-radius: var(--rr-radius-sm);
    color: var(--rr-sidebar-text-muted);
    text-decoration: none;
    font-weight: 600;
}

.nav-link:hover {
    background-color: var(--rr-sidebar-active-bg);
    color: var(--rr-sidebar-text);
}

.nav-link.active {
    background-color: var(--rr-sidebar-active-bg);
    color: var(--rr-sidebar-text);
}

.nav-icon {
    font-size: 1.1rem;
    line-height: 1;
}
```

- [ ] **Step 5: Write `ThemeToggle.razor`**

`src/RuinaRPG.Client/Layout/ThemeToggle.razor`:

```razor
@inject IJSRuntime JS

<button class="theme-toggle" @onclick="ToggleAsync" title="Alternar entre tema Sol e Lua" aria-label="Alternar entre tema Sol e Lua">
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

- [ ] **Step 6: Write `ThemeToggle.razor.css`**

`src/RuinaRPG.Client/Layout/ThemeToggle.razor.css`:

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

.theme-toggle-icon {
    opacity: 0.35;
    font-size: 1rem;
    transition: opacity 0.15s ease;
}

.theme-toggle-icon.active {
    opacity: 1;
}
```

- [ ] **Step 7: Verify the build**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Layout
git commit -m "feat: add the responsive shell with a collapsible sidebar and theme toggle"
```

---

### Task 5: Client-side JWT payload parsing (Domain, TDD)

**Files:**
- Create: `src/RuinaRPG.Domain/Auth/JwtClaimsParser.cs`
- Test: `tests/RuinaRPG.Tests.Unit/Auth/JwtClaimsParserTests.cs`

**Interfaces:**
- Produces: `JwtClaimsParser.ParsePayload(string jwt) : IReadOnlyDictionary<string, string>` — throws `FormatException` on a malformed token (wrong segment count, or a non-base64url/non-JSON payload segment). Task 6's `TokenAuthenticationStateProvider` calls this exact signature.

This is a pure decode of the JWT payload the server already issued — no signature verification (the API is still the actual enforcement point for every request; this only drives what the sidebar *shows*).

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/Auth/JwtClaimsParserTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Auth;

namespace RuinaRPG.Tests.Unit.Auth;

public class JwtClaimsParserTests
{
    // header {"alg":"HS256","typ":"JWT"}, payload {"sub":"a1b2c3d4-0000-0000-0000-000000000001","role":"GM","nickname":"Testador","exp":9999999999}
    // — independently base64url-encoded outside this codebase, not generated by the code under test.
    private const string ValidToken =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJhMWIyYzNkNC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDEiLCJyb2xlIjoiR00iLCJuaWNrbmFtZSI6IlRlc3RhZG9yIiwiZXhwIjo5OTk5OTk5OTk5fQ.dGVzdC1zaWduYXR1cmU";

    [Fact]
    public void ParsePayload_extracts_sub_role_and_nickname_from_a_valid_token()
    {
        var claims = JwtClaimsParser.ParsePayload(ValidToken);

        claims["sub"].Should().Be("a1b2c3d4-0000-0000-0000-000000000001");
        claims["role"].Should().Be("GM");
        claims["nickname"].Should().Be("Testador");
    }

    [Fact]
    public void ParsePayload_throws_FormatException_for_a_token_with_the_wrong_number_of_segments()
    {
        var act = () => JwtClaimsParser.ParsePayload("not-a-jwt");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void ParsePayload_throws_for_a_payload_segment_that_is_not_valid_base64url()
    {
        var act = () => JwtClaimsParser.ParsePayload("header.not!!valid!!base64url.signature");

        act.Should().Throw<FormatException>();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter JwtClaimsParserTests`
Expected: FAIL to compile (`RuinaRPG.Domain.Auth` namespace / `JwtClaimsParser` type don't exist yet).

- [ ] **Step 3: Write `JwtClaimsParser`**

`src/RuinaRPG.Domain/Auth/JwtClaimsParser.cs`:

```csharp
using System.Text;
using System.Text.Json;

namespace RuinaRPG.Domain.Auth;

public static class JwtClaimsParser
{
    public static IReadOnlyDictionary<string, string> ParsePayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length != 3)
            throw new FormatException("Token JWT malformado: esperado 3 partes separadas por '.'.");

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));

        using var document = JsonDocument.Parse(payloadJson);
        var claims = new Dictionary<string, string>();
        foreach (var property in document.RootElement.EnumerateObject())
            claims[property.Name] = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()!
                : property.Value.GetRawText();

        return claims;
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
```

`Convert.FromBase64String` throws `FormatException` on invalid base64 characters (covers Step 2's third test); `JsonDocument.Parse` throws `JsonException` — which derives from... actually it does NOT derive from `FormatException`. If the base64url segment happens to decode into bytes that aren't valid UTF-8 JSON, `JsonDocument.Parse` throws `System.Text.Json.JsonException`, not `FormatException`. The third test above (`"not!!valid!!base64url"`) fails at the `Convert.FromBase64String` step (invalid base64 characters `!`), which does throw `FormatException` — so that specific test passes as written. This is a deliberate, narrow contract: malformed base64 → `FormatException`; valid base64 that isn't JSON → `JsonException` (uncaught, propagates as-is). `TokenAuthenticationStateProvider` (Task 6) only catches `FormatException` for this reason — a payload that's valid base64url but not JSON is not a realistic case for a real API-issued token, so it's deliberately left uncaught rather than papered over.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter JwtClaimsParserTests`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/Auth tests/RuinaRPG.Tests.Unit/Auth
git commit -m "feat: add a pure jwt payload parser for client-side auth state"
```

---

### Task 6: Client-side auth state provider

**Files:**
- Create: `src/RuinaRPG.Client/Services/TokenAuthenticationStateProvider.cs`
- Modify: `src/RuinaRPG.Client/RuinaRPG.Client.csproj`
- Modify: `src/RuinaRPG.Client/Program.cs`
- Modify: `src/RuinaRPG.Client/App.razor`

**Interfaces:**
- Consumes: `JwtClaimsParser.ParsePayload` (Task 5), `AuthStateService.GetAccessTokenAsync() : ValueTask<string?>` (already exists in `src/RuinaRPG.Client/Services/AuthStateService.cs`).
- Produces: `TokenAuthenticationStateProvider : AuthenticationStateProvider` with a public `NotifyUserChanged() : void` method. Task 7 (Login/Cadastro/Painel) and Task 8 (NavMenu's Sair link) both call this exact method after any token change. Registered in DI as both its concrete type and as the `AuthenticationStateProvider` Blazor's `<AuthorizeView>`/`<CascadingAuthenticationState>` resolve — both resolutions MUST return the same scoped instance, or `NotifyUserChanged()` calls made through the concrete-typed injection won't be observed by `<AuthorizeView>`.

- [ ] **Step 1: Add the Domain project reference**

`src/RuinaRPG.Client/RuinaRPG.Client.csproj` — add inside the existing `<ItemGroup>` that has the `RuinaRPG.Contracts` reference:

```xml
    <ProjectReference Include="..\RuinaRPG.Domain\RuinaRPG.Domain.csproj" />
```

- [ ] **Step 2: Write `TokenAuthenticationStateProvider`**

`src/RuinaRPG.Client/Services/TokenAuthenticationStateProvider.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using RuinaRPG.Domain.Auth;

namespace RuinaRPG.Client.Services;

public class TokenAuthenticationStateProvider(AuthStateService authState) : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal AnonymousPrincipal = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await authState.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(AnonymousPrincipal);

        IReadOnlyDictionary<string, string> payload;
        try
        {
            payload = JwtClaimsParser.ParsePayload(token);
        }
        catch (FormatException)
        {
            return new AuthenticationState(AnonymousPrincipal);
        }

        var claims = new List<Claim>();
        if (payload.TryGetValue("sub", out var sub))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        if (payload.TryGetValue("role", out var role))
            claims.Add(new Claim(ClaimTypes.Role, role));
        if (payload.TryGetValue("nickname", out var nickname))
            claims.Add(new Claim(ClaimTypes.Name, nickname));

        var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyUserChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
```

The `role` claim is mapped to `ClaimTypes.Role` (not left as the raw `"role"` string) specifically so `<AuthorizeView Roles="GM">` — which checks `ClaimsPrincipal.IsInRole`, and `IsInRole` checks claims of type `ClaimsIdentity.RoleClaimType`, which defaults to `ClaimTypes.Role` — works without any extra configuration.

- [ ] **Step 3: Register it in `Program.cs`**

`src/RuinaRPG.Client/Program.cs` — add after the existing `builder.Services.AddScoped<AuthStateService>();` line:

```csharp
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<TokenAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TokenAuthenticationStateProvider>());
```

Add `using Microsoft.AspNetCore.Components.Authorization;` to the top of the file.

- [ ] **Step 4: Wrap the router in `CascadingAuthenticationState`**

`src/RuinaRPG.Client/App.razor` (replace the whole file):

```razor
<CascadingAuthenticationState>
    <Router AppAssembly="@typeof(App).Assembly">
        <Found Context="routeData">
            <RouteView RouteData="@routeData" DefaultLayout="@typeof(MainLayout)" />
            <FocusOnNavigate RouteData="@routeData" Selector="h1" />
        </Found>
        <NotFound>
            <PageTitle>Not found</PageTitle>
            <LayoutView Layout="@typeof(MainLayout)">
                <p role="alert">Sorry, there's nothing at this address.</p>
            </LayoutView>
        </NotFound>
    </Router>
</CascadingAuthenticationState>
```

- [ ] **Step 5: Verify the build**

Run: `dotnet build`
Expected: `Build succeeded`, 0 warnings (the whole solution, not just the Client, since `RuinaRPG.Client.csproj` changed).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Services/TokenAuthenticationStateProvider.cs src/RuinaRPG.Client/RuinaRPG.Client.csproj src/RuinaRPG.Client/Program.cs src/RuinaRPG.Client/App.razor
git commit -m "feat: add client-side auth state from the stored jwt"
```

---

### Task 7: Wire login, registration, and logout to the auth state provider

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/Login.razor`
- Modify: `src/RuinaRPG.Client/Pages/Cadastro.razor`
- Modify: `src/RuinaRPG.Client/Pages/Painel.razor`

**Interfaces:**
- Consumes: `TokenAuthenticationStateProvider.NotifyUserChanged()` (Task 6).
- Produces: nothing new — this task just makes the 3 existing token-mutation points call the notifier so `<AuthorizeView>` (Task 8) reacts immediately instead of only after a full page reload.

- [ ] **Step 1: Update `Login.razor`**

`src/RuinaRPG.Client/Pages/Login.razor` — add the injection and the notify call:

```diff
 @page "/login"
 @inject HttpClient Http
 @inject AuthStateService AuthState
 @inject NavigationManager Navigation
+@inject TokenAuthenticationStateProvider AuthProvider
 @using RuinaRPG.Contracts.Auth
```

```diff
         var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
         await AuthState.SetTokensAsync(body!);
+        AuthProvider.NotifyUserChanged();
         Navigation.NavigateTo("/painel");
```

- [ ] **Step 2: Update `Cadastro.razor`**

`src/RuinaRPG.Client/Pages/Cadastro.razor` — same pattern, in `HandleResponseAsync`:

```diff
 @page "/cadastro"
 @inject HttpClient Http
 @inject AuthStateService AuthState
 @inject NavigationManager Navigation
+@inject TokenAuthenticationStateProvider AuthProvider
 @using RuinaRPG.Contracts.Auth
```

```diff
         var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
         await AuthState.SetTokensAsync(body!);
+        AuthProvider.NotifyUserChanged();
         Navigation.NavigateTo("/painel");
```

- [ ] **Step 3: Update `Painel.razor`**

`src/RuinaRPG.Client/Pages/Painel.razor` (replace the whole file):

```razor
@page "/painel"
@inject HttpClient Http
@inject AuthStateService AuthState
@inject NavigationManager Navigation
@inject TokenAuthenticationStateProvider AuthProvider

<h1>Painel</h1>
<p>Em construção.</p>
<button @onclick="LogoutAsync">Sair</button>

@code {
    private async Task LogoutAsync()
    {
        var refreshToken = await AuthState.GetRefreshTokenAsync();
        if (refreshToken is not null)
            await Http.PostAsJsonAsync("auth/logout", new RuinaRPG.Contracts.Auth.RefreshRequest(refreshToken));

        await AuthState.ClearAsync();
        AuthProvider.NotifyUserChanged();
        Navigation.NavigateTo("/");
    }
}
```

- [ ] **Step 4: Verify the build**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Pages/Login.razor src/RuinaRPG.Client/Pages/Cadastro.razor src/RuinaRPG.Client/Pages/Painel.razor
git commit -m "feat: notify the auth state provider on login, registration, and logout"
```

---

### Task 8: Auth-aware sidebar

**Files:**
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`

**Interfaces:**
- Consumes: `<AuthorizeView>`/`<AuthorizeView Roles="GM">` (built-in Blazor, driven by Task 6's provider via the `<CascadingAuthenticationState>` wired in `App.razor`), `TokenAuthenticationStateProvider.NotifyUserChanged()` (Task 6), `AuthStateService` (existing).
- Produces: the sidebar now shows exactly what's reachable per auth state — logged out sees only Início/Entrar/Cadastrar; a logged-in GM sees everything the app has; a logged-in Jogador sees Início/Compêndio/Sair only, since no other page is wired for a Jogador anywhere in this codebase today (confirmed: `MinhaCampanha.razor` and every character-sheet page have no nav entry anywhere, and every other controller besides `CompendioController` is `[Authorize(Roles = "GM")]`) — this task does not add new Jogador-facing pages, it only stops the sidebar from linking to GM-only tools a Jogador can't use.

- [ ] **Step 1: Rewrite `NavMenu.razor`**

`src/RuinaRPG.Client/Layout/NavMenu.razor` (replace the whole file):

```razor
@using Microsoft.AspNetCore.Components.Authorization
@inject HttpClient Http
@inject AuthStateService AuthState
@inject NavigationManager Navigation
@inject TokenAuthenticationStateProvider AuthProvider

<nav class="nav-menu">
    <div class="nav-item">
        <NavLink class="nav-link" href="" Match="NavLinkMatch.All" @onclick="NotifyLinkClicked">
            <span class="nav-icon" aria-hidden="true">🏠</span> Início
        </NavLink>
    </div>

    <AuthorizeView>
        <NotAuthorized>
            <div class="nav-item">
                <NavLink class="nav-link" href="login" @onclick="NotifyLinkClicked">
                    <span class="nav-icon" aria-hidden="true">🔑</span> Entrar
                </NavLink>
            </div>
            <div class="nav-item">
                <NavLink class="nav-link" href="cadastro" @onclick="NotifyLinkClicked">
                    <span class="nav-icon" aria-hidden="true">✒️</span> Cadastrar
                </NavLink>
            </div>
        </NotAuthorized>
        <Authorized>
            <AuthorizeView Roles="GM" Context="gmContext">
                <Authorized>
                    <div class="nav-item">
                        <NavLink class="nav-link" href="painel/convites" @onclick="NotifyLinkClicked">
                            <span class="nav-icon" aria-hidden="true">✉️</span> Convidar Jogador
                        </NavLink>
                    </div>
                    <div class="nav-item">
                        <NavLink class="nav-link" href="painel/jogadores" @onclick="NotifyLinkClicked">
                            <span class="nav-icon" aria-hidden="true">👥</span> Jogadores
                        </NavLink>
                    </div>
                    <div class="nav-item">
                        <NavLink class="nav-link" href="catalogo" @onclick="NotifyLinkClicked">
                            <span class="nav-icon" aria-hidden="true">🎒</span> Catálogo de Itens
                        </NavLink>
                    </div>
                    <div class="nav-item">
                        <NavLink class="nav-link" href="npcs" @onclick="NotifyLinkClicked">
                            <span class="nav-icon" aria-hidden="true">🎭</span> NPCs do GM
                        </NavLink>
                    </div>
                    <div class="nav-item">
                        <NavLink class="nav-link" href="bestiario" @onclick="NotifyLinkClicked">
                            <span class="nav-icon" aria-hidden="true">🐺</span> Bestiário do GM
                        </NavLink>
                    </div>
                    <div class="nav-item">
                        <NavLink class="nav-link" href="banco-de-magias" @onclick="NotifyLinkClicked">
                            <span class="nav-icon" aria-hidden="true">📜</span> Banco de Magias e Habilidades
                        </NavLink>
                    </div>
                    <div class="nav-item">
                        <NavLink class="nav-link" href="campanhas" @onclick="NotifyLinkClicked">
                            <span class="nav-icon" aria-hidden="true">🗺️</span> Campanhas
                        </NavLink>
                    </div>
                </Authorized>
            </AuthorizeView>
            <div class="nav-item">
                <NavLink class="nav-link" href="compendio" @onclick="NotifyLinkClicked">
                    <span class="nav-icon" aria-hidden="true">📖</span> Compêndio de Regras
                </NavLink>
            </div>
            <div class="nav-item">
                <a class="nav-link" href="javascript:void(0)" @onclick="LogoutAsync">
                    <span class="nav-icon" aria-hidden="true">🚪</span> Sair
                </a>
            </div>
        </Authorized>
    </AuthorizeView>
</nav>

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

The outer `<AuthorizeView>` has no `Roles` — it matches any authenticated user (GM or Jogador). The inner `<AuthorizeView Roles="GM" Context="gmContext">` is nested inside the outer's `<Authorized>` fragment and further restricts the GM-only tool block to the GM role specifically; it's given an explicit `Context` name (`gmContext`, unused in the body) purely so it doesn't collide with the outer view's implicit `context` parameter — Blazor requires distinct cascading-parameter names when `<AuthorizeView>` is nested.

- [ ] **Step 2: Verify the build**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`, 0 warnings.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Layout/NavMenu.razor
git commit -m "feat: make the sidebar auth-aware, showing only reachable links per role"
```

---

### Task 9: End-to-end verification through Docker/nginx

**Files:**
- No new source files.

**Interfaces:**
- Consumes: the full stack. Produces: nothing new.

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build`
Expected: `Build succeeded`, 0 Warning(s), 0 Error(s).

- [ ] **Step 2: Boot the stack**

```bash
make deploy
```

- [ ] **Step 3: Confirm the served page carries the new assets, through nginx**

```bash
curl -sf http://localhost/ | grep -E "rr-theme|css/theme.css|css/components.css|js/theme.js|Ruína RPG"
```

Expected: matches on all of: the inline `rr-theme` bootstrap script, the `css/theme.css` and `css/components.css` `<link>` tags, the `js/theme.js` `<script>` tag, and the `<title>Ruína RPG</title>`.

```bash
curl -sf -o /dev/null -w "%{http_code}\n" http://localhost/css/theme.css
curl -sf -o /dev/null -w "%{http_code}\n" http://localhost/fonts/inter-latin.woff2
```

Expected: both `200`.

- [ ] **Step 4: Manual visual check (not curl-verifiable — do this in an actual browser)**

Open `http://localhost/` and confirm:
- The page renders in Tema Sol or Tema Lua matching your OS's current light/dark setting (no flash of the wrong theme on load).
- Clicking the Sun/Moon toggle in the top bar switches themes instantly and the choice survives a reload.
- Below ~992px width (resize the window, or use devtools' responsive mode), the sidebar is hidden behind the hamburger toggle; opening it shows a backdrop, and clicking a link or the backdrop closes it.
- Logged out: the sidebar shows only Início/Entrar/Cadastrar.
- Logged in as a GM (register one via `/cadastro`): the sidebar shows every GM tool, Compêndio, and Sair; clicking Sair returns to the logged-out state immediately (no reload needed).

- [ ] **Step 5: Tear down**

```bash
make down
```

- [ ] **Step 6: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify the redesigned shell end-to-end through nginx"
```

## Explicitly out of scope for this plan

- Redesigning any individual page's internal content/layout (Ficha de Personagem, Gerenciador de Encontros, Compêndio, etc.) — those inherit the new base component styles automatically via `components.css`, but their bespoke markup is untouched. A follow-up round, per the approved design.
- Fixing the pre-existing gap where a Jogador has no reachable page besides Compêndio (no nav link anywhere in this codebase points at `MinhaCampanha.razor` or a character sheet) — the sidebar in this plan renders that gap honestly rather than linking to something broken; actually closing it needs new API surface (e.g. a `GET /api/campaigns/mine`-style endpoint), which is out of scope for a front-end-only plan.
- Token expiry handling in `TokenAuthenticationStateProvider` — it reflects "is a token stored," not "is it currently valid." An expired token still fails server-side on the next API call (existing, unaffected behavior); adding client-side expiry awareness is a separate, small enhancement this plan doesn't attempt.
- `WithAutomaticReconnect`-style SignalR reconnection concerns are unrelated to this plan (that's the Gerenciador de Encontros hub, not touched here).
