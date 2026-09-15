# Automatizar Afinidades — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restringir quais Elementos/Sub-Elementos um Personagem/NPC pode escolher (1.a e 2.c) pela
Vocação atual via Escola de Magia, impedir linhas de Afinidade duplicadas, e calcular
automaticamente Eficiência Elemental e Dano Elemental como Sub-Atributos.

**Architecture:** Dois tipos novos e puros no Domain (`EscolaDeMagia`/`EscolaDeMagiaCatalog`/
`VocacaoEscolaMap` para a restrição; `SubAttributeFormulas.EficienciaElemental`/`.DanoElemental`/
`.ValorDaAfinidadeCorrespondente` para o cálculo) alimentam validação server-side (com uma regra
de "não invalida dado antigo") nos controllers de ficha/afinidade, e filtragem client-side nos
dois componentes de dropdown já existentes. Nenhuma tabela nova — tudo cabe nas colunas já
existentes de `CharacterSheet`/`NpcSheet`/`CharacterAffinity`/`NpcAffinity`.

**Tech Stack:** ASP.NET Core 8, EF Core/PostgreSQL, Blazor WebAssembly, MudBlazor. Nenhuma
migração de banco.

**Spec:** `docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md`

## Global Constraints

- Ficha de Criatura não é tocada — ela não tem Vocação, não tem a lista 2.c, e "Eficiência
  Elemental"/"Dano Elemental" não existem nela (confirmado em `Requisitos - Ficha de
  Criaturas.md`).
- **Regra de "não invalida dado antigo"**: a validação por Vocação (1.a e 2.c) só bloqueia um
  valor que está **mudando** — compare contra o valor já salvo no banco antes de validar contra a
  Escola; se for igual ao que já estava lá, pula a checagem.
- Mapeamento Vocação → Escola (dado pelo usuário, sem equivalente em documento):
  Feiticeiro→[Dobra,Maculação], Adepto→[Dobra,Consagração], Bruxo→[Dobra,Transmutação],
  Campeão/Caçador/nulo→[] (nenhuma).
- Escolas de Magia (extraído de `Docs/Sistema RPG/Escolas_de_Magia.png` + confirmação do
  usuário sobre Alma/Vida): Dobra={Ar,Água,Fogo,Terra}, Transmutação={Flora,Ferro,Raio,Gelo},
  Maculação={Necromancia,Invocação,Ecomancia,Hemomancia}, Consagração={Curar,Aprimorar,Prever,
  Purificar,Alma,Vida}.
- `EficienciaElemental = DanoElemental = Valor` da linha de 2.c cujo Elemento OU Sub-Elemento bate
  com a Afinidade (1.a) escolhida — 0 se não houver Afinidade ou linha correspondente.
- `dotnet build` deve ficar em 0 Warning(s), 0 Error(s) após cada tarefa.

---

### Task 1: Domain — `EscolaDeMagia`, `EscolaDeMagiaCatalog`, `VocacaoEscolaMap`

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/EscolaDeMagia.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/EscolaDeMagiaCatalog.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/VocacaoEscolaMap.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/EscolaDeMagiaCatalogTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/VocacaoEscolaMapTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Domain.CharacterSheets.Vocacao`, `.Elemento`, `.SubElemento`,
  `.AfinidadeElemental` (todos já existentes).
- Produces: `EscolaDeMagia` (enum: `Dobra`, `Transmutacao`, `Maculacao`, `Consagracao`),
  `EscolaDeMagiaCatalog.DoElemento(Elemento) : EscolaDeMagia`,
  `EscolaDeMagiaCatalog.DoSubElemento(SubElemento) : EscolaDeMagia`,
  `VocacaoEscolaMap.PodeEscolherElemento(Vocacao?, Elemento) : bool`,
  `VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao?, SubElemento) : bool`,
  `VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao?, AfinidadeElemental) : bool`. Usados pelas
  Tasks 3, 4 e 6.

Este task não tem I/O — funções puras sobre enums, igual `ElementoSubElementoValidator`.

- [ ] **Step 1: Write the failing tests**

Create `tests/RuinaRPG.Tests.Unit/CharacterSheets/EscolaDeMagiaCatalogTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class EscolaDeMagiaCatalogTests
{
    [Theory]
    [InlineData(Elemento.Ar)]
    [InlineData(Elemento.Agua)]
    [InlineData(Elemento.Fogo)]
    [InlineData(Elemento.Terra)]
    public void DoElemento_every_Elemento_belongs_to_Dobra(Elemento elemento)
    {
        EscolaDeMagiaCatalog.DoElemento(elemento).Should().Be(EscolaDeMagia.Dobra);
    }

    [Theory]
    [InlineData(SubElemento.Flora, EscolaDeMagia.Transmutacao)]
    [InlineData(SubElemento.Ferro, EscolaDeMagia.Transmutacao)]
    [InlineData(SubElemento.Raio, EscolaDeMagia.Transmutacao)]
    [InlineData(SubElemento.Gelo, EscolaDeMagia.Transmutacao)]
    [InlineData(SubElemento.Necromancia, EscolaDeMagia.Maculacao)]
    [InlineData(SubElemento.Invocacao, EscolaDeMagia.Maculacao)]
    [InlineData(SubElemento.Ecomancia, EscolaDeMagia.Maculacao)]
    [InlineData(SubElemento.Hemomancia, EscolaDeMagia.Maculacao)]
    [InlineData(SubElemento.Curar, EscolaDeMagia.Consagracao)]
    [InlineData(SubElemento.Aprimorar, EscolaDeMagia.Consagracao)]
    [InlineData(SubElemento.Prever, EscolaDeMagia.Consagracao)]
    [InlineData(SubElemento.Purificar, EscolaDeMagia.Consagracao)]
    // Alma e Vida não aparecem na imagem "Escolas de Magia" — confirmado com o usuário que os
    // dois pertencem a Consagração (ver docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
    [InlineData(SubElemento.Alma, EscolaDeMagia.Consagracao)]
    [InlineData(SubElemento.Vida, EscolaDeMagia.Consagracao)]
    public void DoSubElemento_maps_every_sub_elemento_to_its_escola(SubElemento subElemento, EscolaDeMagia esperado)
    {
        EscolaDeMagiaCatalog.DoSubElemento(subElemento).Should().Be(esperado);
    }
}
```

Create `tests/RuinaRPG.Tests.Unit/CharacterSheets/VocacaoEscolaMapTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class VocacaoEscolaMapTests
{
    [Fact]
    public void Feiticeiro_can_pick_Dobra_and_Maculacao_but_not_the_other_schools()
    {
        VocacaoEscolaMap.PodeEscolherElemento(Vocacao.Feiticeiro, Elemento.Fogo).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Feiticeiro, SubElemento.Necromancia).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Feiticeiro, SubElemento.Curar).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Feiticeiro, SubElemento.Flora).Should().BeFalse();
    }

    [Fact]
    public void Adepto_can_pick_Dobra_and_Consagracao_but_not_the_other_schools()
    {
        VocacaoEscolaMap.PodeEscolherElemento(Vocacao.Adepto, Elemento.Terra).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Adepto, SubElemento.Vida).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Adepto, SubElemento.Necromancia).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Adepto, SubElemento.Raio).Should().BeFalse();
    }

    [Fact]
    public void Bruxo_can_pick_Dobra_and_Transmutacao_but_not_the_other_schools()
    {
        VocacaoEscolaMap.PodeEscolherElemento(Vocacao.Bruxo, Elemento.Ar).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Bruxo, SubElemento.Gelo).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Bruxo, SubElemento.Purificar).Should().BeFalse();
    }

    [Theory]
    [InlineData(Vocacao.Campeao)]
    [InlineData(Vocacao.Cacador)]
    public void Non_magic_Vocacoes_can_pick_nothing(Vocacao vocacao)
    {
        VocacaoEscolaMap.PodeEscolherElemento(vocacao, Elemento.Fogo).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(vocacao, SubElemento.Curar).Should().BeFalse();
    }

    [Fact]
    public void A_null_Vocacao_can_pick_nothing()
    {
        VocacaoEscolaMap.PodeEscolherElemento(null, Elemento.Fogo).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(null, SubElemento.Curar).Should().BeFalse();
    }

    [Fact]
    public void PodeEscolherAfinidade_resolves_an_Elemento_shaped_value_correctly()
    {
        // AfinidadeElemental.Fogo compartilha o nome de membro com Elemento.Fogo.
        VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao.Feiticeiro, AfinidadeElemental.Fogo).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao.Campeao, AfinidadeElemental.Fogo).Should().BeFalse();
    }

    [Fact]
    public void PodeEscolherAfinidade_resolves_a_SubElemento_shaped_value_correctly()
    {
        // AfinidadeElemental.Necromancia compartilha o nome de membro com SubElemento.Necromancia.
        VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao.Feiticeiro, AfinidadeElemental.Necromancia).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao.Adepto, AfinidadeElemental.Necromancia).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EscolaDeMagiaCatalogTests|FullyQualifiedName~VocacaoEscolaMapTests"`
Expected: FAIL to compile — nenhum dos 3 tipos existe ainda.

