# Habilidades Passivas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new Magia/Habilidade kind **Passiva** (Nome, Descrição, Categoria + Requisitos), authored only in the GM's Banco de Magias, added to Personagem/NPC/Criatura sheets only from the Banco and only when the sheet meets the Requisitos, shown in a new "Habilidades Passivas" section.

**Architecture:** `SpellAbilityTipo.Passiva` reuses the existing Banco/sheet spell-ability tables and all their campaign-attachment/grant machinery; two new nullable columns (`Categoria` int, `Requisitos` jsonb) carry the Passiva-specific data. A pure-domain `PassivaRequisitosEvaluator` compares a `RequisitosDePassiva` against a `FichaParaRequisitos` snapshot built per sheet type by new Api-layer "stats" services (which also absorb the existing `/sub-attributes` computation so formulas aren't duplicated).

**Tech Stack:** .NET 8, ASP.NET Core Web API, EF Core 8 + Npgsql (PostgreSQL jsonb), Blazor WebAssembly + MudBlazor, xUnit + FluentAssertions + bUnit + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-28-habilidades-passivas-design.md`

## Global Constraints

- `dotnet build` must end with **0 warnings, 0 errors**.
- TDD is mandatory (Técnico R0011): failing test first, then minimum code.
- `SpellAbilityTipo.Passiva` is appended **at the end** of the enum (stored as int). New enums are also stored as ints.
- NULL requisito = not a requirement. All non-null requisitos must be met (AND).
- The requisitos block applies to **everyone, GM included**.
- Criatura: requisitos on Vocação, Classe, Linhagem, Variante, Grau/Círculo, Coração de Mana, Estrela, Histórico, and on the attributes Instinto/Vontade/Influência, and on Perícias the Criatura doesn't have, are **ignored**.
- Personagem/NPC with a field present but empty (e.g. Vocação not chosen) **fails** that requisito.
- Passivas are never built from scratch on a sheet: 400 `"Passivas só são cadastradas no Banco de Magias e Habilidades."`
- Unmet requisitos on add: 400 `"Requisitos não cumpridos: " + string.Join(", ", pendencias)`.
- Categoria labels: `Livre` → "Passiva Livre", `Vocacional` → "Passiva Vocacional", `DeClasse` → "Passiva de Classe".
- Sub-Atributo requisitos: exactly Iniciativa, Movimentação, Esquiva Natural, Defesa Natural, Redução Física, Redução Mágica.
- All user-facing text in Brazilian Portuguese. Code comments follow the file's existing language.
- `Docs/` edits: Obsidian `[[wikilinks]]`, continue R-number sequences, never renumber.
- Enums cross the wire as strings (`ToString()` / `Enum.TryParse`), like every existing contract.

## Review Focus

- A Passiva with `Requisitos = null` or an all-empty requisitos object is addable to any sheet — Task 1 unit test + Task 5 integration test.
- A player trying to add a Passiva that is **not** public in the campaign gets the existing `"Entrada do banco não encontrada."` 400, never the requisitos list (no leak of a private entry's requirements) — Task 5 test.
- Switching a Banco entry between Passiva and Magia/Habilidade in the edit form: the client must send `Categoria`/`Requisitos` = null for non-Passivas and `Grau = 0`, `Efeitos = []` for Passivas, and the server rejects the mismatched combos — Task 3 tests + Task 6 bUnit test.
- A Histórico referenced by a requisito is deleted from the catalog: the pendência reads `"Histórico: (removido)"` and nothing throws — Task 1 unit test.
- Granting an NPC/Criatura sheet that holds a Passiva copies Categoria and Requisitos onto the granted copy — Task 5 test.

---

## File Structure

**Domain** (`src/RuinaRPG.Domain/SpellsAndAbilities/`)
- Modify `SpellAbilityTipo.cs` — add `Passiva`.
- Create `CategoriaDePassiva.cs`, `SubAtributo.cs`, `RequisitosDePassiva.cs` (the record + 3 item records), `FichaParaRequisitos.cs`, `RequisitoLabels.cs`, `PassivaRequisitosEvaluator.cs`.

**Infrastructure**
- Modify `SpellsAndAbilities/SpellAbilityBankEntry.cs`, `CharacterSheets/CharacterSpellAbility.cs`, `NpcSheets/NpcSpellAbility.cs`, `CreatureSheets/CreatureSpellAbility.cs` — add `Categoria`, `Requisitos`.
- Create `Persistence/RequisitosDePassivaJson.cs` — value converter + comparer.
- Modify `Persistence/RuinaRpgDbContext.cs` — map the two columns on 4 entities.
- Create migration `AddHabilidadesPassivas`.

**Contracts**
- Create `SpellsAndAbilities/RequisitosDePassivaDto.cs`, `SpellsAndAbilities/PassivaDisponivelResponse.cs`.
- Modify `CreateSpellAbilityEntryRequest.cs`, `UpdateSpellAbilityEntryRequest.cs`, `SpellAbilityEntryResponse.cs`, `CharacterSheets/CharacterSpellAbilityResponse.cs`, `NpcSheets/NpcSpellAbilityResponse.cs`, `CreatureSheets/CreatureSpellAbilityResponse.cs` — trailing optional params.

**Api**
- Create `Controllers/RequisitosDePassivaMapper.cs` — DTO ↔ domain + validation.
- Create `Services/CharacterSheetStats.cs`, `Services/NpcSheetStats.cs`, `Services/CreatureSheetStats.cs` — sub-attributes + `FichaParaRequisitos`.
- Modify `Program.cs` — register the 3 stats services.
- Modify `CharacterSheetsController.cs`, `NpcSheetsController.cs`, `CreatureSheetsController.cs` — `/sub-attributes` delegates to the stats service.
- Modify `SpellAbilityBankController.cs`, `CampaignCatalogController.cs` (response mapping), `Character/Npc/CreatureSpellAbilitiesController.cs`, `CampaignGrantsController.cs`.

**Client**
- Create `Shared/Fields/RequisitosFormModel.cs`, `Shared/Fields/RequisitosDePassivaEditor.razor`, `Shared/HabilidadesPassivasSection.razor`.
- Modify `Pages/BancoDeMagiasForm.razor`, `Pages/BancoDeMagias.razor`, `Pages/FichaDePersonagem.razor`, `Pages/FichaDeNpc.razor`, `Pages/FichaDeCriatura.razor`.

**Docs**
- `Docs/Requisitos/Requisitos - Modelo de Dados.md`, `Requisitos - Banco de Magias e Habilidades.md`, `Requisitos - Ficha de Personagem.md`, `Requisitos - Ficha de NPCs.md`, `Requisitos - Ficha de Criaturas.md`.

---

### Task 1: Domain — Passiva types and the requisitos evaluator

**Files:**
- Modify: `src/RuinaRPG.Domain/SpellsAndAbilities/SpellAbilityTipo.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/CategoriaDePassiva.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/SubAtributo.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/RequisitosDePassiva.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/FichaParaRequisitos.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/RequisitoLabels.cs`
- Create: `src/RuinaRPG.Domain/SpellsAndAbilities/PassivaRequisitosEvaluator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/PassivaRequisitosEvaluatorTests.cs`

**Interfaces:**
- Produces (used by every later task):
  - `enum SpellAbilityTipo { Magia, Habilidade, Racial, Passiva }`
  - `enum CategoriaDePassiva { Livre, Vocacional, DeClasse }`
  - `enum SubAtributo { Iniciativa, Movimentacao, EsquivaNatural, DefesaNatural, ReducaoFisica, ReducaoMagica }`
  - `sealed record RequisitoDeAtributo(Atributo Atributo, int Minimo)`, `RequisitoDeSubAtributo(SubAtributo SubAtributo, int Minimo)`, `RequisitoDePericia(Pericia Pericia, int Minimo)`
  - `sealed record RequisitosDePassiva` with init props `int? Nivel, Vocacao? Vocacao, string? Classe, Linhagem? Linhagem, Variante? Variante, int? Graduacao, bool? CoracaoDeMana, AfinidadeElemental? Afinidade, Estrela? Estrela, Guid? HistoricoId, List<RequisitoDeAtributo> Atributos, List<RequisitoDeSubAtributo> SubAtributos, List<RequisitoDePericia> Pericias`
  - `sealed record FichaParaRequisitos(bool TemIdentidadeDePersonagem, int Nivel, Vocacao? Vocacao, string? Classe, Linhagem? Linhagem, Variante? Variante, int Graduacao, bool PossuiCoracaoDeMana, AfinidadeElemental? Afinidade, Estrela? Estrela, Guid? HistoricoId, IReadOnlyDictionary<Atributo, int> Atributos, IReadOnlyDictionary<SubAtributo, int> SubAtributos, IReadOnlyDictionary<Pericia, int?> Pericias)`
  - `static IReadOnlyList<string> PassivaRequisitosEvaluator.Pendencias(RequisitosDePassiva? requisitos, FichaParaRequisitos ficha, string? nomeDoHistoricoExigido)`
  - `static class RequisitoLabels` with `Categoria(CategoriaDePassiva)`, `Vocacao(Vocacao)`, `Linhagem(Linhagem)`, `Variante(Variante)`, `Afinidade(AfinidadeElemental)`, `Atributo(Atributo)`, `SubAtributo(SubAtributo)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/PassivaRequisitosEvaluatorTests.cs
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class PassivaRequisitosEvaluatorTests
{
    private static FichaParaRequisitos Personagem(
        int nivel = 5, Vocacao? vocacao = Vocacao.Feiticeiro, string? classe = "Elementalista",
        Linhagem? linhagem = Linhagem.Humano, Variante? variante = Variante.Sinir, int graduacao = 3,
        bool coracao = true, AfinidadeElemental? afinidade = AfinidadeElemental.Fogo, Estrela? estrela = Estrela.Liora,
        Guid? historicoId = null,
        Dictionary<Atributo, int>? atributos = null, Dictionary<SubAtributo, int>? subAtributos = null,
        Dictionary<Pericia, int?>? pericias = null) =>
        new(true, nivel, vocacao, classe, linhagem, variante, graduacao, coracao, afinidade, estrela, historicoId,
            atributos ?? Enum.GetValues<Atributo>().ToDictionary(a => a, _ => 3),
            subAtributos ?? Enum.GetValues<SubAtributo>().ToDictionary(s => s, _ => 3),
            pericias ?? Enum.GetValues<Pericia>().ToDictionary(p => p, _ => (int?)3));

    // Criatura: sem identidade de personagem; só Força/Vigor/Agilidade/Destreza/Astúcia; só algumas Perícias.
    private static FichaParaRequisitos Criatura(int nivel = 5, AfinidadeElemental? afinidade = AfinidadeElemental.Fogo) =>
        new(false, nivel, null, null, null, null, 0, false, afinidade, null, null,
            new Dictionary<Atributo, int> { [Atributo.Forca] = 3, [Atributo.Vigor] = 3, [Atributo.Agilidade] = 3, [Atributo.Destreza] = 3, [Atributo.Astucia] = 3 },
            Enum.GetValues<SubAtributo>().ToDictionary(s => s, _ => 3),
            new Dictionary<Pericia, int?> { [Pericia.Atletismo] = 3 });

    [Fact]
    public void Null_requisitos_have_no_pendencias() =>
        PassivaRequisitosEvaluator.Pendencias(null, Personagem(), null).Should().BeEmpty();

    [Fact]
    public void Empty_requisitos_have_no_pendencias() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva(), Personagem(vocacao: null, classe: null), null).Should().BeEmpty();

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Nivel_is_a_minimum(int exigido, bool cumpre) =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Nivel = exigido }, Personagem(nivel: 5), null)
            .Should().BeEquivalentTo(cumpre ? Array.Empty<string>() : [$"Nível {exigido}"]);

    [Fact]
    public void Vocacao_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Vocacao = Vocacao.Campeao }, Personagem(), null)
            .Should().Equal("Vocação: Campeão");

    [Fact]
    public void Empty_vocacao_on_a_personagem_fails_a_vocacao_requisito() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Vocacao = Vocacao.Feiticeiro }, Personagem(vocacao: null), null)
            .Should().Equal("Vocação: Feiticeiro");

    [Fact]
    public void Classe_compares_trimmed_and_case_insensitive()
    {
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Classe = " elementalista " }, Personagem(), null).Should().BeEmpty();
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Classe = "Duelista" }, Personagem(), null).Should().Equal("Classe: Duelista");
    }

    [Fact]
    public void Linhagem_and_variante_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Linhagem = Linhagem.Econos, Variante = Variante.Alora }, Personagem(), null)
            .Should().Equal("Linhagem: Ecônos", "Variante: Alóra");

    [Fact]
    public void Graduacao_is_a_minimum() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Graduacao = 4 }, Personagem(graduacao: 3), null)
            .Should().Equal("Grau/Círculo 4");

    [Fact]
    public void Coracao_de_mana_true_is_required_false_is_not_a_requisito()
    {
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { CoracaoDeMana = true }, Personagem(coracao: false), null).Should().Equal("Coração de Mana");
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { CoracaoDeMana = false }, Personagem(coracao: true), null).Should().BeEmpty();
    }

    [Fact]
    public void Afinidade_and_estrela_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Afinidade = AfinidadeElemental.Agua, Estrela = Estrela.Sadir }, Personagem(), null)
            .Should().Equal("Afinidade: Água", "Estrela: Sadir");

    [Fact]
    public void Historico_must_match_and_uses_the_given_name()
    {
        var exigido = Guid.NewGuid();
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = exigido }, Personagem(historicoId: Guid.NewGuid()), "Nobre")
            .Should().Equal("Histórico: Nobre");
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = exigido }, Personagem(historicoId: exigido), "Nobre")
            .Should().BeEmpty();
    }

    [Fact]
    public void Historico_that_no_longer_exists_reads_removido() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = Guid.NewGuid() }, Personagem(), null)
            .Should().Equal("Histórico: (removido)");

    [Fact]
    public void Each_attribute_subattribute_and_pericia_in_the_lists_is_a_minimum_over_the_total()
    {
        var requisitos = new RequisitosDePassiva
        {
            Atributos = [new(Atributo.Forca, 4), new(Atributo.Agilidade, 3)],
            SubAtributos = [new(SubAtributo.Iniciativa, 5)],
            Pericias = [new(Pericia.Atletismo, 2), new(Pericia.ArmasBrancas, 7)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Personagem(), null)
            .Should().Equal("Força ≥ 4", "Iniciativa ≥ 5", "Armas Brancas ≥ 7");
    }

    [Fact]
    public void A_pericia_without_a_total_fails() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Pericias = [new(Pericia.Atletismo, 0)] },
                Personagem(pericias: new Dictionary<Pericia, int?> { [Pericia.Atletismo] = null }), null)
            .Should().Equal("Atletismo ≥ 0");

    [Fact]
    public void Criatura_ignores_requisitos_on_fields_it_does_not_have()
    {
        var requisitos = new RequisitosDePassiva
        {
            Vocacao = Vocacao.Bruxo, Classe = "X", Linhagem = Linhagem.Humano, Variante = Variante.Sinir, Graduacao = 9,
            CoracaoDeMana = true, Estrela = Estrela.Sadir, HistoricoId = Guid.NewGuid(),
            Atributos = [new(Atributo.Instinto, 99), new(Atributo.Vontade, 99), new(Atributo.Influencia, 99)],
            Pericias = [new(Pericia.Medicina, 99)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Criatura(), "Nobre").Should().BeEmpty();
    }

    [Fact]
    public void Criatura_still_checks_nivel_afinidade_shared_attributes_subattributes_and_its_pericias()
    {
        var requisitos = new RequisitosDePassiva
        {
            Nivel = 6, Afinidade = AfinidadeElemental.Gelo,
            Atributos = [new(Atributo.Forca, 4)], SubAtributos = [new(SubAtributo.DefesaNatural, 4)], Pericias = [new(Pericia.Atletismo, 4)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Criatura(), null)
            .Should().Equal("Nível 6", "Afinidade: Gelo", "Força ≥ 4", "Defesa Natural ≥ 4", "Atletismo ≥ 4");
    }

    [Theory]
    [InlineData(CategoriaDePassiva.Livre, "Passiva Livre")]
    [InlineData(CategoriaDePassiva.Vocacional, "Passiva Vocacional")]
    [InlineData(CategoriaDePassiva.DeClasse, "Passiva de Classe")]
    public void Categoria_labels(CategoriaDePassiva categoria, string label) =>
        RequisitoLabels.Categoria(categoria).Should().Be(label);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~PassivaRequisitosEvaluatorTests`
Expected: build FAIL — `PassivaRequisitosEvaluator`, `RequisitosDePassiva`, etc. not defined.

- [ ] **Step 3: Implement**

`SpellAbilityTipo.cs`:
```csharp
namespace RuinaRPG.Domain.SpellsAndAbilities;

public enum SpellAbilityTipo
{
    Magia,
    Habilidade,
    Racial,
    // Gravado como inteiro — sempre acrescentar no fim. Ver docs/superpowers/specs/2026-09-28-habilidades-passivas-design.md.
    Passiva
}
```

`CategoriaDePassiva.cs`:
```csharp
namespace RuinaRPG.Domain.SpellsAndAbilities;

public enum CategoriaDePassiva
{
    Livre,
    Vocacional,
    DeClasse
}
```

`SubAtributo.cs`:
```csharp
namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>Os sub-atributos (2.b) que o servidor calcula em /sub-attributes e que podem ser requisito
/// de uma Passiva. Adrenalina fica de fora: é máximo de recurso (1.c), não sub-atributo.</summary>
public enum SubAtributo
{
    Iniciativa,
    Movimentacao,
    EsquivaNatural,
    DefesaNatural,
    ReducaoFisica,
    ReducaoMagica
}
```

`RequisitosDePassiva.cs`:
```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.SpellsAndAbilities;

public sealed record RequisitoDeAtributo(Atributo Atributo, int Minimo);
public sealed record RequisitoDeSubAtributo(SubAtributo SubAtributo, int Minimo);
public sealed record RequisitoDePericia(Pericia Pericia, int Minimo);

/// <summary>
/// Requisitos de uma Passiva (Banco de Magias e Habilidades). Todo campo nulo — ou lista vazia — não
/// faz parte dos requisitos; os preenchidos precisam ser todos cumpridos. Gravado como jsonb.
/// </summary>
public sealed record RequisitosDePassiva
{
    public int? Nivel { get; init; }
    public Vocacao? Vocacao { get; init; }
    public string? Classe { get; init; }
    public Linhagem? Linhagem { get; init; }
    public Variante? Variante { get; init; }
    public int? Graduacao { get; init; }
    public bool? CoracaoDeMana { get; init; }
    public AfinidadeElemental? Afinidade { get; init; }
    public Estrela? Estrela { get; init; }
    public Guid? HistoricoId { get; init; }
    public List<RequisitoDeAtributo> Atributos { get; init; } = [];
    public List<RequisitoDeSubAtributo> SubAtributos { get; init; } = [];
    public List<RequisitoDePericia> Pericias { get; init; } = [];
}
```

`FichaParaRequisitos.cs`:
```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// O que uma ficha tem para comparar com os requisitos de uma Passiva.
/// <para><see cref="TemIdentidadeDePersonagem"/> é falso na Criatura, que não tem Vocação, Classe, Linhagem,
/// Variante, Grau/Círculo, Coração de Mana, Estrela nem Histórico — requisitos nesses campos são ignorados.
/// Nos dicionários, só entram as chaves que aquele tipo de ficha tem: uma chave ausente é ignorada; um
/// valor presente mas nulo (Perícia sem atributo escolhido, sem Total) não cumpre.</para>
/// </summary>
public sealed record FichaParaRequisitos(
    bool TemIdentidadeDePersonagem,
    int Nivel,
    Vocacao? Vocacao,
    string? Classe,
    Linhagem? Linhagem,
    Variante? Variante,
    int Graduacao,
    bool PossuiCoracaoDeMana,
    AfinidadeElemental? Afinidade,
    Estrela? Estrela,
    Guid? HistoricoId,
    IReadOnlyDictionary<Atributo, int> Atributos,
    IReadOnlyDictionary<SubAtributo, int> SubAtributos,
    IReadOnlyDictionary<Pericia, int?> Pericias);
```

`RequisitoLabels.cs`:
```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>Rótulos em português usados nas pendências de requisito e na tela da Passiva.</summary>
public static class RequisitoLabels
{
    public static string Categoria(CategoriaDePassiva categoria) => categoria switch
    {
        CategoriaDePassiva.Livre => "Passiva Livre",
        CategoriaDePassiva.Vocacional => "Passiva Vocacional",
        CategoriaDePassiva.DeClasse => "Passiva de Classe",
        _ => categoria.ToString()
    };

    public static string Vocacao(Vocacao vocacao) => vocacao switch
    {
        CharacterSheets.Vocacao.Campeao => "Campeão",
        CharacterSheets.Vocacao.Cacador => "Caçador",
        _ => vocacao.ToString()
    };

    public static string Linhagem(Linhagem linhagem) => linhagem == CharacterSheets.Linhagem.Econos ? "Ecônos" : linhagem.ToString();

    public static string Variante(Variante variante) => variante switch
    {
        CharacterSheets.Variante.PhylacTai => "Phylac'tai",
        CharacterSheets.Variante.EsPhylauc => "Es'Phylauc",
        CharacterSheets.Variante.Alora => "Alóra",
        CharacterSheets.Variante.AloraSolar => "Alóra (Sol)",
        _ => variante.ToString()
    };

    public static string Afinidade(AfinidadeElemental afinidade) => afinidade switch
    {
        AfinidadeElemental.Agua => "Água",
        AfinidadeElemental.Invocacao => "Invocação",
        _ => afinidade.ToString()
    };

    public static string Atributo(Atributo atributo) => atributo switch
    {
        CharacterSheets.Atributo.Influencia => "Influência",
        CharacterSheets.Atributo.Astucia => "Astúcia",
        CharacterSheets.Atributo.Forca => "Força",
        _ => atributo.ToString()
    };

    public static string SubAtributo(SubAtributo subAtributo) => subAtributo switch
    {
        SpellsAndAbilities.SubAtributo.Movimentacao => "Movimentação",
        SpellsAndAbilities.SubAtributo.EsquivaNatural => "Esquiva Natural",
        SpellsAndAbilities.SubAtributo.DefesaNatural => "Defesa Natural",
        SpellsAndAbilities.SubAtributo.ReducaoFisica => "Redução Física",
        SpellsAndAbilities.SubAtributo.ReducaoMagica => "Redução Mágica",
        _ => subAtributo.ToString()
    };
}
```

`PassivaRequisitosEvaluator.cs`:
```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// Compara os requisitos de uma Passiva com uma ficha. Devolve uma pendência legível por requisito não
/// cumprido, na ordem dos campos; lista vazia = cumpre tudo. <paramref name="nomeDoHistoricoExigido"/> é o
/// nome do Histórico do requisito (nulo se ele não existe mais no catálogo).
/// </summary>
public static class PassivaRequisitosEvaluator
{
    public static IReadOnlyList<string> Pendencias(RequisitosDePassiva? requisitos, FichaParaRequisitos ficha, string? nomeDoHistoricoExigido)
    {
        var pendencias = new List<string>();
        if (requisitos is null)
            return pendencias;

        if (requisitos.Nivel is { } nivel && ficha.Nivel < nivel)
            pendencias.Add($"Nível {nivel}");

        if (ficha.TemIdentidadeDePersonagem)
        {
            if (requisitos.Vocacao is { } vocacao && ficha.Vocacao != vocacao)
                pendencias.Add($"Vocação: {RequisitoLabels.Vocacao(vocacao)}");
            if (!string.IsNullOrWhiteSpace(requisitos.Classe)
                && !string.Equals(requisitos.Classe.Trim(), ficha.Classe?.Trim(), StringComparison.OrdinalIgnoreCase))
                pendencias.Add($"Classe: {requisitos.Classe.Trim()}");
            if (requisitos.Linhagem is { } linhagem && ficha.Linhagem != linhagem)
                pendencias.Add($"Linhagem: {RequisitoLabels.Linhagem(linhagem)}");
            if (requisitos.Variante is { } variante && ficha.Variante != variante)
                pendencias.Add($"Variante: {RequisitoLabels.Variante(variante)}");
            if (requisitos.Graduacao is { } graduacao && ficha.Graduacao < graduacao)
                pendencias.Add($"Grau/Círculo {graduacao}");
            if (requisitos.CoracaoDeMana == true && !ficha.PossuiCoracaoDeMana)
                pendencias.Add("Coração de Mana");
        }

        if (requisitos.Afinidade is { } afinidade && ficha.Afinidade != afinidade)
            pendencias.Add($"Afinidade: {RequisitoLabels.Afinidade(afinidade)}");

        if (ficha.TemIdentidadeDePersonagem)
        {
            if (requisitos.Estrela is { } estrela && ficha.Estrela != estrela)
                pendencias.Add($"Estrela: {estrela}");
            if (requisitos.HistoricoId is { } historicoId && ficha.HistoricoId != historicoId)
                pendencias.Add($"Histórico: {nomeDoHistoricoExigido ?? "(removido)"}");
        }

        foreach (var r in requisitos.Atributos)
            if (ficha.Atributos.TryGetValue(r.Atributo, out var total) && total < r.Minimo)
                pendencias.Add($"{RequisitoLabels.Atributo(r.Atributo)} ≥ {r.Minimo}");

        foreach (var r in requisitos.SubAtributos)
            if (ficha.SubAtributos.TryGetValue(r.SubAtributo, out var valor) && valor < r.Minimo)
                pendencias.Add($"{RequisitoLabels.SubAtributo(r.SubAtributo)} ≥ {r.Minimo}");

        foreach (var r in requisitos.Pericias)
            if (ficha.Pericias.TryGetValue(r.Pericia, out var total) && (total is null || total < r.Minimo))
                pendencias.Add($"{PericiaLabels.Label(r.Pericia)} ≥ {r.Minimo}");

        return pendencias;
    }
}
```

Note the ordering in the test `Criatura_still_checks...` ("Nível", "Afinidade", attributes…) and `Afinidade_and_estrela_must_match` ("Afinidade" before "Estrela") — the implementation above produces exactly that order.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~PassivaRequisitosEvaluatorTests`
Expected: PASS. Then `dotnet build` → 0 warnings. (Adding `Passiva` may surface a non-exhaustive `switch` warning somewhere — fix any that appear.)

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/SpellsAndAbilities tests/RuinaRPG.Tests.Unit/SpellsAndAbilities/PassivaRequisitosEvaluatorTests.cs
git commit -m "feat(domain): tipo Passiva e avaliador de requisitos"
```

---

### Task 2: Persistence — Categoria and Requisitos columns

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/SpellsAndAbilities/SpellAbilityBankEntry.cs`
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSpellAbility.cs`
- Modify: `src/RuinaRPG.Infrastructure/NpcSheets/NpcSpellAbility.cs`
- Modify: `src/RuinaRPG.Infrastructure/CreatureSheets/CreatureSpellAbility.cs`
- Create: `src/RuinaRPG.Infrastructure/Persistence/RequisitosDePassivaJson.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs` (entity blocks at ~204, ~335, ~486, ~545)
- Create: migration `AddHabilidadesPassivas` (generated)
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/HabilidadesPassivasMigrationTests.cs`

**Interfaces:**
- Consumes: Task 1 types.
- Produces: `CategoriaDePassiva? Categoria { get; set; }` and `RequisitosDePassiva? Requisitos { get; set; }` on `SpellAbilityBankEntry`, `CharacterSpellAbility`, `NpcSpellAbility`, `CreatureSpellAbility`.

- [ ] **Step 1: Write the failing test**

Model it on `tests/RuinaRPG.Tests.Integration/Persistence/CharacterSpellAbilityMigrationTests.cs` (same user/campaign/sheet setup).

```csharp
// tests/RuinaRPG.Tests.Integration/Persistence/HabilidadesPassivasMigrationTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Persistence;

public class HabilidadesPassivasMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public HabilidadesPassivasMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Categoria_and_requisitos_round_trip_on_the_bank_and_on_a_sheet_copy()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using (var db = new RuinaRpgDbContext(options))
        {
            await db.Database.MigrateAsync();
            (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddHabilidadesPassivas"));
        }

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@passivatest.com", Email = "gm@passivatest.com", Nickname = "PassivaTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@passivatest.com", Email = "player@passivatest.com", Nickname = "PassivaTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        var requisitos = new RequisitosDePassiva
        {
            Nivel = 3, Vocacao = Vocacao.Feiticeiro, Classe = "Elementalista",
            Atributos = [new(Atributo.Forca, 4)], SubAtributos = [new(SubAtributo.Iniciativa, 2)], Pericias = [new(Pericia.Atletismo, 5)]
        };
        var bankId = Guid.NewGuid();
        var sheetEntryId = Guid.NewGuid();

        await using (var db = new RuinaRpgDbContext(options))
        {
            db.Users.AddRange(gm, player);
            await db.SaveChangesAsync();
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync();
            db.CharacterSheets.Add(sheet);
            db.SpellAbilityBankEntries.Add(new SpellAbilityBankEntry
            {
                Id = bankId, GmId = gm.Id, Nome = "Pele de Pedra", Tipo = SpellAbilityTipo.Passiva, Descricao = "d",
                Categoria = CategoriaDePassiva.Vocacional, Requisitos = requisitos
            });
            db.CharacterSpellAbilities.Add(new CharacterSpellAbility
            {
                Id = sheetEntryId, CharacterSheetId = sheet.Id, SourceBankEntryId = bankId, Nome = "Pele de Pedra",
                Tipo = SpellAbilityTipo.Passiva, Descricao = "d", Categoria = CategoriaDePassiva.Vocacional, Requisitos = requisitos
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new RuinaRpgDbContext(options))
        {
            var bank = await db.SpellAbilityBankEntries.SingleAsync(e => e.Id == bankId);
            bank.Categoria.Should().Be(CategoriaDePassiva.Vocacional);
            bank.Requisitos.Should().BeEquivalentTo(requisitos);

            var copy = await db.CharacterSpellAbilities.SingleAsync(e => e.Id == sheetEntryId);
            copy.Requisitos.Should().BeEquivalentTo(requisitos);

            // Uma Magia comum continua sem Categoria/Requisitos.
            var magia = new SpellAbilityBankEntry { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Bola", Tipo = SpellAbilityTipo.Magia, Descricao = "d" };
            db.SpellAbilityBankEntries.Add(magia);
            await db.SaveChangesAsync();
            (await db.SpellAbilityBankEntries.AsNoTracking().SingleAsync(e => e.Id == magia.Id)).Requisitos.Should().BeNull();
        }

        await using (var db = new RuinaRpgDbContext(options))
        {
            // Mudar um item de uma lista dentro do jsonb precisa ser detectado (comparer por valor).
            var bank = await db.SpellAbilityBankEntries.SingleAsync(e => e.Id == bankId);
            bank.Requisitos = bank.Requisitos! with { Nivel = 7 };
            await db.SaveChangesAsync();
        }

        await using (var db = new RuinaRpgDbContext(options))
            (await db.SpellAbilityBankEntries.SingleAsync(e => e.Id == bankId)).Requisitos!.Nivel.Should().Be(7);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~HabilidadesPassivasMigrationTests`
Expected: build FAIL — `Categoria`/`Requisitos` not defined.

- [ ] **Step 3: Implement**

Add to each of the 4 entities (below `Descricao`), with `using RuinaRPG.Domain.SpellsAndAbilities;` where missing:
```csharp
    /// <summary>Só em Passivas (Tipo = Passiva); nulo nos demais tipos.</summary>
    public CategoriaDePassiva? Categoria { get; set; }

    /// <summary>Só em Passivas; gravado como jsonb. Nulo = sem requisitos.</summary>
    public RequisitosDePassiva? Requisitos { get; set; }
```

`RequisitosDePassivaJson.cs`:
```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.Persistence;

/// <summary>
/// Mapeia <see cref="RequisitosDePassiva"/> para uma coluna jsonb. O objeto é sempre lido e gravado
/// inteiro e nunca consultado por dentro, então um único jsonb evita ~11 colunas + 3 tabelas-filhas em
/// cada uma das 4 tabelas de Magia/Habilidade. O comparer compara pelo JSON, porque as listas do record
/// não têm igualdade por valor.
/// </summary>
internal static class RequisitosDePassivaJson
{
    private static string Serialize(RequisitosDePassiva value) => JsonSerializer.Serialize(value);
    private static RequisitosDePassiva Deserialize(string json) => JsonSerializer.Deserialize<RequisitosDePassiva>(json)!;

    public static readonly ValueConverter<RequisitosDePassiva, string> Converter =
        new(v => Serialize(v), s => Deserialize(s));

    public static readonly ValueComparer<RequisitosDePassiva?> Comparer = new(
        (a, b) => (a == null ? null : Serialize(a)) == (b == null ? null : Serialize(b)),
        v => v == null ? 0 : Serialize(v).GetHashCode(),
        v => v == null ? null : Deserialize(Serialize(v)));
}
```

In `RuinaRpgDbContext.OnModelCreating`, add inside each of the 4 `builder.Entity<...>(entity => { ... })` blocks (`SpellAbilityBankEntry`, `CharacterSpellAbility`, `NpcSpellAbility`, `CreatureSpellAbility`):
```csharp
            entity.Property(e => e.Requisitos).HasColumnType("jsonb")
                .HasConversion(RequisitosDePassivaJson.Converter, RequisitosDePassivaJson.Comparer);
```
(`Categoria` needs no configuration — enums map to int by convention.)

Generate the migration:
```bash
dotnet ef migrations add AddHabilidadesPassivas --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```
Inspect it: exactly 8 `AddColumn` calls (`Categoria` integer nullable, `Requisitos` jsonb nullable) on `SpellAbilityBankEntries`, `CharacterSpellAbilities`, `NpcSpellAbilities`, `CreatureSpellAbilities` (use the real table names EF generates) and nothing else.

Update `Docs/Requisitos/Requisitos - Modelo de Dados.md`: in the tables for the Banco entry and for each of the 3 sheet Magia/Habilidade tables, add rows `Categoria` (int, NULL — só em Passiva: Livre/Vocacional/DeClasse) and `Requisitos` (jsonb, NULL — só em Passiva; ver [[Requisitos - Banco de Magias e Habilidades]] R0009), and add `Passiva` to the documented Tipo values. Follow that file's existing table layout.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~HabilidadesPassivasMigrationTests|FullyQualifiedName~SpellAbility"` → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Infrastructure tests/RuinaRPG.Tests.Integration/Persistence/HabilidadesPassivasMigrationTests.cs "Docs/Requisitos/Requisitos - Modelo de Dados.md"
git commit -m "feat(db): colunas Categoria e Requisitos (jsonb) nas Magias/Habilidades"
```

---

### Task 3: Contracts + Banco API for Passivas

**Files:**
- Create: `src/RuinaRPG.Contracts/SpellsAndAbilities/RequisitosDePassivaDto.cs`
- Modify: `src/RuinaRPG.Contracts/SpellsAndAbilities/CreateSpellAbilityEntryRequest.cs`, `UpdateSpellAbilityEntryRequest.cs`, `SpellAbilityEntryResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/RequisitosDePassivaMapper.cs`
- Modify: `src/RuinaRPG.Api/Controllers/SpellAbilityBankController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs` (`ToSpellAbilityResponse`, ~line 158)
- Modify: `Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankPassivaTests.cs`

**Interfaces:**
- Consumes: Task 1/2.
- Produces:
  - `record RequisitoMinimoDto(string Alvo, int Minimo)`
  - `record RequisitosDePassivaDto(int? Nivel = null, string? Vocacao = null, string? Classe = null, string? Linhagem = null, string? Variante = null, int? Graduacao = null, bool? CoracaoDeMana = null, string? Afinidade = null, string? Estrela = null, string? HistoricoId = null, List<RequisitoMinimoDto>? Atributos = null, List<RequisitoMinimoDto>? SubAtributos = null, List<RequisitoMinimoDto>? Pericias = null)`
  - `CreateSpellAbilityEntryRequest(..., bool DeCriatura = false, string? Categoria = null, RequisitosDePassivaDto? Requisitos = null)` — same two trailing params on `UpdateSpellAbilityEntryRequest`.
  - `SpellAbilityEntryResponse(..., bool DeCriatura, string? Categoria = null, RequisitosDePassivaDto? Requisitos = null)`
  - `static class RequisitosDePassivaMapper { static bool TryParse(RequisitosDePassivaDto? dto, out RequisitosDePassiva? requisitos, out string? erro); static RequisitosDePassivaDto? ToDto(RequisitosDePassiva? r); static Task<string?> NomeDoHistoricoAsync(RuinaRpgDbContext db, RequisitosDePassiva? r); }`

- [ ] **Step 1: Write the failing tests**

Copy the `AuthedRequest` / `RegisterGmAndGetTokenAsync` helpers from `tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankControllerTests.cs` (same class-fixture pattern).

```csharp
// tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankPassivaTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SpellAbilityBankPassivaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    // ...fixture fields, InitializeAsync/DisposeAsync, AuthedRequest, RegisterGmAndGetTokenAsync — copied from SpellAbilityBankControllerTests...

    private static RequisitosDePassivaDto RequisitosCompletos() => new(
        Nivel: 3, Vocacao: "Feiticeiro", Classe: "Elementalista", Linhagem: "Humano", Variante: "Sinir",
        Graduacao: 2, CoracaoDeMana: true, Afinidade: "Fogo", Estrela: "Liora",
        Atributos: [new("Forca", 4)], SubAtributos: [new("Iniciativa", 2)], Pericias: [new("Atletismo", 5)]);

    private Task<HttpResponseMessage> PostAsync(string token, CreateSpellAbilityEntryRequest body) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", token, body));

    [Fact]
    public async Task Creates_a_passiva_with_categoria_and_requisitos_and_lists_it_under_the_Passiva_filter()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassivaBankGm1", "passivabank1@teste.com");

        var response = await PostAsync(gm, new CreateSpellAbilityEntryRequest("Pele de Pedra", "Passiva", 0, "Resiste.", [], false, "Vocacional", RequisitosCompletos()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!;
        body.Tipo.Should().Be("Passiva");
        body.Categoria.Should().Be("Vocacional");
        body.Grau.Should().Be(0);
        body.GastoEmPI.Should().Be(0);
        body.Requisitos.Should().BeEquivalentTo(RequisitosCompletos() with { HistoricoId = null });

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank?tipo=Passiva", gm)))
            .Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        list!.Should().ContainSingle(e => e.Nome == "Pele de Pedra");
    }

    [Fact]
    public async Task A_passiva_without_requisitos_is_valid()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassivaBankGm2", "passivabank2@teste.com");
        var response = await PostAsync(gm, new CreateSpellAbilityEntryRequest("Livre", "Passiva", 0, "d", [], false, "Livre", null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    public static TheoryData<CreateSpellAbilityEntryRequest> Invalidos => new()
    {
        new("Sem categoria", "Passiva", 0, "d", [], false, null, null),
        new("Categoria ruim", "Passiva", 0, "d", [], false, "Nenhuma", null),
        new("Com grau", "Passiva", 2, "d", [], false, "Livre", null),
        new("Com efeito", "Passiva", 0, "d", [new SpellAbilityEffectRequest("Dano", 1, 2)], false, "Livre", null),
        new("Magia com categoria", "Magia", 1, "d", [], false, "Livre", null),
        new("Magia com requisitos", "Magia", 1, "d", [], false, null, new RequisitosDePassivaDto(Nivel: 1)),
        new("Classe sem vocação", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Classe: "Duelista")),
        new("Variante sem linhagem", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Variante: "Sinir")),
        new("Variante de outra linhagem", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Linhagem: "Phylauc", Variante: "Sinir")),
        new("Atributo duplicado", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Atributos: [new("Forca", 1), new("Forca", 2)])),
        new("Atributo desconhecido", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Atributos: [new("Ego", 1)])),
        new("Mínimo negativo", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Pericias: [new("Atletismo", -1)])),
        new("Histórico inexistente", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(HistoricoId: Guid.NewGuid().ToString())),
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task Rejects_invalid_passiva_combinations(CreateSpellAbilityEntryRequest request)
    {
        var gm = await RegisterGmAndGetTokenAsync($"PassivaInv{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@teste.com");
        (await PostAsync(gm, request)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_can_turn_a_passiva_back_into_a_magia_when_categoria_and_requisitos_are_cleared()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassivaBankGm3", "passivabank3@teste.com");
        var created = (await (await PostAsync(gm, new CreateSpellAbilityEntryRequest("P", "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Nivel: 2))))
            .Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!;

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/spell-ability-bank/{created.Id}", gm,
            new UpdateSpellAbilityEntryRequest("P", "Magia", 1, "d", [], false, null, null)));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", gm))).Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        var entry = list!.Single(e => e.Id == created.Id);
        entry.Tipo.Should().Be("Magia");
        entry.Categoria.Should().BeNull();
        entry.Requisitos.Should().BeNull();
    }
}
```

(The `Histórico inexistente` case needs a valid-Guid-but-missing id; a Histórico that *does* exist is covered in Task 5 via the catalog's seeded Históricos — look one up with `GET /api/historicos`.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SpellAbilityBankPassivaTests`
Expected: build FAIL (new request params / DTO missing).

- [ ] **Step 3: Implement**

`RequisitosDePassivaDto.cs`:
```csharp
namespace RuinaRPG.Contracts.SpellsAndAbilities;

/// <summary>Um item das listas de requisito: Alvo é o nome do enum (Atributo, SubAtributo ou Pericia).</summary>
public record RequisitoMinimoDto(string Alvo, int Minimo);

/// <summary>Requisitos de uma Passiva. Campo nulo/vazio = não é requisito. Enums trafegam como texto.</summary>
public record RequisitosDePassivaDto(
    int? Nivel = null,
    string? Vocacao = null,
    string? Classe = null,
    string? Linhagem = null,
    string? Variante = null,
    int? Graduacao = null,
    bool? CoracaoDeMana = null,
    string? Afinidade = null,
    string? Estrela = null,
    string? HistoricoId = null,
    List<RequisitoMinimoDto>? Atributos = null,
    List<RequisitoMinimoDto>? SubAtributos = null,
    List<RequisitoMinimoDto>? Pericias = null);
```

Requests/response — append trailing optional params:
```csharp
public record CreateSpellAbilityEntryRequest(
    [Required(AllowEmptyStrings = false)] string Nome,
    string Tipo,
    [Range(0, int.MaxValue)] int Grau,
    string Descricao,
    List<SpellAbilityEffectRequest> Efeitos,
    bool DeCriatura = false,
    string? Categoria = null,
    RequisitosDePassivaDto? Requisitos = null);
// UpdateSpellAbilityEntryRequest: identical parameter list.

public record SpellAbilityEntryResponse(string Id, string Nome, string Tipo, int Grau, int GastoEmPI, int Custo, string Descricao,
    List<SpellAbilityEffectResponse> Efeitos, bool DeCriatura, string? Categoria = null, RequisitosDePassivaDto? Requisitos = null);
```

`RequisitosDePassivaMapper.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>Converte os requisitos de uma Passiva entre o contrato (texto) e o domínio, validando.</summary>
public static class RequisitosDePassivaMapper
{
    public static bool TryParse(RequisitosDePassivaDto? dto, out RequisitosDePassiva? requisitos, out string? erro)
    {
        requisitos = null;
        erro = null;
        if (dto is null)
            return true;

        if (!TryEnum<Vocacao>(dto.Vocacao, "Vocação", out var vocacao, ref erro)
            || !TryEnum<Linhagem>(dto.Linhagem, "Linhagem", out var linhagem, ref erro)
            || !TryEnum<Variante>(dto.Variante, "Variante", out var variante, ref erro)
            || !TryEnum<AfinidadeElemental>(dto.Afinidade, "Afinidade", out var afinidade, ref erro)
            || !TryEnum<Estrela>(dto.Estrela, "Estrela", out var estrela, ref erro))
            return false;

        var classe = string.IsNullOrWhiteSpace(dto.Classe) ? null : dto.Classe.Trim();
        if (classe is not null && vocacao is null)
            return Fail("Uma Classe exige a Vocação.", out erro);
        if (variante is not null && (linhagem is null || !LinhagemVarianteValidator.IsValidCombination(linhagem.Value, variante.Value)))
            return Fail("A Variante exige a Linhagem correspondente.", out erro);
        if (dto.Nivel is < 1)
            return Fail("O Nível mínimo é 1.", out erro);
        if (dto.Graduacao is < 0 or > 9)
            return Fail("Grau/Círculo vai de 0 a 9.", out erro);

        Guid? historicoId = null;
        if (!string.IsNullOrWhiteSpace(dto.HistoricoId))
        {
            if (!Guid.TryParse(dto.HistoricoId, out var parsed))
                return Fail("Histórico não encontrado.", out erro);
            historicoId = parsed;
        }

        if (!TryList<Atributo>(dto.Atributos, "Atributo", out var atributos, out erro)
            || !TryList<SubAtributo>(dto.SubAtributos, "Sub-Atributo", out var subAtributos, out erro)
            || !TryList<Pericia>(dto.Pericias, "Perícia", out var pericias, out erro))
            return false;

        requisitos = new RequisitosDePassiva
        {
            Nivel = dto.Nivel, Vocacao = vocacao, Classe = classe, Linhagem = linhagem, Variante = variante,
            Graduacao = dto.Graduacao, CoracaoDeMana = dto.CoracaoDeMana == true ? true : null,
            Afinidade = afinidade, Estrela = estrela, HistoricoId = historicoId,
            Atributos = atributos.Select(a => new RequisitoDeAtributo(a.Alvo, a.Minimo)).ToList(),
            SubAtributos = subAtributos.Select(s => new RequisitoDeSubAtributo(s.Alvo, s.Minimo)).ToList(),
            Pericias = pericias.Select(p => new RequisitoDePericia(p.Alvo, p.Minimo)).ToList(),
        };
        return true;
    }

    public static RequisitosDePassivaDto? ToDto(RequisitosDePassiva? r) => r is null ? null : new(
        r.Nivel, r.Vocacao?.ToString(), r.Classe, r.Linhagem?.ToString(), r.Variante?.ToString(), r.Graduacao, r.CoracaoDeMana,
        r.Afinidade?.ToString(), r.Estrela?.ToString(), r.HistoricoId?.ToString(),
        r.Atributos.Select(a => new RequisitoMinimoDto(a.Atributo.ToString(), a.Minimo)).ToList(),
        r.SubAtributos.Select(s => new RequisitoMinimoDto(s.SubAtributo.ToString(), s.Minimo)).ToList(),
        r.Pericias.Select(p => new RequisitoMinimoDto(p.Pericia.ToString(), p.Minimo)).ToList());

    /// <summary>Nome do Histórico exigido, para a pendência; nulo se não há requisito ou ele foi removido.</summary>
    public static async Task<string?> NomeDoHistoricoAsync(RuinaRpgDbContext db, RequisitosDePassiva? r) =>
        r?.HistoricoId is { } id ? await db.Historicos.Where(h => h.Id == id).Select(h => h.Nome).FirstOrDefaultAsync() : null;

    private static bool TryEnum<T>(string? raw, string campo, out T? value, ref string? erro) where T : struct, Enum
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        if (Enum.TryParse<T>(raw, out var parsed) && Enum.IsDefined(parsed))
        {
            value = parsed;
            return true;
        }
        erro = $"{campo} desconhecido(a): {raw}.";
        return false;
    }

    private static bool TryList<T>(List<RequisitoMinimoDto>? items, string campo, out List<(T Alvo, int Minimo)> parsed, out string? erro) where T : struct, Enum
    {
        parsed = [];
        erro = null;
        foreach (var item in items ?? [])
        {
            if (!Enum.TryParse<T>(item.Alvo, out var alvo) || !Enum.IsDefined(alvo))
                return Fail($"{campo} desconhecido(a): {item.Alvo}.", out erro);
            if (item.Minimo < 0)
                return Fail($"O mínimo de {campo} não pode ser negativo.", out erro);
            if (parsed.Any(p => p.Alvo.Equals(alvo)))
                return Fail($"{campo} repetido(a): {item.Alvo}.", out erro);
            parsed.Add((alvo, item.Minimo));
        }
        return true;
    }

    private static bool Fail(string mensagem, out string? erro)
    {
        erro = mensagem;
        return false;
    }
}
```
Check the Histórico entity's name property (`Historico.Nome` — confirm in `src/RuinaRPG.Infrastructure/Rules/` or wherever `db.Historicos` is declared) and the `Enum.TryParse` behaviour for numeric strings — `Enum.IsDefined` guards "99".

`SpellAbilityBankController` — add one private helper used by Create and Update, right after the Tipo parse:
```csharp
    /// <summary>
    /// Passiva (R0009): exige Categoria, aceita Requisitos, não tem Grau nem Efeitos. Os demais tipos não
    /// têm Categoria nem Requisitos. Devolve a mensagem de erro, ou nulo quando o pedido é válido.
    /// </summary>
    private async Task<string?> ValidarPassivaAsync(SpellAbilityTipo tipo, int grau, List<SpellAbilityEffectRequest> efeitos,
        string? categoriaRaw, RequisitosDePassivaDto? requisitosDto, Action<CategoriaDePassiva?, RequisitosDePassiva?> aplicar)
    {
        if (tipo != SpellAbilityTipo.Passiva)
        {
            if (categoriaRaw is not null || requisitosDto is not null)
                return "Categoria e Requisitos só existem em Passivas.";
            aplicar(null, null);
            return null;
        }

        if (grau != 0 || efeitos.Count > 0)
            return "Uma Passiva não tem Grau nem Efeitos.";
        if (!Enum.TryParse<CategoriaDePassiva>(categoriaRaw, out var categoria) || !Enum.IsDefined(categoria))
            return "Categoria desconhecida. Use Livre, Vocacional ou DeClasse.";
        if (!RequisitosDePassivaMapper.TryParse(requisitosDto, out var requisitos, out var erro))
            return erro;
        if (requisitos?.HistoricoId is { } historicoId && !await db.Historicos.AnyAsync(h => h.Id == historicoId))
            return "Histórico não encontrado.";

        aplicar(categoria, requisitos);
        return null;
    }
```
In `Create`: after the Tipo parse,
```csharp
        CategoriaDePassiva? categoria = null; RequisitosDePassiva? requisitos = null;
        var passivaError = await ValidarPassivaAsync(tipo, request.Grau, request.Efeitos, request.Categoria, request.Requisitos, (c, r) => { categoria = c; requisitos = r; });
        if (passivaError is not null)
            return BadRequest(passivaError);
```
then set `Categoria = categoria, Requisitos = requisitos` on the new entry. Same in `Update` (assign `entry.Categoria = categoria; entry.Requisitos = requisitos;`). Update the "Tipo desconhecido" messages to `"Tipo desconhecido. Use Magia, Habilidade, Racial ou Passiva."`. `EfeitoValidationHelper.ValidarAsync(db, 0, [])` must pass for a Passiva — if it doesn't, skip it when `tipo == Passiva`.

`ToResponse` in `SpellAbilityBankController` and `ToSpellAbilityResponse` in `CampaignCatalogController`: append `entry.Categoria?.ToString(), RequisitosDePassivaMapper.ToDto(entry.Requisitos)`.

Docs — `Requisitos - Banco de Magias e Habilidades.md`:
- R0004: filter Tipo lists "Magia, Habilidade, Racial ou Passiva"; Passiva rows show the Categoria instead of Grau/Efeitos.
- R0005: mention that a Passiva's fields are those of R0009.
- New `# **R0009** - Passivas são cadastradas só no banco, com Categoria e Requisitos.` with a `**Descrição**:` paragraph: fields Nome, Descrição, Categoria (Passiva Livre / Passiva Vocacional / Passiva de Classe), checkbox Magia/Habilidade de Criatura (R0008), and a Requisitos section (the full list from the spec's Requisitos table, including "Atributo compara com o Total", Sub-Atributos list, Perícias list; NULL = not a requirement; all filled must be met); no Grau/Efeitos/PI/Custo; never built from scratch on a sheet (see [[Requisitos - Ficha de Personagem]] 4.f); an ⓘ popup explains the cadastro to the GM.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SpellAbilityBank|FullyQualifiedName~CampaignCatalog"` → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Contracts src/RuinaRPG.Api tests/RuinaRPG.Tests.Integration/Controllers/SpellAbilityBankPassivaTests.cs "Docs/Requisitos/Requisitos - Banco de Magias e Habilidades.md"
git commit -m "feat(api): Passivas no Banco de Magias com Categoria e Requisitos"
```

---

### Task 4: Sheet stats services (sub-attributes refactor + requisitos snapshot)

**Files:**
- Create: `src/RuinaRPG.Api/Services/CharacterSheetStats.cs`, `NpcSheetStats.cs`, `CreatureSheetStats.cs`
- Modify: `src/RuinaRPG.Api/Program.cs` (next to `builder.Services.AddScoped<EquipmentKitGrantService>();`)
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs` (`SubAttributes`, ~383-470), `NpcSheetsController.cs` (`SubAttributes`, ~346), `CreatureSheetsController.cs` (`SubAttributes`, ~315)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/SheetStatsTests.cs`

**Interfaces:**
- Consumes: Task 1 `FichaParaRequisitos`, `SubAtributo`.
- Produces (each service is `Scoped`, primary-ctor `(RuinaRpgDbContext db, IRulesDataProvider rules)`):
  - `Task<SubAttributesResponse> SubAtributosAsync(CharacterSheet sheet)` / `(NpcSheet sheet)` / `(CreatureSheet sheet)`
  - `Task<FichaParaRequisitos> FichaParaRequisitosAsync(CharacterSheet sheet)` / `(NpcSheet)` / `(CreatureSheet)`

Existing tests for `/sub-attributes` on all 3 sheets are the regression guard for the move — they must stay green unchanged.

- [ ] **Step 1: Write the failing test**

The services are exercised via DI from the `ApiFactory` so the test hits real data. Reuse the sheet-setup helpers pattern from `CharacterSpellAbilitiesControllerTests` (register GM, register linked jogador, create campaign + member + sheet) and from `NpcSpellAbilitiesControllerTests.CreateSheetAsync` / `CreatureSpellAbilitiesControllerTests.CreateSheetAsync`.

```csharp
// tests/RuinaRPG.Tests.Integration/Controllers/SheetStatsTests.cs
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
// ... plus the HTTP usings/helpers copied from CharacterSpellAbilitiesControllerTests

public class SheetStatsTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    // ...fixture + helpers...

    [Fact]
    public async Task Character_snapshot_matches_what_the_sheet_endpoints_show()
    {
        var gm = await RegisterGmAndGetTokenAsync("StatsGm1", "statsgm1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "StatsPlayer1", "statsplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gm, playerId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", playerToken, new UpdateCharacterAttributeRequest(5, 0, false)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Agilidade", playerToken, new UpdateCharacterAttributeRequest(2, 0, false)));

        var subAttributes = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken)))
            .Content.ReadFromJsonAsync<SubAttributesResponse>();
        var skills = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken)))
            .Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var stats = scope.ServiceProvider.GetRequiredService<CharacterSheetStats>();
        var sheet = await db.CharacterSheets.FindAsync(Guid.Parse(sheetId));
        var ficha = await stats.FichaParaRequisitosAsync(sheet!);

        ficha.TemIdentidadeDePersonagem.Should().BeTrue();
        ficha.Nivel.Should().Be(sheet!.Nivel);
        ficha.Atributos[Atributo.Forca].Should().Be(5);
        ficha.Atributos.Should().HaveCount(8);
        ficha.SubAtributos[SubAtributo.Iniciativa].Should().Be(subAttributes!.Iniciativa);
        ficha.SubAtributos[SubAtributo.DefesaNatural].Should().Be(subAttributes.DefesaNatural);
        foreach (var skill in skills!)
            ficha.Pericias[Enum.Parse<Pericia>(skill.Pericia)].Should().Be(skill.Total);
    }

    [Fact]
    public async Task Creature_snapshot_has_no_personagem_identity_and_only_its_own_attributes_and_pericias()
    {
        var gm = await RegisterGmAndGetTokenAsync("StatsGm2", "statsgm2@teste.com");
        var sheetId = await CreateCreatureSheetAsync(gm); // POST /api/creature-sheets — copy CreateSheetAsync from CreatureSpellAbilitiesControllerTests

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var stats = scope.ServiceProvider.GetRequiredService<CreatureSheetStats>();
        var ficha = await stats.FichaParaRequisitosAsync((await db.CreatureSheets.FindAsync(Guid.Parse(sheetId)))!);

        ficha.TemIdentidadeDePersonagem.Should().BeFalse();
        ficha.Atributos.Keys.Should().BeEquivalentTo([Atributo.Forca, Atributo.Vigor, Atributo.Agilidade, Atributo.Destreza, Atributo.Astucia]);
        ficha.Pericias.Keys.Should().BeEquivalentTo(CreatureSkillAllowList.AllowedPericias);
        ficha.SubAtributos.Should().HaveCount(6);
    }

    [Fact]
    public async Task Npc_snapshot_has_personagem_identity()
    {
        var gm = await RegisterGmAndGetTokenAsync("StatsGm3", "statsgm3@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm); // copy CreateSheetAsync from NpcSpellAbilitiesControllerTests

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var stats = scope.ServiceProvider.GetRequiredService<NpcSheetStats>();
        var ficha = await stats.FichaParaRequisitosAsync((await db.NpcSheets.FindAsync(Guid.Parse(sheetId)))!);

        ficha.TemIdentidadeDePersonagem.Should().BeTrue();
        ficha.Atributos.Should().HaveCount(8);
    }
}
```
(Check the real skills route and `CharacterSkillResponse` field names — `Pericia`, `Total` — in `CharacterSkillsController`; `CreatureSkillAllowList` lives in the Domain — `grep -rn "class CreatureSkillAllowList" src`.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SheetStatsTests`
Expected: build FAIL — `CharacterSheetStats` etc. not defined.

