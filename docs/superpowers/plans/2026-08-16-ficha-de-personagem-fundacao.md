# Ficha de Personagem — Fundação & Informações Básicas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the `CharacterSheet` root entity and its creation/access model — a GM creates a sheet for a campaign member (closing the Campanha — Fundação plan's deferred R0004), the owning player edits it, the GM can too — then implement the **Informações Básicas** tab (1.a Identidade, 1.b Nível e Progressão, 1.c Recursos) and the level-up notice (R0002). This is the foundation every later Personagem tab (Atributos & Perícias, Combate, Magias & Habilidades, Posses, Diário) attaches child tables to.

**Architecture:** `CharacterSheet` is one wide table matching Modelo de Dados §6.1's root columns exactly — the child-table tabs (attributes, weapons, spells, inventory, …) are explicitly out of scope here and arrive in the next two Personagem plans. Two fields' "máximo" halves (Vitalidade/Foco/Adrenalina depend on Atributos, not built until the next plan) are stored as **Atual-only** in this plan, matching the schema (Modelo de Dados never persists a "máximo" column at all — it's always computed on read) — the computed-máximo display and "não pode exceder" validation are explicitly deferred to whichever later plan finishes the dependency (Atributos for Vitalidade/Foco, Artefatos for Adrenalina). Authorization follows Técnico R0004 exactly: create/delete are GM-only; edit requires the caller to be either the sheet's owner or the GM of its campaign — a small `CharacterSheetAuthorization` Domain helper centralizes that rule so every later Personagem-tab endpoint (in the next two plans) can reuse it instead of re-deriving it. Sub-vocação/Afinidade dropdown options are validated server-side against `IRulesDataProvider` (Compêndio de Regras plan) where that data already exists (Vocação→Sub-vocação via `Tabela de Classes`), avoiding a second copy of that mapping.

**Tech Stack:** Same as established.

**Spec:** `Docs/Requisitos/Requisitos - Ficha de Personagem.md` R0001 (tabs 1.a-1.c only) and R0002, `Docs/Requisitos/Requisitos - Modelo de Dados.md` §6.1 (root columns only), `Docs/Requisitos/Requisitos - Técnico.md` R0004 (authorization model).

## Global Constraints

- TDD is mandatory (Técnico R0011) — every behavior change gets a failing test first.
- **Create and delete are GM-only.** **Edit** requires the caller to be the sheet's `OwnerId` **or** the GM of the sheet's `Campaign` (Técnico R0004) — every write endpoint in this plan and the next two Personagem plans must apply exactly this rule via the shared `CharacterSheetAuthorization` helper this plan builds.
- Field default/NULL convention (Ficha de Personagem preamble): unless stated otherwise, a field's default is the DB value; when NULL, the Client shows an italic placeholder. `Nivel` defaults to **1** when NULL (not a placeholder — an actual default value), matching R0001 1.b exactly.
- A `CharacterSheet` always belongs to exactly one `Campaign` (`CampaignId` non-nullable) — unlike the later `NpcSheet`/`CreatureSheet`, which have no `CampaignId` at all.
- No secret ever hardcoded — unaffected by this plan.

---

### Task 1: Domain — Ficha de Personagem enums

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/Linhagem.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/Variante.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/Vocacao.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/AfinidadeElemental.cs`
- Create: `src/RuinaRPG.Domain/CharacterSheets/Cobertura.cs`

**Interfaces:**
- Produces: 5 enums consumed by Task 2 (`CharacterSheet` entity) and every later task in this plan and the next two Personagem plans.

- [ ] **Step 1: Write the enums**

`src/RuinaRPG.Domain/CharacterSheets/Linhagem.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum Linhagem
{
    Humano,
    Phylauc,
    Nephrytes,
    Econos
}
```

`src/RuinaRPG.Domain/CharacterSheets/Variante.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum Variante
{
    Sinir,       // Humano
    Laonir,      // Humano
    PhylacTai,   // Phylauc
    EsPhylauc,   // Phylauc
    Yavos,       // Nephrytes
    Koroanos,    // Nephrytes
    Alora        // Econos
}
```

`src/RuinaRPG.Domain/CharacterSheets/Vocacao.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum Vocacao
{
    Campeao,
    Cacador,
    Feiticeiro,
    Adepto,
    Bruxo
}
```

`src/RuinaRPG.Domain/CharacterSheets/AfinidadeElemental.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

// The flat 4-Elemento + 14-Sub-Elemento list for the single "Afinidade" dropdown in 1.a — NOT the
// same as the incremental Elemento/SubElemento pair in 2.c's Afinidades list (a different plan).
public enum AfinidadeElemental
{
    Terra,
    Agua,
    Fogo,
    Ar,
    Gelo,
    Flora,
    Ferro,
    Raio,
    Prever,
    Alma,
    Purificar,
    Ecomancia,
    Hemomancia,
    Curar,
    Vida,
    Aprimorar,
    Necromancia,
    Invocacao
}
```

`src/RuinaRPG.Domain/CharacterSheets/Cobertura.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public enum Cobertura
{
    Nenhuma,
    Parcial,
    Completa
}
```

- [ ] **Step 2: Verify the Domain project still builds**

Run: `dotnet build src/RuinaRPG.Domain`
Expected: `Build succeeded`, 0 warnings, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets
git commit -m "feat: add ficha de personagem enums"
```

---

### Task 2: Domain — Linhagem/Vocação relationship validators

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/LinhagemVarianteValidator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/LinhagemVarianteValidatorTests.cs`

**Interfaces:**
- Consumes: `Linhagem`, `Variante` (Task 1).
- Produces: `LinhagemVarianteValidator.IsValidCombination(Linhagem linhagem, Variante variante) : bool`. Task 4 (sheet update) calls this exact signature.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/LinhagemVarianteValidatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class LinhagemVarianteValidatorTests
{
    [Theory]
    [InlineData(Linhagem.Humano, Variante.Sinir, true)]
    [InlineData(Linhagem.Humano, Variante.Laonir, true)]
    [InlineData(Linhagem.Humano, Variante.Yavos, false)]
    [InlineData(Linhagem.Phylauc, Variante.PhylacTai, true)]
    [InlineData(Linhagem.Phylauc, Variante.EsPhylauc, true)]
    [InlineData(Linhagem.Nephrytes, Variante.Yavos, true)]
    [InlineData(Linhagem.Nephrytes, Variante.Koroanos, true)]
    [InlineData(Linhagem.Econos, Variante.Alora, true)]
    [InlineData(Linhagem.Econos, Variante.Sinir, false)]
    public void IsValidCombination_matches_the_Sistema_Basico_linhagem_variante_pairing(Linhagem linhagem, Variante variante, bool expected)
    {
        var result = LinhagemVarianteValidator.IsValidCombination(linhagem, variante);

        result.Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter LinhagemVarianteValidatorTests`
Expected: FAIL to compile — `LinhagemVarianteValidator` doesn't exist yet.

- [ ] **Step 3: Write `LinhagemVarianteValidator`**

`src/RuinaRPG.Domain/CharacterSheets/LinhagemVarianteValidator.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public static class LinhagemVarianteValidator
{
    private static readonly Dictionary<Linhagem, Variante[]> ValidPairs = new()
    {
        [Linhagem.Humano] = [Variante.Sinir, Variante.Laonir],
        [Linhagem.Phylauc] = [Variante.PhylacTai, Variante.EsPhylauc],
        [Linhagem.Nephrytes] = [Variante.Yavos, Variante.Koroanos],
        [Linhagem.Econos] = [Variante.Alora]
    };

    public static bool IsValidCombination(Linhagem linhagem, Variante variante) =>
        ValidPairs[linhagem].Contains(variante);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter LinhagemVarianteValidatorTests`
Expected: PASS (9/9).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/LinhagemVarianteValidator.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/LinhagemVarianteValidatorTests.cs
git commit -m "feat: add linhagem/variante combination validator"
```

---

### Task 3: Infrastructure — `CharacterSheet` entity and migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/CharacterSheetMigrationTests.cs`

**Interfaces:**
- Consumes: `Linhagem`, `Variante`, `Vocacao`, `AfinidadeElemental`, `Cobertura` (Task 1), `Campaign` (Campanha — Fundação plan), `Image` (Catálogo plan).
- Produces: `CharacterSheet` with every root column from Modelo de Dados §6.1 (see Step 1). `RuinaRpgDbContext.CharacterSheets : DbSet<CharacterSheet>`. Tasks 4-9 and the next two Personagem plans' child tables all FK against `CharacterSheet.Id`.

- [ ] **Step 1: Write the entity**

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs`:

```csharp
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class CharacterSheet
{
    // 1.a Identidade
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ImageId { get; set; }
    public string? Nome { get; set; }
    public Linhagem? Linhagem { get; set; }
    public Variante? Variante { get; set; }
    public Vocacao? Vocacao { get; set; }
    public string? SubVocacao { get; set; }
    public AfinidadeElemental? Afinidade { get; set; }
    public string? Propriedade { get; set; }

    // 1.b Nível e Progressão
    public int Nivel { get; set; } = 1;
    public int Circulo { get; set; }
    public int Grau { get; set; }
    public bool PossuiCoracaoDeMana { get; set; }
    public int ExperienciaAtual { get; set; }
    public int EAPAtual { get; set; }
    public int NucleosRankF { get; set; }
    public int NucleosRankE { get; set; }
    public int NucleosRankD { get; set; }
    public int NucleosRankC { get; set; }
    public int NucleosRankB { get; set; }
    public int NucleosRankA { get; set; }
    public int NucleosRankS { get; set; }
    public int PontosDeIgnicaoAtual { get; set; }
    public int PontosDeIgnicaoTotal { get; set; }

    // 1.c Recursos (Atual only — see plan Architecture note on máximo)
    public int VitalidadeAtual { get; set; }
    public int FocoAtual { get; set; }
    public int AdrenalinaAtual { get; set; }
    public int EstresseAtual { get; set; }

    // Referenced by later tabs (2.b Cobertura; 5.a Ciclos) but stored on the root per Modelo de Dados
    public Cobertura Cobertura { get; set; }
    public int Ciclos { get; set; }

    // R0002
    public int? LastDismissedLevelUpLevel { get; set; }
}
```

- [ ] **Step 2: Write the failing migration test**

`tests/RuinaRPG.Tests.Integration/Persistence/CharacterSheetMigrationTests.cs`:

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

public class CharacterSheetMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterSheetMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CharacterSheets_table_with_a_default_Nivel_of_1()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterSheets"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@sheettest.com", Email = "gm@sheettest.com", Nickname = "SheetTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@sheettest.com", Email = "player@sheettest.com", Nickname = "SheetTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();

        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id, Linhagem = Linhagem.Humano, Variante = Variante.Sinir };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        var reloaded = await db.CharacterSheets.SingleAsync();
        reloaded.Nivel.Should().Be(1);
        reloaded.Linhagem.Should().Be(Linhagem.Humano);
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetMigrationTests`
Expected: FAIL — no `AddCharacterSheets` migration exists yet.

- [ ] **Step 4: Register the DbSet and relationships**

Add `using RuinaRPG.Infrastructure.CharacterSheets;` and:

```csharp
public DbSet<CharacterSheet> CharacterSheets => Set<CharacterSheet>();
```

Inside `OnModelCreating`:

```csharp
builder.Entity<CharacterSheet>(entity =>
{
    entity.HasOne<Campaign>()
        .WithMany()
        .HasForeignKey(s => s.CampaignId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<ApplicationUser>()
        .WithMany()
        .HasForeignKey(s => s.OwnerId)
        .OnDelete(DeleteBehavior.Cascade);
    entity.HasOne<Image>()
        .WithMany()
        .HasForeignKey(s => s.ImageId)
        .OnDelete(DeleteBehavior.SetNull);
});
```

- [ ] **Step 5: Create the migration**

```bash
dotnet ef migrations add AddCharacterSheets \
  --project src/RuinaRPG.Infrastructure \
  --startup-project src/RuinaRPG.Api \
  --output-dir Persistence/Migrations
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetMigrationTests`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Infrastructure/CharacterSheets src/RuinaRPG.Infrastructure/Persistence tests/RuinaRPG.Tests.Integration/Persistence/CharacterSheetMigrationTests.cs
git commit -m "feat: add CharacterSheet entity and migration"
```

---

### Task 4: Domain — shared authorization rule

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/CharacterSheetAuthorization.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/CharacterSheetAuthorizationTests.cs`

**Interfaces:**
- Produces: `CharacterSheetAuthorization.CanEdit(Guid callerId, Guid ownerId, Guid campaignGmId) : bool`. Task 6 (update endpoint) calls this exact signature; the next two Personagem plans' child-table endpoints (attributes, weapons, spells, …) reuse it too rather than re-deriving the rule.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/CharacterSheetAuthorizationTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class CharacterSheetAuthorizationTests
{
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid CampaignGm = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();

    [Fact]
    public void CanEdit_is_true_for_the_owner()
    {
        CharacterSheetAuthorization.CanEdit(Owner, Owner, CampaignGm).Should().BeTrue();
    }

    [Fact]
    public void CanEdit_is_true_for_the_campaigns_gm()
    {
        CharacterSheetAuthorization.CanEdit(CampaignGm, Owner, CampaignGm).Should().BeTrue();
    }

    [Fact]
    public void CanEdit_is_false_for_anyone_else()
    {
        CharacterSheetAuthorization.CanEdit(Stranger, Owner, CampaignGm).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter CharacterSheetAuthorizationTests`
Expected: FAIL to compile — `CharacterSheetAuthorization` doesn't exist yet.

- [ ] **Step 3: Write `CharacterSheetAuthorization`**

`src/RuinaRPG.Domain/CharacterSheets/CharacterSheetAuthorization.cs`:

```csharp
namespace RuinaRPG.Domain.CharacterSheets;

public static class CharacterSheetAuthorization
{
    public static bool CanEdit(Guid callerId, Guid ownerId, Guid campaignGmId) =>
        callerId == ownerId || callerId == campaignGmId;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter CharacterSheetAuthorizationTests`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/CharacterSheetAuthorization.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/CharacterSheetAuthorizationTests.cs
git commit -m "feat: add shared character sheet edit-authorization rule"
```

---

### Task 5: Api — create and delete a sheet (Campanha R0004; Ficha de Personagem preamble)

**Files:**
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CreateCharacterSheetRequest.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `CharacterSheet` (Task 3), `Campaign`/`CampaignMember` (Campanha — Fundação plan), `ApplicationUser.InvitedByGmId` (Convite de Jogador plan).
- Produces: `POST /api/campaigns/{campaignId}/character-sheets` → `201` + `CharacterSheetResponse`, `[Authorize(Roles = "GM")]`, `400` if the target owner isn't a member of that campaign. `DELETE /api/character-sheets/{id}` → `204`/`404`, GM-only, scoped to campaigns the caller owns. `CreateCharacterSheetRequest(string OwnerId)` — the sheet is created blank; the owner fills in Nome/Linhagem/etc. afterward via Task 6's update endpoint (matches R0001's "cria uma ficha de personagem escolhendo qual jogador membro é o dono dela" — nothing in R0004 says the GM fills in character details at creation time). `CharacterSheetResponse` carries every 1.a-1.c field (see Step 1). Tasks 6-9 and the Client tasks depend on this shape.

- [ ] **Step 1: Write the contracts**

`src/RuinaRPG.Contracts/CharacterSheets/CreateCharacterSheetRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CreateCharacterSheetRequest(string OwnerId);
```

`src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record CharacterSheetResponse(
    string Id,
    string CampaignId,
    string OwnerId,
    string? ImageUrl,
    string? Nome,
    string? Linhagem,
    string? Variante,
    string? Vocacao,
    string? SubVocacao,
    string? Afinidade,
    string? Propriedade,
    int Nivel,
    int Circulo,
    int Grau,
    bool PossuiCoracaoDeMana,
    int ExperienciaAtual,
    int EAPAtual,
    int NucleosRankF,
    int NucleosRankE,
    int NucleosRankD,
    int NucleosRankC,
    int NucleosRankB,
    int NucleosRankA,
    int NucleosRankS,
    int PontosDeIgnicaoAtual,
    int PontosDeIgnicaoTotal,
    int VitalidadeAtual,
    int FocoAtual,
    int AdrenalinaAtual,
    int EstresseAtual,
    string Cobertura,
    int Ciclos);
```

- [ ] **Step 2: Write the failing integration tests**

`tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Integration.Controllers;

public class CharacterSheetsControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CharacterSheetsControllerTests(PostgresFixture postgres) => _postgres = postgres;

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

    private async Task<string> RegisterGmAndGetTokenAsync(string nickname, string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return tokens!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<(string PlayerId, string PlayerToken)> RegisterJogadorLinkedToAsync(string gmToken, string nickname, string email)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        var tokens = await response.Content.ReadFromJsonAsync<AuthResponse>();

        var me = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
        var meResponse = await _client.SendAsync(me);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>();
        return (meBody!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        return (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/campaigns/00000000-0000-0000-0000-000000000000/character-sheets", new CreateCharacterSheetRequest("00000000-0000-0000-0000-000000000000"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_for_a_campaign_member_returns_201_with_Nivel_1_and_no_other_field_set()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGm1", "sheet1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayer1", "sheetplayer1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Ficha");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        body!.OwnerId.Should().Be(playerId);
        body.Nivel.Should().Be(1);
        body.Nome.Should().BeNull();
    }

    [Fact]
    public async Task Create_for_a_non_member_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGm2", "sheet2@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayer2", "sheetplayer2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha sem Membro");
        // note: playerId is NOT added as a member of this campaign

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_by_a_jogador_returns_403()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGm3", "sheet3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayer3", "sheetplayer3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Jogador");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", playerToken, new CreateCharacterSheetRequest(playerId)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_an_owned_sheet_returns_204()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("SheetGmDelete1", "sheetdelete1@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerDelete1", "sheetplayerdelete1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Delete");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await createResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}", gmToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Delete_a_sheet_in_a_campaign_owned_by_another_gm_returns_404()
    {
        var gmTokenOwner = await RegisterGmAndGetTokenAsync("SheetGmDeleteOwner", "sheetdeleteowner@teste.com");
        var gmTokenOther = await RegisterGmAndGetTokenAsync("SheetGmDeleteOther", "sheetdeleteother@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmTokenOwner, "SheetPlayerDeleteOwner", "sheetplayerdeleteowner@teste.com");
        var campaignId = await CreateCampaignAsync(gmTokenOwner, "Campanha do Dono");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmTokenOwner, new AddCampaignMemberRequest(playerId)));
        var createResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmTokenOwner, new CreateCharacterSheetRequest(playerId)));
        var sheetId = (await createResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/character-sheets/{sheetId}", gmTokenOther));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: FAIL — the routes don't exist yet.

- [ ] **Step 4: Write `CharacterSheetsController` (create and delete only for now)**

`src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
public class CharacterSheetsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost("api/campaigns/{campaignId}/character-sheets")]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<CharacterSheetResponse>> Create(Guid campaignId, CreateCharacterSheetRequest request)
    {
        var gmId = CurrentUserId();
        var campaignExists = await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var ownerId = Guid.Parse(request.OwnerId);
        var isMember = await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == ownerId);
        if (!isMember)
            return BadRequest("O jogador informado não é membro desta campanha.");

        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaignId, OwnerId = ownerId };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpDelete("api/character-sheets/{id}")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var gmId = CurrentUserId();
        var sheet = await db.CharacterSheets
            .Join(db.Campaigns, s => s.CampaignId, c => c.Id, (s, c) => new { Sheet = s, c.GmId })
            .Where(x => x.Sheet.Id == id && x.GmId == gmId)
            .Select(x => x.Sheet)
            .FirstOrDefaultAsync();
        if (sheet is null)
            return NotFound();

        db.CharacterSheets.Remove(sheet);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<CharacterSheetResponse> ToResponseAsync(CharacterSheet s)
    {
        string? imageUrl = null;
        if (s.ImageId is not null)
        {
            var image = await db.Images.FindAsync(s.ImageId.Value);
            imageUrl = image is not null ? $"/{image.Path}" : null;
        }

        return new CharacterSheetResponse(
            s.Id.ToString(), s.CampaignId.ToString(), s.OwnerId.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade?.ToString(), s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, s.EAPAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, s.PontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

Note: this controller is NOT route-attributed at the class level (unlike every previous controller in this codebase) because its actions live under two different route prefixes (`api/campaigns/{campaignId}/character-sheets` and `api/character-sheets/{id}`) — each `[Http*]` attribute carries its own full route instead.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs
git commit -m "feat: add character sheet creation and deletion"
```

---

### Task 6: Api — get and update a sheet's Informações Básicas fields (R0001 1.a-1.c)

**Files:**
- Create: `src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `CharacterSheetAuthorization.CanEdit` (Task 4), `LinhagemVarianteValidator.IsValidCombination` (Task 2), `CharacterSheetsController` (Task 5).
- Produces: `GET /api/character-sheets/{id}` → `200` + `CharacterSheetResponse`, `404` if the sheet doesn't exist. `PUT /api/character-sheets/{id}` → `204` on success, `400` if Linhagem/Variante don't match, `403` if the caller is neither owner nor the campaign's GM, `404` if the sheet doesn't exist. `UpdateCharacterSheetRequest` mirrors every editable 1.a-1.c field from `CharacterSheetResponse` except `Id`/`CampaignId`/`OwnerId` (immutable after creation — nothing in R0001 describes reassigning a sheet's owner or campaign).

- [ ] **Step 1: Write the request contract**

`src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record UpdateCharacterSheetRequest(
    string? ImageId,
    string? Nome,
    string? Linhagem,
    string? Variante,
    string? Vocacao,
    string? SubVocacao,
    string? Afinidade,
    string? Propriedade,
    int Nivel,
    bool PossuiCoracaoDeMana,
    int ExperienciaAtual,
    int EAPAtual,
    int NucleosRankF,
    int NucleosRankE,
    int NucleosRankD,
    int NucleosRankC,
    int NucleosRankB,
    int NucleosRankA,
    int NucleosRankS,
    int PontosDeIgnicaoAtual,
    int PontosDeIgnicaoTotal,
    int VitalidadeAtual,
    int FocoAtual,
    int AdrenalinaAtual,
    int EstresseAtual,
    string Cobertura,
    int Ciclos);
```

- [ ] **Step 2: Write the failing tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`, inside the class:

```csharp
private async Task<string> CreateSheetForMemberAsync(string gmToken, string campaignId, string playerId)
{
    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
    return (await response.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
}

private static UpdateCharacterSheetRequest ValidUpdate() => new(
    null, "Vann Astrel", "Humano", "Sinir", "Campeao", "Duelista", "Fogo", "Marcado pela Ruína",
    5, true, 750, 120, 0, 0, 0, 0, 0, 0, 0, 20, 40, 30, 15, 8, 3, "Parcial", 100);

[Fact]
public async Task Get_returns_the_sheet()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGet1", "sheetget1@teste.com");
    var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGet1", "sheetplayerget1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Get");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", gmToken));

    response.StatusCode.Should().Be(HttpStatusCode.OK);
}

[Fact]
public async Task Update_by_the_owner_returns_204_and_persists_every_field()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate1", "sheetupdate1@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate1", "sheetplayerupdate1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate()));

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var getResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
    var body = await getResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>();
    body!.Nome.Should().Be("Vann Astrel");
    body.Linhagem.Should().Be("Humano");
    body.Variante.Should().Be("Sinir");
    body.Nivel.Should().Be(5);
    body.VitalidadeAtual.Should().Be(30);
}

[Fact]
public async Task Update_by_the_campaigns_gm_returns_204()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate2", "sheetupdate2@teste.com");
    var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate2", "sheetplayerupdate2@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update GM");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", gmToken, ValidUpdate()));

    response.StatusCode.Should().Be(HttpStatusCode.NoContent);
}