- [ ] **Step 3: Create `EscolaDeMagia`**

Create `src/RuinaRPG.Domain/CharacterSheets/EscolaDeMagia.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// As 4 escolas temáticas de "Escolas de Magia" (Docs/Sistema RPG/Escolas_de_Magia.png) — base
/// da restrição de Elemento/Sub-Elemento por Vocação (ver VocacaoEscolaMap). Nunca tinha texto
/// em nenhum documento do sistema antes deste plano.
/// </summary>
public enum EscolaDeMagia
{
    Dobra,
    Transmutacao,
    Maculacao,
    Consagracao,
}
```

- [ ] **Step 4: Create `EscolaDeMagiaCatalog`**

Create `src/RuinaRPG.Domain/CharacterSheets/EscolaDeMagiaCatalog.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>Qual Escola de Magia cada Elemento/Sub-Elemento pertence — ver EscolaDeMagia.</summary>
public static class EscolaDeMagiaCatalog
{
    // Todo Elemento-base pertence a Dobra.
    public static EscolaDeMagia DoElemento(Elemento elemento) => EscolaDeMagia.Dobra;

    // Alma e Vida não aparecem na imagem "Escolas de Magia" (que só mostra 12 dos 14
    // Sub-Elementos) — confirmado com o usuário: os dois pertencem a Consagração.
    public static EscolaDeMagia DoSubElemento(SubElemento subElemento) => subElemento switch
    {
        SubElemento.Flora or SubElemento.Ferro or SubElemento.Raio or SubElemento.Gelo => EscolaDeMagia.Transmutacao,
        SubElemento.Necromancia or SubElemento.Invocacao or SubElemento.Ecomancia or SubElemento.Hemomancia => EscolaDeMagia.Maculacao,
        SubElemento.Curar or SubElemento.Aprimorar or SubElemento.Prever or SubElemento.Purificar
            or SubElemento.Alma or SubElemento.Vida => EscolaDeMagia.Consagracao,
        _ => throw new ArgumentOutOfRangeException(nameof(subElemento)),
    };
}
```

- [ ] **Step 5: Create `VocacaoEscolaMap`**

Create `src/RuinaRPG.Domain/CharacterSheets/VocacaoEscolaMap.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Quais Escolas de Magia cada Vocação libera — mapeamento dado pelo usuário, sem equivalente em
/// nenhum documento do sistema (ver docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
/// Campeão e Caçador (ausentes do dicionário) e uma Vocação nula não liberam Escola nenhuma.
/// </summary>
public static class VocacaoEscolaMap
{
    private static readonly Dictionary<Vocacao, EscolaDeMagia[]> Escolas = new()
    {
        [Vocacao.Feiticeiro] = [EscolaDeMagia.Dobra, EscolaDeMagia.Maculacao],
        [Vocacao.Adepto] = [EscolaDeMagia.Dobra, EscolaDeMagia.Consagracao],
        [Vocacao.Bruxo] = [EscolaDeMagia.Dobra, EscolaDeMagia.Transmutacao],
    };

    public static bool PodeEscolherElemento(Vocacao? vocacao, Elemento elemento) =>
        vocacao is { } v && Escolas.TryGetValue(v, out var escolas) && escolas.Contains(EscolaDeMagiaCatalog.DoElemento(elemento));

    public static bool PodeEscolherSubElemento(Vocacao? vocacao, SubElemento subElemento) =>
        vocacao is { } v && Escolas.TryGetValue(v, out var escolas) && escolas.Contains(EscolaDeMagiaCatalog.DoSubElemento(subElemento));

    /// <summary>
    /// AfinidadeElemental (o dropdown único de 1.a) compartilha os mesmos nomes de membro que
    /// Elemento (4) e SubElemento (14) — resolve pra qual dos dois enums o valor pertence antes
    /// de checar a Escola.
    /// </summary>
    public static bool PodeEscolherAfinidade(Vocacao? vocacao, AfinidadeElemental afinidade) =>
        Enum.TryParse<Elemento>(afinidade.ToString(), out var elemento)
            ? PodeEscolherElemento(vocacao, elemento)
            : PodeEscolherSubElemento(vocacao, Enum.Parse<SubElemento>(afinidade.ToString()));
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~EscolaDeMagiaCatalogTests|FullyQualifiedName~VocacaoEscolaMapTests"`
Expected: PASS (14 + 8 = 22 tests)

- [ ] **Step 7: Run the full Unit suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Unit && dotnet build RuinaRPG.sln`
Expected: all pass, `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/EscolaDeMagia.cs src/RuinaRPG.Domain/CharacterSheets/EscolaDeMagiaCatalog.cs src/RuinaRPG.Domain/CharacterSheets/VocacaoEscolaMap.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/EscolaDeMagiaCatalogTests.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/VocacaoEscolaMapTests.cs
git commit -m "feat: EscolaDeMagia + VocacaoEscolaMap (Domain, pure)"
```

---

### Task 2: Domain — `LinhaDeAfinidade` e `SubAttributeFormulas.EficienciaElemental`/`.DanoElemental`

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/LinhaDeAfinidade.cs`
- Modify: `src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs`

**Interfaces:**
- Consumes: `Elemento`, `SubElemento`, `AfinidadeElemental` (já existentes).
- Produces: `LinhaDeAfinidade(Elemento? Elemento, int? ElementoValor, SubElemento? SubElemento, int? SubElementoValor)`
  (record struct — projeção pura de uma linha de 2.c, sem depender de `CharacterAffinity`/
  `NpcAffinity`, que vivem em Infrastructure/EF), `SubAttributeFormulas.EficienciaElemental(int) : int`,
  `SubAttributeFormulas.DanoElemental(int) : int`,
  `SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental?, IReadOnlyList<LinhaDeAfinidade>) : int`.
  Usados pela Task 5.

- [ ] **Step 1: Write the failing tests**

Em `tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs`, adicione estes
métodos dentro da classe `SubAttributeFormulasTests` (o arquivo já importa `FluentAssertions` e
`RuinaRPG.Domain.CharacterSheets` — não repita):

```csharp
    [Fact]
    public void EficienciaElemental_and_DanoElemental_pass_the_value_through_1_to_1()
    {
        // 1:1 por ora — cada um é sua própria função porque a proporção pode divergir no futuro
        // (ver docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
        SubAttributeFormulas.EficienciaElemental(valorDaAfinidadeCorrespondente: 5).Should().Be(5);
        SubAttributeFormulas.DanoElemental(valorDaAfinidadeCorrespondente: 5).Should().Be(5);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_0_when_Afinidade_is_null()
    {
        var linhas = new List<LinhaDeAfinidade> { new(Elemento.Fogo, 7, null, null) };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(null, linhas).Should().Be(0);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_finds_the_matching_Elemento_row()
    {
        var linhas = new List<LinhaDeAfinidade> { new(Elemento.Fogo, 7, SubElemento.Vida, 2) };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Fogo, linhas).Should().Be(7);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_finds_the_matching_SubElemento_row()
    {
        var linhas = new List<LinhaDeAfinidade> { new(Elemento.Fogo, 7, SubElemento.Vida, 2) };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Vida, linhas).Should().Be(2);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_0_when_no_row_matches()
    {
        var linhas = new List<LinhaDeAfinidade> { new(Elemento.Fogo, 7, null, null) };

        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Terra, linhas).Should().Be(0);
    }

    [Fact]
    public void ValorDaAfinidadeCorrespondente_is_0_with_an_empty_list_of_linhas()
    {
        SubAttributeFormulas.ValorDaAfinidadeCorrespondente(AfinidadeElemental.Fogo, []).Should().Be(0);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~SubAttributeFormulasTests"`
Expected: FAIL to compile — `LinhaDeAfinidade`/`EficienciaElemental`/`DanoElemental`/`ValorDaAfinidadeCorrespondente` não existem ainda.

- [ ] **Step 3: Create `LinhaDeAfinidade`**

Create `src/RuinaRPG.Domain/CharacterSheets/LinhaDeAfinidade.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Projeção pura de uma linha de Afinidades (2.c) — CharacterAffinity/NpcAffinity vivem em
/// Infrastructure/EF, então SubAttributeFormulas usa este record em vez de depender deles.
/// </summary>
public readonly record struct LinhaDeAfinidade(Elemento? Elemento, int? ElementoValor, SubElemento? SubElemento, int? SubElementoValor);
```

- [ ] **Step 4: Add the two Sub-Atributo formulas and the resolver**

Em `src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs`, dentro da classe
`SubAttributeFormulas`, logo depois de:

```csharp
    public static int ReducaoMagica(int artefato, int armaduraMagica) => artefato + armaduraMagica;
```

Adicione:

```csharp

    // 1:1 por ora — cada uma é sua própria função porque a proporção pode divergir no futuro
    // (ver docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
    public static int EficienciaElemental(int valorDaAfinidadeCorrespondente) => valorDaAfinidadeCorrespondente;

    public static int DanoElemental(int valorDaAfinidadeCorrespondente) => valorDaAfinidadeCorrespondente;

    /// <summary>
    /// Acha, entre as linhas de Afinidade (2.c), o Valor da que bate com a Afinidade escolhida em
    /// 1.a — entrada de EficienciaElemental/DanoElemental acima. AfinidadeElemental compartilha os
    /// mesmos nomes de membro que Elemento/SubElemento (ver VocacaoEscolaMap.PodeEscolherAfinidade),
    /// então resolve pra qual dos dois enums o valor pertence antes de procurar a linha.
    /// </summary>
    public static int ValorDaAfinidadeCorrespondente(AfinidadeElemental? afinidade, IReadOnlyList<LinhaDeAfinidade> linhas)
    {
        if (afinidade is not { } valor)
            return 0;

        if (Enum.TryParse<Elemento>(valor.ToString(), out var elemento))
            return linhas.FirstOrDefault(l => l.Elemento == elemento).ElementoValor ?? 0;

        var subElemento = Enum.Parse<SubElemento>(valor.ToString());
        return linhas.FirstOrDefault(l => l.SubElemento == subElemento).SubElementoValor ?? 0;
    }
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~SubAttributeFormulasTests"`
Expected: PASS (existing tests + 6 new = all green)

- [ ] **Step 6: Run the full Unit suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Unit && dotnet build RuinaRPG.sln`
Expected: all pass, `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/LinhaDeAfinidade.cs src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs
git commit -m "feat: SubAttributeFormulas.EficienciaElemental/.DanoElemental (Domain, pure)"
```

---

### Task 3: API — restrição por Vocação no dropdown único de Afinidade (1.a)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs`

**Interfaces:**
- Consumes: `VocacaoEscolaMap.PodeEscolherAfinidade` (Task 1).
- Produces: nada novo — só adiciona uma validação a um endpoint já existente.

**Regra**: valida a Afinidade recebida contra a Vocação recebida (ambas do mesmo request) **só
quando a Afinidade está realmente mudando** em relação ao que já está salvo no banco (`sheet.Afinidade`,
lido antes de sobrescrever). Isso é o que implementa a "regra de não invalidar dado antigo" das
Global Constraints.

**Um efeito colateral conhecido e necessário**: os fixtures `ValidUpdate()` de
`CharacterSheetsControllerTests.cs` e `NpcSheetsControllerTests.cs`, e um `UpdateCharacterSheetRequest`
direto em `CampaignPlayerViewControllerTests.cs`, hoje enviam `Vocacao="Campeao"` junto com
`Afinidade="Fogo"` — Campeão não libera Escola nenhuma, então essa combinação passa a ser
rejeitada assim que a validação existir. Nenhum teste desses arquivos afirma o valor de Afinidade
especificamente, então o fix é trocar esse `"Fogo"` por `null` nos 3 lugares (Passo 4 abaixo) —
verificado com antecedência lendo os 3 arquivos por inteiro.

- [ ] **Step 1: Write the failing tests**

Em `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, adicione
estes 3 métodos dentro da classe (perto de outros testes de `Update`, ex.: logo depois do teste
que usa `ValidUpdate() with { Vocacao = "Xyz" }`):

```csharp
    [Fact]
    public async Task Update_rejects_an_Afinidade_not_liberada_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmAfin1", "sheetafin1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerAfin1", "sheetplayerafin1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Afinidade 1");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Vocacao=Campeao não libera Escola nenhuma — Fogo (Dobra) deve ser rejeitado.
        var update = ValidUpdate() with { Vocacao = "Campeao", Afinidade = "Fogo" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_allows_an_Afinidade_liberada_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmAfin2", "sheetafin2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerAfin2", "sheetplayerafin2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Afinidade 2");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Feiticeiro libera Dobra+Maculação — Necromancia é de Maculação.
        var update = ValidUpdate() with { Vocacao = "Feiticeiro", Afinidade = "Necromancia" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_keeps_an_old_Afinidade_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmAfin3", "sheetafin3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerAfin3", "sheetplayerafin3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Afinidade 3");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Feiticeiro libera Necromancia (Maculação).
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Feiticeiro", Afinidade = "Necromancia" }));

        // Troca pra Adepto (não libera Maculação) reenviando a MESMA Afinidade — não deve ser bloqueado.
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Necromancia" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
        var body = await getResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.Afinidade.Should().Be("Necromancia");
    }
```

Em `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`, adicione os
mesmos 3 testes (adaptados pra rota `/api/npc-sheets/{id}` e `gmToken` — NPC não tem conceito de
jogador dono nesses testes, `ValidUpdate()` já existe igual):

```csharp
    [Fact]
    public async Task Update_rejects_an_Afinidade_not_liberada_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmAfin1", "npcafin1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Vocacao = "Campeao", Afinidade = "Fogo" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_allows_an_Afinidade_liberada_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmAfin2", "npcafin2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var update = ValidUpdate() with { Vocacao = "Bruxo", Afinidade = "Gelo" };
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, update));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Update_keeps_an_old_Afinidade_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcGmAfin3", "npcafin3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Vocacao = "Bruxo", Afinidade = "Gelo" }));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Gelo" }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", gmToken));
        var body = await getResponse.Content.ReadFromJsonAsync<NpcSheetResponse>();
        body!.Afinidade.Should().Be("Gelo");
    }
```

`CreateSheetAsync(string gmToken)` (linha 65 de `NpcSheetsControllerTests.cs`) já existe no
arquivo — faz `POST /api/npc-sheets` e devolve o Id.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Update_rejects_an_Afinidade|FullyQualifiedName~Update_allows_an_Afinidade|FullyQualifiedName~Update_keeps_an_old_Afinidade"`
Expected: FAIL — hoje toda combinação de Vocação/Afinidade é aceita, então o teste de rejeição
(`Update_rejects_an_Afinidade_not_liberada...`) falha esperando 400 e recebendo 204.

- [ ] **Step 3: Add the validation to `CharacterSheetsController.Update`**

Em `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, encontre:

```csharp
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
```

Substitua por:

```csharp
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
        // Só valida contra a Vocação quando o valor realmente muda — uma Afinidade antiga
        // reenviada sem alteração nunca é invalidada por uma troca de Vocação posterior (ver
        // docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
        if (afinidade is not null && afinidade != sheet.Afinidade && !VocacaoEscolaMap.PodeEscolherAfinidade(vocacao, afinidade.Value))
            return BadRequest("Essa Afinidade não é liberada pela Vocação atual.");
```

- [ ] **Step 4: Add the identical validation to `NpcSheetsController.Update`**

Em `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`, encontre:

```csharp
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
```

Substitua por:

```csharp
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
        if (afinidade is not null && afinidade != sheet.Afinidade && !VocacaoEscolaMap.PodeEscolherAfinidade(vocacao, afinidade.Value))
            return BadRequest("Essa Afinidade não é liberada pela Vocação atual.");
```

- [ ] **Step 5: Fix the 3 pre-existing fixtures that now submit an invalid combination**

Em `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, encontre
(dentro de `ValidUpdate()`):

```csharp
    private static UpdateCharacterSheetRequest ValidUpdate() => new(
        null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Marcado pela Ruína",
```

Substitua por (só o `"Fogo"` vira `null` — Campeão não libera nenhuma Afinidade):

```csharp
    private static UpdateCharacterSheetRequest ValidUpdate() => new(
        null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", null, "Marcado pela Ruína",
```

Em `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`, encontre:

```csharp
    private static UpdateNpcSheetRequest ValidUpdate() => new(
        null, "Sentinela da Ruína", "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Guardiã do Portal",
```

Substitua por:

```csharp
    private static UpdateNpcSheetRequest ValidUpdate() => new(
        null, "Sentinela da Ruína", "Humano", "Sinir", "Campeao", "Duelista", null, "Guardiã do Portal",
```

Em `tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs`, encontre:

```csharp
        var update = new UpdateCharacterSheetRequest(imageId, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Marcado pela Ruína",
```

Substitua por:

```csharp
        var update = new UpdateCharacterSheetRequest(imageId, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", null, "Marcado pela Ruína",
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Update_rejects_an_Afinidade|FullyQualifiedName~Update_allows_an_Afinidade|FullyQualifiedName~Update_keeps_an_old_Afinidade"`
Expected: PASS (6 tests — 3 em cada arquivo de sheet)

- [ ] **Step 7: Run the full CharacterSheets/NpcSheets/CampaignPlayerView Integration batch**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests|FullyQualifiedName~NpcSheetsControllerTests|FullyQualifiedName~CampaignPlayerViewControllerTests"`
Expected: all pass — confirma que nenhum outro teste desses 3 arquivos quebrou com a mudança dos
fixtures (nenhum deles afirma o valor de Afinidade, então nada além dos 6 novos testes deveria
mudar de resultado).

- [ ] **Step 8: Clean-rebuild**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs src/RuinaRPG.Api/Controllers/NpcSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs
git commit -m "feat: restrict 1.a Afinidade by Vocação/Escola de Magia, grandfathering old data"
```

---

### Task 4: API — restrição por Vocação e duplicata em 2.c (Afinidades)

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/CharacterAffinitiesController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcAffinitiesController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffinitiesControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/NpcAffinitiesControllerTests.cs`

**Interfaces:**
- Consumes: `VocacaoEscolaMap.PodeEscolherElemento`/`.PodeEscolherSubElemento` (Task 1).
- Produces: nada novo.