- [ ] **Step 3: Implement**

For each sheet type, create the service by **moving** code out of the controller (not rewriting formulas):

`CharacterSheetStats`:
- `SubAtributosAsync(CharacterSheet sheet)` = the body of `CharacterSheetsController.SubAttributes` from `var artefatos = await GetArtifactBonusInputsAsync(id);` to the `return new SubAttributesResponse(...)`, with `id` → `sheet.Id`. Move `GetArtifactBonusInputsAsync` and `GetAttributeTotalAsync` into the service as private methods; the controller still needs them for `ComputeResourceMaximumsAsync`/other actions — keep the controller's copies only if still referenced after the move, otherwise delete them (no unused private methods).
- `FichaParaRequisitosAsync(CharacterSheet sheet)`:

```csharp
    public async Task<FichaParaRequisitos> FichaParaRequisitosAsync(CharacterSheet sheet)
    {
        var artefatos = await GetArtifactBonusInputsAsync(sheet.Id);
        var atributos = await db.CharacterAttributes.Where(a => a.CharacterSheetId == sheet.Id)
            .ToDictionaryAsync(a => a.Atributo, a => AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria,
                artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, a.Atributo.ToString())));

        // Mesmo Total da 2.d que CharacterSkillsController.List mostra.
        var historico = sheet.HistoricoId is null ? null : await db.Historicos.FindAsync(sheet.HistoricoId.Value);
        var skills = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheet.Id).ToListAsync();
        var pericias = skills.ToDictionary(s => s.Pericia, s =>
        {
            var modificador = SkillFormulas.Modificador(s.Gasto, HistoricoBonusCalculator.For(s.Pericia, historico?.PericiaMaisSeis, historico?.PericiaMaisTres));
            return s.AtributoEscolhido is { } atributo && atributos.TryGetValue(atributo, out var atributoTotal)
                ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, s.Pericia.ToString()))
                : (int?)null;
        });

        var sub = await SubAtributosAsync(sheet);

        // Mesmo Grau/Círculo que CharacterSheetsController.ToResponseAsync mostra em 1.b.
        var eapAtual = EapCalculator.Compute(sheet.Nivel, sheet.NucleosRankF, sheet.NucleosRankE, sheet.NucleosRankD, sheet.NucleosRankC, sheet.NucleosRankB, sheet.NucleosRankA, sheet.NucleosRankS, rules.EapPorNivel);
        var graduacao = sheet.Vocacao is null ? 0 : GraduacaoCalculator.Compute(sheet.Vocacao.Value, eapAtual, sheet.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);

        return new FichaParaRequisitos(
            TemIdentidadeDePersonagem: true, sheet.Nivel, sheet.Vocacao, sheet.SubVocacao, sheet.Linhagem, sheet.Variante,
            graduacao, sheet.PossuiCoracaoDeMana, sheet.Afinidade, sheet.Estrela, sheet.HistoricoId,
            atributos, SubAtributosPorEnum(sub), pericias);
    }

    internal static Dictionary<SubAtributo, int> SubAtributosPorEnum(SubAttributesResponse sub) => new()
    {
        [SubAtributo.Iniciativa] = sub.Iniciativa,
        [SubAtributo.Movimentacao] = sub.Movimentacao,
        [SubAtributo.EsquivaNatural] = sub.EsquivaNatural,
        [SubAtributo.DefesaNatural] = sub.DefesaNatural,
        [SubAtributo.ReducaoFisica] = sub.ReducaoFisica,
        [SubAtributo.ReducaoMagica] = sub.ReducaoMagica,
    };
```
(If `SubAttributesResponse` properties are nullable ints for Criatura, adapt with `?? 0`. Put `SubAtributosPorEnum` as `internal static` on `CharacterSheetStats` and call it from the other two services.)

