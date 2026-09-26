# Afinidades com duas Essências Básicas — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace "Elemento + Caminho + hand-picked Sub-Elemento" in the Afinidades table with two Essências Básicas whose Matriz Elemental intersection is the Sub-Elemento, and drop every Vocação restriction on Afinidades.

**Architecture:** One domain table (`MatrizElemental`) drives the server validation, the data-conversion migration and the client's option lists. The server derives and stores `SubElemento`, recomputing it only when an Essência changes (so unconvertible legacy values survive until edited). Contracts change in two steps (additive in Task 3, final shape in Task 4) so every task builds; `CaminhoNome` is dropped in a second migration in Task 5.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core 8/Npgsql (enums stored as integers), Blazor WebAssembly + MudBlazor 9.9.0, xUnit + FluentAssertions, bUnit 2.9.0, Testcontainers PostgreSQL.

**Spec:** `docs/superpowers/specs/2026-09-26-afinidades-duas-essencias-design.md`

## Global Constraints

- `dotnet build` must finish with **0 warnings, 0 errors**. TDD mandatory: failing test first, watched failing for the right reason.
- `dotnet test` needs Docker. Before any integration run check `df -h /` (stop and report if < 2 GB free). Known flake: `HttpClient.Timeout of 100 seconds elapsing` in random classes — re-run just the failing classes; never try to fix it here.
- `EssenciaBasica` integer values are fixed: `Ar=0, Agua=1, Fogo=2, Terra=3, Alma=4, Vida=5, Mundano=6`. `Elemento`: `Ar=0, Agua=1, Fogo=2, Terra=3`. `SubElemento`: `Gelo=0, Raio=1, Prever=2, Ecomancia=3, Alma=4, Flora=5, Purificar=6, Hemomancia=7, Ferro=8, Curar=9, Necromancia=10, Vida=11, Aprimorar=12, Invocacao=13` — never reorder or remove members.
- Intersections (element pairs symmetric): Ar+Agua=Gelo, Ar+Fogo=Raio, Agua+Terra=Flora, Fogo+Terra=Ferro, Ar+Alma=Prever, Agua+Alma=Purificar, Fogo+Vida=Curar, Terra+Vida=Aprimorar, Ar+Mundano=Ecomancia, Agua+Mundano=Hemomancia, Fogo+Mundano=Necromancia, Terra+Mundano=Invocacao. Everything else: none.
- 400 messages, verbatim: `Escolha a Essência Básica 1 antes da 2.` · `Essas duas Essências não se cruzam na Matriz Elemental.` · `Já existe uma linha de Afinidade com esse Sub-Elemento.` · `Elemento desconhecido.` · `Essência Básica desconhecida.`
- No Vocação restriction on Afinidades or on the 1.a "Afinidade" field; the existing `Alma e Vida são Caminhos, não Afinidades.` rule on 1.a stays.
- User-facing text in Brazilian Portuguese; code comments follow the surrounding file's language. Changelog untouched.
- Commits end with:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01XZXjdTYm1rp27c2J8Pz92M
  ```

## Review Focus

- A legacy row whose Sub-Elemento doesn't match the Matriz, edited only in its numbers/Experiência — must keep its Sub-Elemento (Task 3 test `Update_changing_only_values_keeps_a_legacy_SubElemento`).
- The migration run on real legacy data: symmetric element pairs (Água + Gelo → Essência 2 = Ar), a Caminho-only row gaining its Sub-Elemento, free-text Caminho and legacy Alma/Vida Sub-Elementos left alone (Task 2 migration test).
- A player changing Essência 1 on a row that has an Essência 2 now incompatible — the client clears Essência 2 in the same save instead of getting a 400 (Task 4 client code + bUnit test on the option list).
- Two rows that would end up with the same Sub-Elemento after a change — 400, while an unchanged pre-existing duplicate (possible after the migration) resubmitted untouched is not rejected (Task 3 tests).
- A sheet with no Vocação (or Campeão/Caçador) — must now pick any Elemento/Essência and any 1.a Afinidade (Task 3 tests).

---

### Task 1: Domain — `EssenciaBasica` and `MatrizElemental`

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/EssenciaBasica.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/MatrizElemental.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/MatrizElementalTests.cs`

**Interfaces:**
- Produces: `enum EssenciaBasica { Ar, Agua, Fogo, Terra, Alma, Vida, Mundano }`; `static class MatrizElemental` with `SubElemento? Intersecao(Elemento essencia1, EssenciaBasica essencia2)`, `IReadOnlyList<EssenciaBasica> OpcoesSegundaEssencia(Elemento essencia1)`, `EssenciaBasica? SegundaEssenciaQueProduz(Elemento essencia1, SubElemento subElemento)`.