**Duas regras novas, nos dois lugares (`Add` e `Update`, nos dois controllers)**:
1. **Vocação/Escola**, com a mesma regra de "não invalida dado antigo" da Task 3 — compara o
   Elemento/Sub-Elemento recebido contra o que já estava salvo na linha (`null` em `Add`, já que
   uma linha nova não tem "antigo"); só valida contra a Escola quando o valor muda.
2. **Duplicata**: rejeita se outra linha do mesmo Personagem/NPC já tiver o mesmo Elemento
   não-nulo, ou o mesmo Sub-Elemento não-nulo.

**Efeito colateral conhecido**: `SetUpSheetAsync`/`CreateSheetAsync` nos dois arquivos de teste
criam uma ficha sem Vocação nenhuma — toda Afinidade não-nula que os testes existentes já
adicionam (`"Fogo"`+`"Vida"`, `"Terra"`+`"Vida"`, `"Terra"`+`"Aprimorar"`) passaria a ser
rejeitada. Todas essas combinações cabem em **Adepto** (Dobra+Consagração — Fogo/Terra são Dobra,
Vida/Aprimorar são Consagração), então o fix é dar à ficha de teste uma Vocação=Adepto logo após
criá-la (Passo 5 abaixo) — verificado com antecedência lendo os dois arquivos de teste por
inteiro.

- [ ] **Step 1: Write the failing tests**

Em `tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffinitiesControllerTests.cs`,
adicione dentro da classe:

```csharp
    [Fact]
    public async Task Add_rejects_an_Elemento_not_liberado_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm12", "aff12@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer12", "affplayer12@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto (Dobra+Consagração)

        // Necromancia é de Maculação — Adepto não libera.
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest(null, null, "Necromancia", 1, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_an_old_SubElemento_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm13", "aff13@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer13", "affplayer13@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        // Troca a Vocação pra Feiticeiro (não libera mais Vida, que é de Consagração).
        var updateSheet = new UpdateCharacterSheetRequest(null, "Ficha de Teste", null, null, "Feiticeiro", null, null, null,
            false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, updateSheet));

        // Reenvia a mesma linha sem mudar Elemento/Sub-Elemento — não deve ser bloqueado.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_rejects_a_duplicate_Elemento_already_used_by_another_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm14", "aff14@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer14", "affplayer14@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 5, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_does_not_flag_a_duplicate_against_its_own_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffGm15", "aff15@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffPlayer15", "affplayer15@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 3, null, null, null, null)));
        var added = await addResponse.Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        // Reenviar a mesma linha com o mesmo Elemento não deve se auto-rejeitar como duplicata.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", 5, null, null, null, null)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
```

Em `tests/RuinaRPG.Tests.Integration/Controllers/NpcAffinitiesControllerTests.cs`, adicione
dentro da classe:

```csharp
    [Fact]
    public async Task Add_rejects_an_Elemento_not_liberado_pela_Vocacao_atual()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm12", "npcaff12@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto (Dobra+Consagração)

        // Necromancia é de Maculação — Adepto não libera.
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest(null, null, "Necromancia", 1, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_keeps_an_old_SubElemento_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm13", "npcaff13@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        // Troca a Vocação pra Feiticeiro (não libera mais Vida, que é de Consagração).
        var updateSheet = new UpdateNpcSheetRequest(null, "Ficha de Teste", null, null, "Feiticeiro", null, null, null,
            1, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, updateSheet));

        // Reenvia a mesma linha sem mudar Elemento/Sub-Elemento — não deve ser bloqueado.
        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 3, "Vida", 2, "Caminho da Fênix", 10)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Add_rejects_a_duplicate_Elemento_already_used_by_another_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm14", "npcaff14@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, null, null, null, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 5, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_does_not_flag_a_duplicate_against_its_own_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffGm15", "npcaff15@teste.com");
        var sheetId = await CreateSheetAsync(gmToken); // Vocacao=Adepto

        var addResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", 3, null, null, null, null)));
        var added = await addResponse.Content.ReadFromJsonAsync<NpcAffinityResponse>();

        var updateResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/affinities/{added!.Id}", gmToken,
            new UpdateNpcAffinityRequest("Fogo", 5, null, null, null, null)));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~Add_rejects_an_Elemento|FullyQualifiedName~Update_keeps_an_old_SubElemento|FullyQualifiedName~Add_rejects_a_duplicate|FullyQualifiedName~Update_does_not_flag_a_duplicate"`
Expected: FAIL — hoje nem restrição por Vocação nem checagem de duplicata existem.

- [ ] **Step 3: Rewrite `CharacterAffinitiesController`'s validation + add duplicate check**

Em `src/RuinaRPG.Api/Controllers/CharacterAffinitiesController.cs`, substitua o método inteiro
`TryParseElementoSubElemento` (hoje):

```csharp
    private static bool TryParseElementoSubElemento(string? elementoRaw, string? subElementoRaw,
        out Elemento? elemento, out SubElemento? subElemento, out string? error)
    {
        elemento = null;
        subElemento = null;
        error = null;

        if (!string.IsNullOrEmpty(elementoRaw))
        {
            if (!Enum.TryParse<Elemento>(elementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Elemento desconhecido.";
                return false;
            }
            elemento = parsed;
        }

        if (!string.IsNullOrEmpty(subElementoRaw))
        {
            if (!Enum.TryParse<SubElemento>(subElementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Sub-Elemento desconhecido.";
                return false;
            }
            subElemento = parsed;
        }

        if (elemento is not null && subElemento is not null && !ElementoSubElementoValidator.IsValidCombination(elemento.Value, subElemento.Value))
        {
            error = "Essa combinação de Elemento e Sub-Elemento não existe na Matriz Elemental.";
            return false;
        }

        return true;
    }
```