`NpcSheetStats`: same shape from `NpcSheetsController` — NPC's Graduação uses the stored `s.EAPAtual` exactly like `NpcSheetsController.ToResponseAsync` line ~585 (`GraduacaoCalculator.Compute(vocacao, s.EAPAtual, s.PossuiCoracaoDeMana, rules.CirculoGrauPorEap)`), skills totals from `NpcSkillsController.List`, tables `NpcAttributes`/`NpcSkills`/`NpcArtifacts`.

`CreatureSheetStats`: from `CreatureSheetsController`; attributes map `AtributoCriatura` → `Atributo` for the 5 shared ones only (`Forca, Vigor, Agilidade, Destreza, Astucia`; `Ego` is dropped); skills totals from `CreatureSkillsController.List` (its `AtributoEscolhido` is `AtributoCriatura`, look up the creature totals dictionary keyed by `AtributoCriatura`); return
```csharp
new FichaParaRequisitos(false, sheet.Nivel, null, null, null, null, 0, false, sheet.Afinidade, null, null, atributos, CharacterSheetStats.SubAtributosPorEnum(sub), pericias)
```

`Program.cs`:
```csharp
builder.Services.AddScoped<CharacterSheetStats>();
builder.Services.AddScoped<NpcSheetStats>();
builder.Services.AddScoped<CreatureSheetStats>();
```

