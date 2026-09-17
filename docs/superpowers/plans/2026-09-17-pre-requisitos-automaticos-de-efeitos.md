# Pré-requisitos Automáticos de Efeitos — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stop hiding Efeitos whose prerequisite hasn't been bought yet in the Magia/Habilidade
"Efeito" picker — auto-add the missing prerequisite instead (asking the player only when more than
one candidate could satisfy it), and make every already-added Efeito row editable (Quantidade, and
Custo for GM-manual types) instead of read-only.

**Architecture:** A new pure `EfeitoPrerequisiteResolver` (Domain) resolves — recursively, in
dependency order — which prerequisite Efeitos are still missing for a target Efeito, given what's
already on the Magia/Habilidade and the current Grau; it signals back to the caller when a
prerequisite group has more than one valid candidate so the UI can ask. `AddEfeitoForm.razor`
(shared across all 4 add-forms) stops filtering its dropdown by prerequisite, drives the resolver
on confirm, and emits a batch of rows (auto-added prerequisites + the chosen Efeito) instead of
one row at a time. A new shared `EfeitosTable.razor` component (replacing 4 near-identical
`<MudSimpleTable>` blocks) renders every already-added row with live-editable
Quantidade/Custo and cascades a recompute to any row that derives its cost from the one just
edited.

**Tech Stack:** Blazor WebAssembly, MudBlazor, bUnit. No backend/API/schema changes — the server's
existing `EfeitoValidator` already accepts any submitted list; see the spec's "Fora de escopo".

**Spec:** `docs/superpowers/specs/2026-09-17-pre-requisitos-automaticos-de-efeitos-design.md`

## Global Constraints

- No database migration, no controller change, no `Contracts` change — this plan is 100%
  client-side (`RuinaRPG.Client`) plus one new pure Domain type.
- `dotnet build` must stay at 0 Warning(s), 0 Error(s) after every task.
- `EfeitoLinha` (new shared model) replaces the 4 near-identical local classes
  `BancoDeMagiasForm.EffectFormModel` / `FichaDePersonagem.SpellAbilityEffectFormModel` /
  `FichaDeNpc.SpellAbilityEffectFormModel` / `FichaDeCriatura.SpellAbilityEffectFormModel` — delete
  each local class as its page is migrated (Tasks 4 and 5), don't leave dead code behind.
- Auto-added prerequisite rows: `Fixo` type → `Quantidade = null`, `CustoPI = CustoFixo` (fully
  resolved immediately); every other type (`PorUnidade`, `ManualPorUnidade`, `Manual`,
  `DerivadoDeOutroEfeito`) → `Quantidade = 0`, `CustoPI = 0` ("pending", editable afterward via
  `EfeitosTable`). Only `Fixo`/`PorUnidade` ever occur as real prerequisite targets in today's
  51-row catalog, but this rule must not special-case just those two.
- Removing a row another row depends on stays unguarded (no cascade-block, no cascade-delete) —
  the server's existing validation is the backstop, exactly as today.

---

### Task 1: Domain — `EfeitoPrerequisiteResolver`

