# Versão 1.5.0 — Plano A: Ficha de Criatura, Pontos de Ignição e Magias/Habilidades

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship items 1 to 5 of version 1.5.0: creature-only characteristics with an Auditor popup, Pontos de Ignição that follow the level, a PI balance enforced on Magias/Habilidades, and editing of entries built on the sheet.

**Architecture:** The PI arithmetic lives in two small pure Domain classes used by the sheet controllers and the three spell-ability controllers; every balance change is written in the same `SaveChangesAsync` as the change that caused it. The client only removes/adds controls and reloads the PI field after an operation that the server may have adjusted.

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core + Npgsql, Blazor WebAssembly + MudBlazor, xUnit + FluentAssertions, Testcontainers (integration), bUnit (client).

**Spec:** `docs/superpowers/specs/2026-10-13-versao-1-5-0-a-fichas-pi-e-magias-design.md` — read it before any task.

## Global Constraints

- Branch `release/1.5.0`. Commit after every task; push after every task (`git push`).
- Every commit message ends with these two trailer lines (after a blank line):
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`
  `Claude-Session: https://claude.ai/code/session_014VCUnq1XpwXdvwVmL7kvDx`
- Never `git add` the untracked `Docs/harp_notification.mp3`; stage by path, not `git add -A` at the repo root.
- `dotnet build` must end with **0 warnings, 0 errors**. A new warning is a failure.
- **TDD is mandatory** (Técnico R0011): failing test first, then the minimum code.
- Integration tests need a running Docker daemon. They have a known ~4% timeout flake unrelated to this work: a test that fails with a timeout and passes when **rerun alone** (`--filter FullyQualifiedName~ExactTestName`) is the flake, not a regression. A build plus full run needs ~2 GB free disk.
- UI text and `Docs/` are Brazilian Portuguese. UI uses MudBlazor icons, never emojis.
- `Docs/` cross-references use Obsidian `[[wikilink]]` syntax. Do not renumber requirement IDs.
- Do not bump `AppVersionInfo` or touch `ChangelogDialog`: that happens once, at the end of Plan D.
- Match the surrounding code: comment density, naming (Portuguese domain names), positional records.
- Error messages, copied verbatim from the spec:
  - `Esta característica não é do catálogo de criatura.`
  - `Pontos de Ignição insuficientes: custa {gasto} e a ficha tem {saldo}.`
  - `Só Magias e Habilidades montadas na ficha podem ser editadas.`

## Review Focus

1. **Stale PI in the autosaved form.** After the server adjusts Atual (level change, spell add/edit/delete) the page must show and resend the server value. A second autosave with the old value must not erase the adjustment. Pinned in Task 4 (API: a repeated save with the same level does not re-add) and Tasks 5 and 9 (client reloads the field).
2. **XP corrected down and up again.** Level down then up must land on the same Atual, except where the floor at 0 was hit. Pinned in Task 4.
3. **Editing a spell whose cost drops to less than before.** The refund path of the diff rule, and an edit that changes only the name (diff 0) with a zero balance must be accepted. Pinned in Task 8.
4. **Existing tests and existing sheets with zero balance.** Any test or user flow that adds a costed Magia to a sheet with Atual 0 now gets a 400. New blank sheets start with the level grant (Task 4); tests that need more seed the balance (Task 8).
5. **A granted NPC/Criatura edited by its player owner.** The edit endpoint and the balance rule must work for the owner, not only the GM. Pinned in Tasks 7 and 8.

---

## File map