[Fact]
public async Task Update_by_an_unrelated_jogador_returns_403()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate3", "sheetupdate3@teste.com");
    var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate3", "sheetplayerupdate3@teste.com");
    var (_, otherPlayerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate3b", "sheetplayerupdate3b@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Unrelated");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", otherPlayerToken, ValidUpdate()));

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}

[Fact]
public async Task Update_with_a_Variante_that_does_not_belong_to_the_Linhagem_returns_400()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmUpdate4", "sheetupdate4@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerUpdate4", "sheetplayerupdate4@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Update Invalid");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var invalid = ValidUpdate() with { Linhagem = "Humano", Variante = "Yavos" }; // Yavos belongs to Nephrytes
    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, invalid));

    response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: the 6 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 4: Add `Get` and `Update` actions**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, add `using RuinaRPG.Domain.CharacterSheets;`, then:

```csharp
[HttpGet("api/character-sheets/{id}")]
public async Task<ActionResult<CharacterSheetResponse>> Get(Guid id)
{
    var sheet = await db.CharacterSheets.FindAsync(id);
    if (sheet is null)
        return NotFound();

    return await ToResponseAsync(sheet);
}

[HttpPut("api/character-sheets/{id}")]
public async Task<IActionResult> Update(Guid id, UpdateCharacterSheetRequest request)
{
    var sheet = await db.CharacterSheets.FindAsync(id);
    if (sheet is null)
        return NotFound();

    var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
    if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
        return Forbid();

    var linhagem = ParseEnum<Linhagem>(request.Linhagem);
    var variante = ParseEnum<Variante>(request.Variante);
    if (linhagem is not null && variante is not null && !LinhagemVarianteValidator.IsValidCombination(linhagem.Value, variante.Value))
        return BadRequest("A Variante escolhida não pertence à Linhagem escolhida.");

    sheet.ImageId = request.ImageId is not null ? Guid.Parse(request.ImageId) : null;
    sheet.Nome = request.Nome;
    sheet.Linhagem = linhagem;
    sheet.Variante = variante;
    sheet.Vocacao = ParseEnum<Vocacao>(request.Vocacao);
    sheet.SubVocacao = request.SubVocacao;
    sheet.Afinidade = ParseEnum<AfinidadeElemental>(request.Afinidade);
    sheet.Propriedade = request.Propriedade;
    sheet.Nivel = request.Nivel;
    sheet.PossuiCoracaoDeMana = request.PossuiCoracaoDeMana;
    sheet.ExperienciaAtual = request.ExperienciaAtual;
    sheet.EAPAtual = request.EAPAtual;
    sheet.NucleosRankF = request.NucleosRankF;
    sheet.NucleosRankE = request.NucleosRankE;
    sheet.NucleosRankD = request.NucleosRankD;
    sheet.NucleosRankC = request.NucleosRankC;
    sheet.NucleosRankB = request.NucleosRankB;
    sheet.NucleosRankA = request.NucleosRankA;
    sheet.NucleosRankS = request.NucleosRankS;
    sheet.PontosDeIgnicaoAtual = request.PontosDeIgnicaoAtual;
    sheet.PontosDeIgnicaoTotal = request.PontosDeIgnicaoTotal;
    sheet.VitalidadeAtual = request.VitalidadeAtual;
    sheet.FocoAtual = request.FocoAtual;
    sheet.AdrenalinaAtual = request.AdrenalinaAtual;
    sheet.EstresseAtual = request.EstresseAtual;
    sheet.Cobertura = Enum.Parse<Cobertura>(request.Cobertura);
    sheet.Ciclos = request.Ciclos;

    await db.SaveChangesAsync();
    return NoContent();
}

private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
    value is not null && Enum.TryParse<TEnum>(value, out var parsed) ? parsed : null;
```