**Files:**
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoPrerequisiteResolver.cs`
- Test: `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/EfeitoPrerequisiteResolverTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Domain.SpellsAndAbilities.EfeitoRegra` (existing — `Nome`, `Grau`,
  `TipoDeCusto`, `PreRequisitos` fields used here).
- Produces: `EfeitoResolutionResult(IReadOnlyList<EfeitoRegra> ParaAutoAdicionar, IReadOnlyList<string>?
  GrupoAmbiguo)`, `EfeitoPrerequisiteResolver.Resolver(EfeitoRegra alvo, IReadOnlyList<EfeitoRegra>
  catalogo, IReadOnlySet<string> nomesPresentes, int grauDaMagia, IReadOnlyDictionary<string, string>
  escolhasForcadas) : EfeitoResolutionResult`. Used by Task 3 (`AddEfeitoForm.razor`).

This task has no I/O — pure functions over `EfeitoRegra`, same style as `EfeitoValidator`.

- [ ] **Step 1: Write the failing tests**

Create `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/EfeitoPrerequisiteResolverTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class EfeitoPrerequisiteResolverTests
{
    private static EfeitoRegra Regra(string nome, int grau, params string[][] preRequisitos) => new(
        nome, grau, TipoDeCusto.Fixo, CustoFixo: 1, CustoPorUnidade: null, QuantidadeDerivadaDeEfeito: null,
        MaxUnidades: null, MaxEscalaPorGrau: false, MaxContandoAPartirDoGrau: null,
        CustoAlternativo: null, CustoAlternativoAPartirDoGrau: null, PreRequisitos: preRequisitos);

    private static readonly IReadOnlyDictionary<string, string> SemEscolhas = new Dictionary<string, string>();

    [Fact]
    public void A_group_with_a_single_missing_candidate_resolves_by_itself()
    {
        var duracao = Regra("Duração", 1);
        var alvo = Regra("Cura", 1, ["Dano"]);
        var dano = Regra("Dano", 1);
        var catalogo = new[] { duracao, alvo, dano };

        var resultado = EfeitoPrerequisiteResolver.Resolver(alvo, catalogo, new HashSet<string>(), grauDaMagia: 1, SemEscolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { dano });
    }

    [Fact]
    public void Nothing_to_resolve_when_every_prerequisite_is_already_present()
    {
        var alvo = Regra("Cura", 1, ["Dano"]);
        var dano = Regra("Dano", 1);
        var catalogo = new[] { alvo, dano };

        var resultado = EfeitoPrerequisiteResolver.Resolver(alvo, catalogo, new HashSet<string> { "Dano" }, grauDaMagia: 1, SemEscolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        resultado.ParaAutoAdicionar.Should().BeEmpty();
    }

    [Fact]
    public void A_group_with_more_than_one_Grau_eligible_candidate_is_reported_as_ambiguous()
    {
        var duracao = Regra("Duração", 1);
        var congelar = Regra("Congelar", 3, ["Duração"]);
        var enraizar = Regra("Enraizar", 2, ["Duração"]);
        var atordoamento = Regra("Atordoamento", 8, ["Duração"]);
        var detrito = Regra("Detrito", 4, ["Duração"], ["Atordoamento", "Congelar", "Enraizar"]);
        var catalogo = new[] { duracao, congelar, enraizar, atordoamento, detrito };

        var resultado = EfeitoPrerequisiteResolver.Resolver(detrito, catalogo, new HashSet<string>(), grauDaMagia: 4, SemEscolhas);

        // Atordoamento (Grau 8) não é elegível numa Magia de Grau 4 — só Congelar/Enraizar entram na escolha.
        resultado.GrupoAmbiguo.Should().BeEquivalentTo(new[] { "Congelar", "Enraizar" }, o => o.WithStrictOrdering());
        // O grupo [Duração] (único candidato) já foi resolvido antes do grupo ambíguo ser encontrado.
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { duracao });
    }

    [Fact]
    public void A_group_with_only_one_Grau_eligible_candidate_resolves_without_asking_even_if_the_group_has_more_members()
    {
        var duracao = Regra("Duração", 1);
        var congelar = Regra("Congelar", 3, ["Duração"]);
        var atordoamento = Regra("Atordoamento", 8, ["Duração"]);
        var alvo = Regra("Alvo", 3, ["Atordoamento", "Congelar"]);
        var catalogo = new[] { duracao, congelar, atordoamento, alvo };

        // Grau da Magia = 3: Atordoamento (Grau 8) não é elegível, só Congelar sobra — resolve sozinho.
        var resultado = EfeitoPrerequisiteResolver.Resolver(alvo, catalogo, new HashSet<string>(), grauDaMagia: 3, SemEscolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { duracao, congelar });
    }

    [Fact]
    public void An_answered_ambiguous_group_resolves_using_the_forced_choice_and_recurses_into_it()
    {
        var duracao = Regra("Duração", 1);
        var congelar = Regra("Congelar", 3, ["Duração"]);
        var enraizar = Regra("Enraizar", 2, ["Duração"]);
        var detrito = Regra("Detrito", 4, ["Congelar", "Enraizar"]);
        var catalogo = new[] { duracao, congelar, enraizar, detrito };
        var escolhas = new Dictionary<string, string> { ["Congelar|Enraizar"] = "Congelar" };

        var resultado = EfeitoPrerequisiteResolver.Resolver(detrito, catalogo, new HashSet<string>(), grauDaMagia: 4, escolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        // Congelar (a escolha forçada) exige Duração — resolvida recursivamente antes de Congelar.
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { duracao, congelar }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Recursion_orders_a_chosen_candidates_own_missing_prerequisite_before_the_candidate()
    {
        var duracao = Regra("Duração", 1);
        var enraizar = Regra("Enraizar", 2, ["Duração"]);
        var alvo = Regra("Alvo", 3, ["Enraizar"]);
        var catalogo = new[] { duracao, enraizar, alvo };

        var resultado = EfeitoPrerequisiteResolver.Resolver(alvo, catalogo, new HashSet<string>(), grauDaMagia: 3, SemEscolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { duracao, enraizar }, o => o.WithStrictOrdering());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EfeitoPrerequisiteResolverTests"`
Expected: FAIL to compile — `EfeitoPrerequisiteResolver`/`EfeitoResolutionResult` don't exist yet.

- [ ] **Step 3: Implement `EfeitoPrerequisiteResolver`**

Create `src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoPrerequisiteResolver.cs`:

```csharp
namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// The result of trying to resolve one target Efeito's unmet prerequisites — see
/// docs/superpowers/specs/2026-09-17-pre-requisitos-automaticos-de-efeitos-design.md.
/// </summary>
public sealed record EfeitoResolutionResult(
    IReadOnlyList<EfeitoRegra> ParaAutoAdicionar,
    IReadOnlyList<string>? GrupoAmbiguo);

/// <summary>
/// Resolves which prerequisite Efeitos are still missing for a target Efeito — recursively, in
/// dependency order — instead of the old behavior of simply hiding the target until its
/// prerequisites were bought manually. Pure, no I/O.
/// </summary>
public static class EfeitoPrerequisiteResolver
{
    public static EfeitoResolutionResult Resolver(
        EfeitoRegra alvo,
        IReadOnlyList<EfeitoRegra> catalogo,
        IReadOnlySet<string> nomesPresentes,
        int grauDaMagia,
        IReadOnlyDictionary<string, string> escolhasForcadas)
    {
        var paraAdicionar = new List<EfeitoRegra>();
        var jaConsiderados = new HashSet<string>(nomesPresentes);

        var grupoAmbiguo = ResolverRecursivo(alvo, catalogo, jaConsiderados, grauDaMagia, escolhasForcadas, paraAdicionar);
        return new EfeitoResolutionResult(paraAdicionar, grupoAmbiguo);
    }

    /// <summary>
    /// Chave usada em <paramref name="escolhasForcadas"/> para uma resposta já dada a um grupo
    /// ambíguo — os nomes elegíveis (já filtrados por Grau), na ordem em que aparecem no catálogo,
    /// unidos por "|". Exposta porque o chamador (AddEfeitoForm) monta a mesma chave ao guardar a
    /// resposta do jogador.
    /// </summary>
    public static string ChaveDoGrupo(IReadOnlyList<string> elegiveis) => string.Join("|", elegiveis);

    private static IReadOnlyList<string>? ResolverRecursivo(
        EfeitoRegra efeito, IReadOnlyList<EfeitoRegra> catalogo, HashSet<string> jaConsiderados,
        int grauDaMagia, IReadOnlyDictionary<string, string> escolhasForcadas, List<EfeitoRegra> paraAdicionar)
    {
        foreach (var grupo in efeito.PreRequisitos)
        {
            if (grupo.Any(jaConsiderados.Contains))
                continue;

            var elegiveis = grupo
                .Select(nome => catalogo.First(c => c.Nome == nome))
                .Where(c => c.Grau <= grauDaMagia)
                .ToList();

            if (elegiveis.Count == 0)
                return Array.Empty<string>();

            EfeitoRegra escolhido;
            if (elegiveis.Count == 1)
            {
                escolhido = elegiveis[0];
            }
            else
            {
                var nomesElegiveis = elegiveis.Select(c => c.Nome).ToList();
                if (!escolhasForcadas.TryGetValue(ChaveDoGrupo(nomesElegiveis), out var nomeEscolhido))
                    return nomesElegiveis;

                escolhido = elegiveis.First(c => c.Nome == nomeEscolhido);
            }

            var grupoAmbiguoAninhado = ResolverRecursivo(escolhido, catalogo, jaConsiderados, grauDaMagia, escolhasForcadas, paraAdicionar);
            if (grupoAmbiguoAninhado is not null)
                return grupoAmbiguoAninhado;

            jaConsiderados.Add(escolhido.Nome);
            paraAdicionar.Add(escolhido);
        }

        return null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EfeitoPrerequisiteResolverTests"`
Expected: PASS (6 tests)

- [ ] **Step 5: Run the full Unit suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Unit && dotnet build RuinaRPG.sln`
Expected: all pass, `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Domain/SpellsAndAbilities/EfeitoPrerequisiteResolver.cs tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/EfeitoPrerequisiteResolverTests.cs
git commit -m "feat: EfeitoPrerequisiteResolver (Domain, pure)"
```

---

### Task 2: Client — `EfeitoLinha` + `EfeitosTable.razor`

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Fields/EfeitoLinha.cs`
- Create: `src/RuinaRPG.Client/Shared/Fields/EfeitosTable.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/EfeitosTableTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Contracts.Rules.EfeitoResponse` (existing, fetched from `GET efeitos`),
  `RuinaRPG.Domain.SpellsAndAbilities.EfeitoCustoCalculator.Calcular` (existing).
- Produces: `EfeitoLinha` (mutable class: `EfeitoNome` (`string`), `Quantidade` (`int?`), `CustoPI`
  (`int`), plus a `(string, int?, int)` constructor), `EfeitosTable` component with parameters
  `Efeitos` (`List<EfeitoLinha>`, mutated in place), `Grau` (`int`), `OnChanged` (`EventCallback`).
  Used by Tasks 4 and 5 (the 4 pages).

This task does not depend on Task 1 or Task 3 — it only renders/edits rows it's given, regardless
of how those rows were added.

- [ ] **Step 1: Write the failing tests**

First, create the test double helper this test file needs (mirrors `AddEfeitoFormTests.cs`'s own
`FixoEfeito`/`FakeCatalogClient`, but returns full `EfeitoResponse`-shaped objects with a
caller-chosen `TipoDeCusto`). Create `tests/RuinaRPG.Tests.Client/Shared/Fields/EfeitosTableTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class EfeitosTableTests : MudBunitContext
{
    private static HttpClient FakeCatalogClient(object efeitos) => FakeHttpMessageHandler.CreateClient(request =>
        request.RequestUri!.AbsolutePath.EndsWith("efeitos")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(efeitos) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));

    private static object Efeito(string nome, string tipoDeCusto, int? custoFixo = null, int? custoPorUnidade = null,
        string? quantidadeDerivadaDeEfeito = null) => new
    {
        Id = Guid.NewGuid().ToString(), Nome = nome, Grau = 1, Descricao = "Descrição.", TipoDeCusto = tipoDeCusto,
        CustoFixo = custoFixo, CustoPorUnidade = custoPorUnidade, UnidadeLabel = (string?)null,
        QuantidadeDerivadaDeEfeito = quantidadeDerivadaDeEfeito, MaxUnidades = (int?)null, MaxEscalaPorGrau = false,
        MaxContandoAPartirDoGrau = (int?)null, CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
        PreRequisitos = Array.Empty<string[]>(),
    };

    [Fact]
    public async Task Editing_Quantidade_of_a_PorUnidade_row_recomputes_its_own_Custo()
    {
        var http = FakeCatalogClient(new[] { Efeito("Duração", "PorUnidade", custoPorUnidade: 4) });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Duração", 0, 0) };

        var cut = Render<EfeitosTable>(p => p.Add(x => x.Efeitos, efeitos).Add(x => x.Grau, 3));
        await Task.Delay(50);

        var quantidade = cut.FindComponents<MudNumericField<int?>>().Single();
        await cut.InvokeAsync(() => quantidade.Instance.ValueChanged.InvokeAsync(2));

        efeitos[0].Quantidade.Should().Be(2);
        efeitos[0].CustoPI.Should().Be(8); // 4 por unidade * 2
    }

    [Fact]
    public async Task Editing_Quantidade_of_a_row_another_row_derives_from_recomputes_the_dependent_too()
    {
        var http = FakeCatalogClient(new[]
        {
            Efeito("Dano", "PorUnidade", custoPorUnidade: 2),
            Efeito("Dreno de Vitalidade", "DerivadoDeOutroEfeito", custoPorUnidade: 2, quantidadeDerivadaDeEfeito: "Dano"),
        });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Dano", 3, 6), new("Dreno de Vitalidade", null, 6) };

        var cut = Render<EfeitosTable>(p => p.Add(x => x.Efeitos, efeitos).Add(x => x.Grau, 3));
        await Task.Delay(50);

        var quantidade = cut.FindComponents<MudNumericField<int?>>().Single(); // só "Dano" tem Quantidade editável
        await cut.InvokeAsync(() => quantidade.Instance.ValueChanged.InvokeAsync(5));

        efeitos[0].CustoPI.Should().Be(10); // 2 * 5
        efeitos[1].CustoPI.Should().Be(10); // 2 * quantidadeDerivada(5)
    }

    [Fact]
    public async Task A_Fixo_row_has_no_editable_fields()
    {
        var http = FakeCatalogClient(new[] { Efeito("Contrato Mágico", "Fixo", custoFixo: 3) });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Contrato Mágico", null, 3) };

        var cut = Render<EfeitosTable>(p => p.Add(x => x.Efeitos, efeitos).Add(x => x.Grau, 1));
        await Task.Delay(50);

        cut.FindComponents<MudNumericField<int?>>().Should().BeEmpty();
        cut.FindComponents<MudNumericField<int>>().Should().BeEmpty();
        cut.Markup.Should().Contain("—");
    }

    [Fact]
    public async Task A_Manual_row_has_an_editable_Custo_with_no_automatic_recompute()
    {
        var http = FakeCatalogClient(new[] { Efeito("Imunidade", "Manual") });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Imunidade", null, 5) };

        var cut = Render<EfeitosTable>(p => p.Add(x => x.Efeitos, efeitos).Add(x => x.Grau, 9));
        await Task.Delay(50);

        var custo = cut.FindComponents<MudNumericField<int>>().Single();
        await cut.InvokeAsync(() => custo.Instance.ValueChanged.InvokeAsync(12));

        efeitos[0].CustoPI.Should().Be(12);
    }

    [Fact]
    public async Task Removing_a_row_removes_it_from_the_bound_list_and_notifies_OnChanged()
    {
        var http = FakeCatalogClient(new[] { Efeito("Contrato Mágico", "Fixo", custoFixo: 3) });
        Services.AddScoped(_ => http);
        var efeitos = new List<EfeitoLinha> { new("Contrato Mágico", null, 3) };
        var changed = false;

        var cut = Render<EfeitosTable>(p => p
            .Add(x => x.Efeitos, efeitos)
            .Add(x => x.Grau, 1)
            .Add(x => x.OnChanged, EventCallback.Factory.Create(this, () => changed = true)));
        await Task.Delay(50);

        cut.Find("button").Click();

        efeitos.Should().BeEmpty();
        changed.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~EfeitosTableTests"`
Expected: FAIL to compile — `EfeitoLinha`/`EfeitosTable` don't exist yet.

- [ ] **Step 3: Create `EfeitoLinha`**

Create `src/RuinaRPG.Client/Shared/Fields/EfeitoLinha.cs`:

```csharp
namespace RuinaRPG.Client.Shared.Fields;

/// <summary>
/// One row of an in-progress Magia/Habilidade's Efeitos list — the shared row type behind
/// EfeitosTable, replacing 4 near-identical local classes (BancoDeMagiasForm.EffectFormModel,
/// FichaDePersonagem/FichaDeNpc/FichaDeCriatura's SpellAbilityEffectFormModel).
/// </summary>
public class EfeitoLinha
{
    public string EfeitoNome { get; set; } = "";
    public int? Quantidade { get; set; }
    public int CustoPI { get; set; }

    public EfeitoLinha() { }

    public EfeitoLinha(string efeitoNome, int? quantidade, int custoPI)
    {
        EfeitoNome = efeitoNome;
        Quantidade = quantidade;
        CustoPI = custoPI;
    }
}
```

- [ ] **Step 4: Create `EfeitosTable.razor`**

Create `src/RuinaRPG.Client/Shared/Fields/EfeitosTable.razor`:

```razor
@inject HttpClient Http
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Domain.Enums
@using RuinaRPG.Domain.SpellsAndAbilities
@using MudBlazor

<MudSimpleTable Dense="true" Hover="true" Class="mt-2">
    <thead><tr><th>Nome do Efeito</th><th>Quantidade</th><th>Custo em PI</th><th></th></tr></thead>
    <tbody>
        @for (var i = 0; i < Efeitos.Count; i++)
        {
            var linha = Efeitos[i];
            var index = i; // capture for the closure below
            var tipo = TipoDe(linha.EfeitoNome);
            <tr>
                <td>@linha.EfeitoNome</td>
                <td>
                    @if (tipo is TipoDeCusto.PorUnidade or TipoDeCusto.ManualPorUnidade)
                    {
                        <MudNumericField T="int?" Value="linha.Quantidade" ValueChanged="@(v => OnQuantidadeChangedAsync(linha, v))" Dense="true" />
                    }
                    else
                    {
                        @("—")
                    }
                </td>
                <td>
                    @if (tipo is TipoDeCusto.Manual or TipoDeCusto.ManualPorUnidade)
                    {
                        <MudNumericField T="int" Value="linha.CustoPI" ValueChanged="@(v => OnCustoManualChangedAsync(linha, v))" Dense="true" />
                    }
                    else
                    {
                        @linha.CustoPI
                    }
                </td>
                <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => RemoveAsync(index))">Remover</MudButton></td>
            </tr>
        }
    </tbody>
</MudSimpleTable>

@code {
    [Parameter, EditorRequired] public List<EfeitoLinha> Efeitos { get; set; } = null!;
    [Parameter, EditorRequired] public int Grau { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }

    // Fetch próprio, cacheado — mesmo padrão de AddEfeitoForm. O catálogo tem 51 linhas; buscar
    // duas vezes (uma por componente) é irrelevante, e evita elevar isso a estado compartilhado.
    private List<EfeitoResponse> _todos = [];

    protected override async Task OnParametersSetAsync()
    {
        if (_todos.Count == 0)
            _todos = await Http.GetFromJsonAsync<List<EfeitoResponse>>("efeitos") ?? [];
    }

    private TipoDeCusto? TipoDe(string efeitoNome)
    {
        var regra = _todos.FirstOrDefault(e => e.Nome == efeitoNome);
        return regra is not null && Enum.TryParse<TipoDeCusto>(regra.TipoDeCusto, out var tipo) ? tipo : null;
    }

    private async Task OnQuantidadeChangedAsync(EfeitoLinha linha, int? novaQuantidade)
    {
        linha.Quantidade = novaQuantidade;
        Recalcular(linha);
        RecalcularDependentes(linha.EfeitoNome);
        await NotifyChangedAsync();
    }

    private void Recalcular(EfeitoLinha linha)
    {
        var regra = _todos.FirstOrDefault(e => e.Nome == linha.EfeitoNome);
        if (regra is null || !Enum.TryParse<TipoDeCusto>(regra.TipoDeCusto, out var tipo) || tipo != TipoDeCusto.PorUnidade)
            return;

        linha.CustoPI = EfeitoCustoCalculator.Calcular(
            tipo, regra.CustoFixo, regra.CustoPorUnidade, regra.CustoAlternativo, regra.CustoAlternativoAPartirDoGrau,
            Grau, linha.Quantidade, null, null);
    }

    /// <summary>Recalcula qualquer linha cujo Custo derive (QuantidadeDerivadaDeEfeito) do Nome recém-editado.</summary>
    private void RecalcularDependentes(string nomeOrigem)
    {
        var origem = Efeitos.FirstOrDefault(e => e.EfeitoNome == nomeOrigem);
        foreach (var dependente in Efeitos)
        {
            var regraDependente = _todos.FirstOrDefault(e => e.Nome == dependente.EfeitoNome);
            if (regraDependente is null || regraDependente.QuantidadeDerivadaDeEfeito != nomeOrigem)
                continue;
            if (!Enum.TryParse<TipoDeCusto>(regraDependente.TipoDeCusto, out var tipo))
                continue;

            dependente.CustoPI = EfeitoCustoCalculator.Calcular(
                tipo, regraDependente.CustoFixo, regraDependente.CustoPorUnidade,
                regraDependente.CustoAlternativo, regraDependente.CustoAlternativoAPartirDoGrau,
                Grau, null, null, origem?.Quantidade ?? 0);
        }
    }

    private async Task OnCustoManualChangedAsync(EfeitoLinha linha, int novoCusto)
    {
        linha.CustoPI = novoCusto;
        await NotifyChangedAsync();
    }

    private async Task RemoveAsync(int index)
    {
        Efeitos.RemoveAt(index);
        await NotifyChangedAsync();
    }

    private Task NotifyChangedAsync() => OnChanged.HasDelegate ? OnChanged.InvokeAsync() : Task.CompletedTask;
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~EfeitosTableTests"`
Expected: PASS (5 tests)

- [ ] **Step 6: Run the full Client suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Client && dotnet build RuinaRPG.sln`
Expected: all pass, `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/Shared/Fields/EfeitoLinha.cs src/RuinaRPG.Client/Shared/Fields/EfeitosTable.razor tests/RuinaRPG.Tests.Client/Shared/Fields/EfeitosTableTests.cs
git commit -m "feat: EfeitoLinha + EfeitosTable — shared editable Efeitos row/table"
```

---

### Task 3: Client — `AddEfeitoForm.razor` drives the resolver, emits a batch

**Files:**
- Modify: `src/RuinaRPG.Client/Shared/Fields/AddEfeitoForm.razor`
- Modify: `tests/RuinaRPG.Tests.Client/Shared/Fields/AddEfeitoFormTests.cs`

**Interfaces:**
- Consumes: `EfeitoPrerequisiteResolver.Resolver`/`.ChaveDoGrupo` (Task 1).
- Produces: `AddEfeitoForm.OnAdicionar` changes type from `EventCallback<EfeitoAdicionadoResult>` to
  `EventCallback<List<EfeitoAdicionadoResult>>` — **breaking change**, Tasks 4 and 5 update all 4
  callers.

**A second, load-bearing fix bundled into this task** (found while designing this plan, not in the
original spec text — see rationale below): `RecalcularCusto`'s existing handling of a
`DerivadoDeOutroEfeito` Efeito (Dreno de Vitalidade/Arcana) whose source Efeito (`Dano`) isn't
present yet currently shows a blocking error and disables "Adicionar Efeito" entirely — that dead
end defeats this whole plan's goal for exactly that Efeito type, since `Dano` would never get the
chance to auto-add (the button is disabled before the resolver ever runs). Fix: treat a missing
source the same way an auto-added source starts — `quantidadeDerivada = 0` — instead of blocking.
The resolver will still auto-add `Dano` (it's `Dreno de Vitalidade`'s own listed prerequisite,
independent of this cost-preview concern), starting at `Quantidade 0`; editing it afterward via
`EfeitosTable` (Task 2's cascade recompute) brings `Dreno de Vitalidade`'s Custo up to date. The
now-unreachable `_erroDrenoOrigemAusente` field and its markup are removed as dead code.

- [ ] **Step 1: Update the failing/changing tests**

Open `tests/RuinaRPG.Tests.Client/Shared/Fields/AddEfeitoFormTests.cs`. Replace the whole file:

```csharp
using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class AddEfeitoFormTests : MudBunitContext
{
    private static HttpClient FakeCatalogClient(object efeitos)
    {
        return FakeHttpMessageHandler.CreateClient(request =>
            request.RequestUri!.AbsolutePath.EndsWith("efeitos")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(efeitos) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static object FixoEfeito(string nome, int grau, int custo, params string[][] preRequisitos) => new
    {
        Id = Guid.NewGuid().ToString(), Nome = nome, Grau = grau, Descricao = "Descrição.", TipoDeCusto = "Fixo",
        CustoFixo = custo, CustoPorUnidade = (int?)null, UnidadeLabel = (string?)null, QuantidadeDerivadaDeEfeito = (string?)null,
        MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
        CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
        PreRequisitos = preRequisitos,
    };

    private static object PorUnidadeEfeito(string nome, int grau, int custoPorUnidade, params string[][] preRequisitos) => new
    {
        Id = Guid.NewGuid().ToString(), Nome = nome, Grau = grau, Descricao = "Descrição.", TipoDeCusto = "PorUnidade",
        CustoFixo = (int?)null, CustoPorUnidade = custoPorUnidade, UnidadeLabel = "Dado", QuantidadeDerivadaDeEfeito = (string?)null,
        MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
        CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
        PreRequisitos = preRequisitos,
    };

    /// <summary>
    /// MudSelect's dropdown is a real portal: its items render only inside a MudPopoverProvider
    /// (absent by default outside MainLayout — only <see cref="AddEfeitoForm"/> is under test here)
    /// and only once the select is actually open — mirroring the same MudAutocomplete popover
    /// limitation EntityPickerTests documents for a sibling component. A bare
    /// <c>Render&lt;AddEfeitoForm&gt;()</c> therefore never shows item text in <c>cut.Markup</c>
    /// regardless of the component's filtering logic. This helper renders AddEfeitoForm alongside a
    /// MudPopoverProvider in one composite fragment (so both land in the one returned markup) and
    /// opens the dropdown by dispatching the same MouseDown MudSelect's own input listens for,
    /// instead of asserting against a closed, unpopulated popover.
    /// </summary>
    private async Task<IRenderedComponent<ContainerFragment>> RenderOpenAsync(int grau, IReadOnlyList<(string Nome, int? Quantidade)> efeitosExistentes,
        List<EfeitoAdicionadoResult>? loteCapturado = null)
    {
        var root = Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<AddEfeitoForm>(1);
            builder.AddAttribute(2, nameof(AddEfeitoForm.Grau), grau);
            builder.AddAttribute(3, nameof(AddEfeitoForm.EfeitosExistentes), efeitosExistentes);
            if (loteCapturado is not null)
            {
                builder.AddAttribute(4, nameof(AddEfeitoForm.OnAdicionar),
                    EventCallback.Factory.Create<List<EfeitoAdicionadoResult>>(this, r => loteCapturado.AddRange(r)));
            }
            builder.CloseComponent();
        });
        await Task.Delay(50); // let the fake HTTP fetch + OnParametersSetAsync settle before opening
        root.Find(".mud-input-control").MouseDown();
        return root;
    }

    private static void Selecionar(IRenderedComponent<ContainerFragment> cut, string nome)
    {
        cut.WaitForAssertion(() => cut.Markup.Should().Contain(nome));
        cut.FindAll(".mud-list-item").First(li => li.TextContent.Trim() == nome).Click();
    }

    [Fact]
    public async Task Only_effects_whose_Grau_is_at_or_below_the_parents_Grau_are_offered()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Contrato Mágico", 1, 3),
            FixoEfeito("Atordoamento", 8, 10, ["Duração"]),
        });
        Services.AddScoped(_ => http);

        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>());

        cut.Markup.Should().Contain("Contrato Mágico");
        cut.Markup.Should().NotContain("Atordoamento");
    }

    [Fact]
    public async Task Every_Grau_eligible_effect_is_offered_regardless_of_unmet_prerequisites()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Duração", 1, 0),
            FixoEfeito("Cura", 1, 2, ["Dano"]),
        });
        Services.AddScoped(_ => http);

        // Nem "Dano" nem "Duração" estão presentes — Cura era escondida antes desta mudança.
        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>());

        cut.Markup.Should().Contain("Cura");
    }

    [Fact]
    public async Task Confirming_an_effect_without_its_prerequisite_present_auto_adds_the_prerequisite_first()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Duração", 1, 0),
            PorUnidadeEfeito("Aumentar Armadura", 1, 2, ["Duração"]),
        });
        Services.AddScoped(_ => http);
        var lote = new List<EfeitoAdicionadoResult>();

        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>(), lote);
        Selecionar(cut, "Aumentar Armadura");

        var quantidade = cut.FindComponents<MudNumericField<int?>>().Single();
        await cut.InvokeAsync(() => quantidade.Instance.ValueChanged.InvokeAsync(3));

        cut.Find("button:contains('Adicionar Efeito')").Click();

        lote.Should().HaveCount(2);
        lote[0].Should().Be(new EfeitoAdicionadoResult("Duração", 0, 0));
        lote[1].Should().Be(new EfeitoAdicionadoResult("Aumentar Armadura", 3, 6));
    }

    [Fact]
    public async Task Confirming_a_Fixo_prerequisite_target_auto_adds_it_already_fully_resolved()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Dano", 1, 2),
            FixoEfeito("Cura", 1, 2, ["Dano"]),
        });
        Services.AddScoped(_ => http);
        var lote = new List<EfeitoAdicionadoResult>();

        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>(), lote);
        Selecionar(cut, "Cura");

        cut.Find("button:contains('Adicionar Efeito')").Click();

        lote.Should().HaveCount(2);
        lote[0].Should().Be(new EfeitoAdicionadoResult("Dano", null, 2));
        lote[1].Should().Be(new EfeitoAdicionadoResult("Cura", null, 2));
    }

    [Fact]
    public async Task Confirming_an_effect_whose_prerequisite_group_has_multiple_candidates_prompts_a_choice()
    {
        var http = FakeCatalogClient(new[]
        {
            FixoEfeito("Duração", 1, 0),
            FixoEfeito("Congelar", 1, 4, ["Duração"]),
            FixoEfeito("Enraizar", 1, 2, ["Duração"]),
            FixoEfeito("Detrito", 1, 2, ["Duração"], ["Congelar", "Enraizar"]),
        });
        Services.AddScoped(_ => http);
        var lote = new List<EfeitoAdicionadoResult>();

        var cut = await RenderOpenAsync(1, Array.Empty<(string, int?)>(), lote);
        Selecionar(cut, "Detrito");

        cut.Find("button:contains('Adicionar Efeito')").Click();

        cut.Markup.Should().Contain("Congelar");
        cut.Markup.Should().Contain("Enraizar");
        lote.Should().BeEmpty("a escolha ainda não foi feita — nada deve ser emitido ainda");

        cut.Find("button:contains('Congelar')").Click();

        lote.Should().HaveCount(3);
        lote[0].Should().Be(new EfeitoAdicionadoResult("Duração", 0, 0));
        lote[1].Should().Be(new EfeitoAdicionadoResult("Congelar", null, 4));
        lote[2].Should().Be(new EfeitoAdicionadoResult("Detrito", null, 2));
    }

    [Fact]
    public async Task Selecting_a_DerivadoDeOutroEfeito_effect_without_its_source_present_computes_a_zero_cost_instead_of_blocking()
    {
        var http = FakeCatalogClient(new[]
        {
            new
            {
                Id = Guid.NewGuid().ToString(), Nome = "Dreno de Vitalidade", Grau = 3, Descricao = "Descrição.", TipoDeCusto = "DerivadoDeOutroEfeito",
                CustoFixo = (int?)null, CustoPorUnidade = (int?)2, UnidadeLabel = "Dado", QuantidadeDerivadaDeEfeito = "Dano",
                MaxUnidades = (int?)null, MaxEscalaPorGrau = false, MaxContandoAPartirDoGrau = (int?)null,
                CustoAlternativo = (int?)null, CustoAlternativoAPartirDoGrau = (int?)null,
                PreRequisitos = new[] { new[] { "Dano" } },
            },
            PorUnidadeEfeito("Dano", 1, 2),
        });
        Services.AddScoped(_ => http);
        var lote = new List<EfeitoAdicionadoResult>();

        var cut = await RenderOpenAsync(3, Array.Empty<(string, int?)>(), lote);
        Selecionar(cut, "Dreno de Vitalidade");

        cut.Markup.Should().Contain("Custo em PI: 0");

        cut.Find("button:contains('Adicionar Efeito')").Click();

        lote.Should().HaveCount(2);
        lote[0].Should().Be(new EfeitoAdicionadoResult("Dano", 0, 0));
        lote[1].Should().Be(new EfeitoAdicionadoResult("Dreno de Vitalidade", null, 0));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~AddEfeitoFormTests"`
Expected: FAIL to compile (`AddEfeitoForm.OnAdicionar` is still `EventCallback<EfeitoAdicionadoResult>`)
and/or fail at runtime (old prerequisite-filtering behavior still hides Cura, Aumentar Armadura, etc.)

- [ ] **Step 3: Rewrite `AddEfeitoForm.razor`**

Replace the whole file `src/RuinaRPG.Client/Shared/Fields/AddEfeitoForm.razor`:

```razor
@inject HttpClient Http
@using MudBlazor
@using RuinaRPG.Contracts.Rules
@using RuinaRPG.Domain.Enums
@using RuinaRPG.Domain.SpellsAndAbilities

<MudSelect T="string" Value="@_efeitoNomeSelecionado" ValueChanged="OnEfeitoSelecionado" Label="Efeito">
    @foreach (var efeito in _disponiveis)
    {
        <MudSelectItem Value="@efeito.Nome">@efeito.Nome</MudSelectItem>
    }
</MudSelect>

@if (_grupoAmbiguo is not null)
{
    <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mt-1">
        "@_selecionado?.Nome" exige um destes já presente na mesma Magia/Habilidade — escolha um:
    </MudText>
    @foreach (var nome in _grupoAmbiguo)
    {
        <MudButton Variant="Variant.Outlined" Size="Size.Small" Class="mt-1 mr-2" OnClick="@(() => EscolherAmbiguoAsync(nome))">@nome</MudButton>
    }
}
else if (_selecionado is not null)
{
    <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mt-1">@_selecionado.Descricao</MudText>

    @if (_selecionado.TipoDeCusto is nameof(TipoDeCusto.PorUnidade) or nameof(TipoDeCusto.ManualPorUnidade))
    {
        <MudNumericField T="int?" @bind-Value="_quantidade" @bind-Value:after="RecalcularCusto" Label="@($"Quantidade ({_selecionado.UnidadeLabel})")" Class="mt-2" />
    }

    @if (_selecionado.TipoDeCusto is nameof(TipoDeCusto.Manual))
    {
        <MudNumericField T="int?" @bind-Value="_custoManual" @bind-Value:after="RecalcularCusto" Label="Custo em PI (definido pelo Mestre)" Class="mt-2" />
    }
    @if (_selecionado.TipoDeCusto is nameof(TipoDeCusto.ManualPorUnidade))
    {
        <MudNumericField T="int?" @bind-Value="_custoManual" @bind-Value:after="RecalcularCusto" Label="@($"Custo por {_selecionado.UnidadeLabel} (definido pelo Mestre)")" Class="mt-2" />
    }

    @if (_custoCalculado is not null)
    {
        <MudText Class="mt-2">Custo em PI: @_custoCalculado</MudText>
    }

    <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" Class="mt-2"
               Disabled="@(_custoCalculado is null)" OnClick="ConfirmarAsync">
        Adicionar Efeito
    </MudButton>
}

@code {
    [Parameter, EditorRequired] public int Grau { get; set; }
    [Parameter, EditorRequired] public IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentes { get; set; } = [];
    [Parameter] public EventCallback<List<EfeitoAdicionadoResult>> OnAdicionar { get; set; }

    private List<EfeitoResponse> _todos = [];
    private List<EfeitoResponse> _disponiveis = [];
    private string? _efeitoNomeSelecionado;
    private EfeitoResponse? _selecionado;
    private int? _quantidade;
    private int? _custoManual;
    // Recomputed explicitly on every input change (RecalcularCusto), never as a side effect of
    // reading it — a computed property that mutates state on every getter call is fragile and
    // gets read multiple times per render (the @if check, the display, the button's Disabled).
    private int? _custoCalculado;
    private IReadOnlyList<string>? _grupoAmbiguo;
    private readonly Dictionary<string, string> _escolhasForcadas = new();

    protected override async Task OnParametersSetAsync()
    {
        if (_todos.Count == 0)
            _todos = await Http.GetFromJsonAsync<List<EfeitoResponse>>("efeitos") ?? [];

        // Não filtra mais por pré-requisito satisfeito — ver
        // docs/superpowers/specs/2026-09-17-pre-requisitos-automaticos-de-efeitos-design.md: um
        // Efeito cujo pré-requisito falta é auto-resolvido ao confirmar (ResolverEAdicionarAsync),
        // não escondido do dropdown.
        _disponiveis = _todos
            .Where(e => e.Grau <= Grau)
            .OrderBy(e => e.Grau).ThenBy(e => e.Nome)
            .ToList();
    }

    private void OnEfeitoSelecionado(string nome)
    {
        _efeitoNomeSelecionado = nome;
        _selecionado = _disponiveis.FirstOrDefault(e => e.Nome == nome);
        _quantidade = null;
        _custoManual = null;
        _grupoAmbiguo = null;
        _escolhasForcadas.Clear();
        RecalcularCusto();
    }

    private void RecalcularCusto()
    {
        _custoCalculado = null;

        if (_selecionado is null)
            return;

        if (!Enum.TryParse<TipoDeCusto>(_selecionado.TipoDeCusto, out var tipo))
            return;

        int? quantidadeDerivada = null;
        if (_selecionado.QuantidadeDerivadaDeEfeito is { } origemNome)
        {
            // A origem pode ainda não estar presente — o resolver de pré-requisitos garante que
            // ela será auto-adicionada ao confirmar (ver ResolverEAdicionarAsync), então uma
            // origem ausente aqui vale Quantidade 0, não é mais um erro bloqueante.
            var origem = EfeitosExistentes.FirstOrDefault(e => e.Nome == origemNome);
            quantidadeDerivada = origem.Quantidade ?? 0;
        }

        if (tipo is TipoDeCusto.Manual or TipoDeCusto.ManualPorUnidade && _custoManual is null)
            return;
        if (tipo is TipoDeCusto.PorUnidade or TipoDeCusto.ManualPorUnidade && _quantidade is null)
            return; // quantidade opcional só quando o tipo não a exige; aqui exige e ainda não foi preenchida

        _custoCalculado = EfeitoCustoCalculator.Calcular(
            tipo, _selecionado.CustoFixo, _selecionado.CustoPorUnidade,
            _selecionado.CustoAlternativo, _selecionado.CustoAlternativoAPartirDoGrau,
            Grau, _quantidade, _custoManual, quantidadeDerivada);
    }

    private Task ConfirmarAsync() => ResolverEAdicionarAsync();

    private Task EscolherAmbiguoAsync(string nome)
    {
        _escolhasForcadas[EfeitoPrerequisiteResolver.ChaveDoGrupo(_grupoAmbiguo!)] = nome;
        return ResolverEAdicionarAsync();
    }

    private async Task ResolverEAdicionarAsync()
    {
        if (_selecionado is null || _custoCalculado is not { } custo)
            return;

        var catalogoRegras = _todos.Select(ToRegra).ToList();
        var alvoRegra = ToRegra(_selecionado);
        var nomesPresentes = EfeitosExistentes.Select(e => e.Nome).ToHashSet();

        var resultado = EfeitoPrerequisiteResolver.Resolver(alvoRegra, catalogoRegras, nomesPresentes, Grau, _escolhasForcadas);
        if (resultado.GrupoAmbiguo is not null)
        {
            _grupoAmbiguo = resultado.GrupoAmbiguo;
            return;
        }

        var lote = new List<EfeitoAdicionadoResult>();
        foreach (var regra in resultado.ParaAutoAdicionar)
        {
            var (quantidadeInicial, custoInicial) = regra.TipoDeCusto == TipoDeCusto.Fixo
                ? ((int?)null, regra.CustoFixo!.Value)
                : ((int?)0, 0);
            lote.Add(new EfeitoAdicionadoResult(regra.Nome, quantidadeInicial, custoInicial));
        }
        lote.Add(new EfeitoAdicionadoResult(_selecionado.Nome, _quantidade, custo));

        await OnAdicionar.InvokeAsync(lote);

        _efeitoNomeSelecionado = null;
        _selecionado = null;
        _quantidade = null;
        _custoManual = null;
        _custoCalculado = null;
        _grupoAmbiguo = null;
        _escolhasForcadas.Clear();
    }

    private static EfeitoRegra ToRegra(EfeitoResponse e) => new(
        e.Nome, e.Grau, Enum.Parse<TipoDeCusto>(e.TipoDeCusto), e.CustoFixo, e.CustoPorUnidade,
        e.QuantidadeDerivadaDeEfeito, e.MaxUnidades, e.MaxEscalaPorGrau, e.MaxContandoAPartirDoGrau,
        e.CustoAlternativo, e.CustoAlternativoAPartirDoGrau, e.PreRequisitos);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~AddEfeitoFormTests"`
Expected: PASS (7 tests)

- [ ] **Step 5: Update the 4 call sites' handler signature (mechanical only — not the EfeitosTable swap, that's Tasks 4/5)**