Por (ganha `vocacao`, `elementoAntigo`, `subElementoAntigo` pra aplicar a mesma regra de "não
invalida dado antigo" da Task 3):

```csharp
    private static bool TryParseElementoSubElemento(string? elementoRaw, string? subElementoRaw, Vocacao? vocacao,
        Elemento? elementoAntigo, SubElemento? subElementoAntigo,
        out Elemento? elemento, out SubElemento? subElemento, out string? error)
    {
        elemento = null;
        subElemento = null;
        error = null;

        if (!string.IsNullOrEmpty(elementoRaw))
        {
            if (!Enum.TryParse<Elemento>(elementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Elemento desconhecido.";
                return false;
            }
            elemento = parsed;
        }

        if (!string.IsNullOrEmpty(subElementoRaw))
        {
            if (!Enum.TryParse<SubElemento>(subElementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Sub-Elemento desconhecido.";
                return false;
            }
            subElemento = parsed;
        }

        if (elemento is not null && subElemento is not null && !ElementoSubElementoValidator.IsValidCombination(elemento.Value, subElemento.Value))
        {
            error = "Essa combinação de Elemento e Sub-Elemento não existe na Matriz Elemental.";
            return false;
        }

        // Só valida contra a Vocação quando o valor realmente muda — uma linha antiga preservada
        // nunca é invalidada por uma troca de Vocação posterior (ver
        // docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
        if (elemento is not null && elemento != elementoAntigo && !VocacaoEscolaMap.PodeEscolherElemento(vocacao, elemento.Value))
        {
            error = "Esse Elemento não é liberado pela Vocação atual.";
            return false;
        }
        if (subElemento is not null && subElemento != subElementoAntigo && !VocacaoEscolaMap.PodeEscolherSubElemento(vocacao, subElemento.Value))
        {
            error = "Esse Sub-Elemento não é liberado pela Vocação atual.";
            return false;
        }

        return true;
    }

    private async Task<bool> HasDuplicateAsync(Guid sheetId, Elemento? elemento, SubElemento? subElemento, Guid? excludingId)
    {
        var query = db.CharacterAffinities.Where(a => a.CharacterSheetId == sheetId);
        if (excludingId is not null)
            query = query.Where(a => a.Id != excludingId);

        return await query.AnyAsync(a =>
            (elemento != null && a.Elemento == elemento) ||
            (subElemento != null && a.SubElemento == subElemento));
    }
```

- [ ] **Step 4: Wire the new checks into `Add` and `Update`**

Substitua:

```csharp
        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        var affinity = new CharacterAffinity
        {
            Id = Guid.NewGuid(), CharacterSheetId = sheetId, Elemento = elemento, ElementoValor = request.ElementoValor,
            SubElemento = subElemento, SubElementoValor = request.SubElementoValor, CaminhoNome = request.CaminhoNome, Experiencia = request.Experiencia
        };
        db.CharacterAffinities.Add(affinity);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(affinity));
    }
```

Por:

```csharp
        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, sheet.Vocacao, elementoAntigo: null, subElementoAntigo: null, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        if (await HasDuplicateAsync(sheetId, elemento, subElemento, excludingId: null))
            return BadRequest("Já existe uma linha de Afinidade com esse Elemento ou Sub-Elemento.");

        var affinity = new CharacterAffinity
        {
            Id = Guid.NewGuid(), CharacterSheetId = sheetId, Elemento = elemento, ElementoValor = request.ElementoValor,
            SubElemento = subElemento, SubElementoValor = request.SubElementoValor, CaminhoNome = request.CaminhoNome, Experiencia = request.Experiencia
        };
        db.CharacterAffinities.Add(affinity);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(affinity));
    }
```

Substitua (no método `Update`):

```csharp
        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        var affinity = await db.CharacterAffinities.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        affinity.Elemento = elemento;
```

Por (a ordem muda: a linha existente é buscada ANTES do parse, pra fornecer `elementoAntigo`/
`subElementoAntigo`):

```csharp
        var affinity = await db.CharacterAffinities.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, sheet.Vocacao, affinity.Elemento, affinity.SubElemento, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        if (await HasDuplicateAsync(sheetId, elemento, subElemento, excludingId: id))
            return BadRequest("Já existe uma linha de Afinidade com esse Elemento ou Sub-Elemento.");

        affinity.Elemento = elemento;
```

(As linhas seguintes de `Update` — `affinity.ElementoValor = ...` até `await db.SaveChangesAsync();`
— continuam exatamente como estão, não precisam mudar.)

- [ ] **Step 5: Apply the identical changes to `NpcAffinitiesController`**

Em `src/RuinaRPG.Api/Controllers/NpcAffinitiesController.cs`, substitua o método inteiro
`TryParseElementoSubElemento` (hoje idêntico ao de `CharacterAffinitiesController`, antes do
Passo 3 deste task):

```csharp
    private static bool TryParseElementoSubElemento(string? elementoRaw, string? subElementoRaw,
        out Elemento? elemento, out SubElemento? subElemento, out string? error)
    {
        elemento = null;
        subElemento = null;
        error = null;

        if (!string.IsNullOrEmpty(elementoRaw))
        {
            if (!Enum.TryParse<Elemento>(elementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Elemento desconhecido.";
                return false;
            }
            elemento = parsed;
        }

        if (!string.IsNullOrEmpty(subElementoRaw))
        {
            if (!Enum.TryParse<SubElemento>(subElementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Sub-Elemento desconhecido.";
                return false;
            }
            subElemento = parsed;
        }

        if (elemento is not null && subElemento is not null && !ElementoSubElementoValidator.IsValidCombination(elemento.Value, subElemento.Value))
        {
            error = "Essa combinação de Elemento e Sub-Elemento não existe na Matriz Elemental.";
            return false;
        }

        return true;
    }
```

Por:

```csharp
    private static bool TryParseElementoSubElemento(string? elementoRaw, string? subElementoRaw, Vocacao? vocacao,
        Elemento? elementoAntigo, SubElemento? subElementoAntigo,
        out Elemento? elemento, out SubElemento? subElemento, out string? error)
    {
        elemento = null;
        subElemento = null;
        error = null;

        if (!string.IsNullOrEmpty(elementoRaw))
        {
            if (!Enum.TryParse<Elemento>(elementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Elemento desconhecido.";
                return false;
            }
            elemento = parsed;
        }

        if (!string.IsNullOrEmpty(subElementoRaw))
        {
            if (!Enum.TryParse<SubElemento>(subElementoRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Sub-Elemento desconhecido.";
                return false;
            }
            subElemento = parsed;
        }

        if (elemento is not null && subElemento is not null && !ElementoSubElementoValidator.IsValidCombination(elemento.Value, subElemento.Value))
        {
            error = "Essa combinação de Elemento e Sub-Elemento não existe na Matriz Elemental.";
            return false;
        }

        if (elemento is not null && elemento != elementoAntigo && !VocacaoEscolaMap.PodeEscolherElemento(vocacao, elemento.Value))
        {
            error = "Esse Elemento não é liberado pela Vocação atual.";
            return false;
        }
        if (subElemento is not null && subElemento != subElementoAntigo && !VocacaoEscolaMap.PodeEscolherSubElemento(vocacao, subElemento.Value))
        {
            error = "Esse Sub-Elemento não é liberado pela Vocação atual.";
            return false;
        }

        return true;
    }

    private async Task<bool> HasDuplicateAsync(Guid sheetId, Elemento? elemento, SubElemento? subElemento, Guid? excludingId)
    {
        var query = db.NpcAffinities.Where(a => a.NpcSheetId == sheetId);
        if (excludingId is not null)
            query = query.Where(a => a.Id != excludingId);

        return await query.AnyAsync(a =>
            (elemento != null && a.Elemento == elemento) ||
            (subElemento != null && a.SubElemento == subElemento));
    }
```

Substitua (no método `Add`):

```csharp
        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        var affinity = new NpcAffinity
        {
            Id = Guid.NewGuid(), NpcSheetId = sheetId, Elemento = elemento, ElementoValor = request.ElementoValor,
            SubElemento = subElemento, SubElementoValor = request.SubElementoValor, CaminhoNome = request.CaminhoNome, Experiencia = request.Experiencia
        };
        db.NpcAffinities.Add(affinity);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(affinity));
    }
```

Por:

```csharp
        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, sheet.Vocacao, elementoAntigo: null, subElementoAntigo: null, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        if (await HasDuplicateAsync(sheetId, elemento, subElemento, excludingId: null))
            return BadRequest("Já existe uma linha de Afinidade com esse Elemento ou Sub-Elemento.");

        var affinity = new NpcAffinity
        {
            Id = Guid.NewGuid(), NpcSheetId = sheetId, Elemento = elemento, ElementoValor = request.ElementoValor,
            SubElemento = subElemento, SubElementoValor = request.SubElementoValor, CaminhoNome = request.CaminhoNome, Experiencia = request.Experiencia
        };
        db.NpcAffinities.Add(affinity);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(affinity));
    }
```

Substitua (no método `Update`):

```csharp
        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        var affinity = await db.NpcAffinities.FirstOrDefaultAsync(a => a.Id == id && a.NpcSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        affinity.Elemento = elemento;
```

Por:

```csharp
        var affinity = await db.NpcAffinities.FirstOrDefaultAsync(a => a.Id == id && a.NpcSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        if (!TryParseElementoSubElemento(request.Elemento, request.SubElemento, sheet.Vocacao, affinity.Elemento, affinity.SubElemento, out var elemento, out var subElemento, out var error))
            return BadRequest(error);

        if (await HasDuplicateAsync(sheetId, elemento, subElemento, excludingId: id))
            return BadRequest("Já existe uma linha de Afinidade com esse Elemento ou Sub-Elemento.");

        affinity.Elemento = elemento;
```

(As linhas seguintes de `Update` continuam exatamente como estão.)

- [ ] **Step 6: Give the test fixtures a Vocação that unlocks their existing Elemento/Sub-Elemento choices**

Em `tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffinitiesControllerTests.cs`,
encontre:

```csharp
    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }
```

Substitua por (Adepto libera Dobra+Consagração — cobre todas as combinações que os testes já
existentes usam: Fogo/Terra são Dobra, Vida/Aprimorar são Consagração):

```csharp
    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var setVocacao = new UpdateCharacterSheetRequest(null, "Ficha de Teste", null, null, "Adepto", null, null, null,
            false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, setVocacao));

        return sheetId;
    }
```

Em `tests/RuinaRPG.Tests.Integration/Controllers/NpcAffinitiesControllerTests.cs`, encontre:

```csharp
    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        return (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
    }
```

Substitua por:

```csharp
    private async Task<string> CreateSheetAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", gmToken));
        var sheetId = (await response.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;

        var setVocacao = new UpdateNpcSheetRequest(null, "Ficha de Teste", null, null, "Adepto", null, null, null,
            1, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, setVocacao));

        return sheetId;
    }
```

`RuinaRPG.Contracts.CharacterSheets` (pra `UpdateCharacterSheetRequest`) já está importado em
`CharacterAffinitiesControllerTests.cs` (usado por `CharacterSheetResponse`) — não precisa de
`using` novo. Em `NpcAffinitiesControllerTests.cs`, `RuinaRPG.Contracts.NpcSheets` (pra
`UpdateNpcSheetRequest`) também já está importado (usado por `NpcSheetResponse`).

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterAffinitiesControllerTests|FullyQualifiedName~NpcAffinitiesControllerTests"`
Expected: PASS — todos os testes já existentes continuam passando (agora com Vocação=Adepto) e
os 8 novos (4 por arquivo) passam.

- [ ] **Step 8: Clean-rebuild**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/CharacterAffinitiesController.cs src/RuinaRPG.Api/Controllers/NpcAffinitiesController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffinitiesControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcAffinitiesControllerTests.cs
git commit -m "feat: restrict 2.c Elemento/Sub-Elemento by Vocação + reject duplicate rows"
```

---

### Task 5: API — `EficienciaElemental`/`DanoElemental` em `SubAttributesResponse`

**Files:**
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `SubAttributeFormulas.EficienciaElemental`/`.DanoElemental`/`.ValorDaAfinidadeCorrespondente`,
  `LinhaDeAfinidade` (Task 2).
- Produces: `SubAttributesResponse.EficienciaElemental`/`.DanoElemental : int?`. Consumido pela
  Task 7.

- [ ] **Step 1: Write the failing tests**

Em `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, adicione
(perto dos outros testes de `SubAttributes_...`):

```csharp
    [Fact]
    public async Task SubAttributes_computes_EficienciaElemental_and_DanoElemental_from_the_matching_Afinidade_row()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub7", "sheetsub7@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub7", "sheetplayersub7@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Elemental");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        // Adepto libera Dobra (Fogo) — Afinidade principal = Fogo.
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken,
            ValidUpdate() with { Vocacao = "Adepto", Afinidade = "Fogo" }));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", 7, null, null, null, null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.EficienciaElemental.Should().Be(7);
        body.DanoElemental.Should().Be(7);
    }

    [Fact]
    public async Task SubAttributes_EficienciaElemental_and_DanoElemental_are_0_when_no_row_matches_Afinidade()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub8", "sheetsub8@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub8", "sheetplayersub8@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr Elemental Zero");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.EficienciaElemental.Should().Be(0);
        body.DanoElemental.Should().Be(0);
    }
```

Em `tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs`, o arquivo já
tem um helper `CreateSheetAsync(string gmToken)` (linha 66) e vários testes batendo em
`GET /api/creature-sheets/{sheetId}/sub-attributes`. Adicione dentro da classe:

```csharp
    [Fact]
    public async Task SubAttributes_EficienciaElemental_and_DanoElemental_are_null_for_Criatura()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("CreatureGmSubElemental", "creaturesubelemental@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{sheetId}/sub-attributes", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
        body!.EficienciaElemental.Should().BeNull();
        body.DanoElemental.Should().BeNull();
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~EficienciaElemental"`
Expected: FAIL to compile — `SubAttributesResponse.EficienciaElemental`/`.DanoElemental` não
existem ainda.

- [ ] **Step 3: Add the 2 fields to `SubAttributesResponse`**

Em `src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs`, encontre:

```csharp
public record SubAttributesResponse(int Iniciativa, int Movimentacao, int EsquivaNatural, int DefesaNatural, int ReducaoFisica, int ReducaoMagica, decimal? PesoAtual, decimal? PesoMaximo);
```

Substitua por:

```csharp
public record SubAttributesResponse(int Iniciativa, int Movimentacao, int EsquivaNatural, int DefesaNatural, int ReducaoFisica, int ReducaoMagica, decimal? PesoAtual, decimal? PesoMaximo, int? EficienciaElemental, int? DanoElemental);
```

- [ ] **Step 4: Wire it into `CharacterSheetsController.SubAttributes`**

Em `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, dentro do método `SubAttributes`,
encontre:

```csharp
        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number, so it can't be summed into this
            // numeric formula term today. Unlike Bruto/Artefatos above, this is a real, still-open gap.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo);
    }