- [ ] **Step 1: Failing tests** — `tests/RuinaRPG.Tests.Unit/CharacterSheets/MatrizElementalTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class MatrizElementalTests
{
    [Theory]
    [InlineData(Elemento.Ar, EssenciaBasica.Agua, SubElemento.Gelo)]
    [InlineData(Elemento.Agua, EssenciaBasica.Ar, SubElemento.Gelo)]
    [InlineData(Elemento.Ar, EssenciaBasica.Fogo, SubElemento.Raio)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Ar, SubElemento.Raio)]
    [InlineData(Elemento.Agua, EssenciaBasica.Terra, SubElemento.Flora)]
    [InlineData(Elemento.Terra, EssenciaBasica.Agua, SubElemento.Flora)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Terra, SubElemento.Ferro)]
    [InlineData(Elemento.Terra, EssenciaBasica.Fogo, SubElemento.Ferro)]
    [InlineData(Elemento.Ar, EssenciaBasica.Alma, SubElemento.Prever)]
    [InlineData(Elemento.Agua, EssenciaBasica.Alma, SubElemento.Purificar)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Vida, SubElemento.Curar)]
    [InlineData(Elemento.Terra, EssenciaBasica.Vida, SubElemento.Aprimorar)]
    [InlineData(Elemento.Ar, EssenciaBasica.Mundano, SubElemento.Ecomancia)]
    [InlineData(Elemento.Agua, EssenciaBasica.Mundano, SubElemento.Hemomancia)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Mundano, SubElemento.Necromancia)]
    [InlineData(Elemento.Terra, EssenciaBasica.Mundano, SubElemento.Invocacao)]
    public void Intersecao_follows_the_Matriz_Elemental(Elemento e1, EssenciaBasica e2, SubElemento esperado)
    {
        MatrizElemental.Intersecao(e1, e2).Should().Be(esperado);
    }

    [Theory]
    [InlineData(Elemento.Ar, EssenciaBasica.Terra)]
    [InlineData(Elemento.Terra, EssenciaBasica.Ar)]
    [InlineData(Elemento.Agua, EssenciaBasica.Fogo)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Agua)]
    [InlineData(Elemento.Ar, EssenciaBasica.Ar)]
    [InlineData(Elemento.Agua, EssenciaBasica.Agua)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Fogo)]
    [InlineData(Elemento.Terra, EssenciaBasica.Terra)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Alma)]
    [InlineData(Elemento.Terra, EssenciaBasica.Alma)]
    [InlineData(Elemento.Ar, EssenciaBasica.Vida)]
    [InlineData(Elemento.Agua, EssenciaBasica.Vida)]
    public void Intersecao_is_null_when_the_essencias_dont_cross(Elemento e1, EssenciaBasica e2)
    {
        MatrizElemental.Intersecao(e1, e2).Should().BeNull();
    }

    [Theory]
    [InlineData(Elemento.Ar, new[] { EssenciaBasica.Agua, EssenciaBasica.Fogo, EssenciaBasica.Alma, EssenciaBasica.Mundano })]
    [InlineData(Elemento.Agua, new[] { EssenciaBasica.Ar, EssenciaBasica.Terra, EssenciaBasica.Alma, EssenciaBasica.Mundano })]
    [InlineData(Elemento.Fogo, new[] { EssenciaBasica.Ar, EssenciaBasica.Terra, EssenciaBasica.Vida, EssenciaBasica.Mundano })]
    [InlineData(Elemento.Terra, new[] { EssenciaBasica.Agua, EssenciaBasica.Fogo, EssenciaBasica.Vida, EssenciaBasica.Mundano })]
    public void OpcoesSegundaEssencia_lists_exactly_the_crossing_essencias_in_enum_order(Elemento e1, EssenciaBasica[] esperado)
    {
        MatrizElemental.OpcoesSegundaEssencia(e1).Should().Equal(esperado);
    }

    [Theory]
    [InlineData(Elemento.Ar, SubElemento.Gelo, EssenciaBasica.Agua)]
    [InlineData(Elemento.Agua, SubElemento.Gelo, EssenciaBasica.Ar)]
    [InlineData(Elemento.Fogo, SubElemento.Curar, EssenciaBasica.Vida)]
    [InlineData(Elemento.Ar, SubElemento.Ecomancia, EssenciaBasica.Mundano)]
    public void SegundaEssenciaQueProduz_inverts_the_intersection(Elemento e1, SubElemento sub, EssenciaBasica esperado)
    {
        MatrizElemental.SegundaEssenciaQueProduz(e1, sub).Should().Be(esperado);
    }

    [Theory]
    [InlineData(Elemento.Ar, SubElemento.Curar)]
    [InlineData(Elemento.Ar, SubElemento.Alma)]
    [InlineData(Elemento.Fogo, SubElemento.Vida)]
    [InlineData(Elemento.Terra, SubElemento.Gelo)]
    public void SegundaEssenciaQueProduz_is_null_when_the_SubElemento_cant_come_from_that_Elemento(Elemento e1, SubElemento sub)
    {
        MatrizElemental.SegundaEssenciaQueProduz(e1, sub).Should().BeNull();
    }

    [Fact]
    public void EssenciaBasica_integer_values_are_fixed_and_match_Elemento()
    {
        ((int)EssenciaBasica.Ar).Should().Be((int)Elemento.Ar);
        ((int)EssenciaBasica.Agua).Should().Be((int)Elemento.Agua);
        ((int)EssenciaBasica.Fogo).Should().Be((int)Elemento.Fogo);
        ((int)EssenciaBasica.Terra).Should().Be((int)Elemento.Terra);
        ((int)EssenciaBasica.Alma).Should().Be(4);
        ((int)EssenciaBasica.Vida).Should().Be(5);
        ((int)EssenciaBasica.Mundano).Should().Be(6);
    }
}
```

- [ ] **Step 2: Run, verify failure** — `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~MatrizElementalTests` → build error: `EssenciaBasica`/`MatrizElemental` not found.

- [ ] **Step 3: Implement**

`src/RuinaRPG.Domain/CharacterSheets/EssenciaBasica.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Segunda Essência Básica de uma linha de Afinidade (Ficha de Personagem 2.c): os 4 Elementos mais
/// os 3 Caminhos da Matriz Elemental. Gravado como inteiro — os 4 primeiros valores coincidem com
/// Elemento; nunca reordenar nem remover membros.
/// </summary>
public enum EssenciaBasica
{
    Ar = 0,
    Agua = 1,
    Fogo = 2,
    Terra = 3,
    Alma = 4,
    Vida = 5,
    Mundano = 6,
}
```

`src/RuinaRPG.Domain/CharacterSheets/MatrizElemental.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// A Matriz Elemental ("Matriz_Elemental.png"): o Sub-Elemento de uma linha de Afinidade é a
/// interseção das suas duas Essências Básicas. Fonte única da regra — usada pela validação da API,
/// pela migration que converteu as linhas antigas e pelas opções do cliente.
/// </summary>
public static class MatrizElemental
{
    // Pares de Elementos aparecem uma vez; Intersecao trata a simetria (Água + Ar = Gelo).
    private static readonly (Elemento Essencia1, EssenciaBasica Essencia2, SubElemento SubElemento)[] Tabela =
    [
        (Elemento.Ar, EssenciaBasica.Agua, SubElemento.Gelo),
        (Elemento.Ar, EssenciaBasica.Fogo, SubElemento.Raio),
        (Elemento.Agua, EssenciaBasica.Terra, SubElemento.Flora),
        (Elemento.Fogo, EssenciaBasica.Terra, SubElemento.Ferro),
        (Elemento.Ar, EssenciaBasica.Alma, SubElemento.Prever),
        (Elemento.Agua, EssenciaBasica.Alma, SubElemento.Purificar),
        (Elemento.Fogo, EssenciaBasica.Vida, SubElemento.Curar),
        (Elemento.Terra, EssenciaBasica.Vida, SubElemento.Aprimorar),
        (Elemento.Ar, EssenciaBasica.Mundano, SubElemento.Ecomancia),
        (Elemento.Agua, EssenciaBasica.Mundano, SubElemento.Hemomancia),
        (Elemento.Fogo, EssenciaBasica.Mundano, SubElemento.Necromancia),
        (Elemento.Terra, EssenciaBasica.Mundano, SubElemento.Invocacao),
    ];

    public static SubElemento? Intersecao(Elemento essencia1, EssenciaBasica essencia2)
    {
        foreach (var (e1, e2, sub) in Tabela)
        {
            if (e1 == essencia1 && e2 == essencia2)
                return sub;
            // Simetria dos pares de Elementos: (Agua, Ar) acha a linha (Ar, Agua).
            if (e2 <= EssenciaBasica.Terra && (int)e2 == (int)essencia1 && (int)e1 == (int)essencia2)
                return sub;
        }
        return null;
    }

    public static IReadOnlyList<EssenciaBasica> OpcoesSegundaEssencia(Elemento essencia1) =>
        Enum.GetValues<EssenciaBasica>().Where(e2 => Intersecao(essencia1, e2) is not null).ToList();

    public static EssenciaBasica? SegundaEssenciaQueProduz(Elemento essencia1, SubElemento subElemento) =>
        Enum.GetValues<EssenciaBasica>().Cast<EssenciaBasica?>().FirstOrDefault(e2 => Intersecao(essencia1, e2!.Value) == subElemento);
}
```