`OnAdicionar`'s type change alone breaks every page that passes it a handler — fix the signature at
all 4 call sites in this same task, so the solution stays buildable after every task. This step
does **not** introduce `EfeitosTable`/`EfeitoLinha` yet (that's Tasks 4/5); it keeps each page's
existing `EffectFormModel`/`SpellAbilityEffectFormModel` class exactly as it is today, just adapts
the handler to receive a batch instead of one row.

In `src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`, find:

```csharp
    private Task OnEfeitoAdicionadoAsync(RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult resultado)
    {
        _form.Efeitos.Add(new EffectFormModel { EfeitoNome = resultado.EfeitoNome, Quantidade = resultado.Quantidade, CustoPI = resultado.CustoPI });
        return NotifySavedAsync();
    }
```

Replace with:

```csharp
    private Task OnEfeitoAdicionadoAsync(List<RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult> resultados)
    {
        _form.Efeitos.AddRange(resultados.Select(r => new EffectFormModel { EfeitoNome = r.EfeitoNome, Quantidade = r.Quantidade, CustoPI = r.CustoPI }));
        return NotifySavedAsync();
    }
```

In each of `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`, `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`,
`src/RuinaRPG.Client/Pages/FichaDeCriatura.razor` (identical edit, all 3), find:

```csharp
    private void OnEfeitoAdicionado(RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult resultado) =>
        _spellAbilityForm.Efeitos.Add(new SpellAbilityEffectFormModel { EfeitoNome = resultado.EfeitoNome, Quantidade = resultado.Quantidade, CustoPI = resultado.CustoPI });
```

Replace with:

```csharp
    private void OnEfeitoAdicionado(List<RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult> resultados) =>
        _spellAbilityForm.Efeitos.AddRange(resultados.Select(r => new SpellAbilityEffectFormModel { EfeitoNome = r.EfeitoNome, Quantidade = r.Quantidade, CustoPI = r.CustoPI }));
```

- [ ] **Step 6: Clean-rebuild and run the full Unit + Client suites**

Run: `dotnet build RuinaRPG.sln && dotnet test tests/RuinaRPG.Tests.Unit && dotnet test tests/RuinaRPG.Tests.Client`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`, all tests pass — the whole solution is green
again, exactly like every other task boundary in this plan.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/Shared/Fields/AddEfeitoForm.razor tests/RuinaRPG.Tests.Client/Shared/Fields/AddEfeitoFormTests.cs src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor src/RuinaRPG.Client/Pages/FichaDeCriatura.razor
git commit -m "feat: AddEfeitoForm auto-resolves missing prerequisites and emits a batch"
```

---

### Task 4: Client — wire `EfeitosTable`/`EfeitoLinha` into `BancoDeMagiasForm.razor`

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`
- Modify: `tests/RuinaRPG.Tests.Client/Pages/BancoDeMagiasFormTests.cs` (only if it references
  `EffectFormModel` or the old single-effect `OnAdicionar` shape — check before assuming; read the
  file first)

**Interfaces:**
- Consumes: `EfeitosTable` (Task 2), `EfeitoLinha` (Task 2). `AddEfeitoForm`'s `OnAdicionar` is
  already `EventCallback<List<EfeitoAdicionadoResult>>` and this page's handler already accepts a
  batch (both from Task 3) — this task only swaps the read-only table and the row model, the
  handler signature itself doesn't change again.
- Produces: nothing new.

- [ ] **Step 1: Check whether the existing page test references the old shape**

Run: `grep -n "EffectFormModel\|OnAdicionar\|EfeitoAdicionadoResult" tests/RuinaRPG.Tests.Client/Pages/BancoDeMagiasFormTests.cs`

If nothing matches, no test file changes are needed for this task — skip to Step 2. If something
matches, read the surrounding test and update it to construct/expect `EfeitoLinha`
instead of the old model, following the same rename pattern as `AddEfeitoFormTests.cs` in Task 3.

- [ ] **Step 2: Edit `BancoDeMagiasForm.razor`'s markup**

In `src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`, find:

```razor
    <Section Title="Efeitos">
        <AddEfeitoForm Grau="_form.Grau" EfeitosExistentes="EfeitosExistentesParaPicker()" OnAdicionar="OnEfeitoAdicionadoAsync" />
        <MudSimpleTable Dense="true" Hover="true" Class="mt-2">
            <thead><tr><th>Nome do Efeito</th><th>Quantidade</th><th>Custo em PI</th><th></th></tr></thead>
            <tbody>
                @for (var i = 0; i < _form.Efeitos.Count; i++)
                {
                    var index = i; // capture for the closure below
                    <tr>
                        <td>@_form.Efeitos[index].EfeitoNome</td>
                        <td>@_form.Efeitos[index].Quantidade</td>
                        <td>@_form.Efeitos[index].CustoPI</td>
                        <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => RemoveEffect(index))">Remover</MudButton></td>
                    </tr>
                }
            </tbody>
        </MudSimpleTable>

        <MudText Class="mt-3">Gasto em PI (calculado ao salvar): @GastoEmPI</MudText>
        <MudText>Custo em Foco (calculado ao salvar): @SpellAbilityCostCalculator.Custo(GastoEmPI)</MudText>
    </Section>