| File | Responsibility | Task |
|---|---|---|
| `src/RuinaRPG.Api/Controllers/CreaturePossessionsController.cs` | `AddTrait` creature-catalog only | 1 |
| `src/RuinaRPG.Client/Shared/CaracteristicaDeCriaturaDialog.razor` (new) | Auditor popup that creates a creature characteristic | 2 |
| `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` | picker source, Auditor button, spell edit/PI reload | 2, 9 |
| `src/RuinaRPG.Domain/CharacterSheets/PontosDeIgnicaoPorNivel.cs` (new) | level delta and floor | 3 |
| `src/RuinaRPG.Domain/SpellsAndAbilities/SaldoDePontosDeIgnicao.cs` (new) | debit/refund rule | 3 |
| `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, `NpcSheetsController.cs`, `CampaignGrantsController.cs` | level delta on save, initial PI | 4 |
| `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`, `FichaDeNpc.razor` | remove Total, widen field, spell edit/PI reload | 5, 9 |
| `src/RuinaRPG.Infrastructure/{CharacterSheets,NpcSheets,CreatureSheets}/*SpellAbility.cs` + migration | `CriadaNaFicha` | 6 |
| `src/RuinaRPG.Contracts/SpellsAndAbilities/UpdateSheetSpellAbilityRequest.cs` (new) | edit request | 7 |
| `src/RuinaRPG.Api/Controllers/{Character,Npc,Creature}SpellAbilitiesController.cs` | flag, PUT, balance | 6, 7, 8 |
| `src/RuinaRPG.Client/Shared/EditarMagiaHabilidadeDialog.razor` (new) | edit popup shared by the three sheets | 9 |

---

### Task 1: `AddTrait` accepts only the creature catalog

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CreaturePossessionsController.cs` (`AddTrait`, around lines 195-239)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CreatureAffectionsAndTraitsControllerTests.cs`

**Interfaces:**
- Produces: `POST api/creature-sheets/{sheetId}/traits` returns 400 `Esta característica não é do catálogo de criatura.` for an Id of `Traits`.

- [ ] **Step 1: Write the failing test**

Add to `CreatureAffectionsAndTraitsControllerTests`:

```csharp
[Fact]
public async Task AddTrait_rejects_a_characteristic_of_the_normal_catalog()
{
    var gmToken = await RegisterGmAndGetTokenAsync("CreatureTraitOnly1", "creaturetraitonly1@teste.com");
    var sheetId = await CreateSheetAsync(gmToken);
    var (positiveTraitId, _) = await GetSeededTraitIdsAsync();

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/traits", gmToken,
        new AddCreatureTraitRequest(positiveTraitId, null)));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    (await response.Content.ReadAsStringAsync()).Should().Contain("Esta característica não é do catálogo de criatura.");
}
```

- [ ] **Step 2: Run it and see it fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~AddTrait_rejects_a_characteristic_of_the_normal_catalog`
Expected: FAIL, status is `Created`.

- [ ] **Step 3: Change `AddTrait`**

Replace the block that resolves `normalTrait`/`exclusiveTrait` and the `CreatureTrait` initializer:

```csharp
        // Só o catálogo exclusivo de criatura (Auditoria de Regras R0005). Um Id do catálogo normal é
        // recusado com mensagem própria; as linhas normais concedidas antes da 1.5.0 continuam valendo
        // em ListTraits/SumExistingCustoAsync/DeleteTrait.
        var exclusiveTrait = await db.Set<CreatureExclusiveTrait>().FirstOrDefaultAsync(t => t.Id == traitId && !t.IsDeleted);
        if (exclusiveTrait is null)
        {
            if (await db.Traits.AnyAsync(t => t.Id == traitId))
                return BadRequest("Esta característica não é do catálogo de criatura.");
            return BadRequest("Trait não encontrado.");
        }

        var trait = ToResolved(exclusiveTrait);
```

and in the `new CreatureTrait { ... }` initializer use `TraitId = null,` and `CreatureExclusiveTraitId = exclusiveTrait.Id,`. Leave `ToResolved(Trait)` in place: `ListTraits` still uses it. Update the XML summary on `CreatureExclusiveTraitsController` only if it now states something false (it does not mention the normal catalog being accepted; leave it).

- [ ] **Step 4: Move the existing creature trait tests to the exclusive catalog**

In the same test file these tests grant normal-catalog traits through the API and now get 400. Rewrite each to create its traits with `CreateExclusiveTraitDirectlyAsync` instead of `GetSeededTraitIdsAsync` / `GetTopCostTraitsAsync` / `GetTraitAsync`:

- `AddTrait_lands_in_the_correct_bucket_with_correct_totals_and_DeleteTrait_removes_it` — one Positiva custo 2 and one Negativa custo −2.
- `AddTrait_rejects_a_Positiva_that_would_push_the_side_total_past_the_budget` and `AddTrait_accepts_Negativas_up_to_twice_the_positive_budget_and_rejects_past_it` — create exclusive traits with the same costs the test used (keep its arithmetic and assertions).
- `AddTrait_rejects_a_RequerEspecificacao_trait_added_without_an_Especificacao_and_persists_it_when_provided` — an exclusive Negativa custo −1 with `requerEspecificacao: true`.
- `AddTrait_sums_a_normal_and_a_creature_exclusive_characteristic_into_the_same_Positivas_budget` — keep the intent (legacy rows still count) by inserting the normal row directly:

```csharp
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.CreatureTraits.Add(new RuinaRPG.Infrastructure.CreatureSheets.CreatureTrait
            {
                Id = Guid.NewGuid(), CreatureSheetId = Guid.Parse(sheetId), TraitId = alfabetizado.Id,
                Polaridade = RuinaRPG.Domain.Enums.Polaridade.Positiva,
            });
            await db.SaveChangesAsync();
        }
```

  in place of the first `POST`. Then also assert that the legacy row can still be removed with `DELETE /api/creature-sheets/{sheetId}/traits/{id}` → `NoContent`.

Delete helpers that end up unused (the build must have 0 warnings).

- [ ] **Step 5: Run the file**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~CreatureAffectionsAndTraitsControllerTests`
Expected: all PASS. Also run `--filter FullyQualifiedName~CampaignGrantsControllerTests` and `--filter FullyQualifiedName~CreatureExclusiveTraits` (they may grant creature traits); fix the same way if any fails.

- [ ] **Step 6: Commit**

```bash
git add -A tests/RuinaRPG.Tests.Integration src/RuinaRPG.Api
git commit -m "feat(api): Ficha de Criatura só concede características do catálogo de criatura"
```

---

### Task 2: Auditor popup and creature-only picker on the sheet

**Files:**
- Create: `src/RuinaRPG.Client/Shared/CaracteristicaDeCriaturaDialog.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` (section "Características" at ~117-122, `SearchTraitsAsync` at ~668-690, `OnInitializedAsync`)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Criaturas.md` (5.d), `Docs/Requisitos/Requisitos - Auditoria de Regras.md` (R0005)
- Test: `tests/RuinaRPG.Tests.Client/Shared/CaracteristicaDeCriaturaDialogTests.cs` (new), `tests/RuinaRPG.Tests.Client/Pages/FichaDeCriaturaCaracteristicasTests.cs` (new)

**Interfaces:**
- Consumes: `POST creature-exclusive-traits` with `CreateCreatureExclusiveTraitRequest(Nome, Descricao, Custo, Polaridade, RequerEspecificacao)` → `CreatureExclusiveTraitResponse(Id, Nome, Descricao, Custo, Polaridade, RequerEspecificacao)`; `GET auth/me` → `MeResponse.IsRulesAuditor`; `EntityPicker.SelectExternallyAsync(PickerOption)`.
- Produces: dialog closes with `DialogResult.Ok(CreatureExclusiveTraitResponse)`.

- [ ] **Step 1: Write the failing dialog tests**

Create `CaracteristicaDeCriaturaDialogTests.cs`, modelled on `tests/RuinaRPG.Tests.Client/Shared/FixedItemDialogTests.cs` (same `Render<MudDialogProvider>()` + `IDialogService.ShowAsync` pattern, same `FakeHttpMessageHandler`):

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Rules;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CaracteristicaDeCriaturaDialogTests : MudBunitContext
{
    private readonly List<string> _requests = new();
    private CreateCreatureExclusiveTraitRequest? _corpo;

    private void RegisterHttp(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            if (request.Method == HttpMethod.Post)
                _corpo = request.Content!.ReadFromJsonAsync<CreateCreatureExclusiveTraitRequest>().GetAwaiter().GetResult();
            return respond(request);
        }));

    private async Task<(IRenderedComponent<MudDialogProvider> Cut, IDialogReference Dialog)> AbrirAsync()
    {
        var cut = Render<MudDialogProvider>();
        var dialogs = Services.GetRequiredService<IDialogService>();
        IDialogReference? reference = null;
        await cut.InvokeAsync(async () => reference = await dialogs.ShowAsync<CaracteristicaDeCriaturaDialog>("Nova característica de criatura"));
        return (cut, reference!);
    }

    [Fact]
    public async Task Salvar_posts_the_form_and_closes_with_the_created_characteristic()
    {
        var criada = new CreatureExclusiveTraitResponse("ct-1", "Regeneração Bestial", "Recupera Vitalidade.", 3, "Positiva", false);
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(criada) });
        var (cut, dialog) = await AbrirAsync();

        cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Nome").Find("input").Change("Regeneração Bestial");
        cut.FindComponents<MudNumericField<int>>().Single(c => c.Instance.Label == "Custo").Find("input").Change("3");
        cut.Find("button:contains('Salvar')").Click();
        var resultado = await dialog.Result;

        _requests.Should().Contain("POST /api/creature-exclusive-traits");
        _corpo!.Nome.Should().Be("Regeneração Bestial");
        _corpo.Custo.Should().Be(3);
        _corpo.Polaridade.Should().Be("Positiva");
        resultado!.Canceled.Should().BeFalse();
        resultado.Data.Should().BeEquivalentTo(criada);
    }

    [Fact]
    public async Task A_refused_save_shows_the_api_message_and_keeps_the_dialog_open()
    {
        RegisterHttp(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Já existe uma característica com esse Nome, Custo e Polaridade.") });
        var (cut, _) = await AbrirAsync();

        cut.Find("button:contains('Salvar')").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Já existe uma característica com esse Nome, Custo e Polaridade."));
        cut.FindAll("button:contains('Salvar')").Should().NotBeEmpty();
    }
}
```

If `FakeHttpMessageHandler.CreateClient` builds paths without the `/api` prefix, match whatever `FixedItemDialogTests` asserts for its own route.

- [ ] **Step 2: Run and see them fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~CaracteristicaDeCriaturaDialogTests`
Expected: compile error, `CaracteristicaDeCriaturaDialog` does not exist.

- [ ] **Step 3: Create the dialog**

`src/RuinaRPG.Client/Shared/CaracteristicaDeCriaturaDialog.razor`:

```razor
@using MudBlazor
@using RuinaRPG.Contracts.Rules
@inject HttpClient Http
@*
    Atalho do Auditor de Regras na Ficha de Criatura (Auditoria de Regras R0005): cadastra uma
    característica no catálogo exclusivo de criatura sem sair da ficha. Mesmos campos do formulário
    de AuditoriaCaracteristicasDeCriatura. Fecha com a CreatureExclusiveTraitResponse criada.
*@
<MudDialog>
    <DialogContent>
        <DismissibleAlert @bind-Message="_errorMessage" Class="mb-3" />
        <MudTextField T="string" @bind-Value="_form.Nome" Label="Nome" />
        <MudSelect T="string" @bind-Value="_form.Polaridade" Label="Polaridade">
            <MudSelectItem Value="@("Positiva")">Positiva</MudSelectItem>
            <MudSelectItem Value="@("Negativa")">Negativa</MudSelectItem>
        </MudSelect>
        <MudNumericField T="int" @bind-Value="_form.Custo" Label="Custo" />
        <div class="d-flex align-center" style="gap:4px">
            <MudCheckBox T="bool" @bind-Value="_form.RequerEspecificacao" Label="Exige Especificação?" />
            <InfoPopup Title="O que é Especificação">
                Marque quando a característica pede que o jogador escolha ou nomeie algo ao recebê-la. Nesse caso, a ficha passa a exigir o campo de texto <b>Especificação</b> para essa característica.
            </InfoPopup>
        </div>
        <MudTextField T="string" @bind-Value="_form.Descricao" Label="Descrição" Lines="3" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => MudDialog.Cancel())">Cancelar</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled" Disabled="_salvando" OnClick="SalvarAsync">Salvar</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    private readonly FormModel _form = new();
    private string? _errorMessage;
    private bool _salvando;

    private async Task SalvarAsync()
    {
        _salvando = true;
        try
        {
            var response = await Http.PostAsJsonAsync("creature-exclusive-traits",
                new CreateCreatureExclusiveTraitRequest(_form.Nome, _form.Descricao, _form.Custo, _form.Polaridade, _form.RequerEspecificacao));
            if (!response.IsSuccessStatusCode)
            {
                _errorMessage = await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a característica.";
                return;
            }

            MudDialog.Close(DialogResult.Ok(await response.Content.ReadFromJsonAsync<CreatureExclusiveTraitResponse>()));
        }
        finally
        {
            _salvando = false;
        }
    }

    private class FormModel
    {
        public string Nome { get; set; } = "";
        public string Descricao { get; set; } = "";
        public int Custo { get; set; }
        public string Polaridade { get; set; } = "Positiva";
        public bool RequerEspecificacao { get; set; }
    }
}
```

`ReadErrorMessageAsync` is the existing `HttpContent` extension the sheet pages already use; add its `@using` if the build asks for it (check the top of `FichaDeCriatura.razor`).

- [ ] **Step 4: Run the dialog tests**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~CaracteristicaDeCriaturaDialogTests`
Expected: PASS.

- [ ] **Step 5: Write the failing page tests**

Create `tests/RuinaRPG.Tests.Client/Pages/FichaDeCriaturaCaracteristicasTests.cs`, copying the HTTP stubbing of `tests/RuinaRPG.Tests.Client/Pages/FichaSubAtributosTests.cs` (`FakeAuthStateProvider`, `SingleObjectSuffixes`, the default `Json(new { })` / empty-list fallbacks) and adding one switch for `auth/me`:

```csharp
    private void RegisterHttp(bool auditor)
    {
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            _requests.Add($"{request.Method} {path}");
            if (request.Method != HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK);
            if (path.EndsWith("/auth/me"))
                return Json(new MeResponse("u-1", "Mestre", "GM", auditor, null, false));
            if (path.EndsWith("/level-up-notice"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (Regex.IsMatch(path, "-sheets/[^/]+$"))
                return Json(new { });
            return SingleObjectSuffixes.Any(path.EndsWith) ? Json(new { }) : Json(new List<object>());
        }));
        Services.AddScoped<AuthenticationStateProvider>(_ => new FakeAuthStateProvider());
    }

    [Fact]
    public async Task The_new_characteristic_button_only_renders_for_the_rules_auditor()
    {
        RegisterHttp(auditor: true);
        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        cut.FindAll("button:contains('Nova característica de criatura')").Should().HaveCount(1);
    }

    [Fact]
    public async Task The_new_characteristic_button_is_hidden_from_a_gm_who_is_not_the_auditor()
    {
        RegisterHttp(auditor: false);
        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        cut.FindAll("button:contains('Nova característica de criatura')").Should().BeEmpty();
    }

    [Fact]
    public async Task The_characteristic_picker_searches_only_the_creature_catalog()
    {
        RegisterHttp(auditor: false);
        var cut = Render<FichaDeCriatura>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        _requests.Clear();

        var picker = cut.FindComponents<EntityPicker>().Single(p => p.Instance.Placeholder == "Buscar característica...");
        await cut.InvokeAsync(() => picker.Instance.SearchAsyncForTests("regen"));

        _requests.Should().Contain(r => r.Contains("/creature-exclusive-traits"));
        _requests.Should().NotContain(r => r.EndsWith("/traits") && !r.Contains("-sheets/"));
    }
```

`SearchAsyncForTests` is `internal`; the client test project already calls it in `EntityPickerTests` (InternalsVisibleTo is in place). If the "Características" section is on a tab other than the first, click that tab first the way `FichaSubAtributosTests` clicks "Combate".

- [ ] **Step 6: Run and see them fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~FichaDeCriaturaCaracteristicasTests`
Expected: the auditor test fails (no button) and the picker test fails (it still requests `traits`).

- [ ] **Step 7: Change `FichaDeCriatura.razor`**

Add `@inject IDialogService Dialogs` with the other injects, and `@using RuinaRPG.Contracts.Auth` if absent. In the "Características" section:

```razor
            <EditForm Model="_traitForm" OnValidSubmit="AddTraitAsync">
                <div class="d-flex align-center" style="gap:8px">
                    <div style="flex:1">
                        <EntityPicker @ref="_traitPicker" @bind-Value="_traitForm.TraitId" SearchItems="SearchTraitsAsync" Placeholder="Buscar característica..." />
                    </div>
                    @if (_isRulesAuditor)
                    {
                        <MudButton Variant="Variant.Outlined" Color="Color.Primary" StartIcon="@Icons.Material.Filled.Add" OnClick="NovaCaracteristicaDeCriaturaAsync">Nova característica de criatura</MudButton>
                    }
                </div>
```

(the two following lines of the form, Especificação and the submit button, stay as they are). In `@code`:

```csharp
    private EntityPicker? _traitPicker;
    private bool _isRulesAuditor;

    // Só o catálogo exclusivo de criatura (Ficha de Criaturas 5.d).
    private async Task<List<PickerOption>> SearchTraitsAsync(string query)
    {
        var response = await Http.GetAsync($"creature-exclusive-traits?nome={Uri.EscapeDataString(query ?? "")}");
        if (!response.IsSuccessStatusCode)
            return new();
        var traits = await response.Content.ReadFromJsonAsync<List<CreatureExclusiveTraitResponse>>() ?? new();
        return traits.Select(t => new PickerOption(t.Id, $"{t.Nome} ({t.Custo}, {t.Polaridade})")).ToList();
    }

    private async Task LoadIsRulesAuditorAsync()
    {
        var response = await Http.GetAsync("auth/me");
        if (response.IsSuccessStatusCode)
            _isRulesAuditor = (await response.Content.ReadFromJsonAsync<MeResponse>())?.IsRulesAuditor ?? false;
    }

    private async Task NovaCaracteristicaDeCriaturaAsync()
    {
        var dialog = await Dialogs.ShowAsync<CaracteristicaDeCriaturaDialog>("Nova característica de criatura",
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        if ((await dialog.Result) is { Canceled: false, Data: CreatureExclusiveTraitResponse criada } && _traitPicker is not null)
            await _traitPicker.SelectExternallyAsync(new PickerOption(criada.Id, $"{criada.Nome} ({criada.Custo}, {criada.Polaridade})"));
    }
```

Replace the old two-catalog `SearchTraitsAsync` entirely, keep the same query-encoding the old method used if it differs, and add `LoadIsRulesAuditorAsync()` to the `Task.WhenAll(...)` of the page's initial load (line ~719). Remove usings/variables that become unused.

- [ ] **Step 8: Run client tests for the page**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~FichaDeCriatura|FullyQualifiedName~FichaSubAtributos|FullyQualifiedName~FichaArsenal|FullyQualifiedName~FichaMaestrias"`
Expected: PASS (the other sheet-page tests prove the extra `auth/me` call does not break their stubs; their fallback returns `{}` for it).

- [ ] **Step 9: Update the docs**

- `Docs/Requisitos/Requisitos - Ficha de Criaturas.md`, replace the 5.d bullet with:

  `**5.d Características**: mesmo comportamento do Personagem, com uma diferença — o seletor de característica lista **apenas** o catálogo exclusivo de criatura de "[[Requisitos - Auditoria de Regras]]" R0005; o catálogo normal não é oferecido nem aceito. Uma característica do catálogo normal concedida antes dessa regra continua na ficha, conta no mesmo orçamento de pontos (Positivas/Negativas) e pode ser removida. Quando quem está na ficha é o Auditor de Regras, um botão "Nova característica de criatura" ao lado do seletor abre um popup com os campos de R0005 e cadastra a característica no catálogo sem sair da ficha; ao salvar, ela fica selecionada no seletor.`

- `Docs/Requisitos/Requisitos - Auditoria de Regras.md` R0005: replace `Só a Ficha de Criatura pode conceder uma característica desse catálogo (junto com as de R0003, no mesmo seletor)` with `Só a Ficha de Criatura pode conceder uma característica desse catálogo, e ela só concede as deste catálogo (ver "[[Requisitos - Ficha de Criaturas]]" 5.d, que também descreve o atalho de cadastro do Auditor na própria ficha)`.

- [ ] **Step 10: Build and commit**

Run: `dotnet build` → 0 warnings, 0 errors.

```bash
git add -A src/RuinaRPG.Client tests/RuinaRPG.Tests.Client Docs/Requisitos
git commit -m "feat(client): Ficha de Criatura lista só características de criatura e Auditor cadastra pela ficha"
```

---

### Task 3: Domain rules for Pontos de Ignição

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/PontosDeIgnicaoPorNivel.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/SaldoDePontosDeIgnicao.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/PontosDeIgnicaoPorNivelTests.cs`, `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/SaldoDePontosDeIgnicaoTests.cs`

**Interfaces:**
- Produces:
  - `static int PontosDeIgnicaoPorNivel.Delta(int nivelAntigo, int nivelNovo, ProgressaoDeNivel tabela)`
  - `static int PontosDeIgnicaoPorNivel.Aplicar(int atual, int delta)` → `Math.Max(0, atual + delta)`
  - `static int PontosDeIgnicaoPorNivel.Inicial(int nivel, ProgressaoDeNivel tabela)`
  - `static string? SaldoDePontosDeIgnicao.ErroAoDebitar(int saldo, int gasto)` → null when it fits
  - `static int SaldoDePontosDeIgnicao.Debitar(int saldo, int gasto)` → `saldo - gasto` (gasto may be negative = refund)
  - `static bool SaldoDePontosDeIgnicao.Consome(SpellAbilityTipo tipo)` → Magia or Habilidade

- [ ] **Step 1: Write the failing tests**

`PontosDeIgnicaoPorNivelTests.cs` (same table helper as `PontosDeIgnicaoCalculatorTests`):

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using static RuinaRPG.Tests.Unit.Rules.TabelaDeNiveisDeTeste;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class PontosDeIgnicaoPorNivelTests
{
    private static readonly ProgressaoDeNivel Niveis = Criar(
        Nivel(1, (ChavesDeNivel.PontosDeIgnicao, 10)),
        Nivel(2, (ChavesDeNivel.PontosDeIgnicao, 4)),
        Nivel(3),
        Nivel(4, (ChavesDeNivel.PontosDeIgnicao, 8)));

    [Fact]
    public void Delta_is_zero_when_the_level_does_not_change() =>
        PontosDeIgnicaoPorNivel.Delta(2, 2, Niveis).Should().Be(0);

    [Fact]
    public void Delta_going_up_one_level_is_that_levels_grant() =>
        PontosDeIgnicaoPorNivel.Delta(1, 2, Niveis).Should().Be(4);

    [Fact]
    public void Delta_going_up_several_levels_sums_them_and_an_empty_cell_counts_zero() =>
        PontosDeIgnicaoPorNivel.Delta(1, 4, Niveis).Should().Be(12); // 4 + 0 + 8

    [Fact]
    public void Delta_going_down_is_the_negative_of_going_up() =>
        PontosDeIgnicaoPorNivel.Delta(4, 1, Niveis).Should().Be(-12);

    [Fact]
    public void Aplicar_adds_the_delta() =>
        PontosDeIgnicaoPorNivel.Aplicar(atual: 5, delta: 4).Should().Be(9);

    [Fact]
    public void Aplicar_never_goes_below_zero() =>
        PontosDeIgnicaoPorNivel.Aplicar(atual: 5, delta: -12).Should().Be(0);

    [Fact]
    public void Inicial_is_everything_granted_up_to_the_starting_level() =>
        PontosDeIgnicaoPorNivel.Inicial(2, Niveis).Should().Be(14);
}
```

`SaldoDePontosDeIgnicaoTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class SaldoDePontosDeIgnicaoTests
{
    [Fact]
    public void A_cost_within_the_balance_has_no_error() =>
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 20, gasto: 14).Should().BeNull();

    [Fact]
    public void A_cost_equal_to_the_balance_has_no_error() =>
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 14, gasto: 14).Should().BeNull();

    [Fact]
    public void A_cost_above_the_balance_reports_both_numbers() =>
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 13, gasto: 14)
            .Should().Be("Pontos de Ignição insuficientes: custa 14 e a ficha tem 13.");

    [Fact]
    public void A_zero_or_negative_cost_never_errors_even_with_an_empty_balance()
    {
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 0, gasto: 0).Should().BeNull();
        SaldoDePontosDeIgnicao.ErroAoDebitar(saldo: 0, gasto: -5).Should().BeNull();
    }

    [Fact]
    public void Debitar_subtracts_and_a_negative_cost_refunds()
    {
        SaldoDePontosDeIgnicao.Debitar(saldo: 20, gasto: 14).Should().Be(6);
        SaldoDePontosDeIgnicao.Debitar(saldo: 6, gasto: -14).Should().Be(20);
    }

    [Theory]
    [InlineData(SpellAbilityTipo.Magia, true)]
    [InlineData(SpellAbilityTipo.Habilidade, true)]
    [InlineData(SpellAbilityTipo.Passiva, false)]
    [InlineData(SpellAbilityTipo.Racial, false)]
    public void Only_Magia_and_Habilidade_consume_the_balance(SpellAbilityTipo tipo, bool consome) =>
        SaldoDePontosDeIgnicao.Consome(tipo).Should().Be(consome);
}
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~PontosDeIgnicaoPorNivelTests|FullyQualifiedName~SaldoDePontosDeIgnicaoTests"`
Expected: compile errors, types missing.

- [ ] **Step 3: Implement**

`PontosDeIgnicaoPorNivel.cs`:

```csharp
using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Ficha de Personagem 1.b: os Pontos de Ignição Atual acompanham o Nível. Subir soma o que os
/// níveis ganhos concedem na Tabela de Níveis; descer subtrai o mesmo, sem passar de zero.
/// </summary>
public static class PontosDeIgnicaoPorNivel
{
    public static int Delta(int nivelAntigo, int nivelNovo, ProgressaoDeNivel tabela) =>
        tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivelNovo) - tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivelAntigo);

    public static int Aplicar(int atual, int delta) => Math.Max(0, atual + delta);

    /// <summary>O saldo com que uma ficha nova começa: tudo o que a tabela concede até o nível inicial.</summary>
    public static int Inicial(int nivel, ProgressaoDeNivel tabela) => tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivel);
}
```

`SaldoDePontosDeIgnicao.cs`:

```csharp
namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// Ficha de Personagem 4.b: montar ou adicionar uma Magia/Habilidade consome o Gasto em PI do saldo
/// da ficha, remover devolve, e editar acerta só a diferença. Vale para todos, GM incluído.
/// </summary>
public static class SaldoDePontosDeIgnicao
{
    public static bool Consome(SpellAbilityTipo tipo) => tipo is SpellAbilityTipo.Magia or SpellAbilityTipo.Habilidade;

    public static string? ErroAoDebitar(int saldo, int gasto) =>
        gasto > saldo ? $"Pontos de Ignição insuficientes: custa {gasto} e a ficha tem {saldo}." : null;

    public static int Debitar(int saldo, int gasto) => saldo - gasto;
}
```

Confirm the enum members of `SpellAbilityTipo` (`grep -rn "enum SpellAbilityTipo" -A8 src/RuinaRPG.Domain`); if `Racial` is named differently, use the real name in the test.

- [ ] **Step 4: Run and commit**

Run the same filter → PASS. `dotnet build` → 0 warnings.

```bash
git add src/RuinaRPG.Domain tests/RuinaRPG.Tests.Unit
git commit -m "feat(domain): regras de Pontos de Ignição por nível e de saldo para Magias/Habilidades"
```

---

### Task 4: The level drives Pontos de Ignição Atual (Personagem and NPC)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs` (`Create` ~45, `Update` ~258-281)
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs` (`Create` ~38, `Update` ~132-146, `UpdateNivel` ~284, `UpdateExperienciaAtual` ~306)
- Modify: `src/RuinaRPG.Api/Controllers/CampaignGrantsController.cs` (blank NPC at ~51)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, `NpcSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `PontosDeIgnicaoPorNivel.Delta/Aplicar/Inicial` (Task 3).
- Produces: after `PUT character-sheets/{id}`, `PontosDeIgnicaoAtual = max(0, request.Atual + Δnível + ΔbônusManual)`. NPC: same on `PUT npc-sheets/{id}`, and on `/nivel` and `/experiencia-atual` over the stored value. `request.PontosDeIgnicaoTotal` of the NPC is ignored.

- [ ] **Step 1: Write the failing Personagem tests**

Add to `CharacterSheetsControllerTests` (helpers `CreateOwnedSheetAsync`, `GetSheetAsync`, `ValidUpdate`, `AuthedRequest` already exist there). Add this helper and `using RuinaRPG.Domain.Rules.Niveis; using RuinaRPG.Infrastructure.Rules.Niveis; using Microsoft.Extensions.DependencyInjection;` if missing:

```csharp
    private async Task<int> PiAcumuladoAsync(int nivel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tabela = await scope.ServiceProvider.GetRequiredService<ITabelaDeNiveis>().ObterAsync();
        return tabela.Acumulado(ChavesDeNivel.PontosDeIgnicao, nivel);
    }

    private Task<HttpResponseMessage> PutSheetAsync(string token, string sheetId, UpdateCharacterSheetRequest request) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", token, request));

    [Fact]
    public async Task A_new_sheet_starts_with_the_level_1_Pontos_de_Ignicao()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiNovo");

        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(await PiAcumuladoAsync(1));
    }

    [Fact]
    public async Task Levelling_up_adds_the_gained_levels_Pontos_de_Ignicao_to_Atual()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiSobe");
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoAtual = 7 })).EnsureSuccessStatusCode();

        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoAtual = 7 })).EnsureSuccessStatusCode();

        var sheet = await GetSheetAsync(playerToken, sheetId);
        sheet.Nivel.Should().Be(5);
        sheet.PontosDeIgnicaoAtual.Should().Be(7 + await PiAcumuladoAsync(5) - await PiAcumuladoAsync(1));
    }

    [Fact]
    public async Task Saving_again_at_the_same_level_does_not_add_the_points_twice()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiMesmo");
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoAtual = 7 })).EnsureSuccessStatusCode();
        var depois = (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual;

        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoAtual = depois })).EnsureSuccessStatusCode();

        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(depois);
    }

    [Fact]
    public async Task Losing_levels_subtracts_the_same_points_and_never_goes_below_zero()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("PiDesce");
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoAtual = 0 })).EnsureSuccessStatusCode();
        var noNivel5 = (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual;

        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoAtual = noNivel5 + 3 })).EnsureSuccessStatusCode();
        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(3); // (Δ + 3) − Δ

        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 749, PontosDeIgnicaoAtual = 2 })).EnsureSuccessStatusCode();
        (await PutSheetAsync(playerToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoAtual = 1 })).EnsureSuccessStatusCode();
        (await GetSheetAsync(playerToken, sheetId)).PontosDeIgnicaoAtual.Should().Be(0);
    }

    [Fact]
    public async Task A_manual_bonus_delta_reaches_Atual_as_well_as_the_total()
    {
        var (gmToken, _, sheetId) = await CreateOwnedSheetAsync("PiBonus");
        (await PutSheetAsync(gmToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoAtual = 10, PontosDeIgnicaoBonusManual = 0 })).EnsureSuccessStatusCode();

        (await PutSheetAsync(gmToken, sheetId, ValidUpdate() with { ExperienciaAtual = 0, PontosDeIgnicaoAtual = 10, PontosDeIgnicaoBonusManual = 5 })).EnsureSuccessStatusCode();

        var sheet = await GetSheetAsync(gmToken, sheetId);
        sheet.PontosDeIgnicaoAtual.Should().Be(15);
        sheet.PontosDeIgnicaoBonusManual.Should().Be(5);
        sheet.PontosDeIgnicaoTotal.Should().Be(await PiAcumuladoAsync(1) + 5);
    }
```

The first PUT of the "desce" test starts from Atual `PiAcumulado(1)` at level 1, so `0 + Δ(1→5)`; the assertions only rely on values read back. If the property names in `UpdateCharacterSheetRequest` differ from the ones used in the `with` expressions, use the real names from `src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs`.

- [ ] **Step 2: Run and see them fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Pontos_de_Ignicao|FullyQualifiedName~manual_bonus_delta|FullyQualifiedName~same_level_does_not_add|FullyQualifiedName~Losing_levels"`
Expected: FAIL on the values.

- [ ] **Step 3: Implement in `CharacterSheetsController`**

`Create`, right after `new CharacterSheet { ... }`:

```csharp
        // Ficha nova já nasce com o que a Tabela de Níveis concede até o nível inicial (1.b).
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Inicial(sheet.Nivel, await tabelaDeNiveis.ObterAsync());
```

`Update`: capture the old level before it is overwritten and replace the two PI assignments:

```csharp
        var tabela = await tabelaDeNiveis.ObterAsync();
        var nivel = NivelCalculator.Compute(request.ExperienciaAtual, tabela.ComoXpPorNivel());
        var nivelAntigo = sheet.Nivel;
        sheet.Nivel = nivel;
```

```csharp
        // 1.b: o Atual acompanha o Nível (sobe e desce, piso 0) e também o bônus manual do GM, que
        // continua somando ao total calculado em ToResponseAsync.
        var deltaDePi = PontosDeIgnicaoPorNivel.Delta(nivelAntigo, nivel, tabela)
            + (request.PontosDeIgnicaoBonusManual - sheet.PontosDeIgnicaoBonusManual);
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Aplicar(request.PontosDeIgnicaoAtual, deltaDePi);
        sheet.PontosDeIgnicaoBonusManual = request.PontosDeIgnicaoBonusManual;
```

Keep the existing comment about `PontosDeIgnicaoTotal` being computed. `CharacterSheet.Nivel` must default to 1 for a new entity; check the entity and, if it defaults to 0, use `Math.Max(1, sheet.Nivel)` in `Create`.

- [ ] **Step 4: Run the Personagem tests**

Same filter → PASS. Then the whole file: `--filter FullyQualifiedName~CharacterSheetsControllerTests`. An existing test that asserted `PontosDeIgnicaoAtual` equal to the value sent while also changing level must be updated to the new rule; do not weaken any other assertion.

- [ ] **Step 5: Write the failing NPC tests**

Add to `NpcSheetsControllerTests` (helpers `CreateSheetAsync`, `GetNpcAsync`, `ValidUpdate`, `AuthedRequest` exist; add the same `PiAcumuladoAsync` helper as above):

```csharp
    [Fact]
    public async Task A_new_npc_starts_with_the_level_1_Pontos_de_Ignicao()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiNovo", "npcpinovo@teste.com");
        var id = await CreateSheetAsync(gm);

        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(await PiAcumuladoAsync(1));
    }

    [Fact]
    public async Task Changing_the_npc_level_through_the_nivel_endpoint_moves_Atual()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiNivel", "npcpinivel@teste.com");
        var id = await CreateSheetAsync(gm);
        var inicial = (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual;

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}/nivel", gm, 4))).EnsureSuccessStatusCode();
        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(inicial + await PiAcumuladoAsync(4) - await PiAcumuladoAsync(1));

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}/nivel", gm, 1))).EnsureSuccessStatusCode();
        (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual.Should().Be(inicial);
    }

    [Fact]
    public async Task Changing_the_npc_experience_moves_Atual_with_the_level_it_computes()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiXp", "npcpixp@teste.com");
        var id = await CreateSheetAsync(gm);
        var inicial = (await GetNpcAsync(gm, id)).PontosDeIgnicaoAtual;

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}/experiencia-atual", gm, 749))).EnsureSuccessStatusCode();

        var npc = await GetNpcAsync(gm, id);
        npc.PontosDeIgnicaoAtual.Should().Be(inicial + await PiAcumuladoAsync(npc.Nivel) - await PiAcumuladoAsync(1));
    }

    [Fact]
    public async Task The_full_npc_update_applies_the_level_delta_and_ignores_PontosDeIgnicaoTotal()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiPut", "npcpiput@teste.com");
        var id = await CreateSheetAsync(gm);
        var antes = await GetNpcAsync(gm, id);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{id}", gm,
            ValidUpdate() with { Nivel = 3, PontosDeIgnicaoAtual = 6, PontosDeIgnicaoTotal = 999 }))).EnsureSuccessStatusCode();

        var npc = await GetNpcAsync(gm, id);
        npc.PontosDeIgnicaoAtual.Should().Be(6 + await PiAcumuladoAsync(3) - await PiAcumuladoAsync(antes.Nivel));
        npc.PontosDeIgnicaoTotal.Should().Be(antes.PontosDeIgnicaoTotal);
    }

    [Fact]
    public async Task A_blank_npc_granted_to_a_player_also_starts_with_the_level_1_points()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPiGrant", "npcpigrant@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "NpcPiGrantP", "npcpigrantp@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha PI");
        await AddMemberAsync(gm, campaignId, playerId);
        var id = await GrantBlankNpcAsync(gm, campaignId, playerId);

        (await GetNpcAsync(playerToken, id)).PontosDeIgnicaoAtual.Should().Be(await PiAcumuladoAsync(1));
    }
```

If `ValidUpdate()` in this file needs other fields adjusted for a valid Nível 3 save (e.g. it carries an XP inconsistent with the level), follow what the neighbouring tests do when they change `Nivel`.

- [ ] **Step 6: Run and see them fail, then implement in `NpcSheetsController` and `CampaignGrantsController`**

`Create` (after `new NpcSheet { ... }`):

```csharp
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Inicial(sheet.Nivel, await tabelaDeNiveis.ObterAsync());
```

`Update` — read `var nivelAntigo = sheet.Nivel;` before `sheet.Nivel = request.Nivel;`, obtain the table (reuse the variable if the method already has one), and replace the two PI lines:

```csharp
        // Mesma regra do Personagem (Ficha de Personagem 1.b): o Atual acompanha o Nível. O Total do
        // NPC deixou de ser editável na 1.5.0 — request.PontosDeIgnicaoTotal é aceito mas ignorado.
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Aplicar(request.PontosDeIgnicaoAtual,
            PontosDeIgnicaoPorNivel.Delta(nivelAntigo, request.Nivel, tabela));
```

`UpdateNivel` — before `sheet.Nivel = nivel;`:

```csharp
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Aplicar(sheet.PontosDeIgnicaoAtual,
            PontosDeIgnicaoPorNivel.Delta(sheet.Nivel, nivel, tabela));
```

`UpdateExperienciaAtual` — compute the new level into a local first:

```csharp
        var tabela = await tabelaDeNiveis.ObterAsync();
        var nivel = NivelCalculator.Compute(experienciaAtual, tabela.ComoXpPorNivel());
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Aplicar(sheet.PontosDeIgnicaoAtual,
            PontosDeIgnicaoPorNivel.Delta(sheet.Nivel, nivel, tabela));
        sheet.Nivel = nivel;
```

`CampaignGrantsController`, blank NPC branch (`newSheet = new NpcSheet { Id = ..., GmId = gmId, OwnerId = playerId };`): set `newSheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Inicial(newSheet.Nivel, await tabelaDeNiveis.ObterAsync());`. If that controller does not inject `ITabelaDeNiveis`, add it to the primary constructor (it is registered as scoped). Do **not** touch `DeepCopyNpcAsync` or the Criatura branches.

- [ ] **Step 7: Run NPC and grant tests**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSheetsControllerTests|FullyQualifiedName~CampaignGrantsControllerTests|FullyQualifiedName~NivelLimitsTests"`
Expected: PASS. Fix existing tests that assumed the NPC `PontosDeIgnicaoTotal` round-trips, by asserting it is unchanged instead.

- [ ] **Step 8: Build and commit**

`dotnet build` → 0 warnings.

```bash
git add -A src/RuinaRPG.Api tests/RuinaRPG.Tests.Integration
git commit -m "feat(api): Pontos de Ignição Atual acompanha o nível em Personagem e NPC"
```

---

### Task 5: Sheets without the PI Total, wider bonus field, docs

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (~158-169, form model ~1878-1883, load ~1108-1110)
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor` (~164-165, validation ~1031, form model ~1692-1695)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (1.b), `Docs/Requisitos/Requisitos - Ficha de NPCs.md`
- Test: `tests/RuinaRPG.Tests.Client/Pages/FichaPontosDeIgnicaoTests.cs` (new)

**Interfaces:**
- Consumes: Task 4's API behavior. No new API.

- [ ] **Step 1: Write the failing tests**

Create `FichaPontosDeIgnicaoTests.cs` with the same stubbing as `FichaSubAtributosTests` (copy `FakeAuthStateProvider`, `SingleObjectSuffixes`, `Json`, `RegisterHttp` returning `Json(new { PontosDeIgnicaoAtual = 12, PontosDeIgnicaoTotal = 40 })` for the sheet GET):

```csharp
    [Fact]
    public async Task Personagem_no_longer_shows_the_Pontos_de_Ignicao_Total()
    {
        RegisterHttp();
        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);

        cut.Markup.Should().NotContain("Pontos de Ignição Total");
        cut.FindComponents<MudNumericField<int?>>().Should().Contain(c => c.Instance.Label == "Pontos de Ignição Atual");
    }

    [Fact]
    public async Task Npc_no_longer_has_the_Pontos_de_Ignicao_Total_field()
    {
        RegisterHttp();
        var cut = Render<FichaDeNpc>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);

        cut.FindComponents<MudNumericField<int?>>().Should().NotContain(c => c.Instance.Label == "Pontos de Ignição Total");
        cut.FindComponents<MudNumericField<int?>>().Should().Contain(c => c.Instance.Label == "Pontos de Ignição Atual");
    }

    [Fact]
    public async Task The_manual_bonus_field_is_wide_enough_for_its_label_and_matches_the_xp_field()
    {
        RegisterHttp();
        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);

        var bonus = cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Adicionar/subtrair PI (bônus manual)");
        var xp = cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Adicionar/subtrair XP");
        bonus.Instance.Style.Should().Be("max-width:22rem");
        xp.Instance.Style.Should().Be("max-width:22rem");
    }
```

- [ ] **Step 2: Run and see them fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~FichaPontosDeIgnicaoTests` → 3 FAIL.

- [ ] **Step 3: Change `FichaDePersonagem.razor`**

- Delete the line `<MudText>Pontos de Ignição Total: @_form.PontosDeIgnicaoTotal</MudText>`.
- Both delta fields get `Style="max-width:22rem"` (XP was `14rem`, PI was `16rem`). The label "Adicionar/subtrair PI (bônus manual)" is ~36 characters; 22rem fits it with the field's padding at the default font.
- Keep `PontosDeIgnicaoTotal` in the form model only if something else still reads it; if nothing does, remove the property, its load line and its comment.
- `ApplyPiDeltaAsync` needs no change: it bumps `PontosDeIgnicaoBonusManual` and saves, the server adds the delta to Atual, and `SaveIfValidAsync` reloads the sheet. **Verify** that `SaveIfValidAsync` ends with `LoadSheetAsync()` on the success path and that `LoadSheetAsync` assigns `_form.PontosDeIgnicaoAtual` from the response (line ~1108). If the reload is skipped on any success path, add it; this is Review Focus 1.
- Update the XML comment on `ApplyPiDeltaAsync` to say the delta also reaches Atual.

- [ ] **Step 4: Change `FichaDeNpc.razor`**

- Delete the `MudNumericField` "Pontos de Ignição Total".
- In the form model drop `[Required(ErrorMessage = "Informe os Pontos de Ignição Total.")]` and make the property a plain `public int PontosDeIgnicaoTotal { get; set; }` that only carries the loaded value back; remove it from the null-check at ~1031 and pass `_form.PontosDeIgnicaoTotal` (no `!.Value`) at ~1042.
- `SaveNivelAsync` and `SaveExperienciaAtualAsync` already call `LoadSheetAsync()`; confirm `LoadSheetAsync` assigns `PontosDeIgnicaoAtual` (line ~1010).

- [ ] **Step 5: Run the client tests**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~Ficha"` → PASS.

- [ ] **Step 6: Update the docs**

`Docs/Requisitos/Requisitos - Ficha de Personagem.md`, replace the *Pontos de Ignição (PI)* bullet of 1.b with:

`- *Pontos de Ignição (PI)*: recurso usado na compra de efeitos de magias e habilidades (ver "[[GRAUS & CÍRCULOS]]"). A ficha exibe apenas o valor **atual**, editável diretamente; ele é o saldo que as Magias e Habilidades consomem (ver 4.b). O atual acompanha o *Nível*: ao subir, soma os PI que os níveis ganhos concedem na "[[Tabela de Níveis]]"; ao descer, subtrai os mesmos pontos, sem ficar abaixo de **0**. Uma ficha nova começa com os PI que a tabela concede até o nível inicial. O total acumulado por nível não é repetido aqui — aparece em "Progressão do nível". Um campo dedicado permite ao GM adicionar ou subtrair um bônus manual (mesmo padrão do campo de XP em "Experiência atual", acima); o valor aplicado soma ao total e também ao atual. Quando NULL, o *atual* e o bônus manual assumem **0**.`

`Docs/Requisitos/Requisitos - Ficha de NPCs.md`: the file is a diff against the Personagem doc. Find where it describes the "Nível e Progressão" block (search for "Experiência" / "Nível") and add, in that item, the sentence: `Pontos de Ignição seguem 1.b do Personagem: só o valor atual é exibido, e ele acompanha o Nível (inclusive quando o GM edita o Nível diretamente). O NPC não tem bônus manual.` If no item covers that block, add it as a new requirement continuing the file's `**R00NN**` sequence, titled `Os Pontos de Ignição do NPC acompanham o Nível.`

- [ ] **Step 7: Build and commit**

`dotnet build` → 0 warnings.

```bash
git add -A src/RuinaRPG.Client tests/RuinaRPG.Tests.Client Docs/Requisitos
git commit -m "feat(client): fichas sem o Total de Pontos de Ignição e campo de bônus manual mais largo"
```

---

### Task 6: `CriadaNaFicha` on the three sheet spell-ability tables

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSpellAbility.cs`, `NpcSheets/NpcSpellAbility.cs`, `CreatureSheets/CreatureSpellAbility.cs`
- Create: migration `AddCriadaNaFichaToSheetSpellAbilities`
- Modify: the three `*SpellAbilitiesController.cs` (`Add`, `ToResponseAsync`), `CampaignGrantsController.cs` (the spell-ability copies in `DeepCopyNpcAsync` / `DeepCopyCreatureAsync`)
- Modify: `src/RuinaRPG.Contracts/{CharacterSheets/CharacterSpellAbilityResponse,NpcSheets/NpcSpellAbilityResponse,CreatureSheets/CreatureSpellAbilityResponse}.cs`
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`
- Test: the three `*SpellAbilitiesControllerTests.cs`, `tests/RuinaRPG.Tests.Integration/Persistence/SpellAbilityCriadaNaFichaMigrationTests.cs` (new)

**Interfaces:**
- Produces: `bool CriadaNaFicha` on the three entities and, as the **last** positional parameter with default `false`, on the three response records:
  `..., List<string>? RequisitosPendentes = null, bool CriadaNaFicha = false)`.

- [ ] **Step 1: Write the failing tests**

In `CharacterSpellAbilitiesControllerTests` (helpers `BolaDeFogoFromScratch`, `SetUpSheetAsync`, `GetBankAsync` exist):

```csharp
    [Fact]
    public async Task An_entry_built_from_scratch_is_flagged_CriadaNaFicha_and_one_from_the_bank_is_not()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasFlag1", "magiasflag1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasFlagP1", "magiasflagp1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));
        var bankId = (await GetBankAsync(gmToken)).Single(e => e.Nome == "Bola de Fogo").Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCharacterSpellAbilityRequest(bankId, null, null, null, null, null)));

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken)))
            .Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>();
        list!.Should().HaveCount(2);
        list.Count(e => e.CriadaNaFicha).Should().Be(1);
    }

    private async Task SetPiAsync(string sheetId, int pi)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.CharacterSheets.FindAsync(Guid.Parse(sheetId)))!.PontosDeIgnicaoAtual = pi;
        await db.SaveChangesAsync();
    }
```

`SetPiAsync` is harmless now and required from Task 8 on. Write the equivalent test in `NpcSpellAbilitiesControllerTests` and `CreatureSpellAbilitiesControllerTests` using each file's own sheet-creation and from-scratch helpers, the routes `/api/npc-sheets/...` and `/api/creature-sheets/...`, the request types `AddNpcSpellAbilityRequest` / `AddCreatureSpellAbilityRequest`, and a `SetPiAsync` that sets `db.NpcSheets ... .PontosDeIgnicaoAtual` / `db.CreatureSheets ... .PontosDeIgnicao`.

Migration test `SpellAbilityCriadaNaFichaMigrationTests.cs`, modelled on `Persistence/CreatureExclusiveTraitMigrationTests.cs`:

```csharp
    [Fact]
    public async Task Migrate_adds_CriadaNaFicha_false_by_default_to_the_three_sheet_spell_ability_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddCriadaNaFichaToSheetSpellAbilities"));
        foreach (var tabela in new[] { "CharacterSpellAbilities", "NpcSpellAbilities", "CreatureSpellAbilities" })
        {
            var padrao = await db.Database.SqlQueryRaw<string>(
                $"SELECT column_default AS \"Value\" FROM information_schema.columns WHERE table_name = '{tabela}' AND column_name = 'CriadaNaFicha'").SingleAsync();
            padrao.Should().Be("false");
        }
    }
```

- [ ] **Step 2: Run and see them fail** (compile error: `CriadaNaFicha` missing).

- [ ] **Step 3: Entities, migration, contracts**

Add to each of the three entities:

```csharp
    /// <summary>Montada do zero na ficha (e não escolhida do banco): só essas podem ser editadas (4.b).</summary>
    public bool CriadaNaFicha { get; set; }
```

Generate the migration:

```bash
dotnet ef migrations add AddCriadaNaFichaToSheetSpellAbilities --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Open the generated file and confirm it contains exactly three `AddColumn<bool>(name: "CriadaNaFicha", ..., nullable: false, defaultValue: false)` and nothing else. If EF generated unrelated changes, stop and report: the model snapshot was out of sync before this task.

Append `, bool CriadaNaFicha = false` as the last parameter of the three response records.

- [ ] **Step 4: Controllers and grant copy**

In each `Add`, inside `if (!fromBank) { ... }` add `sheetCopy.CriadaNaFicha = true;`. In each `ToResponseAsync`, pass `CriadaNaFicha: e.CriadaNaFicha` as a named argument at the end of the `new(...)`.

`CampaignGrantsController`: in the loops that copy `NpcSpellAbilities` and `CreatureSpellAbilities` into the granted copy, copy `CriadaNaFicha = s.CriadaNaFicha` (use the loop variable's real name).

- [ ] **Step 5: Run**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SpellAbilitiesControllerTests|FullyQualifiedName~SpellAbilityCriadaNaFichaMigrationTests|FullyQualifiedName~CampaignGrantsControllerTests"` → PASS.

- [ ] **Step 6: Docs**

`Docs/Requisitos/Requisitos - Modelo de Dados.md`: in the tables for `CharacterSpellAbilities`, `NpcSpellAbilities` and `CreatureSpellAbilities` (search those names; if the doc describes them once by reference, edit the one definition), add the column `CriadaNaFicha` — `boolean, NOT NULL, default false` — `Verdadeiro quando a entrada foi montada do zero na ficha; só essas podem ser editadas. Entradas anteriores à 1.5.0 ficam falsas.`

- [ ] **Step 7: Build and commit**

```bash
git add -A src tests Docs/Requisitos
git commit -m "feat: marca Magias/Habilidades montadas do zero na ficha (CriadaNaFicha)"
```

---

### Task 7: Edit endpoint for entries built on the sheet

**Files:**
- Create: `src/RuinaRPG.Contracts/SpellsAndAbilities/UpdateSheetSpellAbilityRequest.cs`
- Modify: the three `*SpellAbilitiesController.cs`
- Test: the three `*SpellAbilitiesControllerTests.cs`

**Interfaces:**
- Produces: `public record UpdateSheetSpellAbilityRequest(string Nome, string Tipo, int Grau, string Descricao, List<SpellAbilityEffectRequest> Efeitos);`
  `PUT api/{character|npc|creature}-sheets/{sheetId}/spell-abilities/{id}` → 200 with the sheet's `*SpellAbilityResponse`.
- Consumes: `CriadaNaFicha` (Task 6).

- [ ] **Step 1: Write the failing tests (Personagem)**

```csharp
    private static UpdateSheetSpellAbilityRequest BolaDeFogoEditada() => new(
        "Bola de Fogo Maior", "Magia", 3, "Explosão ampliada.",
        [new SpellAbilityEffectRequest("Dano", 4, 8)]);

    private async Task<CharacterSpellAbilityResponse> AddAsync(string token, string sheetId, AddCharacterSpellAbilityRequest request) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", token, request)))
            .Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!;

    [Fact]
    public async Task Update_by_the_owner_changes_a_from_scratch_entry_and_leaves_the_bank_copy_alone()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasEdit1", "magiasedit1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasEditP1", "magiaseditp1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(playerToken, sheetId, BolaDeFogoFromScratch());

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}", playerToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>();
        body!.Nome.Should().Be("Bola de Fogo Maior");
        body.Descricao.Should().Be("Explosão ampliada.");
        body.GastoEmPI.Should().Be(8);
        body.Custo.Should().Be(10); // ceil(8 * 1.25)
        body.Efeitos.Should().ContainSingle(e => e.EfeitoNome == "Dano");
        body.CriadaNaFicha.Should().BeTrue();
        (await GetBankAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.GastoEmPI == 14);
    }

    [Fact]
    public async Task Update_refuses_an_entry_that_came_from_the_bank()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasEdit2", "magiasedit2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasEditP2", "magiaseditp2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());
        var bankId = (await GetBankAsync(gmToken)).Single(e => e.Nome == "Bola de Fogo").Id;
        var doBanco = await AddAsync(gmToken, sheetId, new AddCharacterSpellAbilityRequest(bankId, null, null, null, null, null));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{doBanco.Id}", gmToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Só Magias e Habilidades montadas na ficha podem ser editadas.");
    }

    [Fact]
    public async Task Update_validates_like_a_from_scratch_add()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasEdit3", "magiasedit3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasEditP3", "magiaseditp3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());
        var url = $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}";

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada() with { Tipo = "Passiva" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada() with { Tipo = "Inexistente" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            BolaDeFogoEditada() with { Efeitos = [new SpellAbilityEffectRequest("Efeito Que Não Existe", 1, 1)] }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_by_someone_who_cannot_edit_the_sheet_is_refused_and_an_unknown_id_is_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasEdit4", "magiasedit4@teste.com");
        var outroGm = await RegisterGmAndGetTokenAsync("MagiasEdit4b", "magiasedit4b@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasEditP4", "magiaseditp4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 100);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch());

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}", outroGm, BolaDeFogoEditada())))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/spell-abilities/{Guid.NewGuid()}", gmToken, BolaDeFogoEditada())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
```

For NPC and Criatura write the same four tests in their files with these differences: routes and request types as in Task 6; a stranger gets **404** (those controllers answer `NotFound` instead of `Forbid`); and the "by the owner" test must use a sheet **granted to a player** and the player's token (Review Focus 5) — use the file's existing grant helper (in `NpcSheetsControllerTests` it is `GrantBlankNpcAsync`; find the equivalent in each spell-ability test file, or post `POST /api/campaigns/{campaignId}/grants` with `new GrantSheetRequest(playerId, "Npc" | "Criatura", null)` and read the sheet id from the response). For a player to build from scratch on a granted sheet no bank attachment is needed.

- [ ] **Step 2: Run and see them fail** (405 Method Not Allowed / compile error on the request type).

- [ ] **Step 3: Contract**

```csharp
namespace RuinaRPG.Contracts.SpellsAndAbilities;

/// <summary>Edição de uma Magia/Habilidade montada do zero numa ficha (Ficha de Personagem 4.b).</summary>
public record UpdateSheetSpellAbilityRequest(string Nome, string Tipo, int Grau, string Descricao, List<SpellAbilityEffectRequest> Efeitos);
```

- [ ] **Step 4: `CharacterSpellAbilitiesController.Update`**

Add before `Delete`:

```csharp
    [HttpPut("{id}")]
    public async Task<ActionResult<CharacterSpellAbilityResponse>> Update(Guid sheetId, Guid id, UpdateSheetSpellAbilityRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var entry = await db.CharacterSpellAbilities.Include(e => e.Efeitos).FirstOrDefaultAsync(e => e.Id == id && e.CharacterSheetId == sheetId);
        if (entry is null)
            return NotFound();

        // Só o que foi montado na ficha: uma entrada vinda do banco segue somente leitura (4.b).
        if (!entry.CriadaNaFicha)
            return BadRequest("Só Magias e Habilidades montadas na ficha podem ser editadas.");

        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo) || !SaldoDePontosDeIgnicao.Consome(tipo))
            return BadRequest("Tipo desconhecido. Use Magia ou Habilidade.");

        var validationError = await EfeitoValidationHelper.ValidarAsync(db, request.Grau, request.Efeitos);
        if (validationError is not null)
            return BadRequest(validationError);

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

        entry.Nome = request.Nome;
        entry.Tipo = tipo;
        entry.Grau = request.Grau;
        entry.Descricao = request.Descricao;
        entry.GastoEmPI = gastoEmPI;
        entry.Custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

        // Troca os Efeitos por inteiro; a cópia que foi para o banco na criação não é tocada.
        db.CharacterSpellAbilityEffects.RemoveRange(entry.Efeitos);
        entry.Efeitos = request.Efeitos
            .Select(e => new CharacterSpellAbilityEffect { Id = Guid.NewGuid(), CharacterSpellAbilityId = entry.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI })
            .ToList();

        await db.SaveChangesAsync();
        return await ToResponseAsync(entry, null);
    }
```

If replacing the tracked collection makes EF throw a concurrency error (new rows treated as modified), switch to `db.CharacterSpellAbilityEffects.AddRange(novos)` with an explicit list, exactly as `SpellAbilityBankController.Update` does, and build the response from that list.

- [ ] **Step 5: NPC and Criatura**

`NpcSpellAbilitiesController.Update`: same body with `db.NpcSheets`, the controller's own authorization check and failure result —

```csharp
        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();
```

— `db.NpcSpellAbilities`, `e.NpcSheetId == sheetId`, `db.NpcSpellAbilityEffects`, `new NpcSpellAbilityEffect { ..., NpcSpellAbilityId = entry.Id, ... }` and return type `ActionResult<NpcSpellAbilityResponse>`.

`CreatureSpellAbilitiesController.Update`: same with `db.CreatureSheets`, `db.CreatureSpellAbilities`, `e.CreatureSheetId == sheetId`, `db.CreatureSpellAbilityEffects`, `new CreatureSpellAbilityEffect { ..., CreatureSpellAbilityId = entry.Id, ... }` and `ActionResult<CreatureSpellAbilityResponse>`.

Check the real property names on the effect entities before typing them.

- [ ] **Step 6: Run and commit**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SpellAbilitiesControllerTests` → PASS. `dotnet build` → 0 warnings.

```bash
git add -A src tests
git commit -m "feat(api): edição de Magias/Habilidades montadas na ficha nas três fichas"
```

---

### Task 8: PI balance on add, edit and delete

**Files:**
- Modify: the three `*SpellAbilitiesController.cs` (`Add`, `Update`, `Delete`)
- Test: the three `*SpellAbilitiesControllerTests.cs`, plus fixes in the other test files that add costed entries

**Interfaces:**
- Consumes: `SaldoDePontosDeIgnicao.Consome/ErroAoDebitar/Debitar` (Task 3); `Update` (Task 7).
- Balance field: `CharacterSheet.PontosDeIgnicaoAtual`, `NpcSheet.PontosDeIgnicaoAtual`, `CreatureSheet.PontosDeIgnicao`.

- [ ] **Step 1: Write the failing tests (Personagem)**

```csharp
    private async Task<int> GetPiAsync(string sheetId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return (await db.CharacterSheets.AsNoTracking().SingleAsync(s => s.Id == Guid.Parse(sheetId))).PontosDeIgnicaoAtual;
    }

    [Fact]
    public async Task Add_is_refused_when_the_Gasto_em_PI_exceeds_the_balance_even_for_the_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi1", "magiaspi1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP1", "magiaspip1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 13);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Pontos de Ignição insuficientes: custa 14 e a ficha tem 13.");
        (await GetPiAsync(sheetId)).Should().Be(13);
        (await GetBankAsync(gmToken)).Should().NotContain(e => e.Nome == "Bola de Fogo", "uma criação recusada não deixa cópia no banco");
    }

    [Fact]
    public async Task Add_with_exactly_enough_debits_the_balance_to_zero_and_Delete_refunds_it()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi2", "magiaspi2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP2", "magiaspip2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 14);

        var criada = await AddAsync(playerToken, sheetId, BolaDeFogoFromScratch());
        (await GetPiAsync(sheetId)).Should().Be(0);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}", playerToken)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetPiAsync(sheetId)).Should().Be(14);
    }

    [Fact]
    public async Task Add_from_the_bank_also_checks_and_debits()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi3", "magiaspi3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP3", "magiaspip3@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 20);
        await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch()); // 20 - 14 = 6
        var bankId = (await GetBankAsync(gmToken)).Single(e => e.Nome == "Bola de Fogo").Id;

        var recusada = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCharacterSpellAbilityRequest(bankId, null, null, null, null, null)));
        recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await SetPiAsync(sheetId, 14);
        var aceita = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gmToken,
            new AddCharacterSpellAbilityRequest(bankId, null, null, null, null, null)));
        aceita.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetPiAsync(sheetId)).Should().Be(0);
    }

    [Fact]
    public async Task Update_settles_only_the_difference_in_both_directions()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi4", "magiaspi4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP4", "magiaspip4@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await SetPiAsync(sheetId, 14);
        var criada = await AddAsync(gmToken, sheetId, BolaDeFogoFromScratch()); // gasto 14, saldo 0
        var url = $"/api/character-sheets/{sheetId}/spell-abilities/{criada.Id}";

        // Só o nome muda (diferença 0): aceito mesmo com saldo zero.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Outro Nome", "Magia", 3, "Uma explosão de fogo.",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(0);

        // Custo cai de 14 para 8: devolve 6.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada()))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(6);

        // Custo sobe de 8 para 14: precisa de 6, tem 6.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Bola de Fogo", "Magia", 3, "x",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(sheetId)).Should().Be(0);

        // Voltar a 8 e tentar subir para 14 com saldo 5: recusado, nada muda.
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken, BolaDeFogoEditada()))).EnsureSuccessStatusCode();
        await SetPiAsync(sheetId, 5);
        var recusada = await _client.SendAsync(AuthedRequest(HttpMethod.Put, url, gmToken,
            new UpdateSheetSpellAbilityRequest("Bola de Fogo", "Magia", 3, "x",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])));
        recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await recusada.Content.ReadAsStringAsync()).Should().Contain("Pontos de Ignição insuficientes: custa 6 e a ficha tem 5.");
        (await GetPiAsync(sheetId)).Should().Be(5);
    }

    [Fact]
    public async Task Deleting_an_entry_that_was_never_debited_still_refunds_its_Gasto_em_PI()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("MagiasPi5", "magiaspi5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "MagiasPiP5", "magiaspip5@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var legadaId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.CharacterSpellAbilities.Add(new RuinaRPG.Infrastructure.CharacterSheets.CharacterSpellAbility
            {
                Id = legadaId, CharacterSheetId = Guid.Parse(sheetId), Nome = "Antiga", Tipo = RuinaRPG.Domain.SpellsAndAbilities.SpellAbilityTipo.Magia,
                Grau = 1, GastoEmPI = 9, Custo = 12, Descricao = "anterior à 1.5.0",
            });
            await db.SaveChangesAsync();
        }
        await SetPiAsync(sheetId, 2);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}/spell-abilities/{legadaId}", gmToken))).EnsureSuccessStatusCode();

        (await GetPiAsync(sheetId)).Should().Be(11);
    }
```

Add to `CharacterPassivasTests` (or the file that already adds Passivas to a Personagem) one test: with the sheet's PI set to 0, adding a Passiva from the bank still returns `Created` and the balance stays 0; deleting it leaves the balance at 0.

For NPC and Criatura write the first, second and fourth tests in their files (routes/types as before; `GetPiAsync`/`SetPiAsync` over `NpcSheets.PontosDeIgnicaoAtual` and `CreatureSheets.PontosDeIgnicao`), and make the second one run on a **granted** sheet with the player's token.

- [ ] **Step 2: Run and see them fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SpellAbilitiesControllerTests"` → the new tests FAIL (nothing debits).

- [ ] **Step 3: Implement in `CharacterSpellAbilitiesController`**

`Add`, right after `gastoEmPI`/`custo` are computed and before `sheetCopy` is created:

```csharp
        // 4.b: o Gasto em PI sai do saldo da ficha; sem saldo, nem o GM cria. Passivas não têm PI.
        if (SaldoDePontosDeIgnicao.Consome(tipo))
        {
            if (SaldoDePontosDeIgnicao.ErroAoDebitar(sheet.PontosDeIgnicaoAtual, gastoEmPI) is { } semSaldo)
                return BadRequest(semSaldo);
            sheet.PontosDeIgnicaoAtual = SaldoDePontosDeIgnicao.Debitar(sheet.PontosDeIgnicaoAtual, gastoEmPI);
        }
```

`Update`, after `gastoEmPI` is computed and **before** any field of `entry` is assigned:

```csharp
        // Só a diferença: subir o custo precisa caber no saldo; baixar devolve.
        var diferenca = gastoEmPI - entry.GastoEmPI;
        if (SaldoDePontosDeIgnicao.ErroAoDebitar(sheet.PontosDeIgnicaoAtual, diferenca) is { } semSaldo)
            return BadRequest(semSaldo);
        sheet.PontosDeIgnicaoAtual = SaldoDePontosDeIgnicao.Debitar(sheet.PontosDeIgnicaoAtual, diferenca);
```

`Delete`, before `Remove`:

```csharp
        // Remover devolve o Gasto em PI, inclusive o de uma entrada anterior à 1.5.0 (nunca debitada).
        if (SaldoDePontosDeIgnicao.Consome(entry.Tipo))
            sheet.PontosDeIgnicaoAtual = SaldoDePontosDeIgnicao.Debitar(sheet.PontosDeIgnicaoAtual, -entry.GastoEmPI);
```

`sheet` comes from `FindAsync`, so it is tracked and saved by the existing `SaveChangesAsync`.

- [ ] **Step 4: NPC and Criatura**

Same three insertions. NPC uses `sheet.PontosDeIgnicaoAtual`; Criatura uses `sheet.PontosDeIgnicao` in all three places.

- [ ] **Step 5: Run the three files, then repair the rest of the suite**

Run: `--filter FullyQualifiedName~SpellAbilitiesControllerTests` → PASS, after giving a balance (`await SetPiAsync(sheetId, 1000);`) to the pre-existing tests of those files that add costed entries.

Then run each of these files, which also post to `spell-abilities`, and fix every 400 "Pontos de Ignição insuficientes" the same way (a small `SetPiAsync`-style helper per file, writing straight to the database, placed right after the sheet is created):

```
PericiasSheetBehaviourTests  CampaignGrantsControllerTests  NpcCreaturePassivasTests
CharacterPassivasTests  SpellAbilityBankControllerTests  CampaignCatalogControllerTests  NivelLimitsTests
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~PericiasSheetBehaviourTests|FullyQualifiedName~CampaignGrantsControllerTests|FullyQualifiedName~NpcCreaturePassivasTests|FullyQualifiedName~CharacterPassivasTests|FullyQualifiedName~SpellAbilityBankControllerTests|FullyQualifiedName~CampaignCatalogControllerTests|FullyQualifiedName~NivelLimitsTests"`
Expected: PASS. Do not change production code to make an old test pass.

- [ ] **Step 6: Full integration run**

Run: `dotnet test tests/RuinaRPG.Tests.Integration` (several minutes). Rerun each failure **alone**; a test that passes alone was the known timeout flake. Anything still failing is yours to fix.

- [ ] **Step 7: Build and commit**

```bash
git add -A src tests
git commit -m "feat(api): saldo de Pontos de Ignição ao adicionar, editar e remover Magias/Habilidades"
```

---

### Task 9: Edit popup on the three sheets, PI reload, docs

**Files:**
- Create: `src/RuinaRPG.Client/Shared/EditarMagiaHabilidadeDialog.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (list ~663-672, `AddSpellAbilityAsync`/`DeleteSpellAbilityAsync` ~1509-1536), `FichaDeNpc.razor`, `FichaDeCriatura.razor` (the same blocks in each)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (4.b), `Requisitos - Ficha de NPCs.md`, `Requisitos - Ficha de Criaturas.md` (1.a Pontos de Ignição, R0007)
- Test: `tests/RuinaRPG.Tests.Client/Shared/EditarMagiaHabilidadeDialogTests.cs` (new), `tests/RuinaRPG.Tests.Client/Pages/FichaMagiasEdicaoTests.cs` (new)

**Interfaces:**
- Consumes: `PUT {SaveUrl}` with `UpdateSheetSpellAbilityRequest` (Task 7); `bool CriadaNaFicha` on the list responses (Task 6); the existing `AddEfeitoForm` (`Grau`, `EfeitosExistentes`, `OnAdicionar`) and `EfeitosTable` (`Efeitos`, `Grau`) components and `EfeitoLinha(EfeitoNome, Quantidade, CustoPI)`.
- Produces: dialog parameters `string SaveUrl`, `string Nome`, `string Tipo`, `int Grau`, `string Descricao`, `List<SpellAbilityEffectResponse> Efeitos`; closes with `DialogResult.Ok(true)`.

- [ ] **Step 1: Write the failing dialog tests**

Same harness as Task 2's dialog tests (`Render<MudDialogProvider>()`, `ShowAsync` with `DialogParameters<EditarMagiaHabilidadeDialog>`); the fake handler must answer the `efeitos`/`pericias` GETs that `AddEfeitoForm`/`EfeitosTable` make with an empty list (see how `BancoDeMagiasFormTests` stubs them and reuse that exactly):

```csharp
    [Fact]
    public async Task Opens_filled_with_the_entry_and_Salvar_puts_the_edited_fields_to_SaveUrl()
    {
        RegisterHttp(r => r.Method == HttpMethod.Put ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { }) } : Json(new List<object>()));
        var (cut, dialog) = await AbrirAsync("character-sheets/s1/spell-abilities/e1", "Bola de Fogo", "Magia", 3, "Uma explosão.",
            [new SpellAbilityEffectResponse("Dano", 4, 8)]);

        cut.Markup.Should().Contain("Bola de Fogo");
        cut.FindComponents<MudTextField<string>>().Single(c => c.Instance.Label == "Nome").Find("input").Change("Bola de Fogo Maior");
        cut.Find("button:contains('Salvar')").Click();
        var resultado = await dialog.Result;

        _requests.Should().Contain(r => r.StartsWith("PUT") && r.EndsWith("character-sheets/s1/spell-abilities/e1"));
        _corpo!.Nome.Should().Be("Bola de Fogo Maior");
        _corpo.Tipo.Should().Be("Magia");
        _corpo.Grau.Should().Be(3);
        _corpo.Efeitos.Should().ContainSingle(e => e.EfeitoNome == "Dano" && e.Quantidade == 4 && e.CustoPI == 8);
        resultado!.Canceled.Should().BeFalse();
    }

    [Fact]
    public async Task A_refused_save_shows_the_api_message_and_stays_open()
    {
        RegisterHttp(r => r.Method == HttpMethod.Put
            ? new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("Pontos de Ignição insuficientes: custa 6 e a ficha tem 5.") }
            : Json(new List<object>()));
        var (cut, _) = await AbrirAsync("npc-sheets/s1/spell-abilities/e1", "Bola de Fogo", "Magia", 3, "x", [new SpellAbilityEffectResponse("Dano", 4, 8)]);

        cut.Find("button:contains('Salvar')").Click();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Pontos de Ignição insuficientes: custa 6 e a ficha tem 5."));
    }
```

`_corpo` is read from the PUT body as `UpdateSheetSpellAbilityRequest`. Use the real positional shape of `SpellAbilityEffectResponse` (`src/RuinaRPG.Contracts/SpellsAndAbilities/SpellAbilityEffectResponse.cs`).

- [ ] **Step 2: Run and see them fail**, then **Step 3: create the dialog**

```razor
@using MudBlazor
@using RuinaRPG.Client.Shared.Fields
@using RuinaRPG.Contracts.SpellsAndAbilities
@inject HttpClient Http
@*
    Edita uma Magia/Habilidade montada do zero numa ficha (Ficha de Personagem 4.b). Mesmos campos do
    formulário "do zero" das fichas; a URL de gravação vem de fora para as três fichas usarem o mesmo
    popup. O Gasto em PI é recalculado pela API, que acerta só a diferença no saldo da ficha.
*@
<MudDialog>
    <DialogContent>
        <DismissibleAlert @bind-Message="_errorMessage" Class="mb-3" />
        <MudTextField T="string" @bind-Value="_nome" Label="Nome" />
        <MudSelect T="string" @bind-Value="_tipo" Label="Tipo">
            <MudSelectItem Value="@("Magia")">Magia</MudSelectItem>
            <MudSelectItem Value="@("Habilidade")">Habilidade</MudSelectItem>
        </MudSelect>
        <MudNumericField T="int" @bind-Value="_grau" Label="Grau" />
        <MudTextField T="string" @bind-Value="_descricao" Label="Descrição" Lines="3" />
        <AddEfeitoForm Grau="_grau" EfeitosExistentes="@(_efeitos.Select(e => (e.EfeitoNome, e.Quantidade)).ToList())" OnAdicionar="OnEfeitoAdicionado" />
        <EfeitosTable Efeitos="_efeitos" Grau="_grau" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => MudDialog.Cancel())">Cancelar</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled" Disabled="_salvando" OnClick="SalvarAsync">Salvar</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter, EditorRequired] public string SaveUrl { get; set; } = "";
    [Parameter] public string Nome { get; set; } = "";
    [Parameter] public string Tipo { get; set; } = "Magia";
    [Parameter] public int Grau { get; set; }
    [Parameter] public string Descricao { get; set; } = "";
    [Parameter] public List<SpellAbilityEffectResponse> Efeitos { get; set; } = new();

    private string _nome = "", _tipo = "Magia", _descricao = "";
    private int _grau;
    private readonly List<EfeitoLinha> _efeitos = new();
    private string? _errorMessage;
    private bool _salvando;

    protected override void OnInitialized()
    {
        _nome = Nome; _tipo = Tipo; _grau = Grau; _descricao = Descricao;
        _efeitos.AddRange(Efeitos.Select(e => new EfeitoLinha(e.EfeitoNome, e.Quantidade, e.CustoPI)));
    }

    private void OnEfeitoAdicionado(List<EfeitoAdicionadoResult> resultados) =>
        _efeitos.AddRange(resultados.Select(r => new EfeitoLinha(r.EfeitoNome, r.Quantidade, r.CustoPI)));

    private async Task SalvarAsync()
    {
        _salvando = true;
        try
        {
            var response = await Http.PutAsJsonAsync(SaveUrl, new UpdateSheetSpellAbilityRequest(_nome, _tipo, _grau, _descricao,
                _efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList()));
            if (!response.IsSuccessStatusCode)
            {
                _errorMessage = await response.Content.ReadErrorMessageAsync() ?? "Não foi possível salvar a alteração.";
                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            _salvando = false;
        }
    }
}
```

The `AddEfeitoForm`/`EfeitosTable` parameter names, the `OnAdicionar` callback's argument type and the `EfeitoLinha` constructor must match what `FichaDePersonagem.razor` uses at lines ~657-658 and ~1503-1507; copy from there if any differs from the above. Run the dialog tests → PASS.

- [ ] **Step 4: Write the failing page tests**

`FichaMagiasEdicaoTests.cs`, stubbing like `FichaSubAtributosTests` plus a `spell-abilities` list with one entry `CriadaNaFicha = true` ("Montada") and one `false` ("Do Banco"), and a sheet GET that returns `PontosDeIgnicaoAtual = 30` the first time and `16` afterwards:

```csharp
    [Theory]
    [InlineData(typeof(FichaDePersonagem))]
    [InlineData(typeof(FichaDeNpc))]
    [InlineData(typeof(FichaDeCriatura))]
    public async Task Only_entries_built_on_the_sheet_have_the_Editar_button(Type pagina)
    {
        RegisterHttp();
        var cut = RenderPagina(pagina);
        await Task.Delay(100);
        await AbrirAbaAsync(cut, "Magias & Habilidades");

        var linhas = cut.FindAll("tr").Where(tr => tr.TextContent.Contains("Montada") || tr.TextContent.Contains("Do Banco")).ToList();
        linhas.Single(tr => tr.TextContent.Contains("Montada")).QuerySelectorAll("button").Should().Contain(b => b.TextContent.Contains("Editar"));
        linhas.Single(tr => tr.TextContent.Contains("Do Banco")).QuerySelectorAll("button").Should().NotContain(b => b.TextContent.Contains("Editar"));
    }

    [Fact]
    public async Task Removing_a_spell_reloads_the_Pontos_de_Ignicao_shown_on_the_Personagem()
    {
        RegisterHttp();
        var cut = Render<FichaDePersonagem>(p => p.Add(x => x.SheetId, "sheet-1"));
        await Task.Delay(100);
        await AbrirAbaAsync(cut, "Magias & Habilidades");

        cut.FindAll("tr").Single(tr => tr.TextContent.Contains("Montada")).QuerySelectorAll("button").Single(b => b.TextContent.Contains("Remover")).Click();
        await Task.Delay(100);

        _requests.Count(r => r == "GET /api/character-sheets/sheet-1" || r.EndsWith("GET /character-sheets/sheet-1")).Should().BeGreaterThan(1);
        cut.FindComponents<MudNumericField<int?>>().Single(c => c.Instance.Label == "Pontos de Ignição Atual").Instance.Value.Should().Be(16);
    }
```

`RenderPagina` renders the component type with `SheetId = "sheet-1"` (use `Render<T>` in a small switch); `AbrirAbaAsync` clicks the tab like `FichaSubAtributosTests` does for "Combate". If the PI field lives on a tab that is unmounted while another tab is open, read the value after clicking back to the first tab. Adjust the request-path assertion to the exact format `_requests` records.

- [ ] **Step 5: Change the three sheet pages**

In each page (`@inject IDialogService Dialogs` if missing):

1. In the Magias & Habilidades list row, before the "Remover" button:

```razor
                            <td>
                                @if (spellAbility.CriadaNaFicha)
                                {
                                    <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" StartIcon="@Icons.Material.Filled.Edit" Class="mr-1" OnClick="@(() => EditSpellAbilityAsync(spellAbility))">Editar</MudButton>
                                }
                                <MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteSpellAbilityAsync(spellAbility.Id))">Remover</MudButton>
                            </td>
```

2. In `@code` (Personagem shown; NPC uses `NpcSpellAbilityResponse` and `npc-sheets`, Criatura uses `CreatureSpellAbilityResponse` and `creature-sheets`):

```csharp
    private async Task EditSpellAbilityAsync(CharacterSpellAbilityResponse entrada)
    {
        var dialog = await Dialogs.ShowAsync<EditarMagiaHabilidadeDialog>("Editar " + entrada.Tipo,
            new DialogParameters<EditarMagiaHabilidadeDialog>
            {
                { x => x.SaveUrl, $"character-sheets/{SheetId}/spell-abilities/{entrada.Id}" },
                { x => x.Nome, entrada.Nome }, { x => x.Tipo, entrada.Tipo }, { x => x.Grau, entrada.Grau },
                { x => x.Descricao, entrada.Descricao }, { x => x.Efeitos, entrada.Efeitos },
            },
            new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true });
        if ((await dialog.Result) is { Canceled: false })
        {
            await LoadSpellAbilitiesAsync();
            await RecarregarPontosDeIgnicaoAsync();
        }
    }

    /// <summary>
    /// A API mexe no saldo de PI ao adicionar, editar e remover uma Magia/Habilidade (4.b). Traz o valor
    /// novo para o formulário antes do próximo autosave, que senão regravaria o saldo antigo.
    /// </summary>
    private async Task RecarregarPontosDeIgnicaoAsync()
    {
        var sheet = await Http.GetFromJsonAsync<CharacterSheetResponse>($"character-sheets/{SheetId}");
        if (sheet is not null)
            _form.PontosDeIgnicaoAtual = sheet.PontosDeIgnicaoAtual;
    }
```

   NPC: `NpcSheetResponse` / `npc-sheets` / `_form.PontosDeIgnicaoAtual`. Criatura: `CreatureSheetResponse` / `creature-sheets` / `_form.PontosDeIgnicao = sheet.PontosDeIgnicao`.

3. Call `await RecarregarPontosDeIgnicaoAsync();` at the end of the success path of `AddSpellAbilityAsync` and of `DeleteSpellAbilityAsync`. `DeleteSpellAbilityAsync` today ignores the response; keep that, and reload both the list and the PI.

4. `AddSpellAbilityAsync` already shows the API error in `_errorMessage` on failure; confirm the 400 text reaches the alert via `ReadErrorMessageAsync` and, if it shows a fixed generic message instead, switch to `await response.Content.ReadErrorMessageAsync() ?? "<the existing generic message>"`.

- [ ] **Step 6: Run the client tests**

Run: `dotnet test tests/RuinaRPG.Tests.Client` → all PASS.

- [ ] **Step 7: Docs**

`Docs/Requisitos/Requisitos - Ficha de Personagem.md` 4.b:
- Replace `Uma entrada pode ser removida pelo jogador a qualquer momento.` with:

  `Uma entrada pode ser removida pelo jogador a qualquer momento. Uma entrada **montada do zero** nesta ficha também pode ser editada (mesmos campos e validações da criação) por quem pode editar a ficha; uma entrada vinda do banco não é editável — para mudá-la, remove-se e adiciona-se de novo. Editar a entrada da ficha não altera a cópia que a criação gravou no banco. Entradas que já existiam antes dessa regra não são editáveis, pois a ficha não registrava a origem delas.`

  followed by a new paragraph:

  `**Saldo de Pontos de Ignição**: o *Gasto em PI* de uma Magia ou Habilidade é pago com os *Pontos de Ignição* atuais da ficha (1.b). Ao adicionar uma entrada, montada do zero ou vinda do banco, a ficha confere o saldo: se o Gasto em PI for maior que o saldo, a entrada não é criada; se couber, o valor é descontado. Remover uma entrada devolve o Gasto em PI dela ao saldo. Editar acerta apenas a diferença: um custo maior precisa caber no saldo e é descontado, um custo menor devolve a diferença. A regra vale para todos, inclusive o GM. Passivas (4.f) e a Habilidade Racial não têm Gasto em PI e ficam fora dela.`

`Docs/Requisitos/Requisitos - Ficha de NPCs.md`: where the doc covers the Magias & Habilidades tab (or as a new requirement continuing the `R00NN` sequence, titled `O saldo de Pontos de Ignição bloqueia também o GM.`), state: `A aba 4.b do NPC segue "[[Requisitos - Ficha de Personagem]]" 4.b sem alteração, incluindo a edição das entradas montadas do zero e o saldo de Pontos de Ignição. Ao contrário dos contadores de R0007 e de Vocação Arcana deste documento, o saldo de PI **é um limite**: nem o GM adiciona uma Magia/Habilidade cujo Gasto em PI exceda o saldo.` Use the real requirement numbers the doc has for those two counters.

`Docs/Requisitos/Requisitos - Ficha de Criaturas.md`:
- 1.a *Pontos de Ignição*: replace the bullet with `- *Pontos de Ignição*: campo numérico inteiro simples (não é um par atual/total como no Personagem), editável pelo GM. É o saldo de PI que as Magias/Habilidades da Criatura consomem (ver R0007).`
- R0007, 4.b: append `O saldo de Pontos de Ignição e a edição das entradas montadas do zero seguem "[[Requisitos - Ficha de Personagem]]" 4.b, usando o campo *Pontos de Ignição* (1.a) como saldo; o bloqueio vale também para o GM.`

- [ ] **Step 8: Full verification**

```bash
dotnet build            # 0 warnings, 0 errors
dotnet test tests/RuinaRPG.Tests.Unit
dotnet test tests/RuinaRPG.Tests.Client
dotnet test tests/RuinaRPG.Tests.Integration   # rerun any timeout failure alone
```

- [ ] **Step 9: Commit and push**

```bash
git add -A src tests Docs/Requisitos
git commit -m "feat(client): edição de Magias/Habilidades na ficha e saldo de PI atualizado na tela"
git push
```