Controllers: inject the service (primary-ctor param) and make each `SubAttributes` action keep its lookup + authorization lines and then `return await stats.SubAtributosAsync(sheet);`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SheetStatsTests|FullyQualifiedName~SheetsControllerTests|FullyQualifiedName~Skills"` → PASS (the existing sub-attributes tests live in the `*SheetsControllerTests` classes). `dotnet build` → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api tests/RuinaRPG.Tests.Integration/Controllers/SheetStatsTests.cs
git commit -m "refactor(api): serviços de estatísticas da ficha (sub-atributos + snapshot de requisitos)"
```

---

### Task 5: Sheet endpoints — add/list Passivas with requisitos, disponíveis, grants

**Files:**
- Create: `src/RuinaRPG.Contracts/SpellsAndAbilities/PassivaDisponivelResponse.cs`
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSpellAbilityResponse.cs`, `NpcSheets/NpcSpellAbilityResponse.cs`, `CreatureSheets/CreatureSpellAbilityResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSpellAbilitiesController.cs`, `NpcSpellAbilitiesController.cs`, `CreatureSpellAbilitiesController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignGrantsController.cs` (NPC copy ~line 245, Creature copy ~line 303)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md`, `Requisitos - Ficha de NPCs.md`, `Requisitos - Ficha de Criaturas.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterPassivasTests.cs`, `NpcCreaturePassivasTests.cs`

**Interfaces:**
- Consumes: Tasks 1–4 (`PassivaRequisitosEvaluator.Pendencias`, `RequisitosDePassivaMapper.ToDto/NomeDoHistoricoAsync`, `*SheetStats.FichaParaRequisitosAsync`).
- Produces:
  - `record PassivaDisponivelResponse(SpellAbilityEntryResponse Entrada, List<string> Pendencias)`
  - `CharacterSpellAbilityResponse(..., List<SpellAbilityEffectResponse> Efeitos, string? Categoria = null, RequisitosDePassivaDto? Requisitos = null, List<string>? RequisitosPendentes = null)` — same 3 trailing params on the Npc/Creature responses.
  - `GET api/{character|npc|creature}-sheets/{sheetId}/spell-abilities/passivas-disponiveis` → `List<PassivaDisponivelResponse>`

- [ ] **Step 1: Write the failing tests**

`CharacterPassivasTests` — copy the helpers from `CharacterSpellAbilitiesControllerTests` (`AuthedRequest`, `RegisterGmAndGetTokenAsync`, `RegisterJogadorLinkedToAsync`, `SetUpSheetInCampaignAsync`, `PublishBankEntryAsync`) and add:

```csharp
    private async Task<string> CreatePassivaAsync(string gmToken, string nome, RequisitosDePassivaDto? requisitos)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Passiva", 0, "Descrição.", [], false, "Livre", requisitos)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private Task<HttpResponseMessage> AddFromBankAsync(string token, string sheetId, string entryId) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", token,
            new AddCharacterSpellAbilityRequest(entryId, null, null, null, null, null)));

    private Task SetForcaAsync(string token, string sheetId, int gasto) =>
        _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", token, new UpdateCharacterAttributeRequest(gasto, 0, false)));

    [Fact]
    public async Task Building_a_passiva_from_scratch_on_a_sheet_is_refused()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm1", "passgm1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "PassPl1", "passpl1@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/spell-abilities", gm,
            new AddCharacterSpellAbilityRequest(null, "P", "Passiva", 0, "d", [])));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Passivas só são cadastradas no Banco de Magias e Habilidades.");
    }

    [Fact]
    public async Task Unmet_requisitos_block_the_gm_and_the_player_alike()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm2", "passgm2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PassPl2", "passpl2@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Força Bruta", new RequisitosDePassivaDto(Atributos: [new("Forca", 4)]));
        await PublishBankEntryAsync(gm, campaignId, passivaId);

        foreach (var token in new[] { gm, playerToken })
        {
            var response = await AddFromBankAsync(token, sheetId, passivaId);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).Should().Contain("Requisitos não cumpridos: Força ≥ 4");
        }
    }

    [Fact]
    public async Task Met_requisitos_copy_categoria_and_requisitos_and_a_later_change_shows_the_warning()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm3", "passgm3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PassPl3", "passpl3@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Força Bruta", new RequisitosDePassivaDto(Atributos: [new("Forca", 4)]));
        await PublishBankEntryAsync(gm, campaignId, passivaId);
        await SetForcaAsync(playerToken, sheetId, 5);

        var response = await AddFromBankAsync(playerToken, sheetId, passivaId);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await response.Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!;
        created.Tipo.Should().Be("Passiva");
        created.Categoria.Should().Be("Livre");
        created.RequisitosPendentes.Should().BeEmpty();

        await SetForcaAsync(playerToken, sheetId, 2);
        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/spell-abilities", playerToken)))
            .Content.ReadFromJsonAsync<List<CharacterSpellAbilityResponse>>();
        list!.Single(e => e.Id == created.Id).RequisitosPendentes.Should().Equal("Força ≥ 4");
    }

    [Fact]
    public async Task A_passiva_without_requisitos_is_always_addable()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm4", "passgm4@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "PassPl4", "passpl4@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Livre", null);

        (await AddFromBankAsync(gm, sheetId, passivaId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_player_never_sees_the_requisitos_of_a_passiva_that_is_not_public()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm5", "passgm5@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PassPl5", "passpl5@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);
        var passivaId = await CreatePassivaAsync(gm, "Secreta", new RequisitosDePassivaDto(Nivel: 20));

        var response = await AddFromBankAsync(playerToken, sheetId, passivaId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("Entrada do banco não encontrada.");
        text.Should().NotContain("Nível");
    }

    [Fact]
    public async Task Passivas_disponiveis_lists_reachable_passivas_with_their_pendencias()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm6", "passgm6@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "PassPl6", "passpl6@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gm, playerId);
        var publica = await CreatePassivaAsync(gm, "Pública", new RequisitosDePassivaDto(Nivel: 20));
        var privada = await CreatePassivaAsync(gm, "Privada", null);
        await PublishBankEntryAsync(gm, campaignId, publica);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gm,
            new CreateSpellAbilityEntryRequest("Uma Magia", "Magia", 1, "d", [])));

        var url = $"/api/character-sheets/{sheetId}/spell-abilities/passivas-disponiveis";
        var paraGm = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, url, gm))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraGm!.Select(p => p.Entrada.Nome).Should().BeEquivalentTo(["Pública", "Privada"]);
        paraGm.Single(p => p.Entrada.Nome == "Pública").Pendencias.Should().Equal("Nível 20");

        var paraJogador = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, url, playerToken))).Content.ReadFromJsonAsync<List<PassivaDisponivelResponse>>();
        paraJogador!.Select(p => p.Entrada.Nome).Should().Equal("Pública");
    }

    [Fact]
    public async Task A_historico_requisito_is_checked_and_named()
    {
        var gm = await RegisterGmAndGetTokenAsync("PassGm7", "passgm7@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gm, "PassPl7", "passpl7@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gm, playerId);
        var historicos = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/historicos", gm))).Content.ReadFromJsonAsync<List<HistoricoResponse>>();
        var historico = historicos!.First();
        var passivaId = await CreatePassivaAsync(gm, "De Berço", new RequisitosDePassivaDto(HistoricoId: historico.Id));

        var response = await AddFromBankAsync(gm, sheetId, passivaId);

        (await response.Content.ReadAsStringAsync()).Should().Contain($"Histórico: {historico.Nome}");
    }