```

Replace with:

```razor
    <Section Title="Efeitos">
        <AddEfeitoForm Grau="_form.Grau" EfeitosExistentes="EfeitosExistentesParaPicker()" OnAdicionar="OnEfeitoAdicionadoAsync" />
        <EfeitosTable Efeitos="_form.Efeitos" Grau="_form.Grau" OnChanged="NotifySavedAsync" />

        <MudText Class="mt-3">Gasto em PI (calculado ao salvar): @GastoEmPI</MudText>
        <MudText>Custo em Foco (calculado ao salvar): @SpellAbilityCostCalculator.Custo(GastoEmPI)</MudText>
    </Section>
```

- [ ] **Step 3: Edit `BancoDeMagiasForm.razor`'s `@code` block**

Find:

```csharp
    private IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentesParaPicker() =>
        _form.Efeitos.Select(e => (e.EfeitoNome, e.Quantidade)).ToList();

    private Task OnEfeitoAdicionadoAsync(List<RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult> resultados)
    {
        _form.Efeitos.AddRange(resultados.Select(r => new EffectFormModel { EfeitoNome = r.EfeitoNome, Quantidade = r.Quantidade, CustoPI = r.CustoPI }));
        return NotifySavedAsync();
    }

    private void RemoveEffect(int index)
    {
        _form.Efeitos.RemoveAt(index);
        NotifySavedAsync();
    }