**Graduação (Círculo/Grau) is deliberately not in `UpdateCharacterSheetRequest`:** R0001 1.b says it's computed from EAP (and, for non-Campeão/Caçador vocações, gated by `PossuiCoracaoDeMana`) — not a field the player edits directly. Computing and persisting it correctly needs `IRulesDataProvider.CirculoGrauPorEap` (Compêndio de Regras plan); wiring that up is Task 7 of this plan, immediately following.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: PASS (11/11).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/UpdateCharacterSheetRequest.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs
git commit -m "feat: add character sheet get and update for informacoes basicas"
```

---

### Task 7: Domain — Graduação (Círculo/Grau) calculator

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/GraduacaoCalculator.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/GraduacaoCalculatorTests.cs`

**Interfaces:**
- Consumes: `IRulesDataProvider.CirculoGrauPorEap` (Compêndio de Regras plan).
- Produces: `GraduacaoCalculator.Compute(Vocacao vocacao, int eapAtual, bool possuiCoracaoDeMana, IReadOnlyList<CirculoGrauPorEap> tabela) : int`. Task 8 (the `Get`/`Update` response) calls this exact signature to fill a **computed, not stored** `Graduacao` field.

- [ ] **Step 1: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/GraduacaoCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class GraduacaoCalculatorTests
{
    // Real excerpt from Tabela de Circulo e Grau por EAP: 0→0, 1→100, 2→400 EAP absolute thresholds.
    private static readonly IReadOnlyList<CirculoGrauPorEap> Tabela =
    [
        new(0, "0", "0", 3, 0),
        new(1, "100", "100", 5, 2),
        new(2, "400", "300", 7, 2)
    ];

    [Fact]
    public void Compute_for_Campeao_ignores_coracao_de_mana_and_uses_EAP_directly()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Campeao, eapAtual: 150, possuiCoracaoDeMana: false, Tabela);

        result.Should().Be(1); // 150 >= 100 (Grau 1's threshold), < 400 (Grau 2's threshold)
    }

    [Fact]
    public void Compute_for_Cacador_ignores_coracao_de_mana_too()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Cacador, eapAtual: 450, possuiCoracaoDeMana: false, Tabela);

        result.Should().Be(2);
    }

    [Fact]
    public void Compute_for_a_magic_vocacao_without_coracao_de_mana_is_always_zero()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Feiticeiro, eapAtual: 450, possuiCoracaoDeMana: false, Tabela);

        result.Should().Be(0);
    }

    [Fact]
    public void Compute_for_a_magic_vocacao_with_coracao_de_mana_uses_EAP_normally()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Feiticeiro, eapAtual: 450, possuiCoracaoDeMana: true, Tabela);

        result.Should().Be(2);
    }

    [Fact]
    public void Compute_below_the_first_threshold_is_zero()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Campeao, eapAtual: 50, possuiCoracaoDeMana: false, Tabela);

        result.Should().Be(0);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter GraduacaoCalculatorTests`
Expected: FAIL to compile — `GraduacaoCalculator` doesn't exist yet.

- [ ] **Step 3: Write `GraduacaoCalculator`**

`src/RuinaRPG.Domain/CharacterSheets/GraduacaoCalculator.cs`:

```csharp
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