```

Substitua por:

```csharp
        var linhasDeAfinidade = await db.CharacterAffinities.Where(a => a.CharacterSheetId == id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor))
            .ToListAsync();
        var valorDaAfinidade = SubAttributeFormulas.ValorDaAfinidadeCorrespondente(sheet.Afinidade, linhasDeAfinidade);

        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number, so it can't be summed into this
            // numeric formula term today. Unlike Bruto/Artefatos above, this is a real, still-open gap.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo,
            EficienciaElemental: SubAttributeFormulas.EficienciaElemental(valorDaAfinidade),
            DanoElemental: SubAttributeFormulas.DanoElemental(valorDaAfinidade));
    }
```

- [ ] **Step 5: Wire it into `NpcSheetsController.SubAttributes`**

Em `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs`, dentro do método `SubAttributes`,
encontre (os nomes de variável local são idênticos aos de `CharacterSheetsController`):

```csharp
        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number, so it can't be summed into this
            // numeric formula term today. Unlike Bruto/Artefatos above, this is a real, still-open gap.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo);
    }
```

Substitua por:

```csharp
        var linhasDeAfinidade = await db.NpcAffinities.Where(a => a.NpcSheetId == id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor))
            .ToListAsync();
        var valorDaAfinidade = SubAttributeFormulas.ValorDaAfinidadeCorrespondente(sheet.Afinidade, linhasDeAfinidade);

        return new SubAttributesResponse(
            Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao, artefatoOuItem: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Iniciativa)),
            Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.Movimentacao), pesoAtual, pesoMaximo),
            // penalidadeArmadura is hardcoded to 0: Armadura.Penalidade is a free-text string? field
            // in the Catálogo (e.g. "-1 Furtividade"), not a number, so it can't be summed into this
            // numeric formula term today. Unlike Bruto/Artefatos above, this is a real, still-open gap.
            EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.EsquivaNatural), penalidadeArmadura: 0),
            DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude, escudo: equippedShield ?? 0, artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.DefesaNatural), cobertura: coberturaBonus),
            ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoFisica), armadura: armaduraRf),
            ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.SubAtributo, SubAtributoAlvo.ReducaoMagica), armaduraMagica: armaduraRm),
            PesoAtual: pesoAtual,
            PesoMaximo: pesoMaximo,
            EficienciaElemental: SubAttributeFormulas.EficienciaElemental(valorDaAfinidade),
            DanoElemental: SubAttributeFormulas.DanoElemental(valorDaAfinidade));
    }
```

- [ ] **Step 6: Pass `null` from `CreatureSheetsController.SubAttributes`**

Em `src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs`, encontre:

```csharp
            // Espólios (5.a) is loot dropped when defeated, not a carried inventory — out of scope
            // for this feature. See docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md.
            PesoAtual: null,
            PesoMaximo: null);
    }
```

Substitua por:

```csharp
            // Espólios (5.a) is loot dropped when defeated, not a carried inventory — out of scope
            // for this feature. See docs/superpowers/specs/2026-09-08-inventory-weight-and-capacity-design.md.
            PesoAtual: null,
            PesoMaximo: null,
            // Eficiência Elemental/Dano Elemental não existem na Ficha de Criatura — ela não tem
            // Vocação nem a lista incremental de Afinidades (2.c). Ver
            // docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md.
            EficienciaElemental: null,
            DanoElemental: null);
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~EficienciaElemental"`
Expected: PASS (3 tests)

- [ ] **Step 8: Run the full sub-attributes regression batch**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~SubAttributes"`
Expected: all pass — nenhum teste de Sub-Atributo pré-existente afirma o número exato de campos
do record, então adicionar 2 campos no fim não quebra nenhum deles.

- [ ] **Step 9: Clean-rebuild**

Run: `dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs src/RuinaRPG.Api/Controllers/NpcSheetsController.cs src/RuinaRPG.Api/Controllers/CreatureSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/CreatureSheetsControllerTests.cs
git commit -m "feat: compute EficienciaElemental/DanoElemental in SubAttributesResponse"
```

---

### Task 6: Client — filtrar dropdowns de Afinidade/Elemento/Sub-Elemento pela Vocação

**Files:**
- Modify: `src/RuinaRPG.Client/Shared/Fields/AfinidadeSelect.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/AfinidadeSelectTests.cs`

**Interfaces:**
- Consumes: `VocacaoEscolaMap.PodeEscolherElemento`/`.PodeEscolherSubElemento`/`.PodeEscolherAfinidade`
  (Task 1) — o Client já referencia tipos de `RuinaRPG.Domain` diretamente noutros pontos (ex.:
  `AddEfeitoForm.razor` chama `EfeitoCustoCalculator`), então isso é reuso direto, não uma
  duplicação de mapeamento.
- Produces: `AfinidadeSelect.OpcoesPermitidas : (string Valor, string Rotulo)[]?` (novo parâmetro
  opcional — `null`, o padrão, mantém o comportamento atual de `FichaDeCriatura.razor`, que não
  muda neste plano).

`AfinidadeEssenciaField.razor` não muda — seu parâmetro `Opcoes` já é controlado por quem o
chama, então a filtragem acontece só nos 2 arquivos de Ficha.

- [ ] **Step 1: Write the failing tests for `AfinidadeSelect`**

Create `tests/RuinaRPG.Tests.Client/Shared/Fields/AfinidadeSelectTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class AfinidadeSelectTests : MudBunitContext
{
    [Fact]
    public void Without_OpcoesPermitidas_shows_every_one_of_the_18_values()
    {
        var cut = Render<AfinidadeSelect>();

        cut.FindComponents<MudSelectItem<string>>().Should().HaveCount(AfinidadeSelect.Afinidades.Length);
    }

    [Fact]
    public void With_OpcoesPermitidas_shows_only_the_given_subset()
    {
        var subset = new[] { ("Fogo", "Fogo"), ("Necromancia", "Necromancia") };

        var cut = Render<AfinidadeSelect>(p => p.Add(x => x.OpcoesPermitidas, subset));

        cut.FindComponents<MudSelectItem<string>>().Should().HaveCount(2);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~AfinidadeSelectTests"`
Expected: FAIL to compile — `OpcoesPermitidas` não existe ainda.

- [ ] **Step 3: Add the optional filter parameter to `AfinidadeSelect`**

Em `src/RuinaRPG.Client/Shared/Fields/AfinidadeSelect.razor`, encontre:

```razor
<MudSelect T="string" Label="Afinidade" Value="Value" ValueChanged="OnValueChangedAsync" Placeholder="Escolha uma Afinidade">
    @foreach (var (valor, rotulo) in Afinidades)
    {
        <MudSelectItem Value="@valor">@rotulo</MudSelectItem>
    }
</MudSelect>
```