- [ ] **Step 4: Run, verify pass** — same command → all pass; `dotnet build` 0/0.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/EssenciaBasica.cs src/RuinaRPG.Domain/CharacterSheets/MatrizElemental.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/MatrizElementalTests.cs
git commit -m "feat: MatrizElemental e EssenciaBasica (interseção das duas Essências)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01XZXjdTYm1rp27c2J8Pz92M"
```

---

### Task 2: Schema — new columns + conversion migration

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterAffinity.cs`, `src/RuinaRPG.Infrastructure/NpcSheets/NpcAffinity.cs`
- Create: migration `AddAfinidadeSegundaEssencia` (generated, then hand-edited `Up`/`Down`)
- Modify: `src/RuinaRPG.Api/Controllers/CampaignGrantsController.cs` (NpcAffinities deep copy, ~line 186)
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/AfinidadeSegundaEssenciaMigrationTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignGrantsControllerTests.cs` (existing `Grant_from_an_existing_Npc_deep_copies_Affinity_ElementoValor_and_SubElementoValor`, ~line 256)

**Interfaces:**
- Consumes: `EssenciaBasica`, `MatrizElemental.Intersecao` (Task 1).
- Produces: `CharacterAffinity.SegundaEssencia` / `NpcAffinity.SegundaEssencia` (`EssenciaBasica?`), `.SegundaEssenciaValor` (`int?`). `CaminhoNome` stays in this task (dropped in Task 5).

- [ ] **Step 1: Failing migration test** — `tests/RuinaRPG.Tests.Integration/Persistence/AfinidadeSegundaEssenciaMigrationTests.cs`. Each `PostgresFixture` instance is its own empty database, so this class can migrate to an older migration safely.

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class AfinidadeSegundaEssenciaMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public AfinidadeSegundaEssenciaMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_converts_legacy_rows_to_the_two_essencias()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var anterior = db.Database.GetMigrations().Single(m => m.EndsWith("_AddSheetHistoria"));
        await migrator.MigrateAsync(anterior);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@essencias.com", Email = "gm@essencias.com", Nickname = "EssGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "p@essencias.com", Email = "p@essencias.com", Nickname = "EssPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        var npc = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        db.NpcSheets.Add(npc);
        await db.SaveChangesAsync();

        // Legacy rows via raw SQL — the entity already has the new columns, which don't exist yet at this migration.
        var rows = new (Guid Id, int? Elemento, int? SubElemento, string? Caminho)[]
        {
            (Guid.NewGuid(), 0, 0, null),                  // Ar + Gelo            → Essência 2 = Agua
            (Guid.NewGuid(), 1, 0, null),                  // Agua + Gelo          → Essência 2 = Ar (symmetric pair)
            (Guid.NewGuid(), 2, 9, "Vida"),                // Fogo + Curar + Vida  → Essência 2 = Vida
            (Guid.NewGuid(), 2, null, "Vida"),             // Fogo + Caminho Vida  → Essência 2 = Vida, Sub = Curar
            (Guid.NewGuid(), 0, 9, null),                  // Ar + Curar (bad)     → untouched, Sub kept
            (Guid.NewGuid(), 3, null, "Caminho da Fênix"), // free text            → untouched
            (Guid.NewGuid(), 0, 4, null),                  // Ar + legacy Alma     → untouched, Sub kept
            (Guid.NewGuid(), 0, null, "Vida"),             // Ar + Vida (no cross) → untouched
        };
        foreach (var r in rows)
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO \"CharacterAffinities\" (\"Id\", \"CharacterSheetId\", \"Elemento\", \"SubElemento\", \"CaminhoNome\") VALUES ({r.Id}, {sheet.Id}, {r.Elemento}, {r.SubElemento}, {r.Caminho})");
        var npcRow = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"NpcAffinities\" (\"Id\", \"NpcSheetId\", \"Elemento\", \"SubElemento\", \"CaminhoNome\") VALUES ({npcRow}, {npc.Id}, {3}, {(int?)null}, {"Mundano"})");

        await migrator.MigrateAsync();

        var after = await db.CharacterAffinities.AsNoTracking().ToDictionaryAsync(a => a.Id);
        after[rows[0].Id].SegundaEssencia.Should().Be(EssenciaBasica.Agua);
        after[rows[1].Id].SegundaEssencia.Should().Be(EssenciaBasica.Ar);
        after[rows[2].Id].SegundaEssencia.Should().Be(EssenciaBasica.Vida);
        after[rows[3].Id].SegundaEssencia.Should().Be(EssenciaBasica.Vida);
        after[rows[3].Id].SubElemento.Should().Be(SubElemento.Curar);
        after[rows[4].Id].SegundaEssencia.Should().BeNull();
        after[rows[4].Id].SubElemento.Should().Be(SubElemento.Curar);
        after[rows[5].Id].SegundaEssencia.Should().BeNull();
        after[rows[5].Id].SubElemento.Should().BeNull();
        after[rows[6].Id].SegundaEssencia.Should().BeNull();
        after[rows[6].Id].SubElemento.Should().Be(SubElemento.Alma);
        after[rows[7].Id].SegundaEssencia.Should().BeNull();
        after[rows[7].Id].SubElemento.Should().BeNull();

        var npcAfter = await db.NpcAffinities.AsNoTracking().SingleAsync(a => a.Id == npcRow);
        npcAfter.SegundaEssencia.Should().Be(EssenciaBasica.Mundano);
        npcAfter.SubElemento.Should().Be(SubElemento.Invocacao);
    }
}
```

(Copy any extra required `ApplicationUser`/`Campaign`/`NpcSheet` fields from `CharacterAffinityMigrationTests` / `NpcChildTableMigrationTests` if the inserts complain.)

- [ ] **Step 2: Run, verify failure** — `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~AfinidadeSegundaEssenciaMigrationTests` → build error (`SegundaEssencia` not on the entities).

- [ ] **Step 3: Entities** — in both `CharacterAffinity.cs` and `NpcAffinity.cs`, after `SubElementoValor`:

```csharp
    public EssenciaBasica? SegundaEssencia { get; set; }
    public int? SegundaEssenciaValor { get; set; }
```

- [ ] **Step 4: Migration**

```bash
dotnet ef migrations add AddAfinidadeSegundaEssencia --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

The generated `Up` has four `AddColumn`s (`SegundaEssencia` int NULL, `SegundaEssenciaValor` int NULL on both tables) — keep them, and append the conversion after them. Add `using RuinaRPG.Domain.CharacterSheets;` and this at the end of `Up`:

```csharp
            // Converte as linhas antigas (Elemento + Caminho + Sub-Elemento escolhido) pra duas
            // Essências. Gerado a partir de MatrizElemental — ver
            // docs/superpowers/specs/2026-09-26-afinidades-duas-essencias-design.md, "Conversion".
            foreach (var table in new[] { "CharacterAffinities", "NpcAffinities" })
            {
                foreach (var e1 in Enum.GetValues<Elemento>())
                foreach (var e2 in Enum.GetValues<EssenciaBasica>())
                {
                    if (MatrizElemental.Intersecao(e1, e2) is not { } sub)
                        continue;

                    // 1) O Sub-Elemento gravado sai deste par → a Essência 2 é o que o reproduz.
                    migrationBuilder.Sql(
                        $"UPDATE \"{table}\" SET \"SegundaEssencia\" = {(int)e2} " +
                        $"WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = {(int)e1} AND \"SubElemento\" = {(int)sub};");

                    // 2) Sem Sub-Elemento, mas com um Caminho que cruza com o Elemento → vira a Essência 2 e gera o Sub-Elemento.
                    if (e2 >= EssenciaBasica.Alma)
                        migrationBuilder.Sql(
                            $"UPDATE \"{table}\" SET \"SegundaEssencia\" = {(int)e2}, \"SubElemento\" = {(int)sub} " +
                            $"WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = {(int)e1} AND \"CaminhoNome\" = '{e2}';");
                }
            }