```

Replace with:

```csharp
    private IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentesParaPicker() =>
        _form.Efeitos.Select(e => (e.EfeitoNome, e.Quantidade)).ToList();

    private Task OnEfeitoAdicionadoAsync(List<RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult> resultados)
    {
        _form.Efeitos.AddRange(resultados.Select(r => new RuinaRPG.Client.Shared.Fields.EfeitoLinha(r.EfeitoNome, r.Quantidade, r.CustoPI)));
        return NotifySavedAsync();
    }
```

(`RemoveEffect` is deleted — `EfeitosTable` now owns row removal.)

- [ ] **Step 4: Update `OnInitializedAsync`'s mapping and `EntryFormModel`/`EffectFormModel`**

Find:

```csharp
        _form.Efeitos = existing.Efeitos
            .Select(e => new EffectFormModel { EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI })
            .ToList();
```

Replace with:

```csharp
        _form.Efeitos = existing.Efeitos
            .Select(e => new RuinaRPG.Client.Shared.Fields.EfeitoLinha(e.EfeitoNome, e.Quantidade, e.CustoPI))
            .ToList();
```

Find (near the end of the file):

```csharp
    private class EntryFormModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Nome { get; set; } = "";
        public string Tipo { get; set; } = "Magia";
        [Range(0, int.MaxValue)]
        public int Grau { get; set; }
        public string Descricao { get; set; } = "";
        public List<EffectFormModel> Efeitos { get; set; } = new();
    }

    private class EffectFormModel
    {
        public string EfeitoNome { get; set; } = "";
        public int? Quantidade { get; set; }
        [Range(0, int.MaxValue)]
        public int CustoPI { get; set; }
    }
}
```

Replace with:

```csharp
    private class EntryFormModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Nome { get; set; } = "";
        public string Tipo { get; set; } = "Magia";
        [Range(0, int.MaxValue)]
        public int Grau { get; set; }
        public string Descricao { get; set; } = "";
        public List<RuinaRPG.Client.Shared.Fields.EfeitoLinha> Efeitos { get; set; } = new();
    }
}
```

(`_form.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI))` in
`CreateAsync`/`SaveIfValidAsync` needs no change — `EfeitoLinha` has the same 3 property names as
the old `EffectFormModel`.)

- [ ] **Step 5: Clean-rebuild**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` — the whole solution, including the 3 Fichas
(untouched by this task, still on their old read-only table — that's Task 5's scope, and they
already build fine against Task 3's batch-based `OnAdicionar` handler).

- [ ] **Step 6: Run the Client suite**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~BancoDeMagiasFormTests|FullyQualifiedName~AddEfeitoFormTests|FullyQualifiedName~EfeitosTableTests"`
Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor
git commit -m "feat: wire EfeitosTable into BancoDeMagiasForm"
```

(Add the test file too, with `git add tests/RuinaRPG.Tests.Client/Pages/BancoDeMagiasFormTests.cs`,
only if Step 1 found it needed changes.)

---

### Task 5: Client — wire `EfeitosTable`/`EfeitoLinha` into the 3 Fichas

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeCriatura.razor`

**Interfaces:**
- Consumes: `EfeitosTable`/`EfeitoLinha` (Task 2). Same as Task 4: `AddEfeitoForm.OnAdicionar` and
  each page's `OnEfeitoAdicionado` handler are already batch-based (Task 3) — this task only swaps
  the read-only table and the row model.
- Produces: nothing new.

These 3 files have byte-identical markup/code for this feature (verified while writing this plan —
`FichaDePersonagem.razor`, `FichaDeNpc.razor`, and `FichaDeCriatura.razor` all have the exact same
`<AddEfeitoForm>`/`<MudSimpleTable>` block and the exact same `EfeitosExistentesParaPicker`/
`OnEfeitoAdicionado`/`SpellAbilityEffectFormModel` shape). Apply the identical edit to all 3.

- [ ] **Step 1: Edit the markup in all 3 files**

In each of `FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor`, find:

```razor
                    <MudText Typo="Typo.h6" Class="mt-3">Efeitos</MudText>
                    <AddEfeitoForm Grau="_spellAbilityForm.Grau" EfeitosExistentes="EfeitosExistentesParaPicker()" OnAdicionar="OnEfeitoAdicionado" />
                    <MudSimpleTable Dense="true" Hover="true" Class="mt-2">
                        <thead><tr><th>Nome do Efeito</th><th>Quantidade</th><th>Custo em PI</th><th></th></tr></thead>
                        <tbody>
                            @for (var i = 0; i < _spellAbilityForm.Efeitos.Count; i++)
                            {
                                var index = i; // capture for the closure below
                                <tr>
                                    <td>@_spellAbilityForm.Efeitos[index].EfeitoNome</td>
                                    <td>@_spellAbilityForm.Efeitos[index].Quantidade</td>
                                    <td>@_spellAbilityForm.Efeitos[index].CustoPI</td>
                                    <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => _spellAbilityForm.Efeitos.RemoveAt(index))">Remover</MudButton></td>
                                </tr>
                            }
                        </tbody>
                    </MudSimpleTable>
```

Replace with:

```razor
                    <MudText Typo="Typo.h6" Class="mt-3">Efeitos</MudText>
                    <AddEfeitoForm Grau="_spellAbilityForm.Grau" EfeitosExistentes="EfeitosExistentesParaPicker()" OnAdicionar="OnEfeitoAdicionado" />
                    <EfeitosTable Efeitos="_spellAbilityForm.Efeitos" Grau="_spellAbilityForm.Grau" />
```

(No `OnChanged` wired here — unlike `BancoDeMagiasForm`, these 3 pages' Efeitos list only gets
persisted when "Adicionar Magia/Habilidade" is submitted, same as before this plan; there's no
autosave mid-edit to notify.)

- [ ] **Step 2: Edit the `@code` block in all 3 files**

In each of the 3 files, find:

```csharp
    private IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentesParaPicker() =>
        _spellAbilityForm.Efeitos.Select(e => (e.EfeitoNome, e.Quantidade)).ToList();

    private void OnEfeitoAdicionado(List<RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult> resultados) =>
        _spellAbilityForm.Efeitos.AddRange(resultados.Select(r => new SpellAbilityEffectFormModel { EfeitoNome = r.EfeitoNome, Quantidade = r.Quantidade, CustoPI = r.CustoPI }));
```

Replace with:

```csharp
    private IReadOnlyList<(string Nome, int? Quantidade)> EfeitosExistentesParaPicker() =>
        _spellAbilityForm.Efeitos.Select(e => (e.EfeitoNome, e.Quantidade)).ToList();

    private void OnEfeitoAdicionado(List<RuinaRPG.Client.Shared.Fields.EfeitoAdicionadoResult> resultados) =>
        _spellAbilityForm.Efeitos.AddRange(resultados.Select(r => new RuinaRPG.Client.Shared.Fields.EfeitoLinha(r.EfeitoNome, r.Quantidade, r.CustoPI)));
```

- [ ] **Step 3: Replace `SpellAbilityEffectFormModel` with `EfeitoLinha` in all 3 files**

In each of the 3 files, find (the field declaration inside `SpellAbilityFormModel`):

```csharp
        public List<SpellAbilityEffectFormModel> Efeitos { get; set; } = new();
```

Replace with:

```csharp
        public List<RuinaRPG.Client.Shared.Fields.EfeitoLinha> Efeitos { get; set; } = new();
```

Then find and delete the now-unused local class (identical in all 3 files):

```csharp
    private class SpellAbilityEffectFormModel
    {
        public string EfeitoNome { get; set; } = "";
        public int? Quantidade { get; set; }
        public int CustoPI { get; set; }
    }
```

- [ ] **Step 4: Clean-rebuild**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` — the whole solution is green again.

- [ ] **Step 5: Run the full Client suite**

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: all pass.

- [ ] **Step 6: Run the full Unit suite (sanity — this task touched no Domain code)**

Run: `dotnet test tests/RuinaRPG.Tests.Unit`
Expected: all pass, unaffected.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor src/RuinaRPG.Client/Pages/FichaDeCriatura.razor
git commit -m "feat: wire EfeitosTable into the 3 Fichas (Personagem/NPC/Criatura)"
```

---

### Task 6: Docs — update `Requisitos - Ficha de Personagem.md`

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md`

**Interfaces:** None — documentation only, no code dependency on any other task. Can run any time,
placed last so it accurately describes the finished behavior.

- [ ] **Step 1: Update the Efeitos field description**

In `Docs/Requisitos/Requisitos - Ficha de Personagem.md`, find (inside the 4.b "Magias e
Habilidades" bullet list, the `*Efeitos*` line):

```
- *Efeitos*: lista dos Efeitos comprados para essa entrada — os 3 Efeitos Básicos universais (**Dano**, **Alcance**, **Duração**, cada um comprado em unidades até o teto do Grau escolhido, ver tabela no topo de "[[GRAUS & CÍRCULOS]]") e quaisquer Efeitos Especiais nomeados disponíveis até aquele Grau (ex: Aumentar Armadura, Cura, Deslocamento — ver "[[GRAUS & CÍRCULOS]]"). Alguns Efeitos Especiais exigem a compra prévia de outro Efeito (ex: "obrigatória a compra de Duração") — a interface deve impedir a compra de um Efeito cujo pré-requisito não foi comprado.
```

Replace with:

```
- *Efeitos*: lista dos Efeitos comprados para essa entrada — os 3 Efeitos Básicos universais (**Dano**, **Alcance**, **Duração**, cada um comprado em unidades até o teto do Grau escolhido, ver tabela no topo de "[[GRAUS & CÍRCULOS]]") e quaisquer Efeitos Especiais nomeados disponíveis até aquele Grau (ex: Aumentar Armadura, Cura, Deslocamento — ver "[[GRAUS & CÍRCULOS]]"). Alguns Efeitos Especiais exigem a compra prévia de outro Efeito (ex: "obrigatória a compra de Duração"); ao comprar um Efeito cujo pré-requisito ainda não foi comprado, a interface adiciona esse pré-requisito automaticamente (com Quantidade zerada, editável em seguida) em vez de impedir a compra — se o grupo de pré-requisito tiver mais de uma opção válida (ex: Detrito, que aceita Atordoamento, Congelar ou Enraizar), a interface pergunta qual delas usar. Toda linha de Efeito já adicionada — automática ou não — permanece editável (Quantidade e, quando o tipo de custo for manual, o próprio Custo em PI).
```

- [ ] **Step 2: Commit**

```bash
git add "Docs/Requisitos/Requisitos - Ficha de Personagem.md"
git commit -m "docs: describe automatic Efeito prerequisite resolution in Ficha de Personagem"
```