public static class GraduacaoCalculator
{
    public static int Compute(Vocacao vocacao, int eapAtual, bool possuiCoracaoDeMana, IReadOnlyList<CirculoGrauPorEap> tabela)
    {
        var usaGraumSempre = vocacao is Vocacao.Campeao or Vocacao.Cacador;
        if (!usaGraumSempre && !possuiCoracaoDeMana)
            return 0;

        var highestMet = 0;
        foreach (var row in tabela.OrderBy(r => r.CirculoOuGrau))
        {
            if (!int.TryParse(row.EapAbsoluto, out var threshold))
                continue; // "Max." rows aren't a real numeric threshold to compare against

            if (eapAtual >= threshold)
                highestMet = row.CirculoOuGrau;
        }

        return highestMet;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter GraduacaoCalculatorTests`
Expected: PASS (5/5).

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/GraduacaoCalculator.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/GraduacaoCalculatorTests.cs
git commit -m "feat: add graduacao (circulo/grau) calculator"
```

---

### Task 8: Api — wire Graduação into the sheet response

**Files:**
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Modify: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `GraduacaoCalculator.Compute` (Task 7), `IRulesDataProvider` (Compêndio de Regras plan).
- Produces: `CharacterSheetResponse` gains `int Graduacao` and `string GraduacaoLabel` ("Grau" for Campeão/Caçador, "Círculo" otherwise, per R0001 1.b) — computed on every read, never persisted, matching the plan's Architecture note.

- [ ] **Step 1: Add the fields to the response contract**

In `src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs`, add two parameters at the end of the record:

```csharp
    int Ciclos,
    int Graduacao,
    string GraduacaoLabel);
```

(replacing the previous closing `int Ciclos);` line.)

- [ ] **Step 2: Write the failing test**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`:

```csharp
[Fact]
public async Task Get_labels_Graduacao_as_Grau_for_Campeao_and_computes_it_from_EAP()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGrad1", "sheetgrad1@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGrad1", "sheetplayergrad1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Graduacao");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var update = ValidUpdate() with { Vocacao = "Campeao", EAPAtual = 150 };
    await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
    var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
    body!.GraduacaoLabel.Should().Be("Grau");
    body.Graduacao.Should().Be(1); // 150 EAP >= the real Tabela's Grau 1 threshold (100)
}

[Fact]
public async Task Get_labels_Graduacao_as_Circulo_for_a_magic_vocacao()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmGrad2", "sheetgrad2@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerGrad2", "sheetplayergrad2@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha Graduacao 2");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var update = ValidUpdate() with { Vocacao = "Feiticeiro", PossuiCoracaoDeMana = false, EAPAtual = 150 };
    await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, update));

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", playerToken));
    var body = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
    body!.GraduacaoLabel.Should().Be("Círculo");
    body.Graduacao.Should().Be(0); // no coração de mana → always 0 regardless of EAP
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: the 2 new tests FAIL (missing `Graduacao`/`GraduacaoLabel` in the response, or a compile error until Step 1 is saved); earlier tests still pass once the contract change alone doesn't break them (it's purely additive).