Substitua por:

```razor
<MudSelect T="string" Label="Afinidade" Value="Value" ValueChanged="OnValueChangedAsync" Placeholder="Escolha uma Afinidade">
    @foreach (var (valor, rotulo) in OpcoesPermitidas ?? Afinidades)
    {
        <MudSelectItem Value="@valor">@rotulo</MudSelectItem>
    }
</MudSelect>
```

E, no `@code`, encontre:

```csharp
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
```

Substitua por:

```csharp
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
    // null (padrão) mostra todos os 18 valores — usado hoje por FichaDeCriatura, que não filtra
    // por Vocação (Criatura não tem esse conceito). Personagem/NPC passam o subconjunto liberado
    // pela Vocação atual (ver VocacaoEscolaMap).
    [Parameter] public (string Valor, string Rotulo)[]? OpcoesPermitidas { get; set; }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~AfinidadeSelectTests"`
Expected: PASS

- [ ] **Step 5: Filter the 1.a and 2.c dropdowns in `FichaDePersonagem.razor`**

Adicione `@using RuinaRPG.Domain.CharacterSheets` ao topo do arquivo, junto dos outros `@using`
(logo abaixo de `@using RuinaRPG.Contracts.SpellsAndAbilities`):

```razor
@using RuinaRPG.Domain.CharacterSheets
```

Encontre:

```razor
                <AfinidadeSelect @bind-Value="_form.Afinidade" @bind-Value:after="NotifySavedAsync" />
```

Substitua por:

```razor
                <AfinidadeSelect @bind-Value="_form.Afinidade" @bind-Value:after="NotifySavedAsync" OpcoesPermitidas="AfinidadesPermitidasPelaVocacao()" />
```

Encontre (as 2 ocorrências idênticas, linha existente e nova linha):

```razor
                                <AfinidadeEssenciaField Label="Elemento" Opcoes="AfinidadeEssenciaField.Elementos"
                                                         Nome="@affinity.Elemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "Elemento", v))"
                                                         Valor="@affinity.ElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "ElementoValor", v))" />
                            </td>
                            <td>
                                <AfinidadeEssenciaField Label="Sub-Elemento" Opcoes="AfinidadeEssenciaField.SubElementos"
                                                         Nome="@affinity.SubElemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElemento", v))"
                                                         Valor="@affinity.SubElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElementoValor", v))" />
```

Substitua por:

```razor
                                <AfinidadeEssenciaField Label="Elemento" Opcoes="ElementosPermitidosPelaVocacao()"
                                                         Nome="@affinity.Elemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "Elemento", v))"
                                                         Valor="@affinity.ElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "ElementoValor", v))" />
                            </td>
                            <td>
                                <AfinidadeEssenciaField Label="Sub-Elemento" Opcoes="SubElementosPermitidosPelaVocacao()"
                                                         Nome="@affinity.SubElemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElemento", v))"
                                                         Valor="@affinity.SubElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElementoValor", v))" />
```

Encontre:

```razor
                            <AfinidadeEssenciaField Label="Elemento" Opcoes="AfinidadeEssenciaField.Elementos"
                                                     @bind-Nome="_affinityForm.Elemento" @bind-Valor="_affinityForm.ElementoValor" />
                        </td>
                        <td>
                            <AfinidadeEssenciaField Label="Sub-Elemento" Opcoes="AfinidadeEssenciaField.SubElementos"
                                                     @bind-Nome="_affinityForm.SubElemento" @bind-Valor="_affinityForm.SubElementoValor" />
```

Substitua por:

```razor
                            <AfinidadeEssenciaField Label="Elemento" Opcoes="ElementosPermitidosPelaVocacao()"
                                                     @bind-Nome="_affinityForm.Elemento" @bind-Valor="_affinityForm.ElementoValor" />
                        </td>
                        <td>
                            <AfinidadeEssenciaField Label="Sub-Elemento" Opcoes="SubElementosPermitidosPelaVocacao()"
                                                     @bind-Nome="_affinityForm.SubElemento" @bind-Valor="_affinityForm.SubElementoValor" />
```

No `@code`, adicione estes 3 métodos privados (em qualquer lugar do bloco — sugestão: perto de
`AddAffinityAsync`):

```csharp
    private (string Valor, string Rotulo)[] AfinidadesPermitidasPelaVocacao() =>
        Enum.TryParse<Vocacao>(_form.Vocacao, out var vocacao)
            ? AfinidadeSelect.Afinidades.Where(a => Enum.TryParse<AfinidadeElemental>(a.Valor, out var af) && VocacaoEscolaMap.PodeEscolherAfinidade(vocacao, af)).ToArray()
            : [];

    private (string Valor, string Rotulo)[] ElementosPermitidosPelaVocacao() =>
        Enum.TryParse<Vocacao>(_form.Vocacao, out var vocacao)
            ? AfinidadeEssenciaField.Elementos.Where(e => Enum.TryParse<Elemento>(e.Valor, out var el) && VocacaoEscolaMap.PodeEscolherElemento(vocacao, el)).ToArray()
            : [];

    private (string Valor, string Rotulo)[] SubElementosPermitidosPelaVocacao() =>
        Enum.TryParse<Vocacao>(_form.Vocacao, out var vocacao)
            ? AfinidadeEssenciaField.SubElementos.Where(s => Enum.TryParse<SubElemento>(s.Valor, out var se) && VocacaoEscolaMap.PodeEscolherSubElemento(vocacao, se)).ToArray()
            : [];
```

- [ ] **Step 6: Apply the identical changes to `FichaDeNpc.razor`**

Em `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`, adicione `@using RuinaRPG.Domain.CharacterSheets`
ao topo do arquivo, junto dos outros `@using`.

Encontre:

```razor
                <AfinidadeSelect @bind-Value="_form.Afinidade" @bind-Value:after="NotifySavedAsync" />
```

Substitua por:

```razor
                <AfinidadeSelect @bind-Value="_form.Afinidade" @bind-Value:after="NotifySavedAsync" OpcoesPermitidas="AfinidadesPermitidasPelaVocacao()" />
```

Encontre (as 2 ocorrências idênticas, linha existente e nova linha — texto idêntico ao de
`FichaDePersonagem.razor`):

```razor
                                <AfinidadeEssenciaField Label="Elemento" Opcoes="AfinidadeEssenciaField.Elementos"
                                                         Nome="@affinity.Elemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "Elemento", v))"
                                                         Valor="@affinity.ElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "ElementoValor", v))" />
                            </td>
                            <td>
                                <AfinidadeEssenciaField Label="Sub-Elemento" Opcoes="AfinidadeEssenciaField.SubElementos"
                                                         Nome="@affinity.SubElemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElemento", v))"
                                                         Valor="@affinity.SubElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElementoValor", v))" />
```

Substitua por:

```razor
                                <AfinidadeEssenciaField Label="Elemento" Opcoes="ElementosPermitidosPelaVocacao()"
                                                         Nome="@affinity.Elemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "Elemento", v))"
                                                         Valor="@affinity.ElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "ElementoValor", v))" />
                            </td>
                            <td>
                                <AfinidadeEssenciaField Label="Sub-Elemento" Opcoes="SubElementosPermitidosPelaVocacao()"
                                                         Nome="@affinity.SubElemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElemento", v))"
                                                         Valor="@affinity.SubElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElementoValor", v))" />
```

Encontre:

```razor
                            <AfinidadeEssenciaField Label="Elemento" Opcoes="AfinidadeEssenciaField.Elementos"
                                                     @bind-Nome="_affinityForm.Elemento" @bind-Valor="_affinityForm.ElementoValor" />
                        </td>
                        <td>
                            <AfinidadeEssenciaField Label="Sub-Elemento" Opcoes="AfinidadeEssenciaField.SubElementos"
                                                     @bind-Nome="_affinityForm.SubElemento" @bind-Valor="_affinityForm.SubElementoValor" />
```

Substitua por:

```razor
                            <AfinidadeEssenciaField Label="Elemento" Opcoes="ElementosPermitidosPelaVocacao()"
                                                     @bind-Nome="_affinityForm.Elemento" @bind-Valor="_affinityForm.ElementoValor" />
                        </td>
                        <td>
                            <AfinidadeEssenciaField Label="Sub-Elemento" Opcoes="SubElementosPermitidosPelaVocacao()"
                                                     @bind-Nome="_affinityForm.SubElemento" @bind-Valor="_affinityForm.SubElementoValor" />
```

No `@code`, adicione os mesmos 3 métodos privados de `FichaDePersonagem.razor` (idênticos —
`FichaDeNpc.razor` também tem `_form.Vocacao : string?` e `_affinityForm`):