```

`Down` stays as generated (drops the four columns). Confirm the generated `Up` touches nothing else.

- [ ] **Step 5: Deep copy** — in `CampaignGrantsController.DeepCopyNpcAsync`, the `db.NpcAffinities.Add(new() { … })` initializer gains `SegundaEssencia = a.SegundaEssencia, SegundaEssenciaValor = a.SegundaEssenciaValor`. The response/contract doesn't expose these yet (Task 3), so pin it at the entity level: in `Grant_from_an_existing_Npc_deep_copies_Affinity_ElementoValor_and_SubElementoValor`, after the grant, read the copy through `_factory.Services.CreateAsyncScope()` → `RuinaRpgDbContext` and assert `SegundaEssencia`/`SegundaEssenciaValor` equal what was set on the source (set them on the source row the same way, directly through the DbContext, before granting). Run that test first to see it fail, then add the two initializer members.

- [ ] **Step 6: Run, verify pass** — `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~AfinidadeSegundaEssenciaMigrationTests|FullyQualifiedName~Grant_from_an_existing_Npc_deep_copies_Affinity"` → pass. Also run `FullyQualifiedName~Persistence` (all migration tests) → pass. `dotnet build` 0/0.

- [ ] **Step 7: Docs** — `Docs/Requisitos/Requisitos - Modelo de Dados.md`: in the CharacterAffinities description (search for `CharacterAffinities`), add `SegundaEssencia` (enum EssenciaBasica — Ar, Água, Fogo, Terra, Alma, Vida, Mundano — nullable) and `SegundaEssenciaValor` (int, nullable), and note that `SubElemento` is now derived by the server from the Matriz Elemental (see Ficha de Personagem 2.c). NpcAffinities mirrors it (§6.2 mirror rule — nothing to add there).

- [ ] **Step 8: Commit** — `git add` the entities, migration files, controller, the two test files and the doc; message `feat: colunas SegundaEssencia nas afinidades e conversão das linhas antigas` + the two trailer lines.

---

### Task 3: API — derive the Sub-Elemento, drop the Vocação rules (additive contracts)

**Files:**
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/{AddCharacterAffinityRequest,UpdateCharacterAffinityRequest,CharacterAffinityResponse}.cs`
- Modify: `src/RuinaRPG.Contracts/NpcSheets/{AddNpcAffinityRequest,UpdateNpcAffinityRequest,NpcAffinityResponse}.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterAffinitiesController.cs`, `src/RuinaRPG.Api/Controllers/NpcAffinitiesController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs` (~line 210-214), `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs` (~line 86-87)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterAffinitiesControllerTests.cs`, `NpcAffinitiesControllerTests.cs`, `CharacterSheetsControllerTests.cs` (~798-830), `NpcSheetsControllerTests.cs` (~492-515)

**Interfaces:**
- Consumes: `EssenciaBasica`, `MatrizElemental` (Task 1); entity `SegundaEssencia`/`SegundaEssenciaValor` (Task 2).
- Produces (interim, additive — Task 4 finalizes): requests gain trailing `string? SegundaEssencia = null, int? SegundaEssenciaValor = null`; responses gain trailing `string? SegundaEssencia, int? SegundaEssenciaValor`. The server **ignores** the requests' `SubElemento` and `CaminhoNome` and never writes `CaminhoNome` anymore.

- [ ] **Step 1: Contracts (interim)**

```csharp
public record AddCharacterAffinityRequest(string? Elemento, int? ElementoValor, string? SubElemento, int? SubElementoValor, string? CaminhoNome, int? Experiencia,
    string? SegundaEssencia = null, int? SegundaEssenciaValor = null);
public record UpdateCharacterAffinityRequest(string? Elemento, int? ElementoValor, string? SubElemento, int? SubElementoValor, string? CaminhoNome, int? Experiencia,
    string? SegundaEssencia = null, int? SegundaEssenciaValor = null);
public record CharacterAffinityResponse(string Id, string? Elemento, int? ElementoValor, string? SubElemento, int? SubElementoValor, string? CaminhoNome, int? Experiencia,
    string? SegundaEssencia, int? SegundaEssenciaValor);
```

Same three changes for `AddNpcAffinityRequest`, `UpdateNpcAffinityRequest`, `NpcAffinityResponse`. `dotnet build` must still pass (the client only reads responses by name and builds requests positionally without the new optional params).

- [ ] **Step 2: Replace the obsolete integration tests (failing first)**

In `CharacterAffinitiesControllerTests`:
1. `SetUpSheetAsync` keeps setting Vocação=Adepto (harmless) — leave it.
2. **Delete** these tests (they pin rules that no longer exist): `Add_an_invalid_Elemento_SubElemento_combination_returns_400`, `Add_rejects_an_Elemento_not_liberado_pela_Vocacao_atual`, `Update_keeps_an_old_SubElemento_that_no_longer_fits_a_new_Vocacao_when_resubmitted_unchanged`, `Add_rejects_a_duplicate_Elemento_already_used_by_another_row`, `Add_a_Curar_row_with_Fogo_and_the_Vida_Caminho_returns_201`, `Add_a_Curar_row_without_both_Fogo_and_the_Vida_Caminho_returns_400`, `Add_rejects_a_free_text_Caminho`, `Update_rejects_changing_the_Caminho_out_from_under_a_Curar_row`, `Update_keeps_a_legacy_free_text_Caminho_row_when_resubmitted_unchanged`, `Add_rejects_a_new_Alma_or_Vida_as_SubElemento`, `Update_rejects_changing_the_SubElemento_to_Vida`, `Update_keeps_a_legacy_row_whose_SubElemento_is_Vida_when_resubmitted_unchanged`.
3. **Update** the remaining tests that send `SubElemento`/`CaminhoNome` and assert them back: `Add_a_valid_combination_returns_201` → send `new AddCharacterAffinityRequest("Fogo", 3, null, 2, null, 10, "Vida", 4)` and assert `Elemento == "Fogo" && ElementoValor == 3 && SegundaEssencia == "Vida" && SegundaEssenciaValor == 4 && SubElemento == "Curar" && SubElementoValor == 2 && Experiencia == 10`. `Update_an_existing_affinity_returns_200_and_the_list_reflects_the_change`, `Update_can_clear_fields_back_to_null`, `Update_does_not_flag_a_duplicate_against_its_own_row`, `Update_does_not_reject_a_pre_existing_duplicate_left_unchanged`: rewrite their payloads to use `SegundaEssencia` (named argument) instead of `SubElemento`/`CaminhoNome`, keeping each test's intent; for the pre-existing-duplicate test, create the duplicate directly in the DB (two rows with the same `SubElemento`) via `_factory.Services.CreateAsyncScope()` since the API now refuses to create it.
4. **Add**:

```csharp
    private async Task<HttpResponseMessage> AddAsync(string token, string sheetId, string? elemento, string? segunda, int? subValor = null) =>
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", token,
            new AddCharacterAffinityRequest(elemento, null, null, subValor, null, null, segunda, null)));

    private async Task<List<CharacterAffinityResponse>> ListAsync(string token, string sheetId) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/affinities", token)))
            .Content.ReadFromJsonAsync<List<CharacterAffinityResponse>>())!;

    [Theory]
    [InlineData("Ar", "Agua", "Gelo")]
    [InlineData("Agua", "Ar", "Gelo")]
    [InlineData("Terra", "Vida", "Aprimorar")]
    [InlineData("Agua", "Mundano", "Hemomancia")]
    public async Task Add_derives_the_SubElemento_from_the_two_essencias(string e1, string e2, string sub)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"AffEssGm{e1}{e2}", $"affessgm{e1}{e2}@teste.com".ToLowerInvariant());
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"AffEssP{e1}{e2}", $"affessp{e1}{e2}@teste.com".ToLowerInvariant());
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        (await AddAsync(playerToken, sheetId, e1, e2)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await ListAsync(playerToken, sheetId)).Should().ContainSingle(a => a.Elemento == e1 && a.SegundaEssencia == e2 && a.SubElemento == sub);
    }

    [Fact]
    public async Task Add_ignores_a_SubElemento_sent_by_the_client()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssGmIgn", "affessgmign@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssPIgn", "affesspign@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/affinities", playerToken,
            new AddCharacterAffinityRequest("Fogo", null, "Gelo", null, "Alma", null, "Terra", null)));

        (await ListAsync(playerToken, sheetId)).Should().ContainSingle(a => a.SubElemento == "Ferro");
    }

    [Theory]
    [InlineData("Ar", "Terra")]
    [InlineData("Agua", "Fogo")]
    [InlineData("Fogo", "Fogo")]
    [InlineData("Fogo", "Alma")]
    [InlineData("Ar", "Vida")]
    public async Task Add_with_essencias_that_dont_cross_returns_400(string e1, string e2)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"AffEssBadGm{e1}{e2}", $"affessbadgm{e1}{e2}@teste.com".ToLowerInvariant());
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"AffEssBadP{e1}{e2}", $"affessbadp{e1}{e2}@teste.com".ToLowerInvariant());
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await AddAsync(playerToken, sheetId, e1, e2);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Essas duas Essências não se cruzam na Matriz Elemental.");
    }

    [Fact]
    public async Task Add_with_SegundaEssencia_but_no_Elemento_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssNoE1Gm", "affessnoe1gm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssNoE1P", "affessnoe1p@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);

        var response = await AddAsync(playerToken, sheetId, null, "Mundano");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Escolha a Essência Básica 1 antes da 2.");
    }

    [Fact]
    public async Task Add_rejects_a_second_row_with_the_same_SubElemento_but_allows_repeating_Essencia1()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssDupGm", "affessdupgm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssDupP", "affessdupp@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        await AddAsync(playerToken, sheetId, "Fogo", "Vida");

        var sameSub = await AddAsync(playerToken, sheetId, "Fogo", "Vida");
        var sameE1 = await AddAsync(playerToken, sheetId, "Fogo", "Mundano");

        sameSub.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await sameSub.Content.ReadAsStringAsync()).Should().Contain("Já existe uma linha de Afinidade com esse Sub-Elemento.");
        sameE1.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Campeao")]
    [InlineData("Feiticeiro")]
    public async Task Any_Vocacao_can_pick_any_essencias(string? vocacao)
    {
        var suffix = vocacao ?? "Nenhuma";
        var gmToken = await RegisterGmAndGetTokenAsync($"AffEssVocGm{suffix}", $"affessvocgm{suffix}@teste.com".ToLowerInvariant());
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"AffEssVocP{suffix}", $"affessvocp{suffix}@teste.com".ToLowerInvariant());
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var setVocacao = new UpdateCharacterSheetRequest(null, "Ficha de Teste", null, null, vocacao, null, null, null,
            false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "Nenhuma", 0, 0, null, null, 0, null, null);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, setVocacao));

        (await AddAsync(playerToken, sheetId, "Terra", "Mundano")).StatusCode.Should().Be(HttpStatusCode.Created);
        (await AddAsync(playerToken, sheetId, "Agua", "Alma")).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_changing_only_values_keeps_a_legacy_SubElemento()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssLegGm", "affesslegm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssLegP", "affesslegp@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var legacyId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRPG.Infrastructure.Persistence.RuinaRpgDbContext>();
            db.CharacterAffinities.Add(new RuinaRPG.Infrastructure.CharacterSheets.CharacterAffinity
                { Id = legacyId, CharacterSheetId = Guid.Parse(sheetId), Elemento = RuinaRPG.Domain.CharacterSheets.Elemento.Ar, SubElemento = RuinaRPG.Domain.CharacterSheets.SubElemento.Curar });
            await db.SaveChangesAsync();
        }

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{legacyId}", playerToken,
            new UpdateCharacterAffinityRequest("Ar", 5, null, 3, null, 7, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ListAsync(playerToken, sheetId)).Should().ContainSingle(a => a.SubElemento == "Curar" && a.ElementoValor == 5 && a.SubElementoValor == 3 && a.Experiencia == 7);
    }

    [Fact]
    public async Task Update_changing_an_essencia_recomputes_the_SubElemento()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AffEssRecGm", "affessrecgm@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "AffEssRecP", "affessrecp@teste.com");
        var sheetId = await SetUpSheetAsync(gmToken, playerId);
        var added = await (await AddAsync(playerToken, sheetId, "Fogo", "Vida")).Content.ReadFromJsonAsync<CharacterAffinityResponse>();

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added!.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", null, null, null, null, null, "Mundano", null)));
        var afterChange = (await ListAsync(playerToken, sheetId)).Single();
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/affinities/{added.Id}", playerToken,
            new UpdateCharacterAffinityRequest("Fogo", null, null, null, null, null, null, null)));
        var afterClear = (await ListAsync(playerToken, sheetId)).Single();

        afterChange.SubElemento.Should().Be("Necromancia");
        afterClear.SubElemento.Should().BeNull();
        afterClear.SegundaEssencia.Should().BeNull();
    }
```

(Add `using Microsoft.Extensions.DependencyInjection;` if missing. Email/nickname literals must be unique across the whole suite — keep the prefixes above.)

