# Auditoria de Perícias Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the 39 Perícias from a hardcoded C# `enum` to a DB table the Rules Auditor manages (add, rename, describe, suggested attribute, Criatura flag, reversible removal that refunds points), with the description shown as tooltip/popup on the sheets.

**Architecture:** New table `Pericias` (int `Id` = old enum value, immutable string `Chave` = old enum member name). The wire format keeps using the string `Chave`, so Contracts, routes, Passiva JSON and Artefato `Alvo` keep their shape and data. Inside the server, every `Pericia` enum use becomes an `int` id resolved through a scoped `IPericiaCatalogo`. The client gets a scoped `PericiaCatalogo` service (loads `GET api/pericias`) replacing the static `PericiaDisplay`.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core + Npgsql, Blazor WASM + MudBlazor, xUnit + FluentAssertions, Testcontainers, bUnit.

**Spec:** `docs/superpowers/specs/2026-09-29-auditoria-de-pericias-design.md`

## Global Constraints

- TDD is mandatory (Técnico R0011). `dotnet build` = 0 warnings, 0 errors after every task (Api **and** Client — the Client references Domain).
- UI text in Brazilian Portuguese. **No emojis** — MudBlazor icons only (`Icons.Material.Filled.Lock` for protected, `Icons.Material.Filled.Info` via `InfoPopup`).
- Every new UI surface has an `InfoPopup` (ⓘ).
- Seeded Ids 0–38 **must equal** the old enum values; seeded `Chave` **must equal** the old enum member names (existing DB ints, Passiva jsonb ints and Artefato `Alvo` strings depend on it).
- Protected perícias: `Fortitude = 17`, `Prontidao = 32`, `Reflexos = 33` — not removable, `DisponivelParaCriaturas` locked `true`.
- `RequisitoDePericia`'s JSON property name stays `Pericia` (existing jsonb rows use it).
- Perícias are never hard-deleted (FKs `ON DELETE RESTRICT`).
- Auditor check: DB lookup of `ApplicationUser.IsRulesAuditor` (`RequireRulesAuditorAsync` pattern from `DurabilidadesPorRankController`), not a JWT claim.
- Integration tests: Docker required; run with `--filter` in batches of ≤ 8 test classes (known ~2-3% timeout flake — rerun a failure in isolation first). Unique user nicknames/emails per test.
- Commits in Portuguese with conventional prefix, ending `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- A sheet created **before** a perícia was added → the skills list must include it with `Gasto 0`, and `PUT .../skills/{chave}` must upsert the row (not 500 on `SingleAsync`).
- Removing a perícia that is a Histórico's +6/+3 → the sheet's skill list must not show it and `ContaHistorico` must not leak onto another perícia; restoring must not double-count points (Gasto stays 0).
- Mastery (Maestria) on a removed perícia → hidden from the sheet's mastery list, and `ComputeTotal` must not throw.
- Creating a perícia whose Nome slugs to an existing `Chave` (e.g. "Atletismo " or "atletismo" after the original was removed) → gets `Atletismo2`, not a unique-index 500.
- Artefato with `TipoDeAlvo = Pericia` and `Alvo = "ArmasBrancas"` created before the migration → still adds its bonus to Armas Brancas' Total.

---

## File Structure

| File | Responsibility |
|---|---|
| Create `src/RuinaRPG.Domain/CharacterSheets/PericiasIniciais.cs` | the 39 seed rows (Id, Chave, Nome, DisponivelParaCriaturas) — single seed source |
| Create `src/RuinaRPG.Domain/CharacterSheets/PericiasDeSistema.cs` | protected ids |
| Create `src/RuinaRPG.Domain/CharacterSheets/PericiaChave.cs` | slug generator |
| Create `src/RuinaRPG.Infrastructure/Rules/PericiaDefinicao.cs` | EF entity (table `Pericias`) |
| Create `src/RuinaRPG.Infrastructure/Rules/PericiaCatalogo.cs` | `IPericiaCatalogo` scoped per request |
| Create `src/RuinaRPG.Contracts/Rules/PericiaResponse.cs`, `PericiaAuditoriaResponse.cs`, `SalvarPericiaRequest.cs` | DTOs |
| Create `src/RuinaRPG.Api/Controllers/PericiasController.cs` | list + auditor CRUD |
| Modify many (Task 3 list) | enum → int |
| Delete `Pericia.cs`, `PericiaLabels.cs`, `CreatureSkillAllowList.cs`, `Client/Shared/PericiaDisplay.cs` | replaced |
| Create `src/RuinaRPG.Client/Services/PericiaCatalogo.cs` | client catalog |
| Create `src/RuinaRPG.Client/Shared/PericiaNome.razor` | name + tooltip/popup |
| Create `src/RuinaRPG.Client/Pages/AuditoriaPericias.razor` | auditor page |

---

### Task 1: Domain — seed list, protected ids, key generator

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/PericiasIniciais.cs`, `PericiasDeSistema.cs`, `PericiaChave.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/PericiasIniciaisTests.cs`, `PericiaChaveTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record PericiaInicial(int Id, string Chave, string Nome, bool DisponivelParaCriaturas);
  public static class PericiasIniciais { public static IReadOnlyList<PericiaInicial> Todas { get; } public static int IdPorNome(string nome); /* throws KeyNotFoundException */ }
  public static class PericiasDeSistema { public const int Fortitude = 17, Prontidao = 32, Reflexos = 33; public static bool IsProtegida(int id); public static IReadOnlyList<int> Todas { get; } }
  public static class PericiaChave { public static string Gerar(string nome, IEnumerable<string> chavesExistentes); }
  ```

- [ ] **Step 1: Write the failing tests**

`PericiasIniciaisTests.cs` — pins the seed to the current enum/labels/allow-list (this test is deleted in Task 3 together with the enum; its job is to prove the literal list was copied correctly):
```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class PericiasIniciaisTests
{
    [Fact]
    public void Matches_the_legacy_enum_labels_and_creature_allow_list_one_to_one()
    {
        var esperado = Enum.GetValues<Pericia>()
            .Select(p => new PericiaInicial((int)p, p.ToString(), PericiaLabels.Label(p), CreatureSkillAllowList.IsAllowed(p)))
            .ToList();

        PericiasIniciais.Todas.Should().Equal(esperado);
    }

    [Fact]
    public void IdPorNome_resolves_the_display_name()
    {
        PericiasIniciais.IdPorNome("Empatia c/ Animais").Should().Be(14);
    }

    [Fact]
    public void Protected_ids_are_Fortitude_Prontidao_Reflexos()
    {
        PericiasDeSistema.Todas.Select(id => PericiasIniciais.Todas.Single(p => p.Id == id).Chave)
            .Should().BeEquivalentTo("Fortitude", "Prontidao", "Reflexos");
        PericiasDeSistema.IsProtegida(7).Should().BeFalse();
    }
}
```

`PericiaChaveTests.cs`:
```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class PericiaChaveTests
{
    [Theory]
    [InlineData("Navegação Aérea", "NavegacaoAerea")]
    [InlineData("  empatia c/ animais ", "EmpatiaCAnimais")]
    [InlineData("Ofício-2", "Oficio2")]
    public void Gerar_builds_an_accent_free_PascalCase_key(string nome, string esperado)
    {
        PericiaChave.Gerar(nome, Array.Empty<string>()).Should().Be(esperado);
    }

    [Fact]
    public void Gerar_appends_a_number_on_collision_case_insensitively()
    {
        PericiaChave.Gerar("Atletismo", new[] { "Atletismo", "atletismo2" }).Should().Be("Atletismo3");
    }

    [Fact]
    public void Gerar_of_a_name_without_letters_or_digits_falls_back_to_Pericia()
    {
        PericiaChave.Gerar("!!!", Array.Empty<string>()).Should().Be("Pericia");
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~PericiasIniciaisTests|FullyQualifiedName~PericiaChaveTests"`
Expected: build FAIL (types missing).

- [ ] **Step 3: Implement**