- [ ] **Step 4: Wire the calculator into the controller**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, change the constructor to also take `IRulesDataProvider`, add `using RuinaRPG.Domain.Rules;`:

```csharp
public class CharacterSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules) : ControllerBase
```

Update `ToResponseAsync`'s final `return` to compute and append the two new fields:

```csharp
        var vocacao = s.Vocacao ?? RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao; // no vocação chosen yet → Graduacao is meaningless but must not throw
        var graduacao = s.Vocacao is null ? 0 : GraduacaoCalculator.Compute(vocacao, s.EAPAtual, s.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);
        var graduacaoLabel = vocacao is RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao or RuinaRPG.Domain.CharacterSheets.Vocacao.Cacador ? "Grau" : "Círculo";

        return new CharacterSheetResponse(
            s.Id.ToString(), s.CampaignId.ToString(), s.OwnerId.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade?.ToString(), s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, s.EAPAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, s.PontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos, graduacao, graduacaoLabel);
```

Add `using RuinaRPG.Domain.CharacterSheets;` to the top of the file if not already present (it already is, from Task 6).

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: PASS (13/13).

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs
git commit -m "feat: compute graduacao (circulo/grau) in the sheet response"
```

---

### Task 9: Domain + Api — level-up notice (R0002)

**Files:**
- Create: `src/RuinaRPG.Domain/CharacterSheets/LevelUpNoticeCalculator.cs`
- Create: `src/RuinaRPG.Contracts/CharacterSheets/LevelUpNoticeResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/LevelUpNoticeCalculatorTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `IRulesDataProvider.Niveis` (Compêndio de Regras plan).
- Produces: `LevelUpNoticeCalculator.PendingBonuses(int? lastDismissedLevel, int currentLevel, IReadOnlyList<LevelBonus> niveis) : IReadOnlyList<LevelBonus>` (every level strictly between `lastDismissedLevel` and `currentLevel`, inclusive of `currentLevel`, in ascending order — "se subir mais de um Nível de uma vez, a caixa lista os bônus de todos os Níveis ganhos, em ordem"). `GET /api/character-sheets/{id}/level-up-notice` → `200` + `LevelUpNoticeResponse(List<string> BonusTexts)`. `POST /api/character-sheets/{id}/dismiss-level-up-notice` → `204`, sets `LastDismissedLevelUpLevel = Nivel`. Both require the same edit authorization as Task 6's `Update`.

