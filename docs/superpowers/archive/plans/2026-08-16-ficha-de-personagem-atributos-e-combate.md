# Ficha de Personagem — Atributos, Perícias & Combate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement tabs 2 (Atributos & Perícias) and 3 (Combate) of the Ficha de Personagem: the 8 fixed attributes with their Total formula, the computed sub-attributes (Iniciativa, Movimentação, Esquiva/Defesa Natural, Redução Física/Mágica), the incremental Afinidades list, the fixed Perícias list, and the arsenal (weapons/armor/shields) that references the Catálogo. This closes the deferred máximo computation for Vitalidade/Foco (Adrenalina's máximo still waits on Artefatos, tab 5).

**Architecture:** `CharacterAttribute` (8 fixed rows per sheet, seeded at sheet creation) and `CharacterSkill` (one row per fixed Perícia, also seeded at creation) both follow the "1 row per fixed enum value" pattern Modelo de Dados describes. `CharacterAffinity` is a genuine incremental list (player adds rows freely). `CharacterWeapon`/`CharacterArmorSlot`/`CharacterShield` are "live reference to Catálogo" entities (Modelo de Dados' second linking pattern) — they store `ItemId` and read every Item field live except `DurabilidadeAtual`, which is copied at creation and edited independently thereafter (the *only* per-instance field on an otherwise-live-referenced row). All formulas (Total de Atributo, sub-attributes, Perícia Total) live in `RuinaRPG.Domain` as pure functions per `Formulas.md`, unit-tested against the exact values in that doc — the Api layer is a thin translation from persisted fields to formula inputs and back to a response DTO.

**Tech Stack:** Same as established.

**Spec:** `Docs/Requisitos/Requisitos - Ficha de Personagem.md` R0001 tabs 2 (2.a-2.d) and 3 (3.a-3.c, 3.e — 3.d Efeito de Batalha is explicitly "pendente" in the spec and stays unimplemented), `Docs/Sistema RPG/Formulas.md`, `Docs/Requisitos/Requisitos - Modelo de Dados.md` §6.1 (CharacterAttributes/CharacterSkills/CharacterAffinities/CharacterWeapons/CharacterArmorSlots/CharacterShields).

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- Edit authorization for every endpoint in this plan reuses `CharacterSheetAuthorization.CanEdit` (Ficha de Personagem — Fundação plan) — owner or the sheet's campaign GM, nothing else.
- Formula values in this plan's tests are copied verbatim from `Docs/Sistema RPG/Formulas.md` — a test that doesn't match that doc's literal wording is a bug in the test, not the doc.
- `DurabilidadeAtual` on `CharacterWeapons`/`CharacterArmorSlots`/`CharacterShields` is the one field on those rows that is NOT read live from the Catálogo `Item` — it initializes from `Item.DurabilidadeMaxima` at row-creation time and is independently editable afterward (Modelo de Dados' explicit exception to the "referência ao vivo" pattern).
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Domain — Atributo enum and Total formula

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/Atributo.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/AttributeTotalCalculator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/AttributeTotalCalculatorTests.cs`

**Interfaces:**
- Produces: `enum Atributo { Instinto, Vontade, Vigor, Influencia, Agilidade, Destreza, Astucia, Forca }`; `AttributeTotalCalculator.Total(int gasto, int bonus, bool temMaestria, int artefatos) : int`. Task 3 (attribute entity/endpoints) and every later formula in this plan consume the enum; the calculator is consumed by Task 4.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/AttributeTotalCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class AttributeTotalCalculatorTests
{
    [Fact]
    public void Total_without_maestria_halves_the_bonus_rounded_down()
    {
        // "Sem Maestria marcada: Total = Gasto + (Bônus / 2) + Artefatos" — Formulas.md.
        var result = AttributeTotalCalculator.Total(gasto: 5, bonus: 3, temMaestria: false, artefatos: 0);

        result.Should().Be(6); // 5 + (3/2 = 1, floor) + 0
    }

    [Fact]
    public void Total_with_maestria_adds_the_full_bonus()
    {
        // "Com Maestria marcada: Total = Gasto + Bônus + Artefatos" — Formulas.md.
        var result = AttributeTotalCalculator.Total(gasto: 5, bonus: 3, temMaestria: true, artefatos: 0);

        result.Should().Be(8);
    }

    [Fact]
    public void Total_adds_artefatos_in_both_cases()
    {
        AttributeTotalCalculator.Total(gasto: 0, bonus: 0, temMaestria: false, artefatos: 4).Should().Be(4);
        AttributeTotalCalculator.Total(gasto: 0, bonus: 0, temMaestria: true, artefatos: 4).Should().Be(4);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter AttributeTotalCalculatorTests`
Expected: FAIL to compile.

- [ ] **Step 3: Write the enum and calculator**

`src/RuinaRPG.Domain/CharacterSheets/Atributo.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum Atributo
{
    Instinto,
    Vontade,
    Vigor,
    Influencia,
    Agilidade,
    Destreza,
    Astucia,
    Forca
}
```

`src/RuinaRPG.Domain/CharacterSheets/AttributeTotalCalculator.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public static class AttributeTotalCalculator
{
    public static int Total(int gasto, int bonus, bool temMaestria, int artefatos) =>
        temMaestria ? gasto + bonus + artefatos : gasto + bonus / 2 + artefatos;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter AttributeTotalCalculatorTests`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/Atributo.cs src/RuinaRPG.Domain/CharacterSheets/AttributeTotalCalculator.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/AttributeTotalCalculatorTests.cs
git commit -m "feat: add atributo enum and total formula"
```

---

### Task 2: Domain — Perícia enum and sub-attribute formulas

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/Pericia.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/SkillFormulas.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/SkillFormulasTests.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs`

**Interfaces:**
- Produces: `enum Pericia { <38 values, see Step 1> }`; `SkillFormulas.Modificador(int gasto) : int` and `SkillFormulas.Total(int modificador, int atributoTotal) : int`; `SubAttributeFormulas.Iniciativa(int agilidade, int brutoProntidao, int artefatoOuItem) : int`, `.Movimentacao(int agilidade, int artefato, int pesoTotalCarregado, int forca, int vigor) : int` (applies the absolute-minimum-1 floor and the Sobrepeso/Limite-de-Carga sub-formula internally), `.EsquivaNatural(int agilidade, int brutoReflexos, int artefatos, int penalidadeArmadura) : int`, `.DefesaNatural(int vigor, int brutoFortitude, int escudo, int artefatos, int cobertura) : int`, `.ReducaoFisica(int artefato, int armadura) : int`, `.ReducaoMagica(int artefato, int armaduraMagica) : int`. Task 4 (sub-attribute endpoint) consumes every signature exactly.

- [ ] **Step 1: Write the enum**

`src/RuinaRPG.Domain/CharacterSheets/Pericia.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum Pericia
{
    Acrobacia,
    Alquimia,
    Arcano,
    Armadilhas,
    ArmasBrancas,
    ArtefatosMagicos,
    Artistico,
    Atletismo,
    Avaliacao,
    Biblioteca,
    Brigar,
    Conducao,
    Conhecimentos,
    Crime,
    EmpatiaComAnimais,
    Enganacao,
    ForcaDeVontade,
    Fortitude,
    Furtividade,
    Herborismo,
    Intimidacao,
    Intuicao,
    Investigacao,
    Labia,
    Lideranca,
    Linguistica,
    Medicina,
    Navegacao,
    Ocultismo,
    Oficio,
    Percepcao,
    Pontaria,
    Prontidao,
    Reflexos,
    Religiao,
    Saquear,
    Seducao,
    SensoComum,
    Sobrevivencia
}
```

- [ ] **Step 2: Write the failing skill formula tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/SkillFormulasTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class SkillFormulasTests
{
    [Fact]
    public void Modificador_divides_gasto_by_3_rounded_down()
    {
        // "Modificador = Gasto ÷ 3 (arredondado para baixo)" — Sistema Básico §2.
        SkillFormulas.Modificador(gasto: 8).Should().Be(2);
        SkillFormulas.Modificador(gasto: 9).Should().Be(3);
        SkillFormulas.Modificador(gasto: 0).Should().Be(0);
    }

    [Fact]
    public void Total_adds_modificador_and_the_chosen_atributo_total()
    {
        // "Total = Modificador + Total do Atributo escolhido" — Ficha de Personagem 2.d.
        SkillFormulas.Total(modificador: 3, atributoTotal: 6).Should().Be(9);
    }
}
```

- [ ] **Step 3: Write the failing sub-attribute formula tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class SubAttributeFormulasTests
{
    [Fact]
    public void Iniciativa_sums_agilidade_bruto_prontidao_and_artefato_ou_item()
    {
        // "Iniciativa = Agilidade + Bruto Prontidão + Artefato ou item" — 2.b.
        SubAttributeFormulas.Iniciativa(agilidade: 5, brutoProntidao: 2, artefatoOuItem: 1).Should().Be(8);
    }

    [Fact]
    public void Movimentacao_applies_the_formula_with_no_sobrepeso()
    {
        // "Movimentação = (Agilidade × 2) + Artefato − Sobrepeso", Sobrepeso = max(0, Peso Total −
        // Limite de Carga), Limite de Carga = piso((Força + Vigor) / 2) — 2.b.
        // Limite de Carga = floor((10+10)/2) = 10; Peso Total 5 <= 10 → Sobrepeso 0.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoTotalCarregado: 5, forca: 10, vigor: 10);

        result.Should().Be(8); // (4*2) + 0 - 0
    }

    [Fact]
    public void Movimentacao_subtracts_sobrepeso_when_carried_weight_exceeds_the_limit()
    {
        // Limite de Carga = floor((4+4)/2) = 4; Peso Total 10 → Sobrepeso = 10 - 4 = 6.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 4, artefato: 0, pesoTotalCarregado: 10, forca: 4, vigor: 4);

        result.Should().Be(2); // (4*2) + 0 - 6 = 2, above the floor of 1
    }

    [Fact]
    public void Movimentacao_never_goes_below_the_absolute_minimum_of_1()
    {
        // Limite de Carga = floor((2+2)/2) = 2; Peso Total 100 → Sobrepeso huge → formula goes deeply
        // negative, but "mínimo absoluto de 1" clamps it.
        var result = SubAttributeFormulas.Movimentacao(agilidade: 1, artefato: 0, pesoTotalCarregado: 100, forca: 2, vigor: 2);

        result.Should().Be(1);
    }

    [Fact]
    public void EsquivaNatural_subtracts_armor_penalty()
    {
        // "Esquiva Natural = Agilidade + Bruto Reflexos + Artefatos − Penalidade de armadura" — 2.b.
        SubAttributeFormulas.EsquivaNatural(agilidade: 5, brutoReflexos: 2, artefatos: 1, penalidadeArmadura: 3).Should().Be(5);
    }

    [Fact]
    public void DefesaNatural_sums_every_term_including_cobertura()
    {
        // "Defesa Natural = Vigor + Bruto Fortitude + Escudo + Artefatos + Cobertura" — 2.b.
        SubAttributeFormulas.DefesaNatural(vigor: 5, brutoFortitude: 2, escudo: 3, artefatos: 1, cobertura: 5).Should().Be(16);
    }

    [Fact]
    public void ReducaoFisica_and_ReducaoMagica_sum_artefato_and_armor()
    {
        // "Redução Física = Artefato + Armadura" / "Redução Mágica = Artefato + Armadura mágica" — 2.b.
        SubAttributeFormulas.ReducaoFisica(artefato: 2, armadura: 3).Should().Be(5);
        SubAttributeFormulas.ReducaoMagica(artefato: 1, armaduraMagica: 4).Should().Be(5);
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~SkillFormulasTests|FullyQualifiedName~SubAttributeFormulasTests"`
Expected: FAIL to compile.

- [ ] **Step 5: Write `SkillFormulas`**

`src/RuinaRPG.Domain/CharacterSheets/SkillFormulas.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public static class SkillFormulas
{
    public static int Modificador(int gasto) => gasto / 3;

    public static int Total(int modificador, int atributoTotal) => modificador + atributoTotal;
}
```

- [ ] **Step 6: Write `SubAttributeFormulas`**

`src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public static class SubAttributeFormulas
{
    public static int Iniciativa(int agilidade, int brutoProntidao, int artefatoOuItem) =>
        agilidade + brutoProntidao + artefatoOuItem;

    public static int Movimentacao(int agilidade, int artefato, int pesoTotalCarregado, int forca, int vigor)
    {
        var limiteDeCarga = (forca + vigor) / 2;
        var sobrepeso = Math.Max(0, pesoTotalCarregado - limiteDeCarga);
        var raw = agilidade * 2 + artefato - sobrepeso;
        return Math.Max(1, raw);
    }

    public static int EsquivaNatural(int agilidade, int brutoReflexos, int artefatos, int penalidadeArmadura) =>
        agilidade + brutoReflexos + artefatos - penalidadeArmadura;

    public static int DefesaNatural(int vigor, int brutoFortitude, int escudo, int artefatos, int cobertura) =>
        vigor + brutoFortitude + escudo + artefatos + cobertura;

    public static int ReducaoFisica(int artefato, int armadura) => artefato + armadura;

    public static int ReducaoMagica(int artefato, int armaduraMagica) => artefato + armaduraMagica;
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~SkillFormulasTests|FullyQualifiedName~SubAttributeFormulasTests"`
Expected: PASS (2/2 and 7/7).

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/Pericia.cs src/RuinaRPG.Domain/CharacterSheets/SkillFormulas.cs src/RuinaRPG.Domain/CharacterSheets/SubAttributeFormulas.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/SkillFormulasTests.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/SubAttributeFormulasTests.cs
git commit -m "feat: add pericia enum and sub-attribute formulas"
```

---

### Task 3: Infrastructure — `CharacterAttribute`/`CharacterSkill` entities, migration, and seeding on sheet creation

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAttribute.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSkill.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CharacterAttributeAndSkillMigrationTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `Atributo` (Task 1), `Pericia` (Task 2), `CharacterSheetsController.Create` (Ficha de Personagem — Fundação plan).
- Produces: `CharacterAttribute` (`Guid Id`, `Guid CharacterSheetId`, `Atributo Atributo`, `int Gasto`, `int Bonus`, `bool TemMaestria`); `CharacterSkill` (`Guid Id`, `Guid CharacterSheetId`, `Pericia Pericia`, `int Gasto`). `RuinaRpgDbContext.CharacterAttributes`/`CharacterSkills`. `Create` (modified) now also inserts 8 `CharacterAttribute` rows and 38 `CharacterSkill` rows, all zeroed, in the same transaction as the sheet. Tasks 4-5 depend on this shape.

- [ ] **Step 1: Write the entities**

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAttribute.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterAttribute
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Atributo Atributo { get; set; }
    public int Gasto { get; set; }
    public int Bonus { get; set; }
    public bool TemMaestria { get; set; }
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSkill.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSkill
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Pericia Pericia { get; set; }
    public int Gasto { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CharacterAttributeAndSkillMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterAttributeAndSkillMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterAttributeAndSkillMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_attribute_and_skill_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterAttributesAndSkills"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@attrtest.com", Email = "gm@attrtest.com", Nickname = "AttrTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@attrtest.com", Email = "player@attrtest.com", Nickname = "AttrTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        db.CharacterAttributes.Add(new CharacterAttribute { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Atributo = Atributo.Forca, Gasto = 3 });
        db.CharacterSkills.Add(new CharacterSkill { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Pericia = Pericia.Atletismo, Gasto = 6 });
        await db.SaveChangesAsync();

        (await db.CharacterAttributes.CountAsync()).Should().Be(1);
        (await db.CharacterSkills.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterAttributeAndSkillMigrationTests`
Expected: FAIL — no `AddCharacterAttributesAndSkills` migration exists yet.

- [ ] **Step 4: Register the DbSets and relationships**

Add `using RuinaRPG.Infrastructure.CharacterSheets;` (already present) and:

```csharp
public DbSet<CharacterAttribute> CharacterAttributes => Set<CharacterAttribute>();
public DbSet<CharacterSkill> CharacterSkills => Set<CharacterSkill>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<CharacterAttribute>(entity =>
{
    entity.HasIndex(a => new { a.CharacterSheetId, a.Atributo }).IsUnique();
    entity.HasOne<CharacterSheet>()
        .WithMany()
        .HasForeignKey(a => a.CharacterSheetId)
        .OnDelete(DeleteBehavior.Cascade);
});

builder.Entity<CharacterSkill>(entity =>
{
    entity.HasIndex(s => new { s.CharacterSheetId, s.Pericia }).IsUnique();
    entity.HasOne<CharacterSheet>()
        .WithMany()
        .HasForeignKey(s => s.CharacterSheetId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddCharacterAttributesAndSkills \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the migration test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterAttributeAndSkillMigrationTests`
Expected: PASS.

- [ ] **Step 7: Write the failing seeding test**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`:

```csharp
[Fact]
public async Task Create_seeds_8_zeroed_attributes_and_38_zeroed_skills()
{
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();

    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSeed1", "sheetseed1@teste.com");
    var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSeed1", "sheetplayerseed1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Seed");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var attributes = await db.CharacterAttributes.Where(a => a.CharacterSheetId == Guid.Parse(sheetId)).ToListAsync();
    var skills = await db.CharacterSkills.Where(s => s.CharacterSheetId == Guid.Parse(sheetId)).ToListAsync();

    attributes.Should().HaveCount(8);
    attributes.Should().OnlyContain(a => a.Gasto == 0 && a.Bonus == 0 && !a.TemMaestria);
    skills.Should().HaveCount(38);
    skills.Should().OnlyContain(s => s.Gasto == 0);
}
```

Add `using Microsoft.EntityFrameworkCore;` and `using Microsoft.Extensions.DependencyInjection;` at the top if not already present.

- [ ] **Step 8: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter Create_seeds_8_zeroed_attributes_and_38_zeroed_skills`
Expected: FAIL — `Create` doesn't seed attributes/skills yet.

- [ ] **Step 9: Update `Create` to seed attributes and skills**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`'s `Create` action, right before `await db.SaveChangesAsync();`, add:

```csharp
        foreach (var atributo in Enum.GetValues<Atributo>())
            db.CharacterAttributes.Add(new CharacterAttribute { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Atributo = atributo });
        foreach (var pericia in Enum.GetValues<Pericia>())
            db.CharacterSkills.Add(new CharacterSkill { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Pericia = pericia });
```

- [ ] **Step 10: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: PASS (all, including the new seeding test).

- [ ] **Step 11: Commit**

```bash
git add src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAttribute.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSkill.cs src/RuinaRPG.Infrastructure/Persistence src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs tests/RuinaRPG.Tests.Integration
git commit -m "feat: add character attribute and skill entities, seeded on sheet creation"
```

---

### Task 4: Api — attributes and skills endpoints (2.a, 2.d)

**Files:**
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterAttributeResponse.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterAttributeRequest.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSkillResponse.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSkillRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/CharacterAttributesController.cs`
- Create: `src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterAttributesControllerTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSkillsControllerTests.cs`

**Interfaces:**
- Consumes: `AttributeTotalCalculator.Total`, `SkillFormulas.Modificador`/`.Total` (Tasks 1-2), `CharacterAttribute`/`CharacterSkill` (Task 3), `CharacterSheetAuthorization.CanEdit` (Fundação plan).
- Produces: `GET /api/character-sheets/{sheetId}/attributes` → `200` + `List<CharacterAttributeResponse>` (8 rows, `Total` computed with `artefatos: 0` — tab 5's Artefatos aren't built yet, see plan Architecture note); `PUT /api/character-sheets/{sheetId}/attributes/{atributo}` → `204`/`403`/`404`. `GET /api/character-sheets/{sheetId}/skills` → `200` + `List<CharacterSkillResponse>` (38 rows; `Total` needs the caller to choose which attribute backs this roll, so it's computed per-request via a query parameter, not stored); `PUT /api/character-sheets/{sheetId}/skills/{pericia}` → `204`/`403`/`404`. `CharacterAttributeResponse(string Atributo, int Gasto, int Bonus, bool TemMaestria, int Total)`; `UpdateCharacterAttributeRequest(int Gasto, int Bonus, bool TemMaestria)`; `CharacterSkillResponse(string Pericia, int Gasto, int Modificador, string? AtributoEscolhido, int? Total)`; `UpdateCharacterSkillRequest(int Gasto, string? AtributoEscolhido)`.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/CharacterSheets/CharacterAttributeResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterAttributeResponse(string Atributo, int Gasto, int Bonus, bool TemMaestria, int Total);
```

`src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterAttributeRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record UpdateCharacterAttributeRequest(int Gasto, int Bonus, bool TemMaestria);
```

`src/RuinaRPG.Contracts/CharacterSheets/CharacterSkillResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterSkillResponse(string Pericia, int Gasto, int Modificador, string? AtributoEscolhido, int? Total);
```

`src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSkillRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record UpdateCharacterSkillRequest(int Gasto, string? AtributoEscolhido);
```

Note: `AtributoEscolhido` is NOT persisted on `CharacterSkill` (Modelo de Dados: "Atributo usado no teste é escolhido no momento da rolagem, não persistido") — `UpdateCharacterSkillRequest.AtributoEscolhido` only feeds this ONE response's `Total`/`AtributoEscolhido` echo, transiently, and is discarded after the request.

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CharacterAttributesControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterAttributesControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterAttributesControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    [Fact]
    public async Task List_returns_8_attributes_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttrGm1", "attr1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer1", "attrplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/attributes", playerToken));

        var body = await response.Content.ReadFromJsonAsync<List<CharacterAttributeResponse>>();
        body!.Should().HaveCount(8);
        body!.Should().OnlyContain(a => a.Total == 0);
    }

    [Fact]
    public async Task Update_an_attribute_by_the_owner_recomputes_Total()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttrGm2", "attr2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer2", "attrplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", playerToken,
            new UpdateCharacterAttributeRequest(5, 3, false)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/attributes", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterAttributeResponse>>();
        body!.Single(a => a.Atributo == "Forca").Total.Should().Be(6); // 5 + floor(3/2) + 0
    }

    [Fact]
    public async Task Update_an_attribute_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttrGm3", "attr3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer3", "attrplayer3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "AttrPlayer3b", "attrplayer3b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Forca", otherToken,
            new UpdateCharacterAttributeRequest(5, 3, false)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

`tests/RuinaRPG.Tests.Integration/Controllers/CharacterSkillsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterSkillsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterSkillsControllerTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        return ((await meResponse.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> SetUpSheetAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return (await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
    }

    [Fact]
    public async Task List_returns_38_skills_all_zeroed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm1", "skill1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer1", "skillplayer1@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));

        var body = await response.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();
        body!.Should().HaveCount(38);
    }

    [Fact]
    public async Task Update_a_skill_sets_Gasto_and_the_response_computes_Modificador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm2", "skill2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer2", "skillplayer2@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Atletismo", playerToken,
            new UpdateCharacterSkillRequest(9, null)));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/skills", playerToken));
        var body = await listResponse.Content.ReadFromJsonAsync<List<CharacterSkillResponse>>();
        body!.Single(s => s.Pericia == "Atletismo").Modificador.Should().Be(3); // 9 / 3
    }

    [Fact]
    public async Task Update_by_an_unrelated_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SkillGm3", "skill3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer3", "skillplayer3@teste.com");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "SkillPlayer3b", "skillplayer3b@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/Atletismo", otherToken,
            new UpdateCharacterSkillRequest(9, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterAttributesControllerTests|FullyQualifiedName~CharacterSkillsControllerTests"`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 4: Write `CharacterAttributesController`**

`src/RuinaRPG.Api/Controllers/CharacterAttributesController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/attributes")]
public class CharacterAttributesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CharacterAttributeResponse>>> List(Guid sheetId)
    {
        var attributes = await db.CharacterAttributes.Where(a => a.CharacterSheetId == sheetId).ToListAsync();
        return attributes
            .Select(a => new CharacterAttributeResponse(a.Atributo.ToString(), a.Gasto, a.Bonus, a.TemMaestria,
                AttributeTotalCalculator.Total(a.Gasto, a.Bonus, a.TemMaestria, artefatos: 0)))
            .ToList();
    }

    [HttpPut("{atributo}")]
    public async Task<IActionResult> Update(Guid sheetId, Atributo atributo, UpdateCharacterAttributeRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var attribute = await db.CharacterAttributes.SingleAsync(a => a.CharacterSheetId == sheetId && a.Atributo == atributo);
        attribute.Gasto = request.Gasto;
        attribute.Bonus = request.Bonus;
        attribute.TemMaestria = request.TemMaestria;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Write `CharacterSkillsController`**

`src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/skills")]
public class CharacterSkillsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CharacterSkillResponse>>> List(Guid sheetId, [FromQuery] string? atributoEscolhido)
    {
        var skills = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheetId).ToListAsync();

        int? atributoTotal = null;
        if (Enum.TryParse<Atributo>(atributoEscolhido, out var parsedAtributo))
        {
            var attribute = await db.CharacterAttributes.SingleOrDefaultAsync(a => a.CharacterSheetId == sheetId && a.Atributo == parsedAtributo);
            if (attribute is not null)
                atributoTotal = AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
        }

        return skills
            .Select(s =>
            {
                var modificador = SkillFormulas.Modificador(s.Gasto);
                var total = atributoTotal is not null ? SkillFormulas.Total(modificador, atributoTotal.Value) : (int?)null;
                return new CharacterSkillResponse(s.Pericia.ToString(), s.Gasto, modificador, atributoEscolhido, total);
            })
            .ToList();
    }

    [HttpPut("{pericia}")]
    public async Task<IActionResult> Update(Guid sheetId, Pericia pericia, UpdateCharacterSkillRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var skill = await db.CharacterSkills.SingleAsync(s => s.CharacterSheetId == sheetId && s.Pericia == pericia);
        skill.Gasto = request.Gasto;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterAttributesControllerTests|FullyQualifiedName~CharacterSkillsControllerTests"`
Expected: PASS (3/3 and 3/3).

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/CharacterAttributeResponse.cs src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterAttributeRequest.cs src/RuinaRPG.Contracts/CharacterSheets/CharacterSkillResponse.cs src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSkillRequest.cs src/RuinaRPG.Api/Controllers/CharacterAttributesController.cs src/RuinaRPG.Api/Controllers/CharacterSkillsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterAttributesControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSkillsControllerTests.cs
git commit -m "feat: add attribute and skill endpoints"
```

---

### Task 5: Infrastructure + Api — Afinidades incremental list (2.c)

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/Elemento.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/SubElemento.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/ElementoSubElementoValidator.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAffinity.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/AddCharacterAffinityRequest.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterAffinityResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/CharacterAffinitiesController.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/ElementoSubElementoValidatorTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CharacterAffinityMigrationTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffinitiesControllerTests.cs`

**Interfaces:**
- Consumes: `CharacterSheetAuthorization.CanEdit` (Fundação plan).
- Produces: `enum Elemento { Ar, Agua, Fogo, Terra }`; `enum SubElemento { Gelo, Raio, Prever, Ecomancia, Alma, Flora, Purificar, Hemomancia, Ferro, Curar, Necromancia, Vida, Aprimorar, Invocacao }`; `ElementoSubElementoValidator.IsValidCombination(Elemento, SubElemento) : bool`; `CharacterAffinity` (`Guid Id`, `Guid CharacterSheetId`, `Elemento`, `SubElemento`, `string CaminhoNome`, `int Experiencia`). `POST /api/character-sheets/{sheetId}/affinities` → `201`; `GET .../affinities` → `200` + list; `DELETE .../affinities/{id}` → `204`/`404`. `AddCharacterAffinityRequest(string Elemento, string SubElemento, string CaminhoNome, int Experiencia)`; `CharacterAffinityResponse(string Id, string Elemento, string SubElemento, string CaminhoNome, int Experiencia)`.

- [ ] **Step 1: Write the failing validator tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/ElementoSubElementoValidatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class ElementoSubElementoValidatorTests
{
    // Matriz Elemental, per Ficha de Personagem 2.c:
    // Ar: Gelo, Raio, Prever, Ecomancia, Alma. Água: Gelo, Flora, Purificar, Hemomancia, Alma.
    // Fogo: Raio, Ferro, Curar, Necromancia, Vida. Terra: Ferro, Flora, Aprimorar, Invocação, Vida.
    [Theory]
    [InlineData(Elemento.Ar, SubElemento.Gelo, true)]
    [InlineData(Elemento.Ar, SubElemento.Alma, true)]
    [InlineData(Elemento.Ar, SubElemento.Ferro, false)]
    [InlineData(Elemento.Agua, SubElemento.Flora, true)]
    [InlineData(Elemento.Agua, SubElemento.Alma, true)]
    [InlineData(Elemento.Fogo, SubElemento.Vida, true)]
    [InlineData(Elemento.Terra, SubElemento.Vida, true)]
    [InlineData(Elemento.Terra, SubElemento.Curar, false)]
    public void IsValidCombination_matches_the_Matriz_Elemental(Elemento elemento, SubElemento subElemento, bool expected)
    {
        var result = ElementoSubElementoValidator.IsValidCombination(elemento, subElemento);

        result.Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter ElementoSubElementoValidatorTests`
Expected: FAIL to compile.

- [ ] **Step 3: Write the enums and validator**

`src/RuinaRPG.Domain/CharacterSheets/Elemento.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum Elemento
{
    Ar,
    Agua,
    Fogo,
    Terra
}
```

`src/RuinaRPG.Domain/CharacterSheets/SubElemento.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum SubElemento
{
    Gelo,
    Raio,
    Prever,
    Ecomancia,
    Alma,
    Flora,
    Purificar,
    Hemomancia,
    Ferro,
    Curar,
    Necromancia,
    Vida,
    Aprimorar,
    Invocacao
}
```

`src/RuinaRPG.Domain/CharacterSheets/ElementoSubElementoValidator.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public static class ElementoSubElementoValidator
{
    private static readonly Dictionary<Elemento, SubElemento[]> ValidPairs = new()
    {
        [Elemento.Ar] = [SubElemento.Gelo, SubElemento.Raio, SubElemento.Prever, SubElemento.Ecomancia, SubElemento.Alma],
        [Elemento.Agua] = [SubElemento.Gelo, SubElemento.Flora, SubElemento.Purificar, SubElemento.Hemomancia, SubElemento.Alma],
        [Elemento.Fogo] = [SubElemento.Raio, SubElemento.Ferro, SubElemento.Curar, SubElemento.Necromancia, SubElemento.Vida],
        [Elemento.Terra] = [SubElemento.Ferro, SubElemento.Flora, SubElemento.Aprimorar, SubElemento.Invocacao, SubElemento.Vida]
    };

    public static bool IsValidCombination(Elemento elemento, SubElemento subElemento) =>
        ValidPairs[elemento].Contains(subElemento);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter ElementoSubElementoValidatorTests`
Expected: PASS (8/8).

- [ ] **Step 5: Write `CharacterAffinity` and its migration test**

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAffinity.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterAffinity
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Elemento Elemento { get; set; }
    public SubElemento SubElemento { get; set; }
    public required string CaminhoNome { get; set; }
    public int Experiencia { get; set; }
}
```

`tests/RuinaRPG.Tests.Integration/Persistence/CharacterAffinityMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterAffinityMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterAffinityMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CharacterAffinities_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterAffinities"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@afftest.com", Email = "gm@afftest.com", Nickname = "AffTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@afftest.com", Email = "player@afftest.com", Nickname = "AffTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        db.CharacterAffinities.Add(new CharacterAffinity { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Elemento = Elemento.Fogo, SubElemento = SubElemento.Vida, CaminhoNome = "Caminho da Fênix", Experiencia = 10 });
        await db.SaveChangesAsync();

        (await db.CharacterAffinities.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterAffinityMigrationTests`
Expected: FAIL — no `AddCharacterAffinities` migration exists yet.

- [ ] **Step 7: Register the DbSet and relationship**

Add to `RuinaRpgDbContext`:

```csharp
public DbSet<CharacterAffinity> CharacterAffinities => Set<CharacterAffinity>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<CharacterAffinity>(entity =>
{
    entity.HasOne<CharacterSheet>()
        .WithMany()
        .HasForeignKey(a => a.CharacterSheetId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 8: Create the migration and run the test to verify it passes**

```bash
dotnet ef migrations add AddCharacterAffinities \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterAffinityMigrationTests`
Expected: PASS.

- [ ] **Step 9: Write the contracts and the failing controller tests**

`src/RuinaRPG.Contracts/CharacterSheets/AddCharacterAffinityRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record AddCharacterAffinityRequest(string Elemento, string SubElemento, string CaminhoNome, int Experiencia);
```

`src/RuinaRPG.Contracts/CharacterSheets/CharacterAffinityResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterAffinityResponse(string Id, string Elemento, string SubElemento, string CaminhoNome, int Experiencia);
```

`tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffinitiesControllerTests.cs` — follow the exact fixture/helper pattern from `CharacterAttributesControllerTests` (Task 4, Step 2) with these cases:

```csharp
[Fact]
public async Task Add_a_valid_combination_returns_201()
// asserts 201, then GET .../affinities contains it

[Fact]
public async Task Add_an_invalid_Elemento_SubElemento_combination_returns_400()
// Elemento "Ar", SubElemento "Ferro" (not in the Matriz Elemental for Ar) → 400

[Fact]
public async Task Delete_an_existing_affinity_returns_204_and_it_no_longer_appears_on_list()

[Fact]
public async Task Add_by_an_unrelated_jogador_returns_403()
```

(Write these 4 tests in full, mirroring `CharacterAttributesControllerTests`'s exact structure — same `AuthedRequest`/`RegisterGmAndGetTokenAsync`/`RegisterJogadorLinkedToAsync`/`SetUpSheetAsync` helpers, copied verbatim into this new test class, since xUnit test classes don't share instance state across files.)

- [ ] **Step 10: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterAffinitiesControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 11: Write `CharacterAffinitiesController`**

`src/RuinaRPG.Api/Controllers/CharacterAffinitiesController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/affinities")]
public class CharacterAffinitiesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterAffinityResponse>> Add(Guid sheetId, AddCharacterAffinityRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        if (!Enum.TryParse<Elemento>(request.Elemento, out var elemento) || !Enum.TryParse<SubElemento>(request.SubElemento, out var subElemento))
            return BadRequest("Elemento ou Sub-Elemento desconhecido.");

        if (!ElementoSubElementoValidator.IsValidCombination(elemento, subElemento))
            return BadRequest("Essa combinação de Elemento e Sub-Elemento não existe na Matriz Elemental.");

        var affinity = new CharacterAffinity { Id = Guid.NewGuid(), CharacterSheetId = sheetId, Elemento = elemento, SubElemento = subElemento, CaminhoNome = request.CaminhoNome, Experiencia = request.Experiencia };
        db.CharacterAffinities.Add(affinity);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(affinity));
    }

    [HttpGet]
    public async Task<ActionResult<List<CharacterAffinityResponse>>> List(Guid sheetId)
    {
        var affinities = await db.CharacterAffinities.Where(a => a.CharacterSheetId == sheetId).ToListAsync();
        return affinities.Select(ToResponse).ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var affinity = await db.CharacterAffinities.FirstOrDefaultAsync(a => a.Id == id && a.CharacterSheetId == sheetId);
        if (affinity is null)
            return NotFound();

        db.CharacterAffinities.Remove(affinity);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static CharacterAffinityResponse ToResponse(CharacterAffinity a) =>
        new(a.Id.ToString(), a.Elemento.ToString(), a.SubElemento.ToString(), a.CaminhoNome, a.Experiencia);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 12: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterAffinitiesControllerTests`
Expected: PASS (4/4).

- [ ] **Step 13: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/Elemento.cs src/RuinaRPG.Domain/CharacterSheets/SubElemento.cs src/RuinaRPG.Domain/CharacterSheets/ElementoSubElementoValidator.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAffinity.cs src/RuinaRPG.Contracts/CharacterSheets/AddCharacterAffinityRequest.cs src/RuinaRPG.Contracts/CharacterSheets/CharacterAffinityResponse.cs src/RuinaRPG.Api/Controllers/CharacterAffinitiesController.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Unit/CharacterSheets/ElementoSubElementoValidatorTests.cs tests/RuinaRPG.Tests.Integration/Persistence/CharacterAffinityMigrationTests.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffinitiesControllerTests.cs
git commit -m "feat: add affinities incremental list"
```

---

### Task 6: Infrastructure + Api — arsenal (weapons, armor, shields — 3.a-3.c)

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterWeapon.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/ArmorSlotType.cs` (Domain, not Infrastructure — see Step 1)
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterArmorSlot.cs`
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterShield.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterWeaponResponse.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/AddCharacterWeaponRequest.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterArmorSlotResponse.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterArmorSlotRequest.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterShieldResponse.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/AddCharacterShieldRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/CharacterArsenalController.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CharacterArsenalMigrationTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterArsenalControllerTests.cs`

**Interfaces:**
- Consumes: `Item`/`Arma`/`Armadura`/`Escudo` (Catálogo plan), `CharacterSheetAuthorization.CanEdit` (Fundação plan).
- Produces: `enum ArmorSlotType { Capacete, Superior, Inferior }`; `CharacterWeapon` (`Guid Id`, `Guid CharacterSheetId`, `Guid ItemId`, `bool IsEquipped`, `int DurabilidadeAtual`); `CharacterArmorSlot` (`Guid Id`, `Guid CharacterSheetId`, `ArmorSlotType Slot`, `Guid? ItemId`, `int? DurabilidadeAtual`) — 3 rows seeded per sheet, same pattern as attributes/skills; `CharacterShield` (`Guid Id`, `Guid CharacterSheetId`, `Guid ItemId`, `bool IsEquipped`, `int DurabilidadeAtual`). `POST/GET/DELETE /api/character-sheets/{sheetId}/weapons`, `GET/PUT /api/character-sheets/{sheetId}/armor-slots/{slot}`, `POST/GET/DELETE /api/character-sheets/{sheetId}/shields` — every field the Catálogo `Item` exposes for that type is read live via a join, except `DurabilidadeAtual`. Ambidestria's "2 weapons equipped" exception is explicitly out of scope (see plan's out-of-scope list) — this task enforces the single-equipped-weapon default only.

- [ ] **Step 1: Write the entities**

`src/RuinaRPG.Domain/CharacterSheets/ArmorSlotType.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum ArmorSlotType
{
    Capacete,
    Superior,
    Inferior
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterWeapon.cs`:

```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterWeapon
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid ItemId { get; set; }
    public bool IsEquipped { get; set; }
    public int DurabilidadeAtual { get; set; }
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterArmorSlot.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterArmorSlot
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public ArmorSlotType Slot { get; set; }
    public Guid? ItemId { get; set; }
    public int? DurabilidadeAtual { get; set; }
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterShield.cs`:

```csharp
namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterShield
{
    public Guid Id { get; set; }
    public Guid CharacterSheetId { get; set; }
    public Guid ItemId { get; set; }
    public bool IsEquipped { get; set; }
    public int DurabilidadeAtual { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CharacterArsenalMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterArsenalMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterArsenalMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_weapon_armor_and_shield_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterArsenal"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@arsenaltest.com", Email = "gm@arsenaltest.com", Nickname = "ArsenalTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@arsenaltest.com", Email = "player@arsenaltest.com", Nickname = "ArsenalTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        var weaponItem = new Arma { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Espada", Peso = 1, Preco = 10, DurabilidadeMaxima = 20 };
        db.Set<Arma>().Add(weaponItem);
        await db.SaveChangesAsync();

        db.CharacterWeapons.Add(new CharacterWeapon { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, ItemId = weaponItem.Id, IsEquipped = true, DurabilidadeAtual = 20 });
        db.CharacterArmorSlots.Add(new CharacterArmorSlot { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Slot = ArmorSlotType.Capacete });
        await db.SaveChangesAsync();

        (await db.CharacterWeapons.CountAsync()).Should().Be(1);
        (await db.CharacterArmorSlots.CountAsync()).Should().Be(1);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterArsenalMigrationTests`
Expected: FAIL — no `AddCharacterArsenal` migration exists yet.

- [ ] **Step 4: Register the DbSets and relationships**

Add to `RuinaRpgDbContext`:

```csharp
public DbSet<CharacterWeapon> CharacterWeapons => Set<CharacterWeapon>();
public DbSet<CharacterArmorSlot> CharacterArmorSlots => Set<CharacterArmorSlot>();
public DbSet<CharacterShield> CharacterShields => Set<CharacterShield>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<CharacterWeapon>(entity =>
{
    entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(w => w.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Item>().WithMany().HasForeignKey(w => w.ItemId).OnDelete(DeleteBehavior.Restrict);
});

builder.Entity<CharacterArmorSlot>(entity =>
{
    entity.HasIndex(a => new { a.CharacterSheetId, a.Slot }).IsUnique();
    entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(a => a.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Item>().WithMany().HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
});

builder.Entity<CharacterShield>(entity =>
{
    entity.HasOne<CharacterSheet>().WithMany().HasForeignKey(s => s.CharacterSheetId).OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Item>().WithMany().HasForeignKey(s => s.ItemId).OnDelete(DeleteBehavior.Restrict);
});
```

`DeleteBehavior.Restrict` on the `Item` FKs (not `Cascade`) — deleting an Item from the Catálogo while a character still has it equipped should fail loudly, not silently vanish from someone's sheet.

- [ ] **Step 5: Create the migration and run the test to verify it passes**

```bash
dotnet ef migrations add AddCharacterArsenal \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterArsenalMigrationTests`
Expected: PASS.

- [ ] **Step 6: Seed the 3 armor slots on sheet creation**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`'s `Create` action, alongside the attribute/skill seeding from Task 3, add:

```csharp
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.CharacterArmorSlots.Add(new CharacterArmorSlot { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Slot = slot });
```

Add `using RuinaRPG.Domain.CharacterSheets;` if not already present (it already is).

- [ ] **Step 7: Write the contracts**

`src/RuinaRPG.Contracts/CharacterSheets/CharacterWeaponResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterWeaponResponse(string Id, string ItemId, string Nome, string? TipoDeDano, int? Alcance, string? Dados, int? Dano, string? Critico, string? Tier, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima);
```

`src/RuinaRPG.Contracts/CharacterSheets/AddCharacterWeaponRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record AddCharacterWeaponRequest(string ItemId);
```

`src/RuinaRPG.Contracts/CharacterSheets/CharacterArmorSlotResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterArmorSlotResponse(string Slot, string? ItemId, string? Nome, string? Categoria, int? Defesa, int? RF, int? RM, string? Penalidade, int? RequisitoVigor, decimal? Peso, int? DurabilidadeAtual, int? DurabilidadeMaxima);
```

`src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterArmorSlotRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record UpdateCharacterArmorSlotRequest(string? ItemId);
```

`src/RuinaRPG.Contracts/CharacterSheets/CharacterShieldResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterShieldResponse(string Id, string ItemId, string Nome, string? Categoria, int? BonusDefesa, string? Penalidade, int? RequisitoVigor, decimal Peso, bool IsEquipped, int DurabilidadeAtual, int DurabilidadeMaxima);
```

`src/RuinaRPG.Contracts/CharacterSheets/AddCharacterShieldRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record AddCharacterShieldRequest(string ItemId);
```

- [ ] **Step 8: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CharacterArsenalControllerTests.cs` — follow the exact fixture/helper pattern from Task 4's `CharacterAttributesControllerTests`, PLUS a helper that creates a GM-owned `Arma` item directly through `POST /api/items` (Catálogo plan) before adding it to a sheet. Cases:

```csharp
[Fact]
public async Task AddWeapon_links_the_item_and_initializes_durability_from_its_maximo()
// POST an Arma via /api/items (DurabilidadeMaxima 20), then POST it onto the sheet's weapons;
// asserts the weapon response's DurabilidadeAtual == 20 and Nome/Dano/etc. match the Catálogo item.

[Fact]
public async Task Equipping_a_second_weapon_unequips_the_first_by_default()
// add 2 weapons, mark the 2nd IsEquipped via PUT; assert GET shows only the 2nd equipped
// (no Ambidestria exception — see plan out-of-scope).

[Fact]
public async Task ArmorSlots_list_returns_all_3_slots_even_when_empty()
// GET .../armor-slots returns Capacete/Superior/Inferior, all with null ItemId initially.

[Fact]
public async Task Update_an_armor_slot_links_the_item_and_initializes_durability()
// PUT .../armor-slots/Capacete with an Armadura item's id; asserts DurabilidadeAtual == the item's max.

[Fact]
public async Task AddShield_and_list_returns_it_with_live_catalog_fields()

[Fact]
public async Task Weapon_actions_by_an_unrelated_jogador_return_403()
```

(Write all 6 tests in full, following the established helper patterns from prior tasks in this plan — `AuthedRequest`, `RegisterGmAndGetTokenAsync`, `RegisterJogadorLinkedToAsync`, `SetUpSheetAsync`, plus a new `CreateArmaItemAsync(string gmToken, int durabilidadeMaxima) : Task<string>` helper posting to `/api/items` with `CreateItemRequest("Arma", "Espada", 1.5m, 50, null, "Espadas", null, "F", "UmaMao", "2D6", 3, "19", 2, "Cortante", null, durabilidadeMaxima, null, null, null, null, null, null, null, null, null, null)`, matching that contract's exact positional shape from the Catálogo plan's Task 6.)

- [ ] **Step 9: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterArsenalControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 10: Write `CharacterArsenalController`**

`src/RuinaRPG.Api/Controllers/CharacterArsenalController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public class CharacterArsenalController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost("weapons")]
    public async Task<ActionResult<CharacterWeaponResponse>> AddWeapon(Guid sheetId, AddCharacterWeaponRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var itemId = Guid.Parse(request.ItemId);
        var item = await db.Set<Arma>().FirstOrDefaultAsync(a => a.Id == itemId);
        if (item is null)
            return BadRequest("Item de arma não encontrado.");

        var weapon = new CharacterWeapon { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = item.DurabilidadeMaxima ?? 0 };
        db.CharacterWeapons.Add(weapon);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToWeaponResponseAsync(weapon));
    }

    [HttpGet("weapons")]
    public async Task<ActionResult<List<CharacterWeaponResponse>>> ListWeapons(Guid sheetId)
    {
        var weapons = await db.CharacterWeapons.Where(w => w.CharacterSheetId == sheetId).ToListAsync();
        var responses = new List<CharacterWeaponResponse>();
        foreach (var weapon in weapons)
            responses.Add(await ToWeaponResponseAsync(weapon));
        return responses;
    }

    [HttpPut("weapons/{id}")]
    public async Task<IActionResult> UpdateWeapon(Guid sheetId, Guid id, [FromBody] bool isEquipped)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapon = await db.CharacterWeapons.FirstOrDefaultAsync(w => w.Id == id && w.CharacterSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        if (isEquipped)
        {
            // Default rule (no Ambidestria exception — see plan out-of-scope): only 1 weapon equipped at a time.
            var currentlyEquipped = await db.CharacterWeapons.Where(w => w.CharacterSheetId == sheetId && w.IsEquipped).ToListAsync();
            foreach (var other in currentlyEquipped)
                other.IsEquipped = false;
        }
        weapon.IsEquipped = isEquipped;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("weapons/{id}")]
    public async Task<IActionResult> DeleteWeapon(Guid sheetId, Guid id)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var weapon = await db.CharacterWeapons.FirstOrDefaultAsync(w => w.Id == id && w.CharacterSheetId == sheetId);
        if (weapon is null)
            return NotFound();

        db.CharacterWeapons.Remove(weapon);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("armor-slots")]
    public async Task<ActionResult<List<CharacterArmorSlotResponse>>> ListArmorSlots(Guid sheetId)
    {
        var slots = await db.CharacterArmorSlots.Where(a => a.CharacterSheetId == sheetId).ToListAsync();
        var responses = new List<CharacterArmorSlotResponse>();
        foreach (var slot in slots)
            responses.Add(await ToArmorSlotResponseAsync(slot));
        return responses;
    }

    [HttpPut("armor-slots/{slot}")]
    public async Task<IActionResult> UpdateArmorSlot(Guid sheetId, ArmorSlotType slot, UpdateCharacterArmorSlotRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var armorSlot = await db.CharacterArmorSlots.SingleAsync(a => a.CharacterSheetId == sheetId && a.Slot == slot);

        if (request.ItemId is null)
        {
            armorSlot.ItemId = null;
            armorSlot.DurabilidadeAtual = null;
        }
        else
        {
            var itemId = Guid.Parse(request.ItemId);
            var item = await db.Set<Armadura>().FirstOrDefaultAsync(a => a.Id == itemId);
            if (item is null)
                return BadRequest("Item de armadura não encontrado.");

            armorSlot.ItemId = itemId;
            armorSlot.DurabilidadeAtual = item.DurabilidadeMaxima ?? 0;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("shields")]
    public async Task<ActionResult<CharacterShieldResponse>> AddShield(Guid sheetId, AddCharacterShieldRequest request)
    {
        var authError = await CheckEditAuthorizationAsync(sheetId);
        if (authError is not null)
            return authError;

        var itemId = Guid.Parse(request.ItemId);
        var item = await db.Set<Escudo>().FirstOrDefaultAsync(e => e.Id == itemId);
        if (item is null)
            return BadRequest("Item de escudo não encontrado.");

        var shield = new CharacterShield { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ItemId = itemId, IsEquipped = false, DurabilidadeAtual = item.DurabilidadeMaxima ?? 0 };
        db.CharacterShields.Add(shield);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToShieldResponseAsync(shield));
    }

    [HttpGet("shields")]
    public async Task<ActionResult<List<CharacterShieldResponse>>> ListShields(Guid sheetId)
    {
        var shields = await db.CharacterShields.Where(s => s.CharacterSheetId == sheetId).ToListAsync();
        var responses = new List<CharacterShieldResponse>();
        foreach (var shield in shields)
            responses.Add(await ToShieldResponseAsync(shield));
        return responses;
    }

    private async Task<ActionResult?> CheckEditAuthorizationAsync(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        return null;
    }

    private async Task<CharacterWeaponResponse> ToWeaponResponseAsync(CharacterWeapon weapon)
    {
        var item = await db.Set<Arma>().SingleAsync(a => a.Id == weapon.ItemId);
        return new CharacterWeaponResponse(weapon.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeDano?.ToString(), item.Alcance, item.Dados, item.Dano, item.Critico, item.Tier?.ToString(), item.Peso, weapon.IsEquipped, weapon.DurabilidadeAtual, item.DurabilidadeMaxima ?? 0);
    }

    private async Task<CharacterArmorSlotResponse> ToArmorSlotResponseAsync(CharacterArmorSlot slot)
    {
        if (slot.ItemId is null)
            return new CharacterArmorSlotResponse(slot.Slot.ToString(), null, null, null, null, null, null, null, null, null, null, null);

        var item = await db.Set<Armadura>().SingleAsync(a => a.Id == slot.ItemId);
        return new CharacterArmorSlotResponse(slot.Slot.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.Defesa, item.RF, item.RM, item.Penalidade, item.RequisitoVigor, item.Peso, slot.DurabilidadeAtual, item.DurabilidadeMaxima);
    }

    private async Task<CharacterShieldResponse> ToShieldResponseAsync(CharacterShield shield)
    {
        var item = await db.Set<Escudo>().SingleAsync(e => e.Id == shield.ItemId);
        return new CharacterShieldResponse(shield.Id.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.BonusDefesa, item.Penalidade, item.RequisitoVigor, item.Peso, shield.IsEquipped, shield.DurabilidadeAtual, item.DurabilidadeMaxima ?? 0);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 11: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterArsenalControllerTests`
Expected: PASS (6/6).

- [ ] **Step 12: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/ArmorSlotType.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterWeapon.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterArmorSlot.cs src/RuinaRPG.Infrastructure/CharacterSheets/CharacterShield.cs src/RuinaRPG.Contracts/CharacterSheets src/RuinaRPG.Api/Controllers/CharacterArsenalController.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration
git commit -m "feat: add weapons, armor slots, and shields arsenal"
```

---

### Task 7: Api — sub-attributes read endpoint (2.b, 3.e)

**Files:**
- Create: `src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `SubAttributeFormulas` (Task 2), `AttributeTotalCalculator` (Task 1), `CharacterAttribute`/`CharacterWeapon`/`CharacterArmorSlot`/`CharacterShield` (Tasks 3, 6).
- Produces: `GET /api/character-sheets/{id}/sub-attributes` → `200` + `SubAttributesResponse(int Iniciativa, int Movimentacao, int EsquivaNatural, int DefesaNatural, int ReducaoFisica, int ReducaoMagica)`. This is the only READ-ONLY tab-2/3 endpoint in this plan — every input (Agilidade/Vigor Total from Task 4, Peso Total Carregado from Task 6's equipped+carried items, Cobertura from the sheet root, Escudo bonus from an equipped shield) is derived live, nothing new is persisted.

- [ ] **Step 1: Write the response contract**

`src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record SubAttributesResponse(int Iniciativa, int Movimentacao, int EsquivaNatural, int DefesaNatural, int ReducaoFisica, int ReducaoMagica);
```

- [ ] **Step 2: Write the failing test**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`:

```csharp
[Fact]
public async Task SubAttributes_computes_from_attributes_and_arsenal()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmSub1", "sheetsub1@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerSub1", "sheetplayersub1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha SubAttr");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    // Agilidade Gasto 4, no bônus/maestria/artefato → Total 4. Vigor same → Total 4.
    await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Agilidade", playerToken,
        new UpdateCharacterAttributeRequest(4, 0, false)));
    await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/attributes/Vigor", playerToken,
        new UpdateCharacterAttributeRequest(4, 0, false)));

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/sub-attributes", playerToken));

    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var body = await response.Content.ReadFromJsonAsync<SubAttributesResponse>();
    body!.Movimentacao.Should().Be(8); // (4*2) + 0 artefato - 0 sobrepeso (nothing carried yet)
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SubAttributes_computes_from_attributes_and_arsenal`
Expected: FAIL — the route doesn't exist yet.

- [ ] **Step 4: Add the `SubAttributes` action**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, add:

```csharp
[HttpGet("api/character-sheets/{id}/sub-attributes")]
public async Task<ActionResult<SubAttributesResponse>> SubAttributes(Guid id)
{
    var sheet = await db.CharacterSheets.FindAsync(id);
    if (sheet is null)
        return NotFound();

    var attributes = await db.CharacterAttributes.Where(a => a.CharacterSheetId == id).ToListAsync();
    int TotalOf(RuinaRPG.Domain.CharacterSheets.Atributo atributo)
    {
        var attribute = attributes.Single(a => a.Atributo == atributo);
        return AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria, artefatos: 0);
    }

    var agilidade = TotalOf(RuinaRPG.Domain.CharacterSheets.Atributo.Agilidade);
    var vigor = TotalOf(RuinaRPG.Domain.CharacterSheets.Atributo.Vigor);
    var forca = TotalOf(RuinaRPG.Domain.CharacterSheets.Atributo.Forca);

    var weapons = await db.CharacterWeapons.Where(w => w.CharacterSheetId == id).Join(db.Items, w => w.ItemId, i => i.Id, (w, i) => new { w.IsEquipped, i.Peso }).ToListAsync();
    var armorSlots = await db.CharacterArmorSlots.Where(a => a.CharacterSheetId == id && a.ItemId != null).Join(db.Items, a => a.ItemId!.Value, i => i.Id, (a, i) => i.Peso).ToListAsync();
    var shields = await db.CharacterShields.Where(s => s.CharacterSheetId == id).Join(db.Items, s => s.ItemId, i => i.Id, (s, i) => i.Peso).ToListAsync();
    var pesoTotalCarregado = weapons.Sum(w => w.Peso) + armorSlots.Sum() + shields.Sum();

    var equippedShield = await db.CharacterShields
        .Where(s => s.CharacterSheetId == id && s.IsEquipped)
        .Join(db.Set<RuinaRPG.Infrastructure.Items.Escudo>(), s => s.ItemId, i => i.Id, (s, i) => i.BonusDefesa)
        .FirstOrDefaultAsync();
    var coberturaBonus = sheet.Cobertura switch { RuinaRPG.Domain.CharacterSheets.Cobertura.Parcial => 5, RuinaRPG.Domain.CharacterSheets.Cobertura.Completa => 10, _ => 0 };

    return new SubAttributesResponse(
        Iniciativa: SubAttributeFormulas.Iniciativa(agilidade, brutoProntidao: 0, artefatoOuItem: 0),
        Movimentacao: SubAttributeFormulas.Movimentacao(agilidade, artefato: 0, (int)pesoTotalCarregado, forca, vigor),
        EsquivaNatural: SubAttributeFormulas.EsquivaNatural(agilidade, brutoReflexos: 0, artefatos: 0, penalidadeArmadura: 0),
        DefesaNatural: SubAttributeFormulas.DefesaNatural(vigor, brutoFortitude: 0, escudo: equippedShield ?? 0, artefatos: 0, cobertura: coberturaBonus),
        ReducaoFisica: SubAttributeFormulas.ReducaoFisica(artefato: 0, armadura: 0),
        ReducaoMagica: SubAttributeFormulas.ReducaoMagica(artefato: 0, armaduraMagica: 0));
}
```

**"Bruto [Perícia]" terms are hardcoded to 0 in this task** (Prontidão, Reflexos, Fortitude) — "Bruto" means a Perícia's Modificador alone (Sistema Básico §2), computable from `CharacterSkill` (Task 4) once the exact Perícia→sub-attribute mapping is confirmed against the doc; the three sub-attributes that need it (Iniciativa, Esquiva Natural, Defesa Natural) are Formulas.md-correct in shape but read 0 for that one term until a follow-up wires in the real skill lookup — flagged explicitly rather than silently guessed at, since `Formulas.md` names the term but the Ficha de Personagem doc doesn't spell out which of the 38 Perícias is "Prontidão" (it isn't in the R0001 2.d fixed list at all — likely an older term for one of the listed skills, e.g. `Percepcao` — needs a product decision, not a guess baked into a formula).

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter SubAttributes_computes_from_attributes_and_arsenal`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/SubAttributesResponse.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs
git commit -m "feat: add sub-attributes read endpoint"
```

---

### Task 8: Client — Atributos & Perícias and Combate tabs

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`

**Interfaces:**
- Consumes: every Contracts type from Tasks 4-7, the authenticated `HttpClient`.
- Produces: two new sections on the existing `/fichas/{id}` page (Atributos & Perícias, Combate), alongside the Informações Básicas section the Fundação plan already built. No later task depends on this file.

- [ ] **Step 1: Add the two tabs**

In `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`, after the existing Informações Básicas `<EditForm>` and before the closing of the file's markup, add:

```razor
<h2>Atributos & Perícias</h2>
<table>
    <thead><tr><th>Atributo</th><th>Gasto</th><th>Bônus</th><th>Maestria</th><th>Total</th></tr></thead>
    <tbody>
        @foreach (var attribute in _attributes)
        {
            <tr>
                <td>@attribute.Atributo</td>
                <td><input type="number" value="@attribute.Gasto" @onchange="@(e => UpdateAttributeAsync(attribute.Atributo, "Gasto", e.Value))" /></td>
                <td><input type="number" value="@attribute.Bonus" @onchange="@(e => UpdateAttributeAsync(attribute.Atributo, "Bonus", e.Value))" /></td>
                <td><input type="checkbox" checked="@attribute.TemMaestria" @onchange="@(e => UpdateAttributeAsync(attribute.Atributo, "TemMaestria", e.Value))" /></td>
                <td>@attribute.Total</td>
            </tr>
        }
    </tbody>
</table>

<table>
    <thead><tr><th>Perícia</th><th>Gasto</th><th>Modificador</th></tr></thead>
    <tbody>
        @foreach (var skill in _skills)
        {
            <tr>
                <td>@skill.Pericia</td>
                <td><input type="number" value="@skill.Gasto" @onchange="@(e => UpdateSkillAsync(skill.Pericia, e.Value))" /></td>
                <td>@skill.Modificador</td>
            </tr>
        }
    </tbody>
</table>

<h3>Afinidades</h3>
<ul>
    @foreach (var affinity in _affinities)
    {
        <li>@affinity.Elemento / @affinity.SubElemento — @affinity.CaminhoNome (@affinity.Experiencia) <button @onclick="@(() => DeleteAffinityAsync(affinity.Id))">Remover</button></li>
    }
</ul>

<h2>Combate</h2>
<h3>Armas</h3>
<ul>
    @foreach (var weapon in _weapons)
    {
        <li>@weapon.Nome — Durabilidade @weapon.DurabilidadeAtual/@weapon.DurabilidadeMaxima — @(weapon.IsEquipped ? "Equipada" : "")
            <button @onclick="@(() => EquipWeaponAsync(weapon.Id, !weapon.IsEquipped))">@(weapon.IsEquipped ? "Desequipar" : "Equipar")</button>
        </li>
    }
</ul>

<h3>Armaduras</h3>
<ul>
    @foreach (var slot in _armorSlots)
    {
        <li>@slot.Slot: @(slot.Nome ?? "—") @(slot.DurabilidadeAtual is not null ? $"({slot.DurabilidadeAtual}/{slot.DurabilidadeMaximo})" : "")</li>
    }
</ul>

<h3>Escudos</h3>
<ul>
    @foreach (var shield in _shields)
    {
        <li>@shield.Nome — Durabilidade @shield.DurabilidadeAtual/@shield.DurabilidadeMaximo</li>
    }
</ul>

@if (_subAttributes is not null)
{
    <h3>Sub-Atributos</h3>
    <p>Iniciativa: @_subAttributes.Iniciativa</p>
    <p>Movimentação: @_subAttributes.Movimentacao</p>
    <p>Esquiva Natural: @_subAttributes.EsquivaNatural</p>
    <p>Defesa Natural: @_subAttributes.DefesaNatural</p>
    <p>Redução Física: @_subAttributes.ReducaoFisica</p>
    <p>Redução Mágica: @_subAttributes.ReducaoMagica</p>
}
```

Extend the `@code` block:

```csharp
    private List<CharacterAttributeResponse> _attributes = new();
    private List<CharacterSkillResponse> _skills = new();
    private List<CharacterAffinityResponse> _affinities = new();
    private List<CharacterWeaponResponse> _weapons = new();
    private List<CharacterArmorSlotResponse> _armorSlots = new();
    private List<CharacterShieldResponse> _shields = new();
    private SubAttributesResponse? _subAttributes;

    private async Task LoadTabs2And3Async()
    {
        _attributes = await Http.GetFromJsonAsync<List<CharacterAttributeResponse>>($"character-sheets/{SheetId}/attributes") ?? new();
        _skills = await Http.GetFromJsonAsync<List<CharacterSkillResponse>>($"character-sheets/{SheetId}/skills") ?? new();
        _affinities = await Http.GetFromJsonAsync<List<CharacterAffinityResponse>>($"character-sheets/{SheetId}/affinities") ?? new();
        _weapons = await Http.GetFromJsonAsync<List<CharacterWeaponResponse>>($"character-sheets/{SheetId}/weapons") ?? new();
        _armorSlots = await Http.GetFromJsonAsync<List<CharacterArmorSlotResponse>>($"character-sheets/{SheetId}/armor-slots") ?? new();
        _shields = await Http.GetFromJsonAsync<List<CharacterShieldResponse>>($"character-sheets/{SheetId}/shields") ?? new();
        _subAttributes = await Http.GetFromJsonAsync<SubAttributesResponse>($"character-sheets/{SheetId}/sub-attributes");
    }

    private async Task UpdateAttributeAsync(string atributo, string field, object? value)
    {
        var current = _attributes.Single(a => a.Atributo == atributo);
        var gasto = field == "Gasto" ? int.Parse(value?.ToString() ?? "0") : current.Gasto;
        var bonus = field == "Bonus" ? int.Parse(value?.ToString() ?? "0") : current.Bonus;
        var temMaestria = field == "TemMaestria" ? (bool)(value ?? false) : current.TemMaestria;

        await Http.PutAsJsonAsync($"character-sheets/{SheetId}/attributes/{atributo}", new UpdateCharacterAttributeRequest(gasto, bonus, temMaestria));
        await LoadTabs2And3Async();
    }

    private async Task UpdateSkillAsync(string pericia, object? value)
    {
        var gasto = int.Parse(value?.ToString() ?? "0");
        await Http.PutAsJsonAsync($"character-sheets/{SheetId}/skills/{pericia}", new UpdateCharacterSkillRequest(gasto, null));
        await LoadTabs2And3Async();
    }

    private async Task DeleteAffinityAsync(string id)
    {
        await Http.DeleteAsync($"character-sheets/{SheetId}/affinities/{id}");
        await LoadTabs2And3Async();
    }

    private async Task EquipWeaponAsync(string weaponId, bool isEquipped)
    {
        await Http.PutAsJsonAsync($"character-sheets/{SheetId}/weapons/{weaponId}", isEquipped);
        await LoadTabs2And3Async();
    }
```

Change `OnInitializedAsync`'s `Task.WhenAll` call to also include `LoadTabs2And3Async()`:

```csharp
protected override Task OnInitializedAsync() => Task.WhenAll(LoadSheetAsync(), LoadLevelUpNoticeAsync(), LoadTabs2And3Async());
```

Add `@using RuinaRPG.Contracts.CharacterSheets` is already present (from the Fundação plan) — no new using needed since all these response types share that namespace.

`CharacterArmorSlotResponse.DurabilidadeMaximo` in the markup above is a typo guard: the actual contract property (Task 6) is named `DurabilidadeMaximo` — verify against Task 6's `CharacterArmorSlotResponse` record before wiring this up; if it differs, use the exact property name from that record instead of guessing.

- [ ] **Step 2: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "feat: add atributos, pericias, and combate tabs to the character sheet page"
```

---

### Task 9: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files — this task only exercises what Tasks 1-8 built.

**Interfaces:**
- Consumes: the full stack, including everything this plan added.
- Produces: nothing new — this is the plan's acceptance test.

- [ ] **Step 1: Boot the full stack**

```bash
cp -n .env.example .env
make deploy
```

Expected: all 3 containers come up; migrations (including `AddCharacterAttributesAndSkills`, `AddCharacterAffinities`, `AddCharacterArsenal`) apply automatically in Development.

- [ ] **Step 2: Set up a GM, player, campaign, and sheet, through nginx**

(Same setup sequence as the Ficha de Personagem — Fundação plan's smoke test — register GM, invite/register player, create campaign, add member, create sheet — culminating in a `SHEET_ID` variable.)

- [ ] **Step 3: Update an attribute and confirm Total, through nginx**

```bash
curl -sf -X PUT "http://localhost/api/character-sheets/$SHEET_ID/attributes/Forca" \
  -H "Authorization: Bearer $PLAYER_TOKEN" -H "Content-Type: application/json" \
  -d '{"gasto":5,"bonus":2,"temMaestria":false}'

curl -sf "http://localhost/api/character-sheets/$SHEET_ID/attributes" -H "Authorization: Bearer $PLAYER_TOKEN"
```

Expected: PUT returns 204; GET shows Força with `"total":6`.

- [ ] **Step 4: Confirm sub-attributes respond, through nginx**

```bash
curl -sf "http://localhost/api/character-sheets/$SHEET_ID/sub-attributes" -H "Authorization: Bearer $PLAYER_TOKEN"
```

Expected: HTTP 200 with all 6 sub-attribute fields present.

- [ ] **Step 5: Tear down**

```bash
make down
```

- [ ] **Step 6: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify atributos and combate flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- Ambidestria's "2 weapons equipped" exception (3.a) — Ambidestria is a Característica, and Características (5.d) aren't built until the next Personagem plan; this plan's `UpdateWeapon` always enforces the single-equipped-weapon default.
- Eficiência Elemental, Dano Elemental (2.b), Dano de Briga (2.b), Efeito de Batalha (3.d) — all explicitly marked "pendente" / "campo previsto porém não implementado" in the spec itself; not built here or anywhere until the underlying rule exists.
- The "Bruto [Perícia]" terms feeding Iniciativa/Esquiva Natural/Defesa Natural (Prontidão, Reflexos, Fortitude) — hardcoded to 0 in Task 7, flagged explicitly as needing a product decision on the Perícia name mapping, not silently guessed.
- Vitalidade/Foco máximo display (deferred from the Fundação plan) — Vigor/Astúcia Total now exist (this plan), so the *inputs* are available, but wiring the computed máximo into the sheet response is not done here; track as an explicit small follow-up before considering tab 1's Recursos section fully finished. Adrenalina's máximo still waits on Artefatos (tab 5, next plan).
