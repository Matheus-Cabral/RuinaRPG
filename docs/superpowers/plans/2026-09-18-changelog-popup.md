# Popup de Changelog para GM — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a GM-only popup once per app version, listing what changed since 2026-09-14 (v1.2.0),
persisted per-user in the database so it never reappears once dismissed for that version.

**Architecture:** A single `AppVersionInfo.Current` constant is the source of truth for "what
version is this". `ApplicationUser` gains a `LastSeenAppVersion` column (same pattern as the
existing `IsRulesAuditor` flag). `AuthController.Me()` compares the two and exposes
`PendingChangelogVersion` in `MeResponse` (non-null only for a GM who hasn't seen the current
version yet); a new `POST auth/dismiss-changelog` marks it seen. The client's `Painel.razor` shows
a `ChangelogDialog` component whenever that field is non-null, with the changelog text hardcoded
in the component.

**Tech Stack:** ASP.NET Core 8, EF Core/PostgreSQL (Identity's `AspNetUsers` table), Blazor
WebAssembly, MudBlazor. One new migration.

**Spec:** `docs/superpowers/specs/2026-09-18-changelog-popup-design.md`

## Global Constraints

- `dotnet build` must stay at 0 Warning(s), 0 Error(s) after every task.
- The changelog text is hardcoded client-side — not fetched from any endpoint, not editable at
  runtime. The exact text is given verbatim in Task 3 — do not paraphrase it.
- `AppVersionInfo.Current` is `"1.2.0"` — the one and only place this string is defined; every
  other reference (tests, the dialog's title) reads it from there, never repeats the literal.
- `PendingChangelogVersion` is `null` for every Jogador, and for a GM whose `LastSeenAppVersion`
  already equals `AppVersionInfo.Current` — never assume "GM" alone is enough to show it.

---

### Task 1: Domain + Infrastructure — `AppVersionInfo`, `ApplicationUser.LastSeenAppVersion`, migration

**Files:**
- Create: `src/RuinaRPG.Domain/AppVersionInfo.cs`
- Modify: `src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs`
- Create: EF Core migration (via `dotnet ef migrations add`, see Step 3 below)

**Interfaces:**
- Produces: `RuinaRPG.Domain.AppVersionInfo.Current` (`const string`, value `"1.2.0"`),
  `ApplicationUser.LastSeenAppVersion` (`string?`, mutable property). Used by Task 2
  (`AuthController`).

This task has no tests of its own — it's a constant, a property, and a migration. Task 2's
integration tests exercise the property through `AuthController`.

- [ ] **Step 1: Create `AppVersionInfo`**

Create `src/RuinaRPG.Domain/AppVersionInfo.cs`:

```csharp
namespace RuinaRPG.Domain;

/// <summary>
/// Single source of truth for the app's current version — compared against
/// ApplicationUser.LastSeenAppVersion by AuthController.Me() to decide whether to show the
/// changelog popup. Bump this (and ChangelogDialog.razor's hardcoded text) on every release that
/// should re-surface the popup to GMs who already dismissed an older version.
/// </summary>
public static class AppVersionInfo
{
    public const string Current = "1.2.0";
}
```

- [ ] **Step 2: Add `LastSeenAppVersion` to `ApplicationUser`**

In `src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs`, find:

```csharp
    // Granted via `make grant-rules-auditor EMAIL=...` (Program.cs's --grant-rules-auditor arg) —
    // checked directly against this column on every request (AuthController.Me, and the new
    // Traits/RulebookDocuments admin endpoints), never baked into the JWT, so a grant/revoke takes
    // effect on the very next request instead of waiting for the holder to log in again.
    public bool IsRulesAuditor { get; set; }
```

Replace with:

```csharp
    // Granted via `make grant-rules-auditor EMAIL=...` (Program.cs's --grant-rules-auditor arg) —
    // checked directly against this column on every request (AuthController.Me, and the new
    // Traits/RulebookDocuments admin endpoints), never baked into the JWT, so a grant/revoke takes
    // effect on the very next request instead of waiting for the holder to log in again.
    public bool IsRulesAuditor { get; set; }

    /// <summary>
    /// The last AppVersionInfo.Current value this user has dismissed the changelog popup for —
    /// null means never dismissed any version. Compared fresh on every Me() call, same pattern as
    /// IsRulesAuditor above. See AuthController.Me/.DismissChangelog.
    /// </summary>
    public string? LastSeenAppVersion { get; set; }
```

- [ ] **Step 3: Generate and apply the migration**

Run:

```bash
dotnet ef migrations add AddLastSeenAppVersion --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Expected: a new migration pair (`<timestamp>_AddLastSeenAppVersion.cs` and `.Designer.cs`) is
created under `src/RuinaRPG.Infrastructure/Persistence/Migrations/`, adding a nullable
`LastSeenAppVersion` text column to `AspNetUsers`, and `RuinaRpgDbContextModelSnapshot.cs` updates
to match. Open the generated migration file and confirm it only touches `AspNetUsers` — if it
tries to touch anything else, the model has drifted from a prior uncommitted change; stop and
investigate rather than proceeding.

- [ ] **Step 4: Clean-rebuild**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/AppVersionInfo.cs src/RuinaRPG.Infrastructure/Identity/ApplicationUser.cs src/RuinaRPG.Infrastructure/Persistence/Migrations/
git commit -m "feat: AppVersionInfo + ApplicationUser.LastSeenAppVersion (schema)"
```

---

### Task 2: API — `PendingChangelogVersion` on `Me()` + `dismiss-changelog` endpoint

**Files:**
- Modify: `src/RuinaRPG.Contracts/Auth/MeResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/AuthController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerMeTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Domain.AppVersionInfo.Current`, `ApplicationUser.LastSeenAppVersion` (Task 1).
- Produces: `MeResponse.PendingChangelogVersion` (`string?`), `POST api/auth/dismiss-changelog`
  (204, no body). Used by Task 3 (`Painel.razor`).

- [ ] **Step 1: Write the failing tests**

In `tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerMeTests.cs`, add these usings at
the top of the file (the file currently has `System.Net`, `System.Net.Http.Headers`,
`System.Net.Http.Json`, `FluentAssertions`, `RuinaRPG.Contracts.Auth` — add the two below):

```csharp
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Domain;
```

Then add these methods inside the `AuthControllerMeTests` class, right after
`Me_reports_IsRulesAuditor_false_by_default`:

```csharp
    [Fact]
    public async Task Me_reports_PendingChangelogVersion_for_a_GM_who_never_dismissed_anything()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ChangelogGm1", "changeloggm1@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken));

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.PendingChangelogVersion.Should().Be(AppVersionInfo.Current);
    }

    [Fact]
    public async Task Me_reports_no_PendingChangelogVersion_for_a_Jogador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("ChangelogGm2", "changeloggm2@teste.com");
        var (_, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "ChangelogPlayer2", "changelogplayer2@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", playerToken));

        var body = await response.Content.ReadFromJsonAsync<MeResponse>();
        body!.PendingChangelogVersion.Should().BeNull();
    }

    [Fact]
    public async Task DismissChangelog_makes_PendingChangelogVersion_null_for_that_user_only()
    {
        var gmToken1 = await RegisterGmAndGetTokenAsync("ChangelogGm3", "changeloggm3@teste.com");
        var gmToken2 = await RegisterGmAndGetTokenAsync("ChangelogGm4", "changeloggm4@teste.com");

        var dismissResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/auth/dismiss-changelog", gmToken1));
        dismissResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var meAfterDismiss = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken1));
        (await meAfterDismiss.Content.ReadFromJsonAsync<MeResponse>())!.PendingChangelogVersion.Should().BeNull();

        // The second GM never called dismiss — still pending. Confirms the dismissal is per-user,
        // not a global flag.
        var meOther = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", gmToken2));
        (await meOther.Content.ReadFromJsonAsync<MeResponse>())!.PendingChangelogVersion.Should().Be(AppVersionInfo.Current);
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens!.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Me_reports_PendingChangelogVersion|FullyQualifiedName~Me_reports_no_PendingChangelogVersion|FullyQualifiedName~DismissChangelog"`
Expected: FAIL to compile — `MeResponse.PendingChangelogVersion` and the endpoint don't exist yet.

- [ ] **Step 3: Add `PendingChangelogVersion` to `MeResponse`**

Replace the whole file `src/RuinaRPG.Contracts/Auth/MeResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Auth;

public record MeResponse(string Id, string Nickname, string Role, bool IsRulesAuditor, string? PendingChangelogVersion);
```

- [ ] **Step 4: Compute it in `AuthController.Me()` and add the dismiss endpoint**

In `src/RuinaRPG.Api/Controllers/AuthController.cs`, add this using near the top (alongside the
existing ones):

```csharp
using RuinaRPG.Domain;
```

Find:

```csharp
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var isRulesAuditor = userId is not null
            && await db.Users.Where(u => u.Id == Guid.Parse(userId)).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();

        return Ok(new MeResponse(
            userId ?? string.Empty,
            User.FindFirstValue("nickname") ?? string.Empty,
            User.FindFirstValue("role") ?? string.Empty,
            isRulesAuditor));
    }
```

Replace with:

```csharp
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var role = User.FindFirstValue("role") ?? string.Empty;

        bool isRulesAuditor = false;
        string? lastSeenAppVersion = null;
        if (userId is not null)
        {
            var flags = await db.Users.Where(u => u.Id == Guid.Parse(userId))
                .Select(u => new { u.IsRulesAuditor, u.LastSeenAppVersion })
                .SingleOrDefaultAsync();
            isRulesAuditor = flags?.IsRulesAuditor ?? false;
            lastSeenAppVersion = flags?.LastSeenAppVersion;
        }

        // Only a GM who hasn't dismissed the current version sees the popup — see Requisitos/spec
        // for why Jogador never does.
        var pendingChangelogVersion = role == "GM" && lastSeenAppVersion != AppVersionInfo.Current
            ? AppVersionInfo.Current
            : null;

        return Ok(new MeResponse(
            userId ?? string.Empty,
            User.FindFirstValue("nickname") ?? string.Empty,
            role,
            isRulesAuditor,
            pendingChangelogVersion));
    }

    [HttpPost("dismiss-changelog")]
    [Authorize]
    public async Task<IActionResult> DismissChangelog()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (userId is null)
            return Unauthorized();

        var user = await db.Users.FindAsync(Guid.Parse(userId));
        if (user is null)
            return Unauthorized();

        user.LastSeenAppVersion = AppVersionInfo.Current;
        await db.SaveChangesAsync();
        return NoContent();
    }
```

Check the class-level `[Authorize]`/`[AllowAnonymous]` attributes already on `AuthController` and
on `Me()` specifically before adding `[Authorize]` to `DismissChangelog` — if the class is already
`[Authorize]` by default (with `[AllowAnonymous]` only on the public login/register actions), the
explicit `[Authorize]` above is redundant but harmless; if the class has no default, it's required.
Match whatever `Me()` itself does one way or the other for consistency.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~AuthControllerMeTests"`
Expected: PASS (all tests in the file, including the 3 new ones and the pre-existing 5)

- [ ] **Step 6: Clean-rebuild**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Contracts/Auth/MeResponse.cs src/RuinaRPG.Api/Controllers/AuthController.cs tests/RuinaRPG.Tests.Integration/Controllers/AuthControllerMeTests.cs
git commit -m "feat: PendingChangelogVersion on Me() + dismiss-changelog endpoint"
```

---

### Task 3: Client — `ChangelogDialog.razor` + wire into `Painel.razor`

**Files:**
- Create: `src/RuinaRPG.Client/Shared/ChangelogDialog.razor`
- Modify: `src/RuinaRPG.Client/Pages/Painel.razor`

**Interfaces:**
- Consumes: `MeResponse.PendingChangelogVersion` (Task 2), `POST auth/dismiss-changelog` (Task 2).
- Produces: nothing consumed elsewhere — this is the final, user-facing task.

- [ ] **Step 1: Create `ChangelogDialog.razor`**

Create `src/RuinaRPG.Client/Shared/ChangelogDialog.razor`:

```razor
@using MudBlazor

<MudDialog @bind-Visible="_visible" @bind-Visible:after="OnVisibilityChangedAsync" Options="@(new DialogOptions { CloseOnEscapeKey = true })">
    <DialogContent>
        <MudText Typo="Typo.h6">Novidades da Versão @Version</MudText>
        <ul class="mt-2">
            <li>Efeitos de Magias/Habilidades: catálogo com cálculo automático de Custo em PI; ao comprar um Efeito sem o pré-requisito, ele é adicionado automaticamente em vez de bloquear; resumo dos Efeitos ("Dano: 3d6", etc.) agora aparece em toda lista de Magia/Habilidade.</li>
            <li>Afinidades elementais: Elemento/Sub-Elemento agora restritos pela Vocação (Escola de Magia); Eficiência e Dano Elemental calculados automaticamente.</li>
            <li>Cadastro de item direto na ficha: crie um item novo no catálogo sem sair da Ficha de NPC/Criatura.</li>
            <li>Características exclusivas de Criatura: novo catálogo só pra Criaturas.</li>
            <li>Fichas de NPC e Criatura reorganizadas para seguir a mesma ordem de abas/seções da Ficha de Personagem.</li>
            <li>Aviso de "Novo Nível" e contador de Pontos de Perícia gastos agora também em NPC/Criatura.</li>
            <li>Várias correções: mensagens de erro/nível mais legíveis, cor do título no tema claro, sessão expirada redireciona pro login, busca por nome e subcategoria no catálogo de itens, bug em que subir de nível não atualizava os limites de atributos/características.</li>
        </ul>
    </DialogContent>
    <DialogActions>
        <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="@(() => _visible = false)">Fechar</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [Parameter, EditorRequired] public string Version { get; set; } = "";
    [Parameter] public EventCallback OnDismissed { get; set; }

    private bool _visible = true;

    // Fires for every way the dialog can close — the "Fechar" button, Escape, or a backdrop click
    // — not just the button, since any of them means the GM has seen it.
    private async Task OnVisibilityChangedAsync()
    {
        if (!_visible)
            await OnDismissed.InvokeAsync();
    }
}
```

- [ ] **Step 2: Wire it into `Painel.razor`**

In `src/RuinaRPG.Client/Pages/Painel.razor`, find:

```razor
        <MudText Class="mt-3">Bem-vindo(a), @_me.Nickname.</MudText>

        @if (_me.Role == "GM")
        {
```

Replace with:

```razor
        <MudText Class="mt-3">Bem-vindo(a), @_me.Nickname.</MudText>

        @if (_me.PendingChangelogVersion is not null)
        {
            <ChangelogDialog Version="@_me.PendingChangelogVersion" OnDismissed="DismissChangelogAsync" />
        }

        @if (_me.Role == "GM")
        {
```

Then find the `@code` block's `private async Task LogoutAsync()` method and add a new method right
after it:

```csharp
    private async Task DismissChangelogAsync()
    {
        await Http.PostAsync("auth/dismiss-changelog", null);
        _me = _me! with { PendingChangelogVersion = null };
    }
```

`ChangelogDialog` lives in `RuinaRPG.Client.Shared` — check whether `Painel.razor` already has
`@using RuinaRPG.Client.Shared` (the project's `_Imports.razor` already imports it globally for
every page, per `src/RuinaRPG.Client/_Imports.razor` — confirm this before adding a redundant
`@using`, don't add one if it's already implicitly available).

- [ ] **Step 3: Clean-rebuild**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Run the full Unit + Client suites**

Run: `dotnet test tests/RuinaRPG.Tests.Unit && dotnet test tests/RuinaRPG.Tests.Client`
Expected: all pass, unaffected by this task (no test added here — matches the established
precedent of no dedicated page-level bUnit coverage for `Painel.razor`/new dialog components in
this codebase; verified via build + careful diff review instead).

- [ ] **Step 5: Run the Task 2 integration tests once more for full-branch sanity**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~AuthControllerMeTests"`
Expected: PASS (unaffected by this client-only task, confirms nothing regressed)

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Client/Shared/ChangelogDialog.razor src/RuinaRPG.Client/Pages/Painel.razor
git commit -m "feat: GM-only changelog popup on Painel"
```