- [ ] **Step 1: Write the failing calculator tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/LevelUpNoticeCalculatorTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class LevelUpNoticeCalculatorTests
{
    private static readonly IReadOnlyList<LevelBonus> Niveis =
    [
        new(1, "+9 Pontos de Atributo"),
        new(2, "+4 Pontos de Ignição"),
        new(3, "+Status de Vocação de Vida/Foco")
    ];

    [Fact]
    public void PendingBonuses_with_no_prior_dismissal_returns_every_level_up_to_current()
    {
        var result = LevelUpNoticeCalculator.PendingBonuses(lastDismissedLevel: null, currentLevel: 2, Niveis);

        result.Select(b => b.Nivel).Should().Equal(1, 2);
    }

    [Fact]
    public void PendingBonuses_excludes_levels_already_dismissed()
    {
        var result = LevelUpNoticeCalculator.PendingBonuses(lastDismissedLevel: 1, currentLevel: 2, Niveis);

        result.Select(b => b.Nivel).Should().Equal(2);
    }

    [Fact]
    public void PendingBonuses_lists_every_level_gained_at_once_in_ascending_order()
    {
        var result = LevelUpNoticeCalculator.PendingBonuses(lastDismissedLevel: 1, currentLevel: 3, Niveis);

        result.Select(b => b.Nivel).Should().Equal(2, 3);
    }

    [Fact]
    public void PendingBonuses_is_empty_when_fully_caught_up()
    {
        var result = LevelUpNoticeCalculator.PendingBonuses(lastDismissedLevel: 3, currentLevel: 3, Niveis);

        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter LevelUpNoticeCalculatorTests`
Expected: FAIL to compile — `LevelUpNoticeCalculator` doesn't exist yet.

- [ ] **Step 3: Write `LevelUpNoticeCalculator`**

`src/RuinaRPG.Domain/CharacterSheets/LevelUpNoticeCalculator.cs`:

```csharp
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Domain.CharacterSheets;

public static class LevelUpNoticeCalculator
{
    public static IReadOnlyList<LevelBonus> PendingBonuses(int? lastDismissedLevel, int currentLevel, IReadOnlyList<LevelBonus> niveis)
    {
        var floor = lastDismissedLevel ?? 0;
        return niveis
            .Where(n => n.Nivel > floor && n.Nivel <= currentLevel)
            .OrderBy(n => n.Nivel)
            .ToList();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter LevelUpNoticeCalculatorTests`
Expected: PASS (4/4).

- [ ] **Step 5: Write the contract**

`src/RuinaRPG.Contracts/CharacterSheets/LevelUpNoticeResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

public record LevelUpNoticeResponse(List<string> BonusTexts);
```

- [ ] **Step 6: Write the failing integration tests**

Append to `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`:

```csharp
[Fact]
public async Task LevelUpNotice_lists_bonus_text_for_every_level_gained_since_the_last_dismissal()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmLevelUp1", "sheetlevelup1@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp1", "sheetplayerlevelup1@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha LevelUp");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
    await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Nivel = 2 }));

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/level-up-notice", playerToken));

    var body = await response.Content.ReadFromJsonAsync<LevelUpNoticeResponse>();
    body!.BonusTexts.Should().HaveCount(2); // Nível 1 and 2's bonus text, nothing dismissed yet
}

[Fact]
public async Task Dismiss_stops_the_dismissed_levels_from_reappearing()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmLevelUp2", "sheetlevelup2@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp2", "sheetplayerlevelup2@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha LevelUp 2");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
    await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate() with { Nivel = 2 }));

    var dismissResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/dismiss-level-up-notice", playerToken));
    dismissResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

    var noticeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}/level-up-notice", playerToken));
    var body = await noticeResponse.Content.ReadFromJsonAsync<LevelUpNoticeResponse>();
    body!.BonusTexts.Should().BeEmpty();
}

[Fact]
public async Task Dismiss_by_an_unrelated_jogador_returns_403()
{
    var gmToken = await RegisterGmAndGetTokenAsync("SheetGmLevelUp3", "sheetlevelup3@teste.com");
    var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp3", "sheetplayerlevelup3@teste.com");
    var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "SheetPlayerLevelUp3b", "sheetplayerlevelup3b@teste.com");
    var campaignId = await CreateCampaignAsync(gmToken, "Campanha LevelUp 3");
    await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
    var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);

    var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/dismiss-level-up-notice", otherToken));

    response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
}
```

- [ ] **Step 7: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: the 3 new tests FAIL (404 route not found); earlier tests still pass.

- [ ] **Step 8: Add the level-up notice actions**

In `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs`, add:

```csharp
[HttpGet("api/character-sheets/{id}/level-up-notice")]
public async Task<ActionResult<LevelUpNoticeResponse>> LevelUpNotice(Guid id)
{
    var sheet = await db.CharacterSheets.FindAsync(id);
    if (sheet is null)
        return NotFound();

    var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
    if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
        return Forbid();

    var pending = LevelUpNoticeCalculator.PendingBonuses(sheet.LastDismissedLevelUpLevel, sheet.Nivel, rules.Niveis);
    return new LevelUpNoticeResponse(pending.Select(b => b.BonusText).ToList());
}

[HttpPost("api/character-sheets/{id}/dismiss-level-up-notice")]
public async Task<IActionResult> DismissLevelUpNotice(Guid id)
{
    var sheet = await db.CharacterSheets.FindAsync(id);
    if (sheet is null)
        return NotFound();

    var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
    if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
        return Forbid();

    sheet.LastDismissedLevelUpLevel = sheet.Nivel;
    await db.SaveChangesAsync();
    return NoContent();
}
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter CharacterSheetsControllerTests`
Expected: PASS (16/16).

- [ ] **Step 10: Commit**

```bash
git add src/RuinaRPG.Domain/CharacterSheets/LevelUpNoticeCalculator.cs src/RuinaRPG.Contracts/CharacterSheets/LevelUpNoticeResponse.cs src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/LevelUpNoticeCalculatorTests.cs tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs
git commit -m "feat: add the level-up notice"
```

---

### Task 10: Client — sheet list (via campaign detail) and Informações Básicas tab

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`
- Create: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`

**Interfaces:**
- Consumes: `CharacterSheetResponse`/`CreateCharacterSheetRequest`/`UpdateCharacterSheetRequest`/`LevelUpNoticeResponse` (Tasks 5-6, 9), `CampaignMemberResponse` (Campanha — Fundação plan), the authenticated `HttpClient`.
- Produces: the `/fichas/{id}` route, and a "criar ficha" control on `/campanhas/{id}`. No later task in this plan depends on these files; the next two Personagem plans extend `FichaDePersonagem.razor` with their own tabs.

- [ ] **Step 1: Add sheet creation to the campaign detail page**

In `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`, inside the existing `@foreach (var member in _members)` loop (added by the Campanha — Fundação plan), add a button per member that posts to `campaigns/{CampaignId}/character-sheets` and navigates to the new sheet:

```razor
<li>
    @member.Nickname (@member.Email)
    <button @onclick="@(() => CreateSheetForAsync(member.UserId))">Criar Ficha de Personagem</button>
</li>
```

Add to the `@code` block:

```csharp
private async Task CreateSheetForAsync(string ownerId)
{
    var response = await Http.PostAsJsonAsync($"campaigns/{CampaignId}/character-sheets", new RuinaRPG.Contracts.CharacterSheets.CreateCharacterSheetRequest(ownerId));
    if (!response.IsSuccessStatusCode)
    {
        _errorMessage = "Não foi possível criar a ficha.";
        return;
    }

    var body = await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.CharacterSheets.CharacterSheetResponse>();
    Navigation.NavigateTo($"fichas/{body!.Id}");
}
```

Add `@inject NavigationManager Navigation` to the top of the file if not already present (it already is, from the Campanha — Fundação plan's create-campaign flow... check the file: if it's missing, add it).

- [ ] **Step 2: Write the sheet page (Informações Básicas tab only)**

`src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`:

```razor
@page "/fichas/{SheetId}"
@inject HttpClient Http
@using RuinaRPG.Contracts.CharacterSheets