`PericiasIniciais.cs` — write the 39 rows literally (Id, Chave, Nome from `PericiaLabels`, criatura flag from `CreatureSkillAllowList`):
```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public sealed record PericiaInicial(int Id, string Chave, string Nome, bool DisponivelParaCriaturas);

/// <summary>
/// As 39 perícias com que a tabela Pericias nasce (migration AddPericias) — Id e Chave são os do
/// antigo enum Pericia, e precisam continuar iguais: colunas int, o jsonb dos requisitos de Passiva
/// e o Alvo textual dos Artefatos gravados antes da migração apontam para eles. Depois do seed, a
/// fonte da verdade é a tabela (editada na Auditoria); esta lista só serve ao seed e ao parser de
/// Históricos, que lê os nomes originais do Markdown.
/// </summary>
public static class PericiasIniciais
{
    public static IReadOnlyList<PericiaInicial> Todas { get; } =
    [
        new(0, "Acrobacia", "Acrobacia", true),
        new(1, "Alquimia", "Alquimia", false),
        new(2, "Arcano", "Arcano", false),
        new(3, "Armadilhas", "Armadilhas", false),
        new(4, "ArmasBrancas", "Armas Brancas", false),
        new(5, "ArtefatosMagicos", "Artefatos Mágicos", true),
        new(6, "Artistico", "Artístico", false),
        new(7, "Atletismo", "Atletismo", true),
        new(8, "Avaliacao", "Avaliação", false),
        new(9, "Biblioteca", "Biblioteca", false),
        new(10, "Brigar", "Brigar", true),
        new(11, "Conducao", "Condução", false),
        new(12, "Conhecimentos", "Conhecimentos", false),
        new(13, "Crime", "Crime", false),
        new(14, "EmpatiaComAnimais", "Empatia c/ Animais", true),
        new(15, "Enganacao", "Enganação", true),
        new(16, "ForcaDeVontade", "Força de Vontade", true),
        new(17, "Fortitude", "Fortitude", true),
        new(18, "Furtividade", "Furtividade", true),
        new(19, "Herborismo", "Herborismo", false),
        new(20, "Intimidacao", "Intimidação", true),
        new(21, "Intuicao", "Intuição", true),
        new(22, "Investigacao", "Investigação", true),
        new(23, "Labia", "Lábia", false),
        new(24, "Lideranca", "Liderança", false),
        new(25, "Linguistica", "Linguística", false),
        new(26, "Medicina", "Medicina", false),
        new(27, "Navegacao", "Navegação", true),
        new(28, "Ocultismo", "Ocultismo", true),
        new(29, "Oficio", "Ofício", false),
        new(30, "Percepcao", "Percepção", true),
        new(31, "Pontaria", "Pontaria", true),
        new(32, "Prontidao", "Prontidão", true),
        new(33, "Reflexos", "Reflexos", true),
        new(34, "Religiao", "Religião", false),
        new(35, "Saquear", "Saquear", false),
        new(36, "Seducao", "Sedução", true),
        new(37, "SensoComum", "Senso Comum", false),
        new(38, "Sobrevivencia", "Sobrevivência", true),
    ];

    private static readonly Dictionary<string, int> PorNome = Todas.ToDictionary(p => p.Nome, p => p.Id);

    public static int IdPorNome(string nome) => PorNome[nome];
}
```
(The first test cross-checks every value against the enum; if it fails, fix the literal list, not the test.)

`PericiasDeSistema.cs`:
```csharp
namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>Perícias que entram em fórmulas (Iniciativa, Esquiva/Reflexos, Fortitude — ver Formulas.md): não podem ser removidas na Auditoria.</summary>
public static class PericiasDeSistema
{
    public const int Fortitude = 17;
    public const int Prontidao = 32;
    public const int Reflexos = 33;

    public static IReadOnlyList<int> Todas { get; } = [Fortitude, Prontidao, Reflexos];

    public static bool IsProtegida(int id) => Todas.Contains(id);
}
```

`PericiaChave.cs`:
```csharp
using System.Globalization;
using System.Text;

namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>Identificador estável de uma perícia nova: o Nome sem acentos, em PascalCase, só letras e dígitos.</summary>
public static class PericiaChave
{
    public static string Gerar(string nome, IEnumerable<string> chavesExistentes)
    {
        var semAcento = new string(nome.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var palavras = semAcento.Split(c => !char.IsLetterOrDigit(c));
        var baseChave = string.Concat(palavras.Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        if (baseChave.Length == 0)
            baseChave = "Pericia";

        var existentes = new HashSet<string>(chavesExistentes, StringComparer.OrdinalIgnoreCase);
        if (!existentes.Contains(baseChave))
            return baseChave;
        for (var n = 2; ; n++)
            if (!existentes.Contains(baseChave + n))
                return baseChave + n;
    }
}
```
`string.Split(Func<char,bool>)` doesn't exist — implement the split with a small loop or `Regex.Split(semAcento, "[^A-Za-z0-9]+")`. Use the regex form:
```csharp
        var palavras = System.Text.RegularExpressions.Regex.Split(semAcento, "[^A-Za-z0-9]+");
```
Note `"empatia c/ animais"` → words `empatia`,`c`,`animais` → `EmpatiaCAnimais`; `"Ofício-2"` → `Oficio`,`2` → `Oficio2`.

- [ ] **Step 4: Run tests** — same command → PASS.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets tests/RuinaRPG.Tests.Unit/CharacterSheets
git commit -m "feat(domain): perícias iniciais, perícias de sistema e gerador de chave"
```

---

### Task 2: Table `Pericias` + API (list and Auditoria CRUD)

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Rules/PericiaDefinicao.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Create: migration `AddPericias`
- Create: `src/RuinaRPG.Contracts/Rules/PericiaResponse.cs`, `PericiaAuditoriaResponse.cs`, `SalvarPericiaRequest.cs`
- Create: `src/RuinaRPG.Api/Controllers/PericiasController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/PericiasControllerTests.cs`, `tests/RuinaRPG.Tests.Integration/Persistence/PericiasMigrationTests.cs`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  ```csharp
  // Infrastructure.Rules
  public class PericiaDefinicao { int Id; string Chave; string Nome; string? Descricao; Atributo? AtributoSugerido; bool DisponivelParaCriaturas; bool IsDeleted; }
  // DbContext: DbSet<PericiaDefinicao> Pericias
  // Contracts.Rules
  public record PericiaResponse(int Id, string Chave, string Nome, string? Descricao, string? AtributoSugerido, bool DisponivelParaCriaturas, bool Protegida);
  public record PericiaAuditoriaResponse(int Id, string Chave, string Nome, string? Descricao, string? AtributoSugerido, bool DisponivelParaCriaturas, bool Protegida, bool IsDeleted);
  public record SalvarPericiaRequest(string Nome, string? Descricao, string? AtributoSugerido, bool DisponivelParaCriaturas);
  // Routes
  // GET    api/pericias                    any authenticated → List<PericiaResponse> (active, by Nome)
  // GET    api/pericias/auditoria          auditor → List<PericiaAuditoriaResponse> (all, by Nome)
  // POST   api/pericias                    auditor → 201 PericiaAuditoriaResponse
  // PUT    api/pericias/{id:int}           auditor → 200 PericiaAuditoriaResponse
  // DELETE api/pericias/{id:int}           auditor → 204 (logical; zeroes Gasto)
  // POST   api/pericias/{id:int}/restaurar auditor → 200 PericiaAuditoriaResponse
  ```

- [ ] **Step 1: Write the failing tests**

`PericiasMigrationTests.cs` (same shape as `CharacterAttributeAndSkillMigrationTests`):
```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class PericiasMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public PericiasMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_seeds_the_39_pericias_with_the_legacy_ids_keys_and_names()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var linhas = await db.Pericias.OrderBy(p => p.Id).ToListAsync();

        linhas.Select(p => (p.Id, p.Chave, p.Nome, p.DisponivelParaCriaturas))
            .Should().Equal(PericiasIniciais.Todas.Select(p => (p.Id, p.Chave, p.Nome, p.DisponivelParaCriaturas)));
        linhas.Should().OnlyContain(p => !p.IsDeleted && p.Descricao == null && p.AtributoSugerido == null);
    }
}
```

`PericiasControllerTests.cs` — copy the fixture boilerplate (`IClassFixture<PostgresFixture>, IAsyncLifetime`, `AuthedRequest`, `RegisterGmAndGetTokenAsync`, `RegisterJogadorTokenAsync`) from `RacialAbilitiesControllerTests`, plus the auditor-grant helper used by `DurabilidadesPorRankControllerTests` (read that file and copy its `GrantRulesAuditorAsync`/equivalent helper verbatim). Tests:
```csharp
    [Fact]
    public async Task List_returns_the_active_pericias_ordered_by_name_for_any_authenticated_user()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PerGm1", "per1@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "PerJog1", "perjog1@teste.com");

        var body = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias", jogadorToken)))
            .Content.ReadFromJsonAsync<List<PericiaResponse>>();

        body!.Should().HaveCountGreaterThanOrEqualTo(39);
        body.Select(p => p.Nome).Should().BeInAscendingOrder(StringComparer.CurrentCulture);
        body.Single(p => p.Chave == "Prontidao").Protegida.Should().BeTrue();
        body.Single(p => p.Chave == "Atletismo").Protegida.Should().BeFalse();
    }

    [Fact]
    public async Task Auditor_endpoints_return_403_for_a_non_auditor_gm()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("PerGm2", "per2@teste.com");

        (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias/auditoria", gmToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", gmToken, new SalvarPericiaRequest("Nova", null, null, false)))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/pericias/7", gmToken))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_generates_a_key_and_the_next_id()
    {
        var token = await RegisterAuditorAsync("PerAud3", "peraud3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token,
            new SalvarPericiaRequest("Navegação Aérea", "Pilotar aeronaves.", "Destreza", true)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();
        body!.Chave.Should().StartWith("NavegacaoAerea");
        body.Id.Should().BeGreaterThanOrEqualTo(39);
        body.AtributoSugerido.Should().Be("Destreza");
        body.DisponivelParaCriaturas.Should().BeTrue();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("Atletismo")]
    [InlineData("atletismo")]
    public async Task Create_with_a_blank_or_duplicate_active_name_returns_400(string nome)
    {
        var token = await RegisterAuditorAsync($"PerAud4{nome.Trim().Length}{nome.GetHashCode() & 0xffff}", $"peraud4{nome.Trim().Length}{nome.GetHashCode() & 0xffff}@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest(nome, null, null, false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_with_an_unknown_attribute_returns_400()
    {
        var token = await RegisterAuditorAsync("PerAud5", "peraud5@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Xadrez", null, "Sorte", false)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_changes_fields_but_never_the_key()
    {
        var token = await RegisterAuditorAsync("PerAud6", "peraud6@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Cartografia", null, null, false))))
            .Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();

        var updated = await (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/pericias/{created!.Id}", token,
            new SalvarPericiaRequest("Cartografia Arcana", "Mapas mágicos.", "Astucia", true)))).Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();

        updated!.Nome.Should().Be("Cartografia Arcana");
        updated.Chave.Should().Be(created.Chave);
        updated.Descricao.Should().Be("Mapas mágicos.");
    }

    [Fact]
    public async Task Update_of_a_protected_pericia_keeps_it_available_for_criaturas()
    {
        var token = await RegisterAuditorAsync("PerAud7", "peraud7@teste.com");

        var updated = await (await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/17", token,
            new SalvarPericiaRequest("Fortitude", "Resistir a venenos.", "Vigor", false)))).Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();

        updated!.DisponivelParaCriaturas.Should().BeTrue();
        updated.Descricao.Should().Be("Resistir a venenos.");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/17", token, new SalvarPericiaRequest("Fortitude", null, null, true)));
    }

    [Fact]
    public async Task Delete_of_a_protected_pericia_returns_400()
    {
        var token = await RegisterAuditorAsync("PerAud8", "peraud8@teste.com");

        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/pericias/32", token))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_hides_it_from_List_and_Restore_brings_it_back()
    {
        var token = await RegisterAuditorAsync("PerAud9", "peraud9@teste.com");
        var created = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Heráldica", null, null, false))))
            .Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();

        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{created!.Id}", token))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var ativas = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias", token))).Content.ReadFromJsonAsync<List<PericiaResponse>>();
        ativas!.Should().NotContain(p => p.Id == created.Id);
        var todas = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias/auditoria", token))).Content.ReadFromJsonAsync<List<PericiaAuditoriaResponse>>();
        todas!.Single(p => p.Id == created.Id).IsDeleted.Should().BeTrue();

        var restored = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/pericias/{created.Id}/restaurar", token));
        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        ativas = await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/pericias", token))).Content.ReadFromJsonAsync<List<PericiaResponse>>();
        ativas!.Should().Contain(p => p.Id == created.Id);
    }

    [Fact]
    public async Task Restore_when_an_active_pericia_already_has_that_name_returns_400()
    {
        var token = await RegisterAuditorAsync("PerAud10", "peraud10@teste.com");
        var first = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Genealogia", null, null, false))))
            .Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();
        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{first!.Id}", token));
        var second = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/pericias", token, new SalvarPericiaRequest("Genealogia", null, null, false))))
            .Content.ReadFromJsonAsync<PericiaAuditoriaResponse>();
        second!.Chave.Should().NotBe(first.Chave);

        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/pericias/{first.Id}/restaurar", token))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_id_returns_404()
    {
        var token = await RegisterAuditorAsync("PerAud11", "peraud11@teste.com");

        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/9999", token, new SalvarPericiaRequest("X", null, null, false)))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/pericias/9999", token))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