In `NpcAffinitiesControllerTests`: apply the same deletions/updates to its mirror tests (same names, `Npc` routes/contracts; it has `Add_rejects_an_Elemento_not_liberado_pela_Vocacao_atual` at ~line 189 and Caminho/Alma/Vida mirrors — delete every test whose name matches the Character list above), and add these (using the class's own `CreateSheetAsync(gmToken)` helper, GM token as caller, route `/api/npc-sheets/{sheetId}/affinities`):

```csharp
    [Fact]
    public async Task Add_derives_the_SubElemento_from_the_two_essencias()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssGm1", "npcaffessgm1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Fogo", null, null, null, null, null, "Terra", 2)));
        var list = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}/affinities", gmToken)))
            .Content.ReadFromJsonAsync<List<NpcAffinityResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        list!.Should().ContainSingle(a => a.SubElemento == "Ferro" && a.SegundaEssencia == "Terra" && a.SegundaEssenciaValor == 2);
    }

    [Fact]
    public async Task Add_with_essencias_that_dont_cross_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssGm2", "npcaffessgm2@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Agua", null, null, null, null, null, "Vida", null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_rejects_a_duplicate_SubElemento()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcAffEssGm3", "npcaffessgm3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Ar", null, null, null, null, null, "Agua", null)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/affinities", gmToken,
            new AddNpcAffinityRequest("Agua", null, null, null, null, null, "Ar", null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
```

In `CharacterSheetsControllerTests` replace `Update_rejects_an_Afinidade_not_liberada_pela_Vocacao_atual` with `Update_accepts_any_Afinidade_whatever_the_Vocacao` (same setup, a Vocação that previously didn't allow the Afinidade — e.g. Campeão + "Fogo"; assert 204 and the Afinidade persisted); keep `Update_allows_an_Afinidade_liberada_pela_Vocacao_atual` as is. Same replacement in `NpcSheetsControllerTests` (~line 492). The existing Alma/Vida-as-Afinidade rejection tests stay.

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~AffinitiesControllerTests|FullyQualifiedName~Update_accepts_any_Afinidade_whatever_the_Vocacao"` → the new tests FAIL (SubElemento not derived, Vocação still enforced).

- [ ] **Step 3: Controllers** — in `CharacterAffinitiesController` replace `TryParseElementoSubElemento` and `HasDuplicateAsync` with:

```csharp
    // Essência 1 e 2 são opcionais (linha em branco continua valendo). O Sub-Elemento nunca vem do
    // cliente: é a interseção na Matriz Elemental (Requisitos - Ficha de Personagem 2.c).
    private static bool TryParseEssencias(string? elementoRaw, string? segundaRaw,
        out Elemento? elemento, out EssenciaBasica? segunda, out string? error)
    {
        elemento = null;
        segunda = null;
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

        if (!string.IsNullOrEmpty(segundaRaw))
        {
            if (!Enum.TryParse<EssenciaBasica>(segundaRaw, out var parsed) || !Enum.IsDefined(parsed))
            {
                error = "Essência Básica desconhecida.";
                return false;
            }
            segunda = parsed;
        }

        if (segunda is not null && elemento is null)
        {
            error = "Escolha a Essência Básica 1 antes da 2.";
            return false;
        }

        if (elemento is { } e1 && segunda is { } e2 && MatrizElemental.Intersecao(e1, e2) is null)
        {
            error = "Essas duas Essências não se cruzam na Matriz Elemental.";
            return false;
        }

        return true;
    }

    private static SubElemento? Derivar(Elemento? elemento, EssenciaBasica? segunda) =>
        elemento is { } e1 && segunda is { } e2 ? MatrizElemental.Intersecao(e1, e2) : null;

    private async Task<bool> HasDuplicateSubElementoAsync(Guid sheetId, SubElemento? subElemento, Guid? excludingId) =>
        subElemento is not null && await db.CharacterAffinities.AnyAsync(a =>
            a.CharacterSheetId == sheetId && a.SubElemento == subElemento && (excludingId == null || a.Id != excludingId));
```

`Add`: parse with `TryParseEssencias(request.Elemento, request.SegundaEssencia, …)`; `var subElemento = Derivar(elemento, segunda);`; `if (await HasDuplicateSubElementoAsync(sheetId, subElemento, null)) return BadRequest("Já existe uma linha de Afinidade com esse Sub-Elemento.");`; create the entity with `Elemento`, `ElementoValor`, `SegundaEssencia = segunda`, `SegundaEssenciaValor = request.SegundaEssenciaValor`, `SubElemento = subElemento`, `SubElementoValor`, `Experiencia` (no `CaminhoNome`).

`Update`: parse the same way; then

```csharp
        var essenciaMudou = elemento != affinity.Elemento || segunda != affinity.SegundaEssencia;
        var subElemento = essenciaMudou ? Derivar(elemento, segunda) : affinity.SubElemento;
        if (subElemento != affinity.SubElemento && await HasDuplicateSubElementoAsync(sheetId, subElemento, id))
            return BadRequest("Já existe uma linha de Afinidade com esse Sub-Elemento.");
```

and assign `Elemento`, `ElementoValor`, `SegundaEssencia`, `SegundaEssenciaValor`, `SubElemento = subElemento`, `SubElementoValor`, `Experiencia` (stop assigning `CaminhoNome`).

`ToResponse`: `new(a.Id.ToString(), a.Elemento?.ToString(), a.ElementoValor, a.SubElemento?.ToString(), a.SubElementoValor, a.CaminhoNome, a.Experiencia, a.SegundaEssencia?.ToString(), a.SegundaEssenciaValor)`.

Make the identical changes in `NpcAffinitiesController` (its own entity/contract types and its NotFound-for-strangers auth, untouched).

In `CharacterSheetsController.Update` delete the comment + `if (… !VocacaoEscolaMap.PodeEscolherAfinidade(vocacao, afinidade.Value)) return BadRequest("Essa Afinidade não é liberada pela Vocação atual.");` block; same in `NpcSheetsController.Update`. Keep the `CaminhoSubElementoRules.EhCaminho(afinidade.Value)` check.

- [ ] **Step 4: Run, verify pass** — the Step 2 filter → all pass. Then `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests|FullyQualifiedName~NpcSheetsControllerTests|FullyQualifiedName~CampaignGrantsControllerTests"` → pass. `dotnet build` 0/0 (unused-using warnings count as failures — remove any).

- [ ] **Step 5: Commit** — contracts, the 4 controllers, the 4 test files; message `feat: Sub-Elemento derivado das duas Essências e fim da restrição por Vocação` + trailers.

---

### Task 4: Client — two Essências, read-only Sub-Elemento, no Vocação filter (final contracts)

**Files:**
- Modify: the 6 affinity contracts (final shape)
- Modify: `src/RuinaRPG.Api/Controllers/CharacterAffinitiesController.cs`, `NpcAffinitiesController.cs` (`ToResponse` only)
- Modify: `src/RuinaRPG.Client/Shared/Fields/AfinidadeEssenciaField.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`, `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`
- Delete: `src/RuinaRPG.Client/Shared/Fields/CaminhoSelect.razor`, `tests/RuinaRPG.Tests.Client/Shared/Fields/CaminhoSelectTests.cs`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/AfinidadeEssenciaFieldTests.cs` (existing file — extend/replace obsolete tests)
- Test: the integration tests from Task 3 (switch to the final contract shape)

**Interfaces:**
- Consumes: `MatrizElemental.OpcoesSegundaEssencia`, `EssenciaBasica` (Task 1); API behavior (Task 3).
- Produces (final): `AddCharacterAffinityRequest(string? Elemento, int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor, int? SubElementoValor, int? Experiencia)`, same for `UpdateCharacterAffinityRequest` and the two Npc requests; `CharacterAffinityResponse(string Id, string? Elemento, int? ElementoValor, string? SegundaEssencia, int? SegundaEssenciaValor, string? SubElemento, int? SubElementoValor, int? Experiencia)`, same for `NpcAffinityResponse`. `AfinidadeEssenciaField` gains `[Parameter] bool Disabled`, static `EssenciasBasicas` option list and `static (string Valor, string Rotulo)[] OpcoesSegundaEssencia(string? elemento, string? atual)`.

- [ ] **Step 1: Failing bUnit tests** — in `AfinidadeEssenciaFieldTests.cs`, delete tests of `SubElementosDisponiveis` (it's going away) and add:

```csharp
    [Theory]
    [InlineData("Fogo", new[] { "Ar", "Terra", "Vida", "Mundano" })]
    [InlineData("Agua", new[] { "Ar", "Terra", "Alma", "Mundano" })]
    public void OpcoesSegundaEssencia_follow_the_Matriz_for_the_chosen_Essencia1(string elemento, string[] esperado)
    {
        AfinidadeEssenciaField.OpcoesSegundaEssencia(elemento, atual: null).Select(o => o.Valor).Should().Equal(esperado);
    }

    [Fact]
    public void OpcoesSegundaEssencia_is_empty_without_Essencia1()
    {
        AfinidadeEssenciaField.OpcoesSegundaEssencia(null, atual: null).Should().BeEmpty();
    }

    [Fact]
    public void OpcoesSegundaEssencia_labels_Agua_with_its_accent()
    {
        AfinidadeEssenciaField.OpcoesSegundaEssencia("Ar", atual: null).Should().Contain(("Agua", "Água"));
    }

    [Fact]
    public void Disabled_disables_the_name_select()
    {
        var cut = Render<AfinidadeEssenciaField>(p => p
            .Add(x => x.Label, "Essência 2")
            .Add(x => x.Opcoes, Array.Empty<(string, string)>())
            .Add(x => x.Disabled, true));

        cut.FindComponent<MudSelect<string>>().Instance.Disabled.Should().BeTrue();
    }
```

Run `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~AfinidadeEssenciaFieldTests` → build error (members missing).

- [ ] **Step 2: `AfinidadeEssenciaField.razor`** — add `Disabled="Disabled"` to the `MudSelect`, `[Parameter] public bool Disabled { get; set; }`, and replace `SubElementos`, `SubElementosLegados` and `SubElementosDisponiveis` with:

```csharp
    public static readonly (string Valor, string Rotulo)[] EssenciasBasicas =
    [
        ("Ar", "Ar"), ("Agua", "Água"), ("Fogo", "Fogo"), ("Terra", "Terra"), ("Alma", "Alma"), ("Vida", "Vida"), ("Mundano", "Mundano"),
    ];

    // Essência 2 de uma linha de Afinidade: só o que cruza com a Essência 1 na Matriz Elemental. O valor
    // já salvo (atual) fica na lista pro select não aparecer em branco.
    public static (string Valor, string Rotulo)[] OpcoesSegundaEssencia(string? elemento, string? atual)
    {
        if (!Enum.TryParse<Elemento>(elemento, out var e1))
            return [];
        var validas = MatrizElemental.OpcoesSegundaEssencia(e1).Select(e => e.ToString()).ToHashSet();
        return EssenciasBasicas.Where(o => validas.Contains(o.Valor) || o.Valor == atual).ToArray();
    }

    public static string RotuloSubElemento(string? subElemento) => subElemento switch
    {
        null or "" => "—",
        "Invocacao" => "Invocação",
        _ => subElemento,
    };
```

Update the header comment (the second cell is now "Essência 2", not "Sub-Elemento"). Run the Step 1 filter → pass.

- [ ] **Step 3: Final contracts + API `ToResponse`** — rewrite the 6 records to the final shape above. `ToResponse` in both affinity controllers: `new(a.Id.ToString(), a.Elemento?.ToString(), a.ElementoValor, a.SegundaEssencia?.ToString(), a.SegundaEssenciaValor, a.SubElemento?.ToString(), a.SubElementoValor, a.Experiencia)`. Update every request construction in `CharacterAffinitiesControllerTests`/`NpcAffinitiesControllerTests` (and any other test that builds these records — `grep -rn "AffinityRequest(" tests`) to the 6-argument shape, e.g. `new AddCharacterAffinityRequest("Fogo", 3, "Vida", 4, 2, 10)`; drop `CaminhoNome` assertions.

- [ ] **Step 4: Ficha de Personagem** — in `FichaDePersonagem.razor`:
1. 1.a: `<AfinidadeSelect @bind-Value="_form.Afinidade" @bind-Value:after="NotifySavedAsync" OpcoesPermitidas="AfinidadeSelect.Afinidades" />` (check `AfinidadeSelect.Afinidades` already excludes Alma/Vida; if it includes them, pass `AfinidadeSelect.Afinidades.Where(a => a.Valor is not ("Alma" or "Vida")).ToArray()` through a static field).
2. Replace the Afinidades table header and rows with:

```razor
                <thead><tr><th>Essência Básica 1</th><th>Essência Básica 2</th><th>Sub-Elemento</th><th>Experiência</th><th></th></tr></thead>
                <tbody>
                    @foreach (var affinity in _affinities)
                    {
                        <tr>
                            <td>
                                <AfinidadeEssenciaField Label="Essência 1" Opcoes="AfinidadeEssenciaField.Elementos"
                                                         Nome="@affinity.Elemento" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "Elemento", v))"
                                                         Valor="@affinity.ElementoValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "ElementoValor", v))" />
                            </td>
                            <td>
                                <AfinidadeEssenciaField Label="Essência 2" Opcoes="AfinidadeEssenciaField.OpcoesSegundaEssencia(affinity.Elemento, affinity.SegundaEssencia)"
                                                         Disabled="@string.IsNullOrEmpty(affinity.Elemento)"
                                                         Nome="@affinity.SegundaEssencia" NomeChanged="@(v => UpdateAffinityAsync(affinity.Id, "SegundaEssencia", v))"
                                                         Valor="@affinity.SegundaEssenciaValor" ValorChanged="@(v => UpdateAffinityAsync(affinity.Id, "SegundaEssenciaValor", v))" />
                            </td>
                            <td>
                                <div class="d-flex align-center" style="gap:4px">
                                    <MudText Style="min-width:7rem">@AfinidadeEssenciaField.RotuloSubElemento(affinity.SubElemento)</MudText>
                                    <MudNumericField T="int?" Value="@affinity.SubElementoValor" ValueChanged="@(v => UpdateAffinityAsync(affinity.Id, "SubElementoValor", v))" Margin="Margin.Dense" Style="max-width:4.5rem" />
                                </div>
                            </td>
                            <td><MudNumericField T="int?" Value="@affinity.Experiencia" ValueChanged="@(v => UpdateAffinityAsync(affinity.Id, "Experiencia", v))" /></td>
                            <td><MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteAffinityAsync(affinity.Id))">Remover</MudButton></td>
                        </tr>
                    }
                    <tr>
                        <td>
                            <AfinidadeEssenciaField Label="Essência 1" Opcoes="AfinidadeEssenciaField.Elementos"
                                                     Nome="@_affinityForm.Elemento" NomeChanged="OnNovaEssencia1Changed" @bind-Valor="_affinityForm.ElementoValor" />
                        </td>
                        <td>
                            <AfinidadeEssenciaField Label="Essência 2" Opcoes="AfinidadeEssenciaField.OpcoesSegundaEssencia(_affinityForm.Elemento, null)"
                                                     Disabled="@string.IsNullOrEmpty(_affinityForm.Elemento)"
                                                     @bind-Nome="_affinityForm.SegundaEssencia" @bind-Valor="_affinityForm.SegundaEssenciaValor" />
                        </td>
                        <td>
                            <div class="d-flex align-center" style="gap:4px">
                                <MudText Style="min-width:7rem">@AfinidadeEssenciaField.RotuloSubElemento(SubElementoDaNovaLinha())</MudText>
                                <MudNumericField T="int?" @bind-Value="_affinityForm.SubElementoValor" Margin="Margin.Dense" Style="max-width:4.5rem" />
                            </div>
                        </td>
                        <td><MudNumericField T="int?" @bind-Value="_affinityForm.Experiencia" /></td>
                        <td><MudButton Variant="Variant.Filled" Color="Color.Primary" Size="Size.Small" OnClick="AddAffinityAsync">Adicionar</MudButton></td>
                    </tr>
                </tbody>