```
(Confirm the Históricos list route and response type with `grep -n "Http\|record" src/RuinaRPG.Api/Controllers/HistoricosController.cs src/RuinaRPG.Contracts/*/Historico*`.)

`NpcCreaturePassivasTests` — copy helpers from `NpcSpellAbilitiesControllerTests` and `CreatureSpellAbilitiesControllerTests` (`CreateSheetAsync`, `CreateCampaignAsync`, `RegisterJogadorLinkedToAsync`, `GrantBlankNpcAsync`/`GrantBlankCreatureAsync`, `PublishBankEntryAsync`). Tests:

```csharp
    [Fact]
    public async Task Npc_add_from_bank_checks_requisitos_and_from_scratch_passiva_is_refused()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPassGm1", "npcpassgm1@teste.com");
        var sheetId = await CreateNpcSheetAsync(gm);
        var passivaId = await CreatePassivaAsync(gm, "Nobreza", new RequisitosDePassivaDto(Vocacao: "Bruxo"));

        var add = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)));
        (await add.Content.ReadAsStringAsync()).Should().Contain("Requisitos não cumpridos: Vocação: Bruxo");

        var scratch = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(null, "P", "Passiva", 0, "d", [])));
        scratch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creature_ignores_personagem_only_requisitos_but_checks_nivel()
    {
        var gm = await RegisterGmAndGetTokenAsync("CrPassGm1", "crpassgm1@teste.com");
        var sheetId = await CreateCreatureSheetAsync(gm);
        var ignorada = await CreatePassivaAsync(gm, "De Bruxo", new RequisitosDePassivaDto(Vocacao: "Bruxo", Estrela: "Sadir", Atributos: [new("Instinto", 99)]));
        var alta = await CreatePassivaAsync(gm, "Alta", new RequisitosDePassivaDto(Nivel: 30));

        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(ignorada, null, null, null, null, null)))).StatusCode.Should().Be(HttpStatusCode.Created);
        var bloqueada = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/creature-sheets/{sheetId}/spell-abilities", gm,
            new AddCreatureSpellAbilityRequest(alta, null, null, null, null, null)));
        (await bloqueada.Content.ReadAsStringAsync()).Should().Contain("Nível 30");
    }

    [Fact]
    public async Task Granting_an_npc_copies_its_passivas_categoria_and_requisitos()
    {
        var gm = await RegisterGmAndGetTokenAsync("NpcPassGm2", "npcpassgm2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gm, "NpcPassPl2", "npcpasspl2@teste.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha");
        var sourceId = await CreateNpcSheetAsync(gm);
        var passivaId = await CreatePassivaAsync(gm, "Vigia", new RequisitosDePassivaDto(Nivel: 1));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sourceId}/spell-abilities", gm,
            new AddNpcSpellAbilityRequest(passivaId, null, null, null, null, null)));

        var grantedId = await GrantNpcAsync(gm, campaignId, playerId, sourceId); // same call GrantBlankNpcAsync makes, with sourceId

        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{grantedId}/spell-abilities", playerToken)))
            .Content.ReadFromJsonAsync<List<NpcSpellAbilityResponse>>();
        var copy = list!.Single(e => e.Nome == "Vigia");
        copy.Categoria.Should().Be("Livre");
        copy.Requisitos!.Nivel.Should().Be(1);
    }
```
(`GrantNpcAsync` = the body of `GrantBlankNpcAsync` in `NpcSpellAbilitiesControllerTests` with the source sheet id passed in instead of a blank one — read that helper to see the grant request shape.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterPassivasTests|FullyQualifiedName~NpcCreaturePassivasTests"`
Expected: build FAIL (`PassivaDisponivelResponse`, response params missing).

- [ ] **Step 3: Implement**

`PassivaDisponivelResponse.cs`:
```csharp
namespace RuinaRPG.Contracts.SpellsAndAbilities;

/// <summary>Uma Passiva do Banco que a ficha pode receber, com o que falta para cumprir os requisitos.</summary>
public record PassivaDisponivelResponse(SpellAbilityEntryResponse Entrada, List<string> Pendencias);
```

Responses — append to each of the 3 sheet response records:
```csharp
    string? Categoria = null, RequisitosDePassivaDto? Requisitos = null, List<string>? RequisitosPendentes = null
```

`CharacterSpellAbilitiesController` (inject `CharacterSheetStats stats` in the primary ctor). Changes to `Add`:
1. In the from-scratch branch, right after parsing `tipo`:
```csharp
            if (tipo == SpellAbilityTipo.Passiva)
                return BadRequest("Passivas só são cadastradas no Banco de Magias e Habilidades.");
```
2. In the from-bank branch, **after** both existing access checks (so a non-public entry still answers "Entrada do banco não encontrada."):
```csharp
            if (bankEntry.Tipo == SpellAbilityTipo.Passiva)
            {
                var pendencias = PassivaRequisitosEvaluator.Pendencias(bankEntry.Requisitos, await stats.FichaParaRequisitosAsync(sheet),
                    await RequisitosDePassivaMapper.NomeDoHistoricoAsync(db, bankEntry.Requisitos));
                if (pendencias.Count > 0)
                    return BadRequest("Requisitos não cumpridos: " + string.Join(", ", pendencias));
            }
            categoria = bankEntry.Categoria; requisitos = bankEntry.Requisitos;
```
(declare `CategoriaDePassiva? categoria = null; RequisitosDePassiva? requisitos = null;` next to the other locals) and set `Categoria = categoria, Requisitos = requisitos` on `sheetCopy`.
3. `ToResponse` becomes async-aware: compute pendências only for Passivas. Replace the static `ToResponse` with:
```csharp
    private async Task<CharacterSpellAbilityResponse> ToResponseAsync(CharacterSpellAbility e, FichaParaRequisitos? ficha)
    {
        List<string>? pendentes = null;
        if (e.Tipo == SpellAbilityTipo.Passiva && ficha is not null)
            pendentes = PassivaRequisitosEvaluator.Pendencias(e.Requisitos, ficha, await RequisitosDePassivaMapper.NomeDoHistoricoAsync(db, e.Requisitos)).ToList();
        return new(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao,
            e.Efeitos.Select(ef => new SpellAbilityEffectResponse(ef.EfeitoNome, ef.Quantidade, ef.CustoPI)).ToList(),
            e.Categoria?.ToString(), RequisitosDePassivaMapper.ToDto(e.Requisitos), pendentes);
    }
```
In `Add`: `return Created(string.Empty, await ToResponseAsync(sheetCopy, sheetCopy.Tipo == SpellAbilityTipo.Passiva ? await stats.FichaParaRequisitosAsync(sheet) : null));`
In `List`: build the snapshot once only if any entry is a Passiva, then map each entry sequentially (`foreach` + `await`, no `Task.WhenAll` — one DbContext).
4. New action:
```csharp
    [HttpGet("passivas-disponiveis")]
    public async Task<ActionResult<List<PassivaDisponivelResponse>>> PassivasDisponiveis(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return NotFound();

        // Mesmo alcance do Add: o GM vê todas as Passivas do próprio banco; o jogador, só as públicas na campanha.
        var query = db.SpellAbilityBankEntries.Where(e => e.GmId == campaignGmId && e.Tipo == SpellAbilityTipo.Passiva);
        if (CurrentUserId() != campaignGmId)
            query = query.Where(e => db.CampaignAttachments.Any(a => a.CampaignId == sheet.CampaignId && a.IsPublic && a.SpellAbilityBankEntryId == e.Id));

        var entradas = await query.OrderBy(e => e.Nome).ToListAsync();
        var ficha = await stats.FichaParaRequisitosAsync(sheet);
        var result = new List<PassivaDisponivelResponse>();
        foreach (var e in entradas)
        {
            var pendencias = PassivaRequisitosEvaluator.Pendencias(e.Requisitos, ficha, await RequisitosDePassivaMapper.NomeDoHistoricoAsync(db, e.Requisitos));
            result.Add(new PassivaDisponivelResponse(
                new SpellAbilityEntryResponse(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao, [], e.DeCriatura,
                    e.Categoria?.ToString(), RequisitosDePassivaMapper.ToDto(e.Requisitos)),
                pendencias.ToList()));
        }
        return result;
    }
```

`NpcSpellAbilitiesController` / `CreatureSpellAbilitiesController`: the same four changes, using `NpcSheetStats`/`CreatureSheetStats`, `sheet.GmId` as the GM, `GrantedSheetAuthorization.CanEdit` (→ `NotFound`) as in their existing actions, and for the player branch of `passivas-disponiveis` resolve `campaignId` exactly like their `Add` does (grant-link `CampaignAttachments` lookup); a player with no resolvable campaign gets an empty list.

`CampaignGrantsController`: in both `new NpcSpellAbility { ... }` (~245) and `new CreatureSpellAbility { ... }` (~303) add `Categoria = sa.Categoria, Requisitos = sa.Requisitos`. (`PublicarMagiaNaCampanhaAsync` reuses the source bank entry via `SourceBankEntryId`, which a Passiva always has, so the bank side needs no change.)

Docs:
- `Requisitos - Ficha de Personagem.md`: in the tab-4 summary line add "habilidades passivas"; in 4.b change *Tipo* to note that Passivas are not built here (they live in 4.f); add `### 4.f) Habilidades Passivas` after 4.e (or wherever the last 4.x subsection is — continue the letter sequence): lista incremental; o jogador/GM só adiciona escolhendo uma Passiva do "[[Requisitos - Banco de Magias e Habilidades]]" (R0009) — jogador só entre as públicas da campanha (R0003); cada entrada mostra Nome, Categoria, Descrição e, se a ficha deixou de cumprir os requisitos, o aviso "⚠ Requisitos não cumpridos" com o que falta; removível. Add a new `# **R00NN**` (next number in that file) "Uma Passiva só entra na ficha se ela cumprir os requisitos" — the server refuses for anyone, GM included; the add dialog shows the Passivas that don't meet them disabled, with the reason; a Passiva already on the sheet stays with the warning when the sheet stops meeting them.
- `Requisitos - Ficha de NPCs.md`: one line in its 4.x diff list: 4.f exists, same rules.
- `Requisitos - Ficha de Criaturas.md`: 4.f exists; requisitos on fields the Criatura doesn't have (list them) are ignored; attributes compared only for Força, Vigor, Agilidade, Destreza, Astúcia.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Passivas|FullyQualifiedName~SpellAbilit|FullyQualifiedName~CampaignGrants"` → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src tests/RuinaRPG.Tests.Integration/Controllers/CharacterPassivasTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcCreaturePassivasTests.cs Docs/Requisitos
git commit -m "feat(api): Passivas nas fichas com bloqueio por requisitos e aviso"
```

---

### Task 6: Client — Passiva form in the Banco de Magias

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Fields/RequisitosFormModel.cs`
- Create: `src/RuinaRPG.Client/Shared/Fields/RequisitosDePassivaEditor.razor`
- Modify: `src/RuinaRPG.Client/Pages/BancoDeMagiasForm.razor`
- Modify: `src/RuinaRPG.Client/Pages/BancoDeMagias.razor`
- Test: `tests/RuinaRPG.Tests.Client/Pages/BancoDeMagiasFormTests.cs`, `tests/RuinaRPG.Tests.Client/Shared/Fields/RequisitosFormModelTests.cs`

**Interfaces:**
- Consumes: Task 3 contracts (`RequisitosDePassivaDto`, `RequisitoMinimoDto`, request/response params), existing field components `VocacaoSubVocacaoFields` (`Vocacao`/`SubVocacao` two-way), `LinhagemVarianteFields` (`Linhagem`/`Variante`), `AfinidadeSelect` (`Value`), `EstrelaSelect` (`Value`), `HistoricoSelect` (`Value`), `InfoPopup`, `Section` (`TitleInfo`/`ChildContent`), `AtributoDisplay`, `PericiaDisplay`, `RequisitoLabels`.
- Produces:
  - `class RequisitosFormModel` — mutable: `int? Nivel; string? Vocacao; string? Classe; string? Linhagem; string? Variante; int? Graduacao; bool CoracaoDeMana; string? Afinidade; string? Estrela; string? HistoricoId; List<RequisitoMinimoLinha> Atributos, SubAtributos, Pericias;` with `static RequisitosFormModel FromDto(RequisitosDePassivaDto?)` and `RequisitosDePassivaDto ToDto()`; `class RequisitoMinimoLinha { string Alvo; int Minimo; }`.
  - `<RequisitosDePassivaEditor Model="..." OnChanged="..." />`

- [ ] **Step 1: Write the failing tests**

`RequisitosFormModelTests`:
```csharp
using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class RequisitosFormModelTests
{
    [Fact]
    public void Round_trips_through_the_dto()
    {
        var dto = new RequisitosDePassivaDto(Nivel: 3, Vocacao: "Bruxo", Classe: "Ocultista", CoracaoDeMana: true,
            Atributos: [new("Forca", 4)], SubAtributos: [new("Iniciativa", 2)], Pericias: [new("Atletismo", 1)]);

        RequisitosFormModel.FromDto(dto).ToDto().Should().BeEquivalentTo(dto with { Linhagem = null });
    }

    [Fact]
    public void Blank_strings_and_unchecked_coracao_become_null()
    {
        var model = new RequisitosFormModel { Vocacao = "", Classe = "  ", CoracaoDeMana = false };
        var dto = model.ToDto();
        dto.Vocacao.Should().BeNull();
        dto.Classe.Should().BeNull();
        dto.CoracaoDeMana.Should().BeNull();
    }
}
```

In `BancoDeMagiasFormTests` add (reuse `RenderWithDialogProvider` and the `efeitos` stub from the existing tests; for `historicos`/`estrelas` GETs from `HistoricoSelect`/`EstrelaSelect` answer `[]`):
```csharp
    [Fact]
    public async Task Choosing_Passiva_hides_grau_and_efeitos_and_shows_categoria_and_requisitos()
    {
        var http = FakeHttpMessageHandler.CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
        Services.AddScoped(_ => http);
        var cut = Render<BancoDeMagiasForm>();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Efeitos");
        await cut.InvokeAsync(() => cut.FindComponents<MudSelect<string>>().First(s => s.Instance.Label == "Tipo").Instance.ValueChanged.InvokeAsync("Passiva"));

        cut.FindComponents<MudNumericField<int>>().Should().NotContain(f => f.Instance.Label == "Grau");
        cut.Markup.Should().NotContain("Gasto em PI");
        cut.FindComponents<MudSelect<string>>().Should().Contain(s => s.Instance.Label == "Categoria");
        cut.Markup.Should().Contain("Requisitos");
        cut.Find("button[title='Como cadastrar uma Passiva']");
    }

    [Fact]
    public async Task Saving_a_passiva_sends_grau_zero_no_efeitos_categoria_and_requisitos()
    {
        CreateSpellAbilityEntryRequest? sent = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                sent = request.Content!.ReadFromJsonAsync<CreateSpellAbilityEntryRequest>().Result;
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { Id = "x" }) };
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
        });
        Services.AddScoped(_ => http);
        var cut = Render<BancoDeMagiasForm>();
        await Task.Delay(50);

        await cut.InvokeAsync(() => cut.FindComponents<MudTextField<string>>().First(f => f.Instance.Label == "Nome").Instance.ValueChanged.InvokeAsync("Pele de Pedra"));
        await cut.InvokeAsync(() => cut.FindComponents<MudSelect<string>>().First(s => s.Instance.Label == "Tipo").Instance.ValueChanged.InvokeAsync("Passiva"));
        await cut.InvokeAsync(() => cut.FindComponents<MudSelect<string>>().First(s => s.Instance.Label == "Categoria").Instance.ValueChanged.InvokeAsync("DeClasse"));
        await cut.InvokeAsync(() => cut.FindComponents<MudNumericField<int?>>().First(f => f.Instance.Label == "Nível mínimo").Instance.ValueChanged.InvokeAsync(4));
        cut.FindAll("button").First(b => b.TextContent.Contains("Salvar")).Click();
        await Task.Delay(50);

        sent!.Tipo.Should().Be("Passiva");
        sent.Grau.Should().Be(0);
        sent.Efeitos.Should().BeEmpty();
        sent.Categoria.Should().Be("DeClasse");
        sent.Requisitos!.Nivel.Should().Be(4);
    }

    [Fact]
    public async Task Saving_a_magia_sends_null_categoria_and_requisitos()
    {
        CreateSpellAbilityEntryRequest? sent = null;
        // same handler as above
        // set Nome = "Bola", keep Tipo = Magia, click Salvar
        sent!.Categoria.Should().BeNull();
        sent.Requisitos.Should().BeNull();
    }
```
(Write the third test out fully with the same handler; the comment lines above are just where the handler/steps from the previous test go — copy them.)

In the existing `BancoDeMagiasTests` (the list page) add a test that the Tipo filter offers "Passiva" and that a Passiva row renders `"Passiva Vocacional"` for `Categoria = "Vocacional"` instead of the Grau/Efeitos summary — follow that file's existing row-rendering test.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~BancoDeMagias|FullyQualifiedName~RequisitosFormModel"`
Expected: FAIL (types missing / no Passiva option).

- [ ] **Step 3: Implement**

`RequisitosFormModel.cs`:
```csharp
using RuinaRPG.Contracts.SpellsAndAbilities;

namespace RuinaRPG.Client.Shared.Fields;

public class RequisitoMinimoLinha
{
    public string Alvo { get; set; } = "";
    public int Minimo { get; set; }
}

/// <summary>Estado editável da seção Requisitos de uma Passiva; texto vazio vira nulo no DTO.</summary>
public class RequisitosFormModel
{
    public int? Nivel { get; set; }
    public string? Vocacao { get; set; }
    public string? Classe { get; set; }
    public string? Linhagem { get; set; }
    public string? Variante { get; set; }
    public int? Graduacao { get; set; }
    public bool CoracaoDeMana { get; set; }
    public string? Afinidade { get; set; }
    public string? Estrela { get; set; }
    public string? HistoricoId { get; set; }
    public List<RequisitoMinimoLinha> Atributos { get; set; } = new();
    public List<RequisitoMinimoLinha> SubAtributos { get; set; } = new();
    public List<RequisitoMinimoLinha> Pericias { get; set; } = new();

    public static RequisitosFormModel FromDto(RequisitosDePassivaDto? dto) => dto is null ? new() : new()
    {
        Nivel = dto.Nivel, Vocacao = dto.Vocacao, Classe = dto.Classe, Linhagem = dto.Linhagem, Variante = dto.Variante,
        Graduacao = dto.Graduacao, CoracaoDeMana = dto.CoracaoDeMana == true, Afinidade = dto.Afinidade, Estrela = dto.Estrela,
        HistoricoId = dto.HistoricoId,
        Atributos = Linhas(dto.Atributos), SubAtributos = Linhas(dto.SubAtributos), Pericias = Linhas(dto.Pericias),
    };

    public RequisitosDePassivaDto ToDto() => new(
        Nivel, Blank(Vocacao), Blank(Classe), Blank(Linhagem), Blank(Variante), Graduacao, CoracaoDeMana ? true : null,
        Blank(Afinidade), Blank(Estrela), Blank(HistoricoId), Dtos(Atributos), Dtos(SubAtributos), Dtos(Pericias));

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static List<RequisitoMinimoLinha> Linhas(List<RequisitoMinimoDto>? items) =>
        (items ?? []).Select(i => new RequisitoMinimoLinha { Alvo = i.Alvo, Minimo = i.Minimo }).ToList();
    private static List<RequisitoMinimoDto> Dtos(List<RequisitoMinimoLinha> linhas) =>
        linhas.Where(l => !string.IsNullOrWhiteSpace(l.Alvo)).Select(l => new RequisitoMinimoDto(l.Alvo, l.Minimo)).ToList();
}
```
(The round-trip test expects empty lists, not nulls, back — `Dtos` always returns a list; make the test's source DTO match, or use `BeEquivalentTo` with lists present as written.)

`RequisitosDePassivaEditor.razor` — renders, in this order, each field calling `OnChanged` after a change:
- `MudNumericField T="int?" Label="Nível mínimo" Clearable`
- `<VocacaoSubVocacaoFields @bind-Vocacao="Model.Vocacao" @bind-SubVocacao="Model.Classe" />` (after-change → `OnChanged`)
- `<LinhagemVarianteFields @bind-Linhagem="Model.Linhagem" @bind-Variante="Model.Variante" />`
- `MudNumericField T="int?" Label="Grau/Círculo mínimo" Min="0" Max="9"`
- `MudCheckBox T="bool" Label="Exige Coração de Mana"`
- `<AfinidadeSelect @bind-Value="Model.Afinidade" />`, `<EstrelaSelect @bind-Value="Model.Estrela" />`, `<HistoricoSelect @bind-Value="Model.HistoricoId" />`
- Three list blocks — "Atributos", "Sub-Atributos", "Perícias" — each a small table of rows `MudSelect<string>` (options: `Enum.GetValues<Atributo>()` labelled with `AtributoDisplay.Label`; `Enum.GetValues<SubAtributo>()` with `RequisitoLabels.SubAtributo`; `Enum.GetValues<Pericia>()` with `PericiaDisplay.Label`) + `MudNumericField<int> Label="Mínimo" Min="0"` + remove button, and an "Adicionar" button that appends a blank `RequisitoMinimoLinha`. An option already chosen on another row of the same list is disabled.

Check each reused field component's markup first: if one of them requires a value (no "clear" option), add a "— sem requisito —" first option only inside this editor (wrap it; don't change the sheet's behaviour).

`BancoDeMagiasForm.razor`:
- Tipo select: add `<MudSelectItem Value="@("Passiva")">Passiva</MudSelectItem>`.
- Wrap the Grau field and the whole "Efeitos" `Section` in `@if (_form.Tipo != "Passiva")`.
- When Passiva, render after "Geral":
```razor
        <MudSelect T="string" @bind-Value="_form.Categoria" Label="Categoria" @bind-Value:after="NotifySavedAsync">
            <MudSelectItem Value="@("Livre")">Passiva Livre</MudSelectItem>
            <MudSelectItem Value="@("Vocacional")">Passiva Vocacional</MudSelectItem>
            <MudSelectItem Value="@("DeClasse")">Passiva de Classe</MudSelectItem>
        </MudSelect>
```
(inside the Geral section, only when Passiva) and:
```razor
    <Section Title="Requisitos">
        <TitleInfo>
            <InfoPopup Title="Como cadastrar uma Passiva">
                Passivas só são cadastradas aqui, no Banco: nas fichas elas só podem ser escolhidas. Uma Passiva tem Nome, Descrição e Categoria (Passiva Livre, Vocacional ou de Classe), sem Grau nem Efeitos. Em <b>Requisitos</b>, preencha só o que a ficha precisa ter — campo vazio não é requisito — e todos os preenchidos precisam ser cumpridos. Atributos, Perícias e Sub-Atributos comparam com o Total da ficha. Uma ficha que não cumpre os requisitos não recebe a Passiva, nem pelo Mestre. Fichas de Criatura ignoram os requisitos de campos que não têm (Vocação, Classe, Linhagem, Variante, Grau/Círculo, Coração de Mana, Estrela, Histórico e os atributos Instinto, Vontade e Influência). Se a ficha deixar de cumprir os requisitos depois, a Passiva continua lá, com um aviso.
            </InfoPopup>
        </TitleInfo>
        <ChildContent>
            <RequisitosDePassivaEditor Model="_form.Requisitos" OnChanged="NotifySavedAsync" />
        </ChildContent>
    </Section>
```
- `EntryFormModel`: add `public string Categoria { get; set; } = "Livre";` and `public RequisitosFormModel Requisitos { get; set; } = new();`.
- Load: `_form.Categoria = existing.Categoria ?? "Livre"; _form.Requisitos = RequisitosFormModel.FromDto(existing.Requisitos);`
- Build requests via one helper used by both `CreateAsync` and `SaveIfValidAsync`:
```csharp
    private (int Grau, List<SpellAbilityEffectRequest> Efeitos, string? Categoria, RequisitosDePassivaDto? Requisitos) CamposPorTipo() =>
        _form.Tipo == "Passiva"
            ? (0, new(), _form.Categoria, _form.Requisitos.ToDto())
            : (_form.Grau, _form.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList(), null, null);
```

`BancoDeMagias.razor`: add `<MudSelectItem Value="@("Passiva")">Passiva</MudSelectItem>` to the Tipo filter; in the row, when `entry.Tipo == "Passiva"`, render the Grau/Gasto/Custo cells as `—` and the Efeitos cell as `RequisitoLabels.Categoria(Enum.Parse<CategoriaDePassiva>(entry.Categoria!))`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client` → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat(client): cadastro de Passiva com Requisitos no Banco de Magias"
```

---

### Task 7: Client — Habilidades Passivas section on the three sheets

**Files:**
- Create: `src/RuinaRPG.Client/Shared/HabilidadesPassivasSection.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (4.b section ~576-622, `LoadSpellAbilitiesAsync` ~1455), `FichaDeNpc.razor`, `FichaDeCriatura.razor` (their equivalent "Magias e Habilidades" sections)
- Test: `tests/RuinaRPG.Tests.Client/Shared/HabilidadesPassivasSectionTests.cs`

**Interfaces:**
- Consumes: Task 5 endpoints (`GET {SpellAbilitiesUrl}`, `GET {SpellAbilitiesUrl}/passivas-disponiveis`, `POST {SpellAbilitiesUrl}` with `{ SourceBankEntryId }`, `DELETE {SpellAbilitiesUrl}/{id}`), `PassivaDisponivelResponse`, `RequisitoLabels.Categoria`.
- Produces: `<HabilidadesPassivasSection SpellAbilitiesUrl="character-sheets/{id}/spell-abilities" />` (relative URL, same style the sheets already use with `Http`).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/RuinaRPG.Tests.Client/Shared/HabilidadesPassivasSectionTests.cs
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.SpellsAndAbilities;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class HabilidadesPassivasSectionTests : MudBunitContext
{
    private const string Url = "character-sheets/s1/spell-abilities";

    private static object Entrada(string id, string nome, string tipo, string? categoria = null, List<string>? pendentes = null) =>
        new { Id = id, Nome = nome, Tipo = tipo, Grau = 0, GastoEmPI = 0, Custo = 0, Descricao = $"Desc {nome}",
              Efeitos = new List<object>(), Categoria = categoria, RequisitosPendentes = pendentes };

    private static PassivaDisponivelResponse Disponivel(string id, string nome, params string[] pendencias) =>
        new(new SpellAbilityEntryResponse(id, nome, "Passiva", 0, 0, 0, "d", [], false, "Livre"), pendencias.ToList());

    [Fact]
    public async Task Lists_only_passivas_with_categoria_and_the_warning_when_requisitos_are_unmet()
    {
        var http = FakeHttpMessageHandler.CreateClient(request => request.RequestUri!.AbsolutePath.EndsWith("passivas-disponiveis")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
            {
                Entrada("1", "Bola de Fogo", "Magia"),
                Entrada("2", "Pele de Pedra", "Passiva", "Vocacional", []),
                Entrada("3", "Força Bruta", "Passiva", "Livre", ["Força ≥ 4"]),
            }) });
        Services.AddScoped(_ => http);

        var cut = Render<HabilidadesPassivasSection>(p => p.Add(x => x.SpellAbilitiesUrl, Url));
        await Task.Delay(50);

        cut.Markup.Should().NotContain("Bola de Fogo");
        cut.Markup.Should().Contain("Pele de Pedra").And.Contain("Passiva Vocacional");
        cut.Markup.Should().Contain("Requisitos não cumpridos").And.Contain("Força ≥ 4");
    }

    [Fact]
    public async Task The_add_picker_disables_passivas_whose_requisitos_are_unmet_and_shows_why()
    {
        var http = FakeHttpMessageHandler.CreateClient(request => request.RequestUri!.AbsolutePath.EndsWith("passivas-disponiveis")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Disponivel("a", "Livre"), Disponivel("b", "Alta", "Nível 20") }) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) });
        Services.AddScoped(_ => http);

        var cut = Render<HabilidadesPassivasSection>(p => p.Add(x => x.SpellAbilitiesUrl, Url));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Alta — falta: Nível 20");
        // the option for "Alta" is disabled, "Livre" is not — assert on the MudSelectItem components' Disabled
        cut.FindComponents<MudBlazor.MudSelectItem<string>>().Single(i => i.Instance.Value == "b").Instance.Disabled.Should().BeTrue();
        cut.FindComponents<MudBlazor.MudSelectItem<string>>().Single(i => i.Instance.Value == "a").Instance.Disabled.Should().BeFalse();
    }

    [Fact]
    public async Task Adding_posts_the_bank_entry_id_and_reloads()
    {
        string? posted = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Post)
            {
                posted = request.Content!.ReadAsStringAsync().Result;
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(new { Id = "n" }) };
            }
            return request.RequestUri!.AbsolutePath.EndsWith("passivas-disponiveis")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Disponivel("a", "Livre") }) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<object>()) };
        });
        Services.AddScoped(_ => http);

        var cut = Render<HabilidadesPassivasSection>(p => p.Add(x => x.SpellAbilitiesUrl, Url));
        await Task.Delay(50);
        await cut.InvokeAsync(() => cut.FindComponent<MudBlazor.MudSelect<string>>().Instance.ValueChanged.InvokeAsync("a"));
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar Passiva")).Click();
        await Task.Delay(50);

        posted.Should().Contain("\"sourceBankEntryId\":\"a\"");
    }
}
```
(Adjust `posted` casing to what `PostAsJsonAsync` emits by default — `System.Net.Http.Json` uses web defaults, camelCase.)

In the existing `FichaDePersonagem` client tests (if a 4.b test exists — `grep -rn "Magias e Habilidades" tests/RuinaRPG.Tests.Client`), add an assertion that a Passiva returned by `spell-abilities` is **not** rendered in the 4.b list and that the 4.b bank dropdown excludes Passiva entries.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~HabilidadesPassivasSection`
Expected: build FAIL.

- [ ] **Step 3: Implement**

`HabilidadesPassivasSection.razor`:
```razor
@inject HttpClient Http
@using RuinaRPG.Contracts.SpellsAndAbilities
@using RuinaRPG.Domain.SpellsAndAbilities
@using MudBlazor

@* Ficha 4.f (Personagem, NPC e Criatura): Passivas só entram escolhidas do Banco; as que a ficha não
   cumpre aparecem desabilitadas, com o motivo. Ver Requisitos - Ficha de Personagem 4.f. *@
<Section Title="Habilidades Passivas">
    <DismissibleAlert @bind-Message="_errorMessage" />
    <MudStack Row="true" AlignItems="AlignItems.Center">
        <MudSelect T="string" @bind-Value="_selecionada" Label="Passiva do Banco">
            @foreach (var p in _disponiveis)
            {
                <MudSelectItem Value="@p.Entrada.Id" Disabled="@(p.Pendencias.Count > 0)">
                    @(p.Pendencias.Count > 0 ? $"{p.Entrada.Nome} — falta: {string.Join(", ", p.Pendencias)}" : p.Entrada.Nome)
                </MudSelectItem>
            }
        </MudSelect>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Disabled="@string.IsNullOrEmpty(_selecionada)" OnClick="AddAsync">Adicionar Passiva</MudButton>
    </MudStack>

    <MudSimpleTable Class="mt-3">
        <tbody>
            @foreach (var p in _passivas)
            {
                <tr>
                    <td>
                        <b>@p.Nome</b> (@CategoriaLabel(p.Categoria)) — @p.Descricao
                        @if (p.RequisitosPendentes is { Count: > 0 } pendentes)
                        {
                            <MudTooltip Text="@string.Join(", ", pendentes)">
                                <MudChip T="string" Size="Size.Small" Color="Color.Warning">⚠ Requisitos não cumpridos: @string.Join(", ", pendentes)</MudChip>
                            </MudTooltip>
                        }
                    </td>
                    <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => RemoveAsync(p.Id))">Remover</MudButton></td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>
</Section>

@code {
    [Parameter, EditorRequired] public string SpellAbilitiesUrl { get; set; } = "";

    // Subconjunto comum das respostas de Character/Npc/CreatureSpellAbilityResponse.
    private record PassivaDaFicha(string Id, string Nome, string Tipo, string Descricao, string? Categoria, List<string>? RequisitosPendentes);

    private List<PassivaDaFicha> _passivas = new();
    private List<PassivaDisponivelResponse> _disponiveis = new();
    private string? _selecionada;
    private string? _errorMessage;

    protected override Task OnParametersSetAsync() => ReloadAsync();

    public async Task ReloadAsync()
    {
        var todas = await Http.GetFromJsonAsync<List<PassivaDaFicha>>(SpellAbilitiesUrl) ?? new();
        _passivas = todas.Where(e => e.Tipo == "Passiva").ToList();
        _disponiveis = await Http.GetFromJsonAsync<List<PassivaDisponivelResponse>>($"{SpellAbilitiesUrl}/passivas-disponiveis") ?? new();
    }

    private async Task AddAsync()
    {
        var response = await Http.PostAsJsonAsync(SpellAbilitiesUrl, new { SourceBankEntryId = _selecionada });
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a Passiva.";
            return;
        }
        _selecionada = null;
        await ReloadAsync();
    }

    private async Task RemoveAsync(string id)
    {
        await Http.DeleteAsync($"{SpellAbilitiesUrl}/{id}");
        await ReloadAsync();
    }

    private static string CategoriaLabel(string? categoria) =>
        Enum.TryParse<CategoriaDePassiva>(categoria, out var c) ? RequisitoLabels.Categoria(c) : "—";
}
```
(`ReadErrorMessageAsync` is the extension the pages already use — add its `@using` if it lives in another namespace.)

Sheets — in each of `FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor`:
- In the 4.b list `@foreach (var spellAbility in _spellAbilities)`, iterate `_spellAbilities.Where(s => s.Tipo != "Passiva")`.
- In the 4.b bank dropdown, iterate `_bankEntries.Where(e => e.Tipo != "Passiva")`.
- Right after the 4.b `</Section>`: `<HabilidadesPassivasSection SpellAbilitiesUrl="@($"character-sheets/{SheetId}/spell-abilities")" />` (`npc-sheets/...` and `creature-sheets/...` on the other two, using each page's own id parameter name).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client` → PASS. `dotnet build` → 0 warnings.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat(client): seção Habilidades Passivas nas fichas"
```

---

### Task 8: Full verification

- [ ] **Step 1:** `dotnet build` → 0 warnings, 0 errors.
- [ ] **Step 2:** `dotnet test` (Docker running). The integration suite has a known ~2-3% timeout flake; rerun any failed test in isolation with `--filter` before treating it as real.
- [ ] **Step 3:** Grep for missed spots: `grep -rn "Magia, Habilidade ou Racial" src Docs` — every user-facing Tipo list mentions Passiva where Passiva is accepted; `grep -rn "new NpcSpellAbility\|new CreatureSpellAbility\|new CharacterSpellAbility" src` — every construction that copies from another entry carries `Categoria`/`Requisitos`.
- [ ] **Step 4:** Commit any fixes: `git commit -m "fix: ajustes finais das Passivas"`.