<h1>Ficha de Personagem</h1>

@if (_errorMessage is not null)
{
    <p class="error">@_errorMessage</p>
}

@if (_levelUpBonuses.Count > 0)
{
    <div class="level-up-notice">
        <h2>Novo Nível!</h2>
        <ul>
            @foreach (var bonus in _levelUpBonuses)
            {
                <li>@bonus</li>
            }
        </ul>
        <button @onclick="DismissLevelUpAsync">Fechar</button>
    </div>
}

<h2>Informações Básicas</h2>

<EditForm Model="_form" OnValidSubmit="SaveAsync">
    <label>Nome <InputText @bind-Value="_form.Nome" /></label>
    <label>
        Linhagem
        <select @bind="_form.Linhagem">
            <option value="">Escolha uma Linhagem</option>
            <option value="Humano">Humano</option>
            <option value="Phylauc">Phylauc</option>
            <option value="Nephrytes">Nephrytes</option>
            <option value="Econos">Ecônos</option>
        </select>
    </label>
    <label>Variante <InputText @bind-Value="_form.Variante" /></label>
    <label>
        Vocação
        <select @bind="_form.Vocacao">
            <option value="">Escolha uma Vocação</option>
            <option value="Campeao">Campeão</option>
            <option value="Cacador">Caçador</option>
            <option value="Feiticeiro">Feiticeiro</option>
            <option value="Adepto">Adepto</option>
            <option value="Bruxo">Bruxo</option>
        </select>
    </label>
    <label>Sub-vocação <InputText @bind-Value="_form.SubVocacao" /></label>
    <label>Afinidade <InputText @bind-Value="_form.Afinidade" /></label>
    <label>Propriedade <InputText @bind-Value="_form.Propriedade" /></label>

    <label>Nível <InputNumber @bind-Value="_form.Nivel" /></label>
    <p>@_form.GraduacaoLabel: @_form.Graduacao</p>
    <label>Possui Coração de Mana? <InputCheckbox @bind-Value="_form.PossuiCoracaoDeMana" /></label>
    <label>Experiência Atual <InputNumber @bind-Value="_form.ExperienciaAtual" /></label>
    <label>EAP Atual <InputNumber @bind-Value="_form.EAPAtual" /></label>
    <label>Pontos de Ignição Atual <InputNumber @bind-Value="_form.PontosDeIgnicaoAtual" /></label>
    <label>Pontos de Ignição Total <InputNumber @bind-Value="_form.PontosDeIgnicaoTotal" /></label>

    <h3>Âmbares Absorvidos</h3>
    <label>Rank F <InputNumber @bind-Value="_form.NucleosRankF" /></label>
    <label>Rank E <InputNumber @bind-Value="_form.NucleosRankE" /></label>
    <label>Rank D <InputNumber @bind-Value="_form.NucleosRankD" /></label>
    <label>Rank C <InputNumber @bind-Value="_form.NucleosRankC" /></label>
    <label>Rank B <InputNumber @bind-Value="_form.NucleosRankB" /></label>
    <label>Rank A <InputNumber @bind-Value="_form.NucleosRankA" /></label>
    <label>Rank S <InputNumber @bind-Value="_form.NucleosRankS" /></label>

    <h3>Recursos</h3>
    <label>Adrenalina (PA) Atual <InputNumber @bind-Value="_form.AdrenalinaAtual" /></label>
    <label>Foco (PF) Atual <InputNumber @bind-Value="_form.FocoAtual" /></label>
    <label>Estresse Atual <InputNumber @bind-Value="_form.EstresseAtual" /></label>
    <label>Vitalidade Atual <InputNumber @bind-Value="_form.VitalidadeAtual" /></label>

    <button type="submit">Salvar</button>
</EditForm>

@code {
    [Parameter] public string SheetId { get; set; } = "";

    private readonly SheetFormModel _form = new();
    private List<string> _levelUpBonuses = new();
    private string? _errorMessage;

    protected override Task OnInitializedAsync() => Task.WhenAll(LoadSheetAsync(), LoadLevelUpNoticeAsync());

    private async Task LoadSheetAsync()
    {
        var response = await Http.GetAsync($"character-sheets/{SheetId}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar a ficha.";
            return;
        }

        var sheet = await response.Content.ReadFromJsonAsync<CharacterSheetResponse>();
        _form.Nome = sheet!.Nome;
        _form.Linhagem = sheet.Linhagem;
        _form.Variante = sheet.Variante;
        _form.Vocacao = sheet.Vocacao;
        _form.SubVocacao = sheet.SubVocacao;
        _form.Afinidade = sheet.Afinidade;
        _form.Propriedade = sheet.Propriedade;
        _form.Nivel = sheet.Nivel;
        _form.Graduacao = sheet.Graduacao;
        _form.GraduacaoLabel = sheet.GraduacaoLabel;
        _form.PossuiCoracaoDeMana = sheet.PossuiCoracaoDeMana;
        _form.ExperienciaAtual = sheet.ExperienciaAtual;
        _form.EAPAtual = sheet.EAPAtual;
        _form.NucleosRankF = sheet.NucleosRankF;
        _form.NucleosRankE = sheet.NucleosRankE;
        _form.NucleosRankD = sheet.NucleosRankD;
        _form.NucleosRankC = sheet.NucleosRankC;
        _form.NucleosRankB = sheet.NucleosRankB;
        _form.NucleosRankA = sheet.NucleosRankA;
        _form.NucleosRankS = sheet.NucleosRankS;
        _form.PontosDeIgnicaoAtual = sheet.PontosDeIgnicaoAtual;
        _form.PontosDeIgnicaoTotal = sheet.PontosDeIgnicaoTotal;
        _form.VitalidadeAtual = sheet.VitalidadeAtual;
        _form.FocoAtual = sheet.FocoAtual;
        _form.AdrenalinaAtual = sheet.AdrenalinaAtual;
        _form.EstresseAtual = sheet.EstresseAtual;
        _form.Cobertura = sheet.Cobertura;
        _form.Ciclos = sheet.Ciclos;
        _errorMessage = null;
    }

    private async Task LoadLevelUpNoticeAsync()
    {
        var response = await Http.GetAsync($"character-sheets/{SheetId}/level-up-notice");
        if (response.IsSuccessStatusCode)
            _levelUpBonuses = (await response.Content.ReadFromJsonAsync<LevelUpNoticeResponse>())!.BonusTexts;
    }

    private async Task DismissLevelUpAsync()
    {
        await Http.PostAsync($"character-sheets/{SheetId}/dismiss-level-up-notice", null);
        _levelUpBonuses.Clear();
    }

    private async Task SaveAsync()
    {
        var request = new UpdateCharacterSheetRequest(null, _form.Nome, _form.Linhagem, _form.Variante, _form.Vocacao, _form.SubVocacao,
            _form.Afinidade, _form.Propriedade, _form.Nivel, _form.PossuiCoracaoDeMana, _form.ExperienciaAtual, _form.EAPAtual,
            _form.NucleosRankF, _form.NucleosRankE, _form.NucleosRankD, _form.NucleosRankC, _form.NucleosRankB, _form.NucleosRankA, _form.NucleosRankS,
            _form.PontosDeIgnicaoAtual, _form.PontosDeIgnicaoTotal, _form.VitalidadeAtual, _form.FocoAtual, _form.AdrenalinaAtual, _form.EstresseAtual,
            _form.Cobertura, _form.Ciclos);

        var response = await Http.PutAsJsonAsync($"character-sheets/{SheetId}", request);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível salvar a ficha.";
            return;
        }

        await LoadSheetAsync();
    }

    private class SheetFormModel
    {
        public string? Nome { get; set; }
        public string? Linhagem { get; set; }
        public string? Variante { get; set; }
        public string? Vocacao { get; set; }
        public string? SubVocacao { get; set; }
        public string? Afinidade { get; set; }
        public string? Propriedade { get; set; }
        public int Nivel { get; set; } = 1;
        public int Graduacao { get; set; }
        public string GraduacaoLabel { get; set; } = "Grau";
        public bool PossuiCoracaoDeMana { get; set; }
        public int ExperienciaAtual { get; set; }
        public int EAPAtual { get; set; }
        public int NucleosRankF { get; set; }
        public int NucleosRankE { get; set; }
        public int NucleosRankD { get; set; }
        public int NucleosRankC { get; set; }
        public int NucleosRankB { get; set; }
        public int NucleosRankA { get; set; }
        public int NucleosRankS { get; set; }
        public int PontosDeIgnicaoAtual { get; set; }
        public int PontosDeIgnicaoTotal { get; set; }
        public int VitalidadeAtual { get; set; }
        public int FocoAtual { get; set; }
        public int AdrenalinaAtual { get; set; }
        public int EstresseAtual { get; set; }
        public string Cobertura { get; set; } = "Nenhuma";
        public int Ciclos { get; set; }
    }
}
```

- [ ] **Step 3: Verify the Client builds**

Run: `dotnet build src/RuinaRPG.Client`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor src/RuinaRPG.Client/Pages/FichaDePersonagem.razor
git commit -m "feat: add character sheet creation flow and informacoes basicas tab"
```