```

3. Code: delete `AfinidadesPermitidasPelaVocacao`, `ElementosPermitidosPelaVocacao`, `SubElementosDisponiveis`. `AffinityFormModel`: remove `SubElemento`, `CaminhoNome`; add `public string? SegundaEssencia { get; set; }`, `public int? SegundaEssenciaValor { get; set; }`. Add:

```csharp
    private void OnNovaEssencia1Changed(string? elemento)
    {
        _affinityForm.Elemento = elemento;
        if (!AfinidadeEssenciaField.OpcoesSegundaEssencia(elemento, null).Any(o => o.Valor == _affinityForm.SegundaEssencia))
            _affinityForm.SegundaEssencia = null;
    }

    private string? SubElementoDaNovaLinha() =>
        Enum.TryParse<Elemento>(_affinityForm.Elemento, out var e1) && Enum.TryParse<EssenciaBasica>(_affinityForm.SegundaEssencia, out var e2)
            ? MatrizElemental.Intersecao(e1, e2)?.ToString()
            : null;
```

`AddAffinityAsync`: `new AddCharacterAffinityRequest(_affinityForm.Elemento, _affinityForm.ElementoValor, _affinityForm.SegundaEssencia, _affinityForm.SegundaEssenciaValor, _affinityForm.SubElementoValor, _affinityForm.Experiencia)`; reset `SegundaEssencia`/`SegundaEssenciaValor` instead of `SubElemento`/`CaminhoNome`; on failure show the API message: `_errorMessage = await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a afinidade.";`.
`UpdateAffinityAsync`: build from `current` with `segunda = field == "SegundaEssencia" ? value?.ToString() : current.SegundaEssencia`, `segundaValor = field == "SegundaEssenciaValor" ? value as int? : current.SegundaEssenciaValor`; and when `field == "Elemento"`, clear an incompatible Essência 2 in the same request:

```csharp
        if (field == "Elemento" && !AfinidadeEssenciaField.OpcoesSegundaEssencia(elemento, null).Any(o => o.Valor == segunda))
            segunda = null;
```

then `new UpdateCharacterAffinityRequest(elemento, elementoValor, segunda, segundaValor, subElementoValor, experiencia)`.

- [ ] **Step 5: Ficha de NPC** — the same edits in `FichaDeNpc.razor` (its own `Npc` contracts and `npc-sheets/{SheetId}/affinities` routes).

- [ ] **Step 6: Delete `CaminhoSelect`** — remove `CaminhoSelect.razor` and `CaminhoSelectTests.cs` (no remaining references: `grep -rn CaminhoSelect src tests` must be empty).

- [ ] **Step 7: Verify** — `dotnet build` 0/0; `dotnet test tests/RuinaRPG.Tests.Client` (known flake `AutoSaveCoordinatorTests.A_delegate_returning_true_sets_state_Saved_and_LastSavedAt` passes on re-run); `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~AffinitiesControllerTests|FullyQualifiedName~CampaignGrantsControllerTests"`.

- [ ] **Step 8: Commit** — message `feat: fichas com duas Essências Básicas e Sub-Elemento calculado` + trailers.

---

### Task 5: Drop `CaminhoNome`, remove the dead rules, docs

**Files:**
- Modify: `CharacterAffinity.cs`, `NpcAffinity.cs` (remove `CaminhoNome`)
- Create: migration `DropAfinidadeCaminhoNome`
- Modify: `tests/RuinaRPG.Tests.Integration/Persistence/CharacterAffinityMigrationTests.cs`, `NpcChildTableMigrationTests.cs` (stop using `CaminhoNome`)
- Delete (only if no references remain after this task): `CaminhoSubElementoRules` members other than `EhCaminho(AfinidadeElemental)`, `ElementoSubElementoValidator.cs`, `VocacaoEscolaMap.cs`, `EscolaDeMagiaCatalog.cs`, `EscolaDeMagia.cs`, `Caminho.cs`, and their unit tests (`CaminhoSubElementoRulesTests` — keep only the `EhCaminho(AfinidadeElemental)` test —, `ElementoSubElementoValidatorTests`, `VocacaoEscolaMapTests`, `EscolaDeMagiaCatalogTests`)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md` (1.a Afinidade, 2.c), `Docs/Requisitos/Requisitos - Modelo de Dados.md`

**Interfaces:**
- Consumes: everything above. Produces: no new API.

- [ ] **Step 1: Failing check** — `grep -rn "CaminhoNome" src tests` lists the entities, the migration Designer/snapshots (fine) and tests. Remove `CaminhoNome` from the two entities; `dotnet build` now fails wherever it's still used (the two migration tests, `AfinidadeSegundaEssenciaMigrationTests` uses raw SQL only — must keep compiling). Fix the two old migration tests by removing their `CaminhoNome = …` initializers/assertions.

- [ ] **Step 2: Migration** — `dotnet ef migrations add DropAfinidadeCaminhoNome …` (same flags as Task 2). `Up` must only `DropColumn("CaminhoNome")` on both tables; `Down` re-adds it as `text` NULL. Run `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~Persistence` → pass (including `AfinidadeSegundaEssenciaMigrationTests`, which migrates through both new migrations).

- [ ] **Step 3: Dead rules** — trim `CaminhoSubElementoRules` to:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Alma e Vida continuam no enum AfinidadeElemental (gravado como inteiro — remover um membro
/// deslocaria os valores salvos), mas são Caminhos, não uma Afinidade válida pra 1.a.
/// </summary>
public static class CaminhoSubElementoRules
{
    public static bool EhCaminho(AfinidadeElemental afinidade) => afinidade is AfinidadeElemental.Alma or AfinidadeElemental.Vida;
}
```

then delete each listed file whose `grep -rn "<TypeName>" src` is empty (check `SubAttributeFormulas.cs` — it only mentions `VocacaoEscolaMap` in a comment; reword that comment). Delete the matching unit tests; trim `CaminhoSubElementoRulesTests` to the `EhCaminho(AfinidadeElemental)` theory. `dotnet build` 0/0, `dotnet test tests/RuinaRPG.Tests.Unit` → pass.

- [ ] **Step 4: Docs** — `Requisitos - Ficha de Personagem.md`:
  - 1.a *Afinidade*: remove the statement that it's restricted by Vocação (keep that Alma/Vida aren't valid).
  - 2.c: rewrite the row fields as: *Essência Básica 1* (Ar/Água/Fogo/Terra) + valor; *Essência Básica 2* (only what crosses with Essência 1 on the Matriz — Elementos and the Caminhos Alma/Vida/Mundano) + valor; *Sub-Elemento* (not chosen — the intersection, read-only; value editable); *Experiência*. Replace the Caminho × Sub-Elemento table with the full 12-row intersection table from the spec. Delete the Escolas de Magia paragraph + table and the Vocação → Escolas paragraph. Replace the uniqueness paragraph with: two rows can't produce the same Sub-Elemento; Essência 1 may repeat. Keep a sentence that rows saved before this change whose Sub-Elemento doesn't come from the Matriz keep it until an Essência is changed.
  - `Requisitos - Modelo de Dados.md`: remove `CaminhoNome` from CharacterAffinities.

- [ ] **Step 5: Full verification** — `dotnet build` 0/0; `dotnet test` (disk check first; re-run flake classes only).

- [ ] **Step 6: Commit** — message `refactor: remove CaminhoNome e as regras de Caminho/Escola das afinidades` + trailers.

---

## After all tasks (controller)

Whole-branch review (most capable model) with the ledger's deferred minors; one fix wave if needed; merge per the repo's pattern (fast-forward to main, push). Remind the user: two migrations → `make deploy` then `make migrate`.