```csharp
    private (string Valor, string Rotulo)[] AfinidadesPermitidasPelaVocacao() =>
        Enum.TryParse<Vocacao>(_form.Vocacao, out var vocacao)
            ? AfinidadeSelect.Afinidades.Where(a => Enum.TryParse<AfinidadeElemental>(a.Valor, out var af) && VocacaoEscolaMap.PodeEscolherAfinidade(vocacao, af)).ToArray()
            : [];

    private (string Valor, string Rotulo)[] ElementosPermitidosPelaVocacao() =>
        Enum.TryParse<Vocacao>(_form.Vocacao, out var vocacao)
            ? AfinidadeEssenciaField.Elementos.Where(e => Enum.TryParse<Elemento>(e.Valor, out var el) && VocacaoEscolaMap.PodeEscolherElemento(vocacao, el)).ToArray()
            : [];

    private (string Valor, string Rotulo)[] SubElementosPermitidosPelaVocacao() =>
        Enum.TryParse<Vocacao>(_form.Vocacao, out var vocacao)
            ? AfinidadeEssenciaField.SubElementos.Where(s => Enum.TryParse<SubElemento>(s.Valor, out var se) && VocacaoEscolaMap.PodeEscolherSubElemento(vocacao, se)).ToArray()
            : [];
```

`_form.Vocacao` já é o nome real do campo em `FichaDeNpc.razor` (idêntico ao de
`FichaDePersonagem.razor` — confirmado: `_form.Vocacao = sheet.Vocacao;` já existe no arquivo).

- [ ] **Step 7: Run the full Client suite and clean-rebuild**

Run: `dotnet test tests/RuinaRPG.Tests.Client && dotnet build RuinaRPG.sln`
Expected: all pass, `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Shared/Fields/AfinidadeSelect.razor src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor tests/RuinaRPG.Tests.Client/Shared/Fields/AfinidadeSelectTests.cs
git commit -m "feat: filter Afinidade/Elemento/Sub-Elemento dropdowns by Vocação in Personagem/NPC"
```

---

### Task 7: Client — exibir Eficiência Elemental e Dano Elemental

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`

**Interfaces:**
- Consumes: `SubAttributesResponse.EficienciaElemental`/`.DanoElemental` (Task 5).
- Produces: nada — última tarefa que toca as duas Fichas.

- [ ] **Step 1: Display the 2 new Sub-Atributos in `FichaDePersonagem.razor`**

Encontre:

```razor
                <MudText>Redução Física: @_subAttributes.ReducaoFisica</MudText>
                <MudText>Redução Mágica: @_subAttributes.ReducaoMagica</MudText>
```

Substitua por:

```razor
                <MudText>Redução Física: @_subAttributes.ReducaoFisica</MudText>
                <MudText>Redução Mágica: @_subAttributes.ReducaoMagica</MudText>
                <MudText>Eficiência Elemental: @_subAttributes.EficienciaElemental</MudText>
                <MudText>Dano Elemental: @_subAttributes.DanoElemental</MudText>
```

- [ ] **Step 2: Apply the identical change to `FichaDeNpc.razor`**

Encontre o mesmo bloco (idêntico byte-a-byte) e aplique a mesma substituição.

- [ ] **Step 3: Clean-rebuild the whole client**

Run: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Run the full Client suite**

Run: `dotnet test tests/RuinaRPG.Tests.Client`
Expected: all pass — nenhum teste existente afirma o conteúdo exato da seção de Sub-Atributos ao
ponto de quebrar com 2 linhas a mais.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor
git commit -m "feat: display Eficiência Elemental / Dano Elemental in Sub-Atributos"
```

---

### Task 8: Docs — Requisitos e Fórmulas

**Files:**
- Modify: `Docs/Sistema RPG/Formulas.md`
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md`

**Interfaces:**
- Consumes: nada (tarefa só de documentação).
- Produces: nada.

- [ ] **Step 1: Update `Formulas.md`**

Em `Docs/Sistema RPG/Formulas.md`, encontre:

```
Eficiência elemental = Tabela
Dano elemental = Tabela
```

Substitua por:

```
Eficiência elemental = Valor da linha de Afinidades (2.c) correspondente à Afinidade escolhida (1.a)
Dano elemental = Valor da linha de Afinidades (2.c) correspondente à Afinidade escolhida (1.a)
```

- [ ] **Step 2: Update 2.b in `Requisitos - Ficha de Personagem.md`**

Encontre:

```
- *Eficiência Elemental* e *Dano Elemental*: "[[Formulas]]" indica que ambos vêm de uma tabela, que ainda não foi escrita em nenhum documento do sistema. Campos previstos porém não implementados até a tabela existir.
```

Substitua por:

```
- *Eficiência Elemental* e *Dano Elemental*: campos calculados, iguais entre si — ambos assumem o *Valor* (do Elemento ou do Sub-Elemento, conforme o caso) da linha de Afinidades (2.c) cujo Elemento ou Sub-Elemento bate com a *Afinidade* escolhida em 1.a. Sem Afinidade escolhida, ou sem uma linha correspondente em 2.c, os dois valem **0**. Dano Elemental é um bônus de dano exibido — mesmo tratamento que os Modificadores de Dano (3.f) já recebem: um número que o jogador aplica manualmente ao narrar um ataque elemental, sem integração automática com Efeitos/Magias. Eficiência Elemental reduz o Custo em Arcana/Foco de Magias elementais pelo mesmo raciocínio — também exibido, também aplicado manualmente.
```

- [ ] **Step 3: Document Vocação/Escola de Magia + duplicate rule in 2.c**

Encontre o fim da seção 2.c (a última frase do bloco, sobre editar/remover linhas):

```
Qualquer campo de uma linha de Afinidade já existente pode ser editado pelo jogador a qualquer momento (não só no momento de adicionar a linha), e a linha pode ser removida a qualquer momento.
```

Substitua por:

```
Qualquer campo de uma linha de Afinidade já existente pode ser editado pelo jogador a qualquer momento (não só no momento de adicionar a linha), e a linha pode ser removida a qualquer momento.

O *Elemento* e o *Sub-Elemento* de cada linha (e, por extensão, o dropdown único de *Afinidade* em 1.a) são restritos pela *Vocação* atual do personagem, via 4 Escolas de Magia (ver "Escolas_de_Magia.png" em `Docs/Sistema RPG`, sem uma seção correspondente no documento fonte — mesmo tratamento de material de referência que "Matriz_Elemental.png" já recebe):

| Escola | Elementos/Sub-Elementos |
|---|---|
| Dobra | Ar, Água, Fogo, Terra |
| Transmutação | Flora, Ferro, Raio, Gelo |
| Maculação | Necromancia, Invocação, Ecomancia, Hemomancia |
| Consagração | Curar, Aprimorar, Prever, Purificar, Alma, Vida |

Cada Vocação libera um subconjunto fixo de Escolas: Feiticeiro → Dobra e Maculação; Adepto →
Dobra e Consagração; Bruxo → Dobra e Transmutação; Campeão, Caçador, ou nenhuma Vocação
escolhida → nenhuma Escola (não é possível escolher Elemento/Sub-Elemento algum). Uma troca de
Vocação nunca invalida uma escolha já salva — só uma escolha *nova* (ou uma mudança pra um valor
diferente) é bloqueada quando cai fora da Vocação atual.

Duas linhas de Afinidade não podem ter o mesmo Elemento não-nulo entre si, nem o mesmo
Sub-Elemento não-nulo entre si — o servidor rejeita a segunda.
```

- [ ] **Step 4: Run the full Unit + Client suites as a regression check**

Run: `dotnet build RuinaRPG.sln && dotnet test tests/RuinaRPG.Tests.Unit && dotnet test tests/RuinaRPG.Tests.Client`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`, both suites pass (esta tarefa não toca
código, é uma checagem de regressão pura).

- [ ] **Step 5: Commit**

```bash
git add "Docs/Sistema RPG/Formulas.md" "Docs/Requisitos/Requisitos - Ficha de Personagem.md"
git commit -m "docs: Requisitos for Escola de Magia restriction + Eficiência/Dano Elemental formula"
```

---

## Final verification (after all 8 tasks)

- [ ] Clean-rebuild: `rm -rf src/RuinaRPG.Client/obj src/RuinaRPG.Client/bin && dotnet build RuinaRPG.sln` → 0 Warning(s), 0 Error(s).
- [ ] `dotnet test tests/RuinaRPG.Tests.Unit` → all pass.
- [ ] `dotnet test tests/RuinaRPG.Tests.Client` → all pass.
- [ ] `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests|FullyQualifiedName~NpcSheetsControllerTests|FullyQualifiedName~CharacterAffinitiesControllerTests|FullyQualifiedName~NpcAffinitiesControllerTests|FullyQualifiedName~CreatureSheetsControllerTests|FullyQualifiedName~CampaignPlayerViewControllerTests"` → all pass (scoped — a suíte completa de Integration é sabidamente instável sob contenção do Testcontainers neste ambiente).
- [ ] Confirmar manualmente (sem navegador headless disponível neste ambiente, mesma limitação já
      documentada em rounds anteriores): numa Ficha de Personagem/NPC com Vocação=Campeão, o
      dropdown de Afinidade (1.a) e os de Elemento/Sub-Elemento (2.c) aparecem vazios/sem opção;
      trocando pra Feiticeiro/Adepto/Bruxo, cada um mostra o subconjunto certo; Eficiência
      Elemental/Dano Elemental aparecem como mais dois Sub-Atributos e refletem o Valor da linha
      de Afinidade correspondente.