```
`RegisterAuditorAsync(nickname, email)` = register a GM and grant rules auditor exactly the way `DurabilidadesPorRankControllerTests` does, returning the token. The pericias table is shared across tests in the class (same container) — tests that create perícias use unique names, and never delete seeded perícias except where the test restores them.

Point-refund on delete is tested in Task 4 (needs the sheet-side changes); here Delete only needs to set `IsDeleted` and zero the skill rows — implement it now anyway (Step 4), it's one statement per table.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~PericiasControllerTests|FullyQualifiedName~PericiasMigrationTests"`
Expected: build FAIL.

- [ ] **Step 3: Entity, DbContext, migration**

`PericiaDefinicao.cs`:
```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>Uma perícia do sistema (Requisitos - Auditoria de Regras R0012). Nunca é apagada de verdade — IsDeleted.</summary>
public class PericiaDefinicao
{
    public int Id { get; set; }
    /// <summary>Identificador estável usado na API e no Alvo de Artefatos; nunca muda depois de criado.</summary>
    public required string Chave { get; set; }
    public required string Nome { get; set; }
    public string? Descricao { get; set; }
    public Atributo? AtributoSugerido { get; set; }
    public bool DisponivelParaCriaturas { get; set; }
    public bool IsDeleted { get; set; }
}
```
(`Atributo` lives in `RuinaRPG.Domain.CharacterSheets` — check with a grep and fix the using if not.)

DbContext: `public DbSet<PericiaDefinicao> Pericias => Set<PericiaDefinicao>();` and
```csharp
        builder.Entity<PericiaDefinicao>(entity =>
        {
            entity.ToTable("Pericias");
            entity.Property(p => p.Id).ValueGeneratedNever();
            entity.HasIndex(p => p.Chave).IsUnique();
            entity.HasIndex(p => p.Nome).IsUnique().HasFilter("\"IsDeleted\" = false");
        });
```

Generate: `dotnet ef migrations add AddPericias --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`
Then edit the generated `Up` to seed right after `CreateTable` (literal data so the migration never changes if `PericiasIniciais` does):
```csharp
            migrationBuilder.InsertData(
                table: "Pericias",
                columns: new[] { "Id", "Chave", "Nome", "Descricao", "AtributoSugerido", "DisponivelParaCriaturas", "IsDeleted" },
                values: new object?[,]
                {
                    { 0, "Acrobacia", "Acrobacia", null, null, true, false },
                    // … one line per row of PericiasIniciais.Todas, in Id order, 39 rows …
                    { 38, "Sobrevivencia", "Sobrevivência", null, null, true, false },
                });
```
Write all 39 lines out (copy from `PericiasIniciais`). The migration test compares them.

- [ ] **Step 4: Contracts + controller**

Contracts (namespace `RuinaRPG.Contracts.Rules`) exactly as in **Interfaces**.

`PericiasController.cs`:
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Auditoria de Perícias (Requisitos - Auditoria de Regras R0012). A lista ativa é aberta a qualquer
/// usuário autenticado (fichas e dropdowns); todo o resto exige o Auditor de Regras, conferido no banco.
/// Remover é lógico e devolve os pontos gastos: zera o Gasto da perícia em todas as fichas.
/// </summary>
[ApiController]
[Route("api/pericias")]
[Authorize]
public class PericiasController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PericiaResponse>>> List()
    {
        var ativas = await db.Pericias.Where(p => !p.IsDeleted).ToListAsync();
        return ativas.OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p => new PericiaResponse(p.Id, p.Chave, p.Nome, p.Descricao, p.AtributoSugerido?.ToString(), p.DisponivelParaCriaturas, PericiasDeSistema.IsProtegida(p.Id)))
            .ToList();
    }

    [HttpGet("auditoria")]
    public async Task<ActionResult<List<PericiaAuditoriaResponse>>> ListAuditoria()
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var todas = await db.Pericias.ToListAsync();
        return todas.OrderBy(p => p.Nome, StringComparer.CurrentCulture).Select(ToAuditoria).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<PericiaAuditoriaResponse>> Create(SalvarPericiaRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        if (await ValidateAsync(request, idAtual: null) is { } invalid)
            return invalid;

        var chaves = await db.Pericias.Select(p => p.Chave).ToListAsync();
        var proximoId = await db.Pericias.MaxAsync(p => p.Id) + 1;
        var pericia = new PericiaDefinicao
        {
            Id = proximoId,
            Chave = PericiaChave.Gerar(request.Nome, chaves),
            Nome = request.Nome.Trim(),
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            AtributoSugerido = ParseAtributo(request.AtributoSugerido),
            DisponivelParaCriaturas = request.DisponivelParaCriaturas,
        };
        db.Pericias.Add(pericia);
        await db.SaveChangesAsync();
        return Created($"/api/pericias/{pericia.Id}", ToAuditoria(pericia));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PericiaAuditoriaResponse>> Update(int id, SalvarPericiaRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        var pericia = await db.Pericias.FindAsync(id);
        if (pericia is null)
            return NotFound();
        if (await ValidateAsync(request, idAtual: id) is { } invalid)
            return invalid;

        pericia.Nome = request.Nome.Trim();
        pericia.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        pericia.AtributoSugerido = ParseAtributo(request.AtributoSugerido);
        // As protegidas entram nas fórmulas de Criatura também — não podem sair da lista de Criaturas.
        pericia.DisponivelParaCriaturas = PericiasDeSistema.IsProtegida(id) || request.DisponivelParaCriaturas;
        await db.SaveChangesAsync();
        return ToAuditoria(pericia);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        var pericia = await db.Pericias.FindAsync(id);
        if (pericia is null)
            return NotFound();
        if (PericiasDeSistema.IsProtegida(id))
            return BadRequest("Esta perícia entra em fórmulas e não pode ser removida.");

        await using var tx = await db.Database.BeginTransactionAsync();
        pericia.IsDeleted = true;
        await db.SaveChangesAsync();
        await ZerarGastoAsync(id);
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/restaurar")]
    public async Task<ActionResult<PericiaAuditoriaResponse>> Restore(int id)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        var pericia = await db.Pericias.FindAsync(id);
        if (pericia is null)
            return NotFound();
        if (await NomeEmUsoAsync(pericia.Nome, id))
            return BadRequest("Já existe uma perícia ativa com esse nome.");

        pericia.IsDeleted = false;
        await db.SaveChangesAsync();
        return ToAuditoria(pericia);
    }

    private async Task<ActionResult?> ValidateAsync(SalvarPericiaRequest request, int? idAtual)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest("Informe o nome da perícia.");
        if (request.AtributoSugerido is not null && ParseAtributo(request.AtributoSugerido) is null)
            return BadRequest("Atributo sugerido desconhecido.");
        if (await NomeEmUsoAsync(request.Nome.Trim(), idAtual))
            return BadRequest("Já existe uma perícia ativa com esse nome.");
        return null;
    }

    private Task<bool> NomeEmUsoAsync(string nome, int? excetoId) =>
        db.Pericias.AnyAsync(p => !p.IsDeleted && p.Id != excetoId && p.Nome.ToLower() == nome.ToLower());

    private static Atributo? ParseAtributo(string? valor) =>
        Enum.GetNames<Atributo>().Contains(valor) && Enum.TryParse<Atributo>(valor, out var a) ? a : null;

    // Task 3 converts the skill entities' Pericia to int PericiaId; until then compare through the enum.
    private async Task ZerarGastoAsync(int id)
    {
        var pericia = (Pericia)id;
        await db.CharacterSkills.Where(s => s.Pericia == pericia).ExecuteUpdateAsync(s => s.SetProperty(x => x.Gasto, 0));
        await db.NpcSkills.Where(s => s.Pericia == pericia).ExecuteUpdateAsync(s => s.SetProperty(x => x.Gasto, 0));
        await db.CreatureSkills.Where(s => s.Pericia == pericia).ExecuteUpdateAsync(s => s.SetProperty(x => x.Gasto, 0));
    }

    private static PericiaAuditoriaResponse ToAuditoria(PericiaDefinicao p) =>
        new(p.Id, p.Chave, p.Nome, p.Descricao, p.AtributoSugerido?.ToString(), p.DisponivelParaCriaturas, PericiasDeSistema.IsProtegida(p.Id), p.IsDeleted);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run tests** — Step 2 command → PASS. `dotnet build` → 0/0.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat(api): tabela de Perícias com auditoria (criar, editar, remover e restaurar)"