---

### Task 11: End-to-end smoke test through Docker/nginx

**Files:**
- No new source files — this task only exercises what Tasks 1-10 built.

**Interfaces:**
- Consumes: the full stack, including everything this plan added.
- Produces: nothing new — this is the plan's acceptance test.

- [ ] **Step 1: Boot the full stack**

```bash
cp -n .env.example .env
make deploy
```

Expected: all 3 containers come up; migrations (including `AddCharacterSheets`) apply automatically in Development.

- [ ] **Step 2: Register a GM, a linked player, a campaign, add the player as a member, and create a sheet, through nginx**

```bash
curl -sf -X POST http://localhost/api/auth/register/gm \
  -H "Content-Type: application/json" \
  -d '{"nickname":"SmokeFichaGm","email":"smokefichagm@teste.com","senha":"Senha!123","confirmacaoSenha":"Senha!123"}' \
  | tee /tmp/gm-register.json
GM_TOKEN=$(jq -r .accessToken /tmp/gm-register.json)

curl -sf -X POST http://localhost/api/invite-codes -H "Authorization: Bearer $GM_TOKEN" | tee /tmp/invite.json
CODE=$(jq -r .code /tmp/invite.json)

curl -sf -X POST http://localhost/api/auth/register/jogador \
  -H "Content-Type: application/json" \
  -d "{\"nickname\":\"SmokeFichaPlayer\",\"email\":\"smokefichaplayer@teste.com\",\"senha\":\"Senha!123\",\"confirmacaoSenha\":\"Senha!123\",\"codigoDeAcesso\":\"$CODE\"}" \
  | tee /tmp/player-register.json
PLAYER_TOKEN=$(jq -r .accessToken /tmp/player-register.json)

curl -sf http://localhost/api/auth/me -H "Authorization: Bearer $PLAYER_TOKEN" | tee /tmp/me.json
PLAYER_ID=$(jq -r .id /tmp/me.json)

curl -sf -X POST http://localhost/api/campaigns -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d '{"nome":"Campanha de Fumaça","descricao":""}' | tee /tmp/campaign.json
CAMPAIGN_ID=$(jq -r .id /tmp/campaign.json)

curl -sf -X POST "http://localhost/api/campaigns/$CAMPAIGN_ID/members" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d "{\"userId\":\"$PLAYER_ID\"}"

curl -sf -X POST "http://localhost/api/campaigns/$CAMPAIGN_ID/character-sheets" -H "Authorization: Bearer $GM_TOKEN" -H "Content-Type: application/json" \
  -d "{\"ownerId\":\"$PLAYER_ID\"}" | tee /tmp/sheet.json
SHEET_ID=$(jq -r .id /tmp/sheet.json)
```

Expected: sheet create returns HTTP 201 with `"nivel":1`.

- [ ] **Step 3: Update the sheet as the owning player, through nginx**

```bash
curl -sf -X PUT "http://localhost/api/character-sheets/$SHEET_ID" -H "Authorization: Bearer $PLAYER_TOKEN" -H "Content-Type: application/json" \
  -d '{"imageId":null,"nome":"Vann Astrel","linhagem":"Humano","variante":"Sinir","vocacao":"Campeao","subVocacao":"Duelista","afinidade":"Fogo","propriedade":"","nivel":2,"possuiCoracaoDeMana":false,"experienciaAtual":150,"eapAtual":150,"nucleosRankF":0,"nucleosRankE":0,"nucleosRankD":0,"nucleosRankC":0,"nucleosRankB":0,"nucleosRankA":0,"nucleosRankS":0,"pontosDeIgnicaoAtual":10,"pontosDeIgnicaoTotal":10,"vitalidadeAtual":20,"focoAtual":8,"adrenalinaAtual":10,"estresseAtual":0,"cobertura":"Nenhuma","ciclos":0}'
```

Expected: HTTP 204.

- [ ] **Step 4: Confirm the level-up notice reflects both levels through nginx**

```bash
curl -sf "http://localhost/api/character-sheets/$SHEET_ID/level-up-notice" -H "Authorization: Bearer $PLAYER_TOKEN"
```

Expected: HTTP 200, `bonusTexts` with 2 entries (Nível 1 and 2's bonus text).

- [ ] **Step 5: Tear down**

```bash
make down
```

- [ ] **Step 6: Commit (only if any step required a fix)**

```bash
git add -A
git commit -m "chore: verify the ficha de personagem foundation flow end-to-end through nginx"
```

## Explicitly out of scope for this plan

- Tabs 2-6 (Atributos & Perícias, Combate, Magias & Habilidades, Posses, Diário) — the next two Personagem plans.
- Computed **máximo** display and enforcement for Vitalidade/Foco/Adrenalina (needs Atributos from tab 2 and Artefatos from tab 5) — deferred, tracked explicitly in the plan Architecture note. Estresse's máximo is a flat 10 with no dependency, but even that display polish is left to the tab-2/tab-5 plans for consistency (all four resources get their máximo treatment together).
- Image upload UI on the sheet's Identidade section — the field (`ImageId`) exists and round-trips, but the Client page doesn't yet offer an upload/reference control for it (same pattern as the Catálogo item form, Task 10 of that plan — straightforward to add later, not required by any requirement this specific plan implements beyond the field existing).
- Sub-vocação/Afinidade dropdown population from `IRulesDataProvider`/`Tabela de Classes` on the Client — the Client form currently uses free-text inputs for these two fields rather than a populated dropdown; the API stores whatever string is sent without validating it against the known list. Cheap follow-up, not a defect in what's required here (R0001 describes the *interaction model*, not a hard validation rule enforced server-side).