```

---

### Task 3: Replace the `Pericia` enum with the table everywhere

A compile-driven refactor. Behaviour must not change for any seeded perícia: the whole existing test suite is the regression net.

**Files (every file referencing the enum — re-run `grep -rn "\bPericia\b\|PericiaLabels\|CreatureSkillAllowList\|PericiaDisplay" src tests --include=*.cs --include=*.razor | grep -v Migrations` to confirm the list):**
- Delete: `src/RuinaRPG.Domain/CharacterSheets/Pericia.cs`, `PericiaLabels.cs`, `src/RuinaRPG.Domain/CreatureSheets/CreatureSkillAllowList.cs`, `src/RuinaRPG.Client/Shared/PericiaDisplay.cs`, `tests/RuinaRPG.Tests.Unit/CreatureSheets/CreatureSkillAllowListTests.cs`, `tests/RuinaRPG.Tests.Client/Shared/PericiaDisplayTests.cs`, and the first test of `PericiasIniciaisTests` (the enum cross-check)
- Create: `src/RuinaRPG.Infrastructure/Rules/PericiaCatalogo.cs`, `src/RuinaRPG.Client/Services/PericiaCatalogo.cs`, `tests/RuinaRPG.Tests.Client/Services/PericiaCatalogoTests.cs`
- Modify Domain: `HistoricoBonusCalculator.cs`, `SpellsAndAbilities/RequisitosDePassiva.cs`, `FichaParaRequisitos.cs`, `PassivaRequisitosEvaluator.cs`, `Rules/HistoricoSeed.cs`, `Rules/HistoricoSeedParser.cs`, `Items/TipoDeAlvo.cs` (comment only, if it names the enum)
- Modify Infrastructure: the 6 skill/mastery entities, `Rules/Historico.cs`, `Rules/HistoricoSeeder.cs`, `Rules/RulebookRenderer.cs`, `RuinaRpgDbContext.cs`, new migration `PericiasComoTabela`
- Modify Api: `Program.cs` (DI), `CharacterSkillsController`, `NpcSkillsController`, `CreatureSkillsController`, the 3 `*MasteriesController`, `CharacterSheetsController`, `NpcSheetsController`, `CreatureSheetsController`, `CampaignGrantsController`, `HistoricosController`, `RequisitosDePassivaMapper`, `PericiasController` (drop the enum cast), `Services/CharacterSheetStats.cs`, `NpcSheetStats.cs`, `CreatureSheetStats.cs`
- Modify Client: `Program.cs` (DI), `FichaDePersonagem.razor`, `FichaDeNpc.razor`, `FichaDeCriatura.razor`, `AuditoriaHistoricos.razor`, `CatalogoItemForm.razor`, `Shared/Fields/RequisitosDePassivaEditor.razor`
- Modify tests that used the enum (unit: `PassivaRequisitosEvaluatorTests`, `HistoricoBonusCalculatorTests`, `HistoricoSeederTests`, `HistoricoSeedParserTests`; integration: `CharacterMasteriesControllerTests`, `SheetStatsTests`, `RulebookControllerTests`, `CreatureSheetsControllerTests`, and the `Persistence/*MigrationTests` that set `Pericia = Pericia.X`) — replace `Pericia.X` with the literal id from `PericiasIniciais` (e.g. `Pericia.Atletismo` → `7`, or `PericiasIniciais.Todas.Single(p => p.Chave == "Atletismo").Id` where readability matters).

**Interfaces:**
- Consumes: Tasks 1–2.
- Produces:
  ```csharp
  // Infrastructure.Rules — registered AddScoped<IPericiaCatalogo, PericiaCatalogo>()
  public interface IPericiaCatalogo
  {
      Task<IReadOnlyList<PericiaDefinicao>> TodasAsync();          // all rows incl. removed, cached per request
      Task<PericiaDefinicao?> AtivaPorChaveAsync(string chave);     // null if unknown or removed
      Task<IReadOnlyDictionary<int, PericiaDefinicao>> PorIdAsync(); // all rows incl. removed
  }
  // Entities: CharacterSkill/NpcSkill/CreatureSkill/CharacterMastery/NpcMastery/CreatureMastery: int PericiaId (FK → Pericias, Restrict)
  // Historico: int PericiaMaisSeisId, int PericiaMaisTresId (FKs, Restrict)
  // Domain
  public static int HistoricoBonusCalculator.For(int periciaId, int? periciaMaisSeisId, int? periciaMaisTresId);
  public sealed record RequisitoDePericia(int Pericia, int Minimo); // JSON name "Pericia" kept
  public sealed record HistoricoSeed(string Nome, string Descricao, int PericiaMaisSeisId, int PericiaMaisTresId);
  // FichaParaRequisitos.Pericias: IReadOnlyDictionary<int, int?>
  // Client — registered AddScoped<PericiaCatalogo>()
  public class PericiaCatalogo(HttpClient http)
  {
      Task CarregarAsync();                              // GET pericias once; no-op if loaded
      IReadOnlyList<PericiaResponse> Ativas { get; }
      string Label(string chave);                        // Nome, or the chave itself if unknown
      string? Descricao(string chave);
  }
  ```

- [ ] **Step 1: Write the failing client catalog test** (`tests/RuinaRPG.Tests.Client/Services/PericiaCatalogoTests.cs`)

```csharp
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class PericiaCatalogoTests
{
    private static PericiaCatalogo Create(out Func<int> chamadas)
    {
        var count = 0;
        chamadas = () => count;
        var http = FakeHttpMessageHandler.CreateClient(_ =>
        {
            count++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<PericiaResponse>
            {
                new(4, "ArmasBrancas", "Armas Brancas", "Lâminas.", "Forca", false, false),
            }) };
        });
        return new PericiaCatalogo(http);
    }

    [Fact]
    public async Task Label_and_Descricao_come_from_the_api_and_load_only_once()
    {
        var catalogo = Create(out var chamadas);

        await catalogo.CarregarAsync();
        await catalogo.CarregarAsync();

        catalogo.Label("ArmasBrancas").Should().Be("Armas Brancas");
        catalogo.Descricao("ArmasBrancas").Should().Be("Lâminas.");
        chamadas().Should().Be(1);
    }

    [Fact]
    public async Task Unknown_key_falls_back_to_the_key_itself()
    {
        var catalogo = Create(out _);
        await catalogo.CarregarAsync();

        catalogo.Label("Inexistente").Should().Be("Inexistente");
        catalogo.Descricao("Inexistente").Should().BeNull();
    }
}
```
(Check `FakeHttpMessageHandler.CreateClient` sets a `BaseAddress`; the catalog calls the relative URL `"pericias"` like every other client call.)

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~PericiaCatalogoTests` → build FAIL.

- [ ] **Step 2: Client catalog**

`src/RuinaRPG.Client/Services/PericiaCatalogo.cs`:
```csharp
using System.Net.Http.Json;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Client.Services;

/// <summary>
/// Perícias ativas (GET api/pericias), carregadas uma vez por sessão do app. Substitui o antigo
/// PericiaDisplay estático: nomes e descrições agora vêm da Auditoria de Perícias. A Chave continua
/// sendo o valor que os endpoints de ficha recebem e devolvem.
/// </summary>
public class PericiaCatalogo(HttpClient http)
{
    private List<PericiaResponse>? _ativas;

    public IReadOnlyList<PericiaResponse> Ativas => _ativas ?? (IReadOnlyList<PericiaResponse>)Array.Empty<PericiaResponse>();

    public async Task CarregarAsync() =>
        _ativas ??= await http.GetFromJsonAsync<List<PericiaResponse>>("pericias") ?? new();

    /// <summary>Recarrega após uma edição na Auditoria.</summary>
    public async Task RecarregarAsync()
    {
        _ativas = null;
        await CarregarAsync();
    }

    public string Label(string chave) => _ativas?.FirstOrDefault(p => p.Chave == chave)?.Nome ?? chave;

    public string? Descricao(string chave) => _ativas?.FirstOrDefault(p => p.Chave == chave)?.Descricao;
}
```
Register in `src/RuinaRPG.Client/Program.cs`: `builder.Services.AddScoped<PericiaCatalogo>();` (next to the other `AddScoped` registrations; check the namespace of existing services and match it).

- [ ] **Step 3: Server catalog**

`src/RuinaRPG.Infrastructure/Rules/PericiaCatalogo.cs`:
```csharp
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

public interface IPericiaCatalogo
{
    Task<IReadOnlyList<PericiaDefinicao>> TodasAsync();
    Task<PericiaDefinicao?> AtivaPorChaveAsync(string chave);
    Task<IReadOnlyDictionary<int, PericiaDefinicao>> PorIdAsync();
}

/// <summary>Perícias carregadas uma vez por requisição (scoped) — a tabela tem ~40 linhas.</summary>
public class PericiaCatalogo(RuinaRpgDbContext db) : IPericiaCatalogo
{
    private List<PericiaDefinicao>? _todas;

    public async Task<IReadOnlyList<PericiaDefinicao>> TodasAsync() =>
        _todas ??= await db.Pericias.AsNoTracking().ToListAsync();

    public async Task<PericiaDefinicao?> AtivaPorChaveAsync(string chave) =>
        (await TodasAsync()).FirstOrDefault(p => !p.IsDeleted && p.Chave == chave);

    public async Task<IReadOnlyDictionary<int, PericiaDefinicao>> PorIdAsync() =>
        (await TodasAsync()).ToDictionary(p => p.Id);
}
```
Register in `src/RuinaRPG.Api/Program.cs` next to the other scoped rules services: `builder.Services.AddScoped<IPericiaCatalogo, PericiaCatalogo>();`

- [ ] **Step 4: Entities + migration**

In the 6 skill/mastery entities replace `public Pericia Pericia { get; set; }` with `public int PericiaId { get; set; }`. In `Historico` replace the two properties with `public int PericiaMaisSeisId { get; set; }` / `public int PericiaMaisTresId { get; set; }`.
DbContext: update the three unique indexes (`new { s.CharacterSheetId, s.PericiaId }` etc.), and for each of the 6 entities plus `Historico` add
```csharp
            entity.HasOne<PericiaDefinicao>().WithMany().HasForeignKey(s => s.PericiaId).OnDelete(DeleteBehavior.Restrict);
```
(for `Historico`, two of them, on `PericiaMaisSeisId` and `PericiaMaisTresId`).

Generate: `dotnet ef migrations add PericiasComoTabela --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`
Inspect: it must contain only `RenameColumn` (`Pericia`→`PericiaId` ×6, `PericiaMaisSeis`→`PericiaMaisSeisId`, `PericiaMaisTres`→`PericiaMaisTresId`), index renames, and `AddForeignKey`s. If EF emitted `DropColumn`/`AddColumn` instead of `RenameColumn`, rewrite those as `RenameColumn` by hand — data must survive.

- [ ] **Step 5: Domain**

- `HistoricoBonusCalculator.For(int periciaId, int? periciaMaisSeisId, int? periciaMaisTresId)` — same body.
- `RequisitoDePericia(int Pericia, int Minimo)` — keep the property name `Pericia` (jsonb compatibility).
- `FichaParaRequisitos`: `IReadOnlyDictionary<int, int?> Pericias`.
- `PassivaRequisitosEvaluator` line ~59: the pending message needs a label; change the evaluator to take a `Func<int, string> nomeDaPericia` parameter (or an `IReadOnlyDictionary<int,string>`) and pass it from callers. Grep callers of `PassivaRequisitosEvaluator.` and thread the names from `IPericiaCatalogo.PorIdAsync()`.
- `HistoricoSeed(string Nome, string Descricao, int PericiaMaisSeisId, int PericiaMaisTresId)`; `HistoricoSeedParser` uses `PericiasIniciais.Todas.ToDictionary(p => p.Nome, p => p.Id)`.

- [ ] **Step 6: Api — mechanical rules**

Apply to every controller/service in the file list:
1. `Enum.GetValues<Pericia>()` at sheet creation (`CharacterSheetsController:46`, `NpcSheetsController`, `CampaignGrantsController:139`) → `(await pericias.TodasAsync()).Where(p => !p.IsDeleted)`, creating rows with `PericiaId = p.Id, AtributoEscolhido = p.AtributoSugerido`. Inject `IPericiaCatalogo pericias` into the controller's primary constructor.
2. `CreatureSkillAllowList.AllowedPericias` (`CreatureSheetsController:40`, `CampaignGrantsController:153`) → same, filtered `p.DisponivelParaCriaturas`.
3. Route binding `Pericia pericia` in `*SkillsController.Update` → `string pericia`; resolve `var def = await pericias.AtivaPorChaveAsync(pericia); if (def is null) return NotFound();` then use `def.Id`. `CreatureSkillsController`'s allow-list check becomes `if (!def.DisponivelParaCriaturas) return BadRequest(...)` (keep the existing message).
4. `Enum.TryParse<Pericia>(request.Pericia, …)` in `*MasteriesController` and `HistoricosController` → `AtivaPorChaveAsync`; unknown/removed → the same 400 message as today.
5. Output `s.Pericia.ToString()` / `m.Pericia.ToString()` / `h.PericiaMaisSeis.ToString()` → the row's `Chave` via `var porId = await pericias.PorIdAsync();` then `porId[s.PericiaId].Chave`.
6. `ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, s.Pericia.ToString())` → `…, porId[s.PericiaId].Chave)` — Artefato `Alvo` stores the Chave.
7. `Pericia.Prontidao/Reflexos/Fortitude` in the three `*SheetStats` → `PericiasDeSistema.Prontidao` etc.; `BrutoOf(Pericia pericia)` → `BrutoOf(int periciaId)`.
8. `OrderBy(s => s.Pericia)` → `OrderBy(s => s.PericiaId)` (Task 4 changes list ordering to Nome).
9. `RequisitosDePassivaMapper`: `TryList<Pericia>` can't parse a table value — resolve each `RequisitoMinimoDto.Alvo` with the catalog (`AtivaPorChaveAsync`, unknown → the same "Perícia inválida" error). The mapper is static today; give the Perícia part an `IReadOnlyList<PericiaDefinicao>` parameter and have its callers pass `await pericias.TodasAsync()`. Output: `porId[p.Pericia].Chave`.
10. `PericiasController.ZerarGastoAsync`: replace the enum cast with `s.PericiaId == id`.
11. `RulebookRenderer.BuildHistoricosAsync`: load `db.Pericias` into a dictionary and use `.Nome` instead of `PericiaLabels.Label(...)`.

- [ ] **Step 7: Client — mechanical rules**

1. `@inject PericiaCatalogo Pericias` in `FichaDePersonagem`, `FichaDeNpc`, `FichaDeCriatura`, `AuditoriaHistoricos`, `CatalogoItemForm`, `RequisitosDePassivaEditor`; call `await Pericias.CarregarAsync();` at the start of `OnInitializedAsync` (for `RequisitosDePassivaEditor`, in `OnInitializedAsync` of the component itself).
2. `PericiaDisplay.Label(x)` → `Pericias.Label(x)`.
3. `AuditoriaHistoricos`' static `Pericias` string array → `Pericias.Ativas.Select(p => p.Chave)`; the new-form defaults `"Acrobacia"`/`"Alquimia"` stay (valid seeded keys). Rename the injected field if it clashes with the existing `Pericias` member.
4. `CatalogoItemForm` line ~357 `"Pericia" => Enum.GetValues<Pericia>().Select(p => p.ToString())` → `"Pericia" => Pericias.Ativas.Select(p => p.Chave)`. If the dropdown shows raw values, show `Pericias.Label(v)` as the item text.
5. `RequisitosDePassivaEditor`'s static `PericiaOpcoes` → instance property `Pericias.Ativas.Select(p => (p.Chave, p.Nome)).ToArray()`.
6. bUnit tests rendering these pages/components now need `PericiaCatalogo` in DI and a `GET pericias` response from their fake handler: add `Services.AddScoped<PericiaCatalogo>()` and make the fake handler answer `.../pericias` with a small `List<PericiaResponse>` (e.g. `Atletismo`, `Acrobacia`, `Alquimia`). Fix each failing client test this way — don't weaken assertions.

- [ ] **Step 8: Tests that used the enum**

Update the test files listed under **Files** (replace `Pericia.X` with its seeded id; `PericiaLabels.Label(Pericia.X)` with the seeded `Nome` literal). Delete the enum-cross-check test in `PericiasIniciaisTests` (keep the other two).

- [ ] **Step 9: Verify the whole suite**

Run: `dotnet build` → 0/0.
Run: `dotnet test tests/RuinaRPG.Tests.Unit` → PASS.
Run: `dotnet test tests/RuinaRPG.Tests.Client` → PASS.
Run the integration suite in batches (≤ 8 classes per `--filter`), at minimum every class whose name contains `Skill`, `Master`, `Sheet`, `Historico`, `Passiva`, `SpellAbilit`, `Rulebook`, `Grant`, `Items`, `Pericias`, `Migration`. All PASS (rerun isolated before calling a timeout real).

- [ ] **Step 10: Commit**

```bash
git add -A src tests
git commit -m "refactor: perícias vêm da tabela Pericias em vez do enum"
```

---

### Task 4: Sheet behaviour — new, removed and suggested perícias

**Files:**
- Modify: `CharacterSkillsController.cs`, `NpcSkillsController.cs`, `CreatureSkillsController.cs` (List, Budget, Update), the 3 `*MasteriesController.cs` (List/ComputeTotal), `*SheetStats.cs` (Passiva evaluation dictionary), `RequisitosDePassivaMapper` output
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSkillResponse.cs`, `NpcSheets/NpcSkillResponse.cs`, `CreatureSheets/CreatureSkillResponse.cs` — add `string Nome, string? Descricao` **at the end** (positional records: append to avoid reordering)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/PericiasSheetBehaviourTests.cs`

**Interfaces:**
- Consumes: `IPericiaCatalogo`, `PericiasController` routes (Tasks 2–3).
- Produces: `CharacterSkillResponse(string Pericia, int Gasto, int Modificador, string? AtributoEscolhido, int? Total, bool ContaHistorico, string Nome, string? Descricao)`; `NpcSkillResponse`/`CreatureSkillResponse` likewise gain `Nome, Descricao` at the end. Skill lists are ordered by `Nome` (culture-aware).

Rules:
- **List** = every *active* perícia (Creature: active **and** `DisponivelParaCriaturas`). For a perícia with no row on the sheet: `Gasto 0`, `AtributoEscolhido = AtributoSugerido`. Rows of removed perícias are skipped.
- **Row with `AtributoEscolhido == null`** → treat as `AtributoSugerido` for the Total.
- **Update** upserts: if the sheet has no row for that active perícia, create it.
- **Budget** sums `Gasto` over rows of active perícias only.
- **Histórico** bonus: if the Histórico's perícia is removed it simply isn't in the list (no leak).
- **Masteries**: list skips masteries whose perícia is removed; `ComputeTotal` uses `FirstOrDefault` on the skill row (Gasto 0 when missing).
- **Passiva requisites**: the `FichaParaRequisitos.Pericias` dictionary only contains active perícias, so a requisite on a removed one is ignored (evaluator's existing `TryGetValue` skip). When a Passiva's requisites are returned to the client, requisites on removed perícias are omitted.

- [ ] **Step 1: Write the failing integration tests** (`PericiasSheetBehaviourTests` — reuse the helpers from `CharacterSheetsControllerTests`: `RegisterGmAndGetTokenAsync`, `RegisterJogadorLinkedToAsync`, `CreateCampaignAsync`, `CreateSheetForMemberAsync`, `AddCampaignMemberRequest`, `ValidUpdate()`; from `NpcSheetsControllerTests` `CreateSheetAsync`; from `CreatureSheetsControllerTests` its create helper; and the auditor helper from Task 2. Copy them into this class.)

```csharp
    [Fact]
    public async Task A_pericia_created_after_the_sheet_is_listed_with_zero_and_its_suggested_attribute_and_can_be_saved()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud1", "persheetaud1@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet1");
        var nova = await CreatePericiaAsync(auditor, "Esgrima Élfica", "Destreza", criaturas: false);

        var skills = await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token);
        var linha = skills.Single(s => s.Pericia == nova.Chave);
        linha.Gasto.Should().Be(0);
        linha.AtributoEscolhido.Should().Be("Destreza");
        linha.Nome.Should().Be("Esgrima Élfica");

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/{nova.Chave}", token, new UpdateCharacterSkillRequest(2, "Destreza")));
        put.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(2);
    }

    [Fact]
    public async Task Removing_a_pericia_refunds_its_points_hides_it_and_restoring_brings_it_back_at_zero()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud2", "persheetaud2@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet2");
        var nova = await CreatePericiaAsync(auditor, "Falcoaria", null, criaturas: false);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/{nova.Chave}", token, new UpdateCharacterSkillRequest(3, null)));
        var antes = await GetAsync<SkillPointBudgetResponse>($"/api/character-sheets/{sheetId}/skills/budget", token);

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{nova.Id}", auditor));

        var depois = await GetAsync<SkillPointBudgetResponse>($"/api/character-sheets/{sheetId}/skills/budget", token);
        depois.GastoTotal.Should().Be(antes.GastoTotal - 3);
        (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Should().NotContain(s => s.Pericia == nova.Chave);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/{nova.Chave}", token, new UpdateCharacterSkillRequest(1, null))))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/pericias/{nova.Id}/restaurar", auditor));
        (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(0);
    }

    [Fact]
    public async Task Removal_zeroes_the_pericia_on_npc_and_creature_sheets_too()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud3", "persheetaud3@teste.com");
        var nova = await CreatePericiaAsync(auditor, "Domar Feras", null, criaturas: true);
        var npcId = await CreateNpcSheetAsync(auditor);
        var creatureId = await CreateCreatureSheetAsync(auditor);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{npcId}/skills/{nova.Chave}", auditor, new UpdateNpcSkillRequest(4, null)));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}/skills/{nova.Chave}", auditor, new UpdateCreatureSkillRequest(4, null)));

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{nova.Id}", auditor));
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/pericias/{nova.Id}/restaurar", auditor));

        (await GetAsync<List<NpcSkillResponse>>($"/api/npc-sheets/{npcId}/skills", auditor)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(0);
        (await GetAsync<List<CreatureSkillResponse>>($"/api/creature-sheets/{creatureId}/skills", auditor)).Single(s => s.Pericia == nova.Chave).Gasto.Should().Be(0);
    }

    [Fact]
    public async Task Creature_sheets_only_list_pericias_available_for_criaturas()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud4", "persheetaud4@teste.com");
        var sim = await CreatePericiaAsync(auditor, "Rugir", null, criaturas: true);
        var nao = await CreatePericiaAsync(auditor, "Contabilidade", null, criaturas: false);
        var creatureId = await CreateCreatureSheetAsync(auditor);

        var skills = await GetAsync<List<CreatureSkillResponse>>($"/api/creature-sheets/{creatureId}/skills", auditor);

        skills.Should().Contain(s => s.Pericia == sim.Chave);
        skills.Should().NotContain(s => s.Pericia == nao.Chave);
        (await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/creature-sheets/{creatureId}/skills/{nao.Chave}", auditor, new UpdateCreatureSkillRequest(1, null))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Skill_list_shows_the_description_and_is_ordered_by_name()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud5", "persheetaud5@teste.com");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/7", auditor, new SalvarPericiaRequest("Atletismo", "Correr, saltar, escalar.", null, true)));
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet5");

        var skills = await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token);

        skills.Single(s => s.Pericia == "Atletismo").Descricao.Should().Be("Correr, saltar, escalar.");
        skills.Select(s => s.Nome).Should().BeInAscendingOrder(StringComparer.CurrentCulture);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, "/api/pericias/7", auditor, new SalvarPericiaRequest("Atletismo", null, null, true)));
    }

    [Fact]
    public async Task A_mastery_on_a_removed_pericia_is_hidden()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud6", "persheetaud6@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet6");
        var nova = await CreatePericiaAsync(auditor, "Tecelagem", null, criaturas: false);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/masteries", token, new AddCharacterMasteryRequest("Tear Rápido", nova.Chave, "Destreza", 1)));

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/pericias/{nova.Id}", auditor));

        var masteries = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/masteries", token));
        masteries.StatusCode.Should().Be(HttpStatusCode.OK);
        (await masteries.Content.ReadFromJsonAsync<List<CharacterMasteryResponse>>())!.Should().NotContain(m => m.Pericia == nova.Chave);
    }

    [Fact]
    public async Task A_legacy_artefato_targeting_ArmasBrancas_still_adds_its_bonus()
    {
        var auditor = await RegisterAuditorAsync("PerSheetAud7", "persheetaud7@teste.com");
        var (sheetId, token) = await CreateCharacterSheetAsync(auditor, "PerSheet7");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/skills/ArmasBrancas", token, new UpdateCharacterSkillRequest(0, "Forca")));
        var antes = (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Single(s => s.Pericia == "ArmasBrancas").Total;

        // Same CreateItemRequest shape as CharacterSheetsControllerTests.CreateDanoArtefatoItemAsync,
        // with TipoDeAlvo "Pericia" and Alvo "ArmasBrancas" (the legacy enum name = seeded Chave).
        var item = await (await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/items", auditor,
            new CreateItemRequest("Artefato", "Anel do Duelista", 0.1m, 500, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, "Pericia", "ArmasBrancas", 2, null)))).Content.ReadFromJsonAsync<ItemResponse>();
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/artifacts", token, new AddCharacterArtifactRequest(item!.Id)));

        var depois = (await GetAsync<List<CharacterSkillResponse>>($"/api/character-sheets/{sheetId}/skills", token)).Single(s => s.Pericia == "ArmasBrancas").Total;
        depois.Should().Be(antes + 2);
    }
```
If `CreateItemRequest`'s positional shape has changed since this plan was written, copy the current `CreateDanoArtefatoItemAsync` call and change only the last four arguments. Helpers to add in this class: `CreateCharacterSheetAsync(gmToken, prefix)` → `(sheetId, playerToken)` built from the CharacterSheetsControllerTests helpers; `CreatePericiaAsync(auditorToken, nome, atributo, criaturas)` → `PericiaAuditoriaResponse`; `CreateNpcSheetAsync`, `CreateCreatureSheetAsync`; `GetAsync<T>(url, token)`. Check the exact request record names (`UpdateCharacterSkillRequest`, `UpdateNpcSkillRequest`, `UpdateCreatureSkillRequest`, `SkillPointBudgetResponse` fields) in `src/RuinaRPG.Contracts` and adjust.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~PericiasSheetBehaviourTests` → FAIL (missing `Nome`/`Descricao`, missing rows, 500 on upsert).

- [ ] **Step 3: Implement the rules above** in the three skill controllers (List/Budget/Update), the three masteries controllers and the Passiva evaluation dictionaries. Sketch for `CharacterSkillsController.List`:
```csharp
        var porId = await pericias.PorIdAsync();
        var ativas = porId.Values.Where(p => !p.IsDeleted).ToList();
        var linhas = await db.CharacterSkills.Where(s => s.CharacterSheetId == sheetId).ToDictionaryAsync(s => s.PericiaId);

        return ativas
            .OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p =>
            {
                linhas.TryGetValue(p.Id, out var s);
                var gasto = s?.Gasto ?? 0;
                var atributo = s?.AtributoEscolhido ?? p.AtributoSugerido;
                var historicoBonus = HistoricoBonusCalculator.For(p.Id, historico?.PericiaMaisSeisId, historico?.PericiaMaisTresId);
                var modificador = SkillFormulas.Modificador(gasto, historicoBonus);
                var total = atributo is not null && attributeTotals.TryGetValue(atributo.Value, out var atributoTotal)
                    ? SkillFormulas.Total(modificador, atributoTotal, ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Pericia, p.Chave))
                    : (int?)null;
                return new CharacterSkillResponse(p.Chave, gasto, modificador, atributo?.ToString(), total, historicoBonus > 0, p.Nome, p.Descricao);
            })
            .ToList();
```
`Update` upsert:
```csharp
        var def = await pericias.AtivaPorChaveAsync(pericia);
        if (def is null)
            return NotFound();
        var skill = await db.CharacterSkills.SingleOrDefaultAsync(s => s.CharacterSheetId == sheetId && s.PericiaId == def.Id);
        if (skill is null)
        {
            skill = new CharacterSkill { Id = Guid.NewGuid(), CharacterSheetId = sheetId, PericiaId = def.Id };
            db.CharacterSkills.Add(skill);
        }
```
`Budget`: `SumAsync` over `db.CharacterSkills.Where(s => s.CharacterSheetId == sheetId && !db.Pericias.Any(p => p.Id == s.PericiaId && p.IsDeleted))`.
Mirror in Npc/Creature (Creature list also filters `p.DisponivelParaCriaturas`; Creature has no Histórico — bonus 0). Apply the same "active only" filter to the `*SheetStats` dictionaries built for Passiva requisites (`skills.ToDictionary(...)` → only rows whose perícia is active, keyed by `PericiaId`).

- [ ] **Step 4: Run tests** — Step 2 command → PASS; then the skills/masteries/sheet/passiva integration classes in batches → PASS. `dotnet build` → 0/0 (the Client compiles against the new positional response records — append-only, so existing client code keeps compiling).

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(api): fichas mostram perícias novas, escondem removidas e usam o atributo sugerido"
```

---

### Task 5: Client — perícia name with description (tooltip / tap popup)

**Files:**
- Create: `src/RuinaRPG.Client/Shared/PericiaNome.razor`
- Modify: `FichaDePersonagem.razor` (~line 272), `FichaDeNpc.razor` (~246), `FichaDeCriatura.razor` (~191) skill-table name cells
- Test: `tests/RuinaRPG.Tests.Client/Shared/PericiaNomeTests.cs`

**Interfaces:**
- Consumes: `CharacterSkillResponse.Nome/Descricao` etc. (Task 4).
- Produces: `<PericiaNome Nome="string" Descricao="string?" />`.

Behaviour: no description → plain text. With description → the name is wrapped in a `MudTooltip` (hover, desktop) **and** is a button-like element that, when clicked/tapped, opens a `MudDialog` with the name as title and the description as body (mobile has no hover). A small `Icons.Material.Outlined.Info` icon (size small, `Color.Default`) follows the name to signal it's explained.

- [ ] **Step 1: Failing bUnit tests**

```csharp
using Bunit;
using Bunit.Rendering;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class PericiaNomeTests : MudBunitContext
{
    private IRenderedComponent<ContainerFragment> RenderNome(string nome, string? descricao) =>
        Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<MudDialogProvider>(1);
            builder.CloseComponent();
            builder.OpenComponent<PericiaNome>(2);
            builder.AddAttribute(3, nameof(PericiaNome.Nome), nome);
            builder.AddAttribute(4, nameof(PericiaNome.Descricao), descricao);
            builder.CloseComponent();
        });

    [Fact]
    public void Without_description_renders_plain_text_and_no_tooltip()
    {
        var cut = RenderNome("Atletismo", null);

        cut.Markup.Should().Contain("Atletismo");
        cut.FindComponents<MudTooltip>().Should().BeEmpty();
        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void With_description_has_a_tooltip_with_the_text()
    {
        var cut = RenderNome("Atletismo", "Correr e saltar.");

        cut.FindComponent<MudTooltip>().Instance.Text.Should().Be("Correr e saltar.");
    }

    [Fact]
    public void Clicking_the_name_opens_a_popup_with_the_description()
    {
        var cut = RenderNome("Atletismo", "Correr e saltar.");

        cut.Find("button[aria-label='Descrição de Atletismo']").Click();

        cut.FindAll(".mud-dialog").Should().ContainSingle();
        cut.Find(".mud-dialog").TextContent.Should().Contain("Correr e saltar.");
    }
}
```

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~PericiaNomeTests` → build FAIL.

- [ ] **Step 2: Implement**

```razor
@using MudBlazor

@* Nome de uma perícia na ficha. Com Descrição (Auditoria de Perícias): tooltip ao passar o mouse
   (desktop) e popup ao tocar/clicar (mobile não tem hover). *@
@if (string.IsNullOrWhiteSpace(Descricao))
{
    <span>@Nome</span>
}
else
{
    <MudTooltip Text="@Descricao">
        <button type="button" class="rr-pericia-nome" aria-label="@($"Descrição de {Nome}")" @onclick="@(() => _open = true)">
            @Nome <MudIcon Icon="@Icons.Material.Outlined.Info" Size="Size.Small" Style="vertical-align:middle;" />
        </button>
    </MudTooltip>

    <MudDialog @bind-Visible="_open">
        <TitleContent>@Nome</TitleContent>
        <DialogContent>@Descricao</DialogContent>
        <DialogActions>
            <MudButton OnClick="@(() => _open = false)">Fechar</MudButton>
        </DialogActions>
    </MudDialog>
}

@code {
    [Parameter, EditorRequired] public string Nome { get; set; } = "";
    [Parameter] public string? Descricao { get; set; }

    private bool _open;
}
```
Add to `src/RuinaRPG.Client/wwwroot/css/components.css` (or the app's shared stylesheet — find where `.rr-*` classes live):
```css
.content .rr-pericia-nome {
    background: none;
    border: 0;
    padding: 0;
    color: inherit;
    font: inherit;
    cursor: help;
    text-align: left;
}
```
(Scoped under `.content` like the other element rules; the bare `.content button` rule there would otherwise restyle it — check specificity so this wins: `.content .rr-pericia-nome` (0,2,0) beats `.content button` (0,1,1).)

In the three sheets, replace the name cell `<td>@Pericias.Label(skill.Pericia)</td>` (after Task 3) with `<td><PericiaNome Nome="@skill.Nome" Descricao="@skill.Descricao" /></td>`.

- [ ] **Step 3: Run tests** — `dotnet test tests/RuinaRPG.Tests.Client` → PASS. `dotnet build` → 0/0.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat(client): descrição da perícia em tooltip e popup nas fichas"
```

---

### Task 6: Client — `/auditoria/pericias` page

**Files:**
- Create: `src/RuinaRPG.Client/Pages/AuditoriaPericias.razor`
- Modify: `src/RuinaRPG.Client/Layout/RulesAuditorNavLinks.razor` (add link after Históricos)
- Test: `tests/RuinaRPG.Tests.Client/Pages/AuditoriaPericiasTests.cs`; update `tests/RuinaRPG.Tests.Client/Layout/RulesAuditorNavLinksTests.cs` if it asserts the exact link list

**Interfaces:**
- Consumes: Task 2 routes/DTOs; `PericiaCatalogo.RecarregarAsync()` (Task 3) — call after every successful write so the rest of the app sees the change.

Page layout (follow `AuditoriaDurabilidadePorRank.razor` / `AuditoriaHistoricos.razor` for structure, breadcrumbs, `DismissibleAlert`, `Section` + `TitleInfo`):
- `Section Title="Perícias"` with InfoPopup "Como funciona a Auditoria de Perícias":
  > Aqui você adiciona, edita e remove as perícias usadas em todas as fichas. O Atributo sugerido só vem pré-selecionado numa ficha nova — o jogador pode trocar a qualquer momento, pois o GM pode pedir uma perícia com outro atributo numa ação específica. A Descrição aparece ao passar o mouse sobre o nome da perícia na ficha (no celular, ao tocar no nome). "Disponível para Criaturas" controla se a perícia aparece nas fichas de Criatura. Prontidão, Reflexos e Fortitude (ícone de cadeado) entram em fórmulas: não podem ser removidas nem retiradas das Criaturas, mas podem ser renomeadas.
- A "Nova perícia" form row: Nome, Descrição, Atributo sugerido (`MudSelect` with "—" = null + the 8 atributos via `AtributoDisplay`), Disponível para Criaturas (checkbox), button "Adicionar" (`Icons.Material.Filled.Add`).
- `MudSimpleTable` of active perícias: Nome (text field), Descrição (multiline), Atributo sugerido (select), Criaturas (checkbox, disabled for protected), actions: protected → `MudIcon Icons.Material.Filled.Lock` with `title="Usada em fórmulas — não pode ser removida"`; others → `MudIconButton Icons.Material.Filled.Delete` `aria-label="Remover {Nome}"`. Each field saves on change via `PUT api/pericias/{id}` sending the whole row.
- Remove asks for confirmation in a `MudDialog`: "Remover {Nome}? Os pontos gastos nesta perícia serão devolvidos em todas as fichas." Confirm → `DELETE`.
- `Section Title="Removidas"` (only if any) with InfoPopup:
  > Remover uma perícia devolve os pontos gastos nela ao saldo de Pontos de Perícia de todas as fichas, para serem redistribuídos. Enquanto removida, ela some das fichas, e os bônus de Histórico, requisitos de Passiva e Maestrias ligados a ela deixam de valer. Ao restaurar, ela volta zerada em todas as fichas.
  Rows: Nome + button "Restaurar" (`Icons.Material.Filled.Restore`).
- Errors from the API (400 body text) go to the `DismissibleAlert`; after an error, reload the list so fields revert.

- [ ] **Step 1: Failing bUnit tests** — stateful fake HTTP like `AuditoriaDurabilidadePorRankTests` (a mutable list of `PericiaAuditoriaResponse`, handling `GET pericias/auditoria`, `GET pericias`, `POST pericias`, `PUT pericias/{id}`, `DELETE pericias/{id}`, `POST pericias/{id}/restaurar`; register `PericiaCatalogo` in DI). Tests:
  1. `Renders_active_rows_and_a_lock_instead_of_delete_for_protected` — seed Atletismo (7) and Prontidão (32, Protegida): exactly one `button[aria-label='Remover Atletismo']`, none for Prontidão, and a lock icon present in Prontidão's row.
  2. `Adding_posts_the_form_and_shows_the_new_row` — fill Nome "Heráldica", click Adicionar → POST body has Nome "Heráldica"; the row appears.
  3. `Editing_the_description_puts_the_whole_row` — change Atletismo's description text field → PUT `pericias/7` with Nome "Atletismo", new Descricao, unchanged other fields.
  4. `Removing_asks_for_confirmation_then_deletes_and_moves_it_to_Removidas` — click remove → dialog text contains "devolvidos em todas as fichas"; confirm → DELETE `pericias/7` logged; "Removidas" section lists Atletismo with "Restaurar".
  5. `Restaurar_posts_restore` — seed a removed row → click Restaurar → POST `pericias/{id}/restaurar`.
  6. `A_400_shows_the_message` — POST returns 400 "Já existe uma perícia ativa com esse nome." → alert shows it.
  7. `Info_popups_explain_the_page` — render with `MudDialogProvider`, click the first `InfoPopup` button → markup contains "Atributo sugerido só vem pré-selecionado".

Write each test fully in the file (same idioms as `AuditoriaDurabilidadePorRankTests`: `Render<AuditoriaPericias>()`, `await Task.Delay(50)`, `FindComponents<MudTextField<string>>()`, `ValueChanged.InvokeAsync`, a `putLog`/request log list).

- [ ] **Step 2: Run** `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~AuditoriaPericiasTests` → FAIL.

- [ ] **Step 3: Implement** the page per the layout above (`@page "/auditoria/pericias"`, `@inject HttpClient Http`, `@inject PericiaCatalogo Catalogo`). Nav link:
```razor
        <MudNavLink Href="auditoria/pericias" Icon="@Icons.Material.Filled.FormatListBulleted" IconColor="Color.Primary">Perícias</MudNavLink>
```
- [ ] **Step 4: Run** the client suite → PASS; `dotnet build` → 0/0.
- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat(client): página de Auditoria de Perícias"
```

---

### Task 7: Requirements docs

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Auditoria de Regras.md` (append R0012)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md:168` (the fixed perícia list paragraph)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Criaturas.md` (the "lista fixa mais curta" of R0005)
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md` (new `Pericias` table in §11 Auditoria de Regras; skill/mastery/Historicos columns become `PericiaId` FKs)

- [ ] **Step 1: R0012** — append:

```markdown

# **R0012** - O Auditor mantém a lista de Perícias.

**Descrição**: A página **Auditoria → Perícias** lista todas as perícias do sistema. Cada perícia tem **Nome** (obrigatório, único entre as ativas), **Descrição** (opcional), **Atributo sugerido** (opcional) e **Disponível para Criaturas**. O Auditor pode:

- **Adicionar** uma perícia: ela aparece em todas as fichas (Personagem, NPC e, se marcada, Criatura) com 0 pontos.
- **Editar** qualquer campo. O Atributo sugerido só vem pré-selecionado numa ficha — o jogador sempre pode escolher outro atributo, porque o GM pode pedir a perícia com outro atributo numa ação específica.
- **Remover** uma perícia: a remoção é lógica. Os pontos gastos nela são **devolvidos** ao saldo de Pontos de Perícia de todas as fichas, e enquanto removida ela some das fichas e das listas de escolha; bônus de Histórico, requisitos de Passiva e Maestrias ligados a ela deixam de valer.
- **Restaurar** uma perícia removida: ela volta com 0 pontos em todas as fichas. Recusado se já houver uma perícia ativa com o mesmo nome.

**Prontidão**, **Reflexos** e **Fortitude** entram em fórmulas (ver "[[Formulas]]"): podem ser renomeadas e descritas, mas não removidas nem retiradas das Criaturas. Na ficha, a Descrição aparece ao passar o mouse sobre o nome da perícia (desktop) ou ao tocar nele (celular).
```

- [ ] **Step 2: Ficha de Personagem** — replace the sentence "A ficha exibe uma lista fixa das Perícias do sistema: Acrobacia, … e Sobrevivência." with:

```markdown
A ficha exibe todas as Perícias ativas, mantidas pelo Auditor (ver "[[Requisitos - Auditoria de Regras]]" R0012), em ordem alfabética; a lista inicial é: Acrobacia, Alquimia, Arcano, Armadilhas, Armas Brancas, Artefatos Mágicos, Artístico, Atletismo, Avaliação, Biblioteca, Brigar, Condução, Conhecimentos, Crime, Empatia c/ Animais, Enganação, Força de Vontade, Fortitude, Furtividade, Herborismo, Intimidação, Intuição, Investigação, Lábia, Liderança, Linguística, Medicina, Navegação, Ocultismo, Ofício, Percepção, Pontaria, Prontidão, Reflexos, Religião, Saquear, Sedução, Senso Comum e Sobrevivência. O nome de uma perícia com Descrição mostra o texto ao passar o mouse (ou ao tocar, no celular). Uma perícia nova começa com o Atributo sugerido já escolhido. Cada linha tem:
```

- [ ] **Step 3: Ficha de Criaturas** — where R0005 lists the fixed shorter perícia list, add: "A lista passa a ser as perícias marcadas como *Disponível para Criaturas* na Auditoria de Perícias ("[[Requisitos - Auditoria de Regras]]" R0012); a lista acima é a inicial."

- [ ] **Step 4: Modelo de Dados** — add under §11:

```markdown

**Pericias** — perícias do sistema (Auditoria de Regras R0012). Nunca apagadas de verdade.

| Coluna | Tipo |
|---|---|
| Id | int, PK (0–38 = perícias iniciais; novas = maior Id + 1) |
| Chave | string, único, imutável — identificador usado pela API e pelo Alvo de Artefatos |
| Nome | string, único entre as não removidas |
| Descricao | text, nullable |
| AtributoSugerido | enum Atributo, nullable |
| DisponivelParaCriaturas | bool |
| IsDeleted | bool |
```
and change the `Pericia` column rows of CharacterSkills/NpcSkills/CreatureSkills, the Maestria tables and Historicos to `PericiaId` / `PericiaMaisSeisId` / `PericiaMaisTresId` — `FK → Pericias`.

- [ ] **Step 5: Commit**

```bash
git add Docs/Requisitos
git commit -m "docs: requisitos da Auditoria de Perícias (R0012)"
```
