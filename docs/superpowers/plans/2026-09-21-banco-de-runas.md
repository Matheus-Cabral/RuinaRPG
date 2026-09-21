# Banco de Runas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Criar o Banco de Runas do GM — o equivalente, para Runas, do Banco de Magias e Habilidades — com integração às fichas de Personagem e NPC e à Campanha.

**Architecture:** Pilha paralela e dedicada, sem tocar no Banco de Magias: entidade `RuneBankEntry`, `RuneBankController`, novo alvo de anexo de campanha (`CampaignAttachment.RuneBankEntryId`), endpoint `available-runes` para o seletor do jogador e um seletor de origem ("do zero" / "do banco") em 4.d das duas fichas. Toda Runa criada numa ficha grava uma cópia independente no banco do GM; quando o criador é o jogador, a cópia também vira anexo público da campanha.

**Tech Stack:** .NET 8, ASP.NET Core (controllers), EF Core + Npgsql (PostgreSQL), Blazor WebAssembly + MudBlazor, xUnit + FluentAssertions + bUnit, Testcontainers (integração).

**Spec:** `docs/superpowers/specs/2026-09-21-banco-de-runas-design.md`

## Global Constraints

- **Todo o texto de UI, erros da API e docs em português do Brasil.**
- **TDD é obrigatório** (Técnico R0011): teste falhando primeiro, depois o mínimo de código, para cada unidade de comportamento.
- `dotnet build` termina com **0 warnings, 0 errors**; warning novo é falha.
- Os campos da Runa continuam **`Nome`, `Descricao`, `Grau`** — nenhum campo extra (restrição do usuário).
- O banco **começa vazio**: nenhuma migração de dados, nenhum backfill das Runas já existentes nas fichas.
- **Ficha de Criatura não tem Runas** — não tocar em nada de Criatura.
- `GET api/rune-bank` é **GM-only** (diferença deliberada em relação ao banco de magias). O jogador só vê o que foi anexado como público, via `campaigns/{id}/available-runes`.
- Enums são gravados como inteiro (sem `HasConversion`); Runa não usa enum.
- `SourceBankEntryId` em `CharacterRune`/`NpcRune` é um `Guid?` **sem FK** (como `CharacterSpellAbility`).
- Migração EF: `dotnet ef migrations add <Nome> --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`.
- Commits terminam com a linha `Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.
- Depois do deploy é preciso `make migrate` (o stack local roda em Production e não aplica migrations sozinho).
- Testes de integração exigem Docker. Para rodar um recorte: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NomeDaClasse"`. A suíte completa tem um flake conhecido (~1%, timeouts de ~100 s em classes dispersas): se algo sem relação falhar por timeout, rode a classe isolada.
- Branch de trabalho: `feat/banco-de-runas` (o spec já está commitado nela). Nomes de apelido/e-mail dos testes de integração devem ser **únicos por teste** (o container de banco é compartilhado).

## File Structure

**Criar**
- `src/RuinaRPG.Infrastructure/Runes/RuneBankEntry.cs` — entidade do banco.
- `src/RuinaRPG.Contracts/Runes/CreateRuneBankEntryRequest.cs`, `UpdateRuneBankEntryRequest.cs`, `RuneBankEntryResponse.cs` — contratos.
- `src/RuinaRPG.Api/Controllers/RuneBankController.cs` — CRUD + lista com filtros (GM-only).
- `src/RuinaRPG.Client/Pages/BancoDeRunas.razor`, `BancoDeRunasForm.razor` — páginas do GM.
- `src/RuinaRPG.Client/Shared/Fields/RuneOrigemModel.cs`, `RuneOrigemFields.razor` — seletor de origem compartilhado pelas duas fichas.
- `Docs/Requisitos/Requisitos - Banco de Runas.md` — requisitos.
- Testes: `tests/RuinaRPG.Tests.Integration/Persistence/RuneBankMigrationTests.cs`, `tests/RuinaRPG.Tests.Integration/Controllers/RuneBankControllerTests.cs`, `tests/RuinaRPG.Tests.Client/Pages/BancoDeRunasFormTests.cs`, `tests/RuinaRPG.Tests.Client/Shared/Fields/RuneOrigemFieldsTests.cs`.

**Modificar**
- `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterRune.cs`, `NpcSheets/NpcRune.cs` — `SourceBankEntryId`.
- `src/RuinaRPG.Infrastructure/Campaigns/CampaignAttachment.cs`, `src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTarget.cs`, `CampaignAttachmentTargetValidator.cs`.
- `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs` — DbSet + mapeamento; nova migration.
- `src/RuinaRPG.Contracts/Campaigns/AttachToCampaignRequest.cs`, `CharacterSheets/AddCharacterRuneRequest.cs`, `NpcSheets/AddNpcRuneRequest.cs`.
- `src/RuinaRPG.Api/Controllers/CampaignAttachmentsController.cs`, `CampaignCatalogController.cs`, `CampaignPlayerViewController.cs`, `CharacterRunesController.cs`, `NpcRunesController.cs`.
- `src/RuinaRPG.Client/Layout/NavMenu.razor`, `Shared/TipoChip.razor`, `Pages/FichaDePersonagem.razor`, `Pages/FichaDeNpc.razor`, `Pages/CampanhaDetalhe.razor`.
- Docs: `Requisitos - Modelo de Dados.md`, `Requisitos - Campanha.md`, `Requisitos - Ficha de Personagem.md`, `Requisitos - Ficha de NPCs.md`.

---

### Task 1: Schema — tabela do banco, colunas novas, alvo de anexo e migration

**Files:**
- Create: `src/RuinaRPG.Infrastructure/Runes/RuneBankEntry.cs`
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterRune.cs`
- Modify: `src/RuinaRPG.Infrastructure/NpcSheets/NpcRune.cs`
- Modify: `src/RuinaRPG.Infrastructure/Campaigns/CampaignAttachment.cs`
- Modify: `src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTarget.cs`
- Modify: `src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTargetValidator.cs`
- Modify: `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`
- Create (gerado): `src/RuinaRPG.Infrastructure/Persistence/Migrations/<timestamp>_AddRuneBank.cs` (+ `.Designer.cs`, snapshot atualizado)
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md`
- Test: `tests/RuinaRPG.Tests.Unit/Campaigns/CampaignAttachmentTargetValidatorTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/RuneBankMigrationTests.cs`

**Interfaces:**
- Produces:
  - `RuinaRPG.Infrastructure.Runes.RuneBankEntry { Guid Id; Guid GmId; string Nome; string Descricao; int Grau }` (Nome/Descricao `required`).
  - `RuinaRpgDbContext.RuneBankEntries : DbSet<RuneBankEntry>`.
  - `CharacterRune.SourceBankEntryId : Guid?`, `NpcRune.SourceBankEntryId : Guid?`.
  - `CampaignAttachment.RuneBankEntryId : Guid?` (FK cascade para `RuneBankEntry`).
  - `CampaignAttachmentTarget.RuneBankEntry`.
  - `CampaignAttachmentTargetValidator.ExactlyOneSet(params string?[] targetIds) : bool`.

- [ ] **Step 1: Write the failing unit test**

Acrescente ao fim da classe `CampaignAttachmentTargetValidatorTests` (antes da chave de fechamento):

```csharp
    [Fact]
    public void ExactlyOneSet_counts_the_rune_bank_target_as_a_sixth_alternative()
    {
        CampaignAttachmentTargetValidator.ExactlyOneSet(null, null, null, null, null, "rune-1").Should().BeTrue();
        CampaignAttachmentTargetValidator.ExactlyOneSet("item-1", null, null, null, null, "rune-1").Should().BeFalse();
        CampaignAttachmentTargetValidator.ExactlyOneSet(null, null, null, null, null, null).Should().BeFalse();
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~CampaignAttachmentTargetValidatorTests" 2>&1 | grep -E "error|Passed!|Failed"`
Expected: erro de compilação — `ExactlyOneSet` não aceita 6 argumentos.

- [ ] **Step 3: Implement the validator, enum and entities**

`src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTargetValidator.cs` (substitua o conteúdo):

```csharp
namespace RuinaRPG.Domain.Campaigns;

public static class CampaignAttachmentTargetValidator
{
    // "params" so every target FK a CampaignAttachment can carry (item, NPC, criatura, banco de
    // magias, imagem, banco de runas) is counted the same way — exactly one must be set.
    public static bool ExactlyOneSet(params string?[] targetIds) =>
        targetIds.Count(id => id is not null) == 1;
}
```

`src/RuinaRPG.Domain/Campaigns/CampaignAttachmentTarget.cs` — acrescente `RuneBankEntry` ao fim do enum:

```csharp
namespace RuinaRPG.Domain.Campaigns;

public enum CampaignAttachmentTarget
{
    Item,
    NpcSheet,
    CreatureSheet,
    SpellAbilityBankEntry,
    Image,
    RuneBankEntry
}
```

Crie `src/RuinaRPG.Infrastructure/Runes/RuneBankEntry.cs`:

```csharp
namespace RuinaRPG.Infrastructure.Runes;

/// <summary>
/// Uma Runa na biblioteca do GM (Requisitos - Banco de Runas). Os campos são os mesmos de
/// CharacterRune/NpcRune — Nome, Descrição e Grau; toda Runa de ficha tem uma cópia independente aqui.
/// </summary>
public class RuneBankEntry
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Grau { get; set; }
}
```

`src/RuinaRPG.Infrastructure/CharacterSheets/CharacterRune.cs` e `src/RuinaRPG.Infrastructure/NpcSheets/NpcRune.cs` — acrescente, depois de `Grau`:

```csharp
    /// <summary>Só rastreio de origem (sem FK): apagar a entrada do banco não afeta a Runa da ficha.</summary>
    public Guid? SourceBankEntryId { get; set; }
```

`src/RuinaRPG.Infrastructure/Campaigns/CampaignAttachment.cs` — acrescente depois de `ImageId`:

```csharp
    public Guid? RuneBankEntryId { get; set; }
```

- [ ] **Step 4: Map them in the DbContext**

Em `src/RuinaRPG.Infrastructure/Persistence/RuinaRpgDbContext.cs`:

1. Acrescente `using RuinaRPG.Infrastructure.Runes;` junto dos outros `using`.
2. Depois da linha `public DbSet<SpellAbilityBankEffect> SpellAbilityBankEffects => Set<SpellAbilityBankEffect>();` acrescente:

```csharp
    public DbSet<RuneBankEntry> RuneBankEntries => Set<RuneBankEntry>();
```

3. Logo depois do bloco `builder.Entity<SpellAbilityBankEntry>(entity => { ... });` acrescente:

```csharp
        builder.Entity<RuneBankEntry>(entity =>
        {
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(e => e.GmId)
                .OnDelete(DeleteBehavior.Cascade);
        });
```

4. No bloco `builder.Entity<CampaignAttachment>(entity => { ... })`, depois da linha do `SpellAbilityBankEntry`, acrescente:

```csharp
            entity.HasOne<RuneBankEntry>().WithMany().HasForeignKey(a => a.RuneBankEntryId).OnDelete(DeleteBehavior.Cascade);
```

- [ ] **Step 5: Generate the migration**

Run: `dotnet ef migrations add AddRuneBank --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations`
Expected: `Done.` e um arquivo `..._AddRuneBank.cs`. Abra-o e confirme que o `Up` contém: `CreateTable "RuneBankEntries"`, `AddColumn "SourceBankEntryId"` em `CharacterRunes` e em `NpcRunes`, `AddColumn "RuneBankEntryId"` em `CampaignAttachments`, um índice e as duas FKs (`RuneBankEntries → AspNetUsers`, `CampaignAttachments → RuneBankEntries`). Nenhum `INSERT` (sem backfill).

- [ ] **Step 6: Write the migration integration test**

Crie `tests/RuinaRPG.Tests.Integration/Persistence/RuneBankMigrationTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Runes;

namespace RuinaRPG.Tests.Integration.Persistence;

public class RuneBankMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RuneBankMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_rune_bank_table_and_the_new_columns()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddRuneBank"));

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = $"gm{suffix}@runebank.test", Email = $"gm{suffix}@runebank.test", Nickname = $"RuneBank{suffix}", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var entry = new RuneBankEntry { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Runa do Fogo", Descricao = "Queima o alvo.", Grau = 2 };
        db.RuneBankEntries.Add(entry);
        await db.SaveChangesAsync();

        (await db.RuneBankEntries.CountAsync(e => e.GmId == gm.Id)).Should().Be(1);

        (await ColumnsOfAsync(db, "CharacterRunes")).Should().Contain("SourceBankEntryId");
        (await ColumnsOfAsync(db, "NpcRunes")).Should().Contain("SourceBankEntryId");
        (await ColumnsOfAsync(db, "CampaignAttachments")).Should().Contain("RuneBankEntryId");

        db.RuneBankEntries.Remove(entry);
        await db.SaveChangesAsync();
    }

    private static Task<List<string>> ColumnsOfAsync(RuinaRpgDbContext db, string table) =>
        db.Database
            .SqlQueryRaw<string>($"SELECT CAST(column_name AS text) AS \"Value\" FROM information_schema.columns WHERE table_name = '{table}'")
            .ToListAsync();
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | grep -E "error|warning|Build succeeded"` → `Build succeeded.` com 0 warnings.
Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter "FullyQualifiedName~CampaignAttachmentTargetValidatorTests" 2>&1 | grep -E "Passed!|Failed"` → `Passed!` (todos, incluindo os 3 antigos).
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RuneBankMigrationTests|FullyQualifiedName~CampaignAttachmentMigrationTests|FullyQualifiedName~CharacterRuneAndMasteryMigrationTests" 2>&1 | grep -E "Passed!|Failed"` → `Passed!`.

- [ ] **Step 8: Update the data-model doc**

Em `Docs/Requisitos/Requisitos - Modelo de Dados.md` (use Edit; cada trecho abaixo é único no arquivo):

a) Logo depois do parágrafo que começa com `O mesmo par de tabelas (entrada + efeitos) se repete, como **cópia independente**` (e antes da linha em branco seguida de `# 5. Campanhas`), insira:

```markdown
**RuneBankEntries** *(ver "[[Requisitos - Banco de Runas]]")* — biblioteca de Runas do GM; cada Runa criada numa ficha (Personagem ou NPC) tem uma cópia independente aqui.

| Coluna | Tipo | Nota |
|---|---|---|
| Id | PK | |
| GmId | FK → Users | |
| Nome | string | |
| Descricao | text | |
| Grau | int | |
```

b) Em `**CampaignAttachments** — polimórfico via 5 FKs anuláveis (exatamente uma preenchida por linha).` troque `5 FKs` por `6 FKs`; depois da linha `| ImageId | FK → Images, nullable | imagem avulsa (R0007 da Campanha) |` insira:

```markdown
| RuneBankEntryId | FK → RuneBankEntries, nullable | |
```

e na linha `| IsPublic | bool | usado quando ItemId/SpellAbilityBankEntryId/ImageId está setado |` troque `ItemId/SpellAbilityBankEntryId/ImageId` por `ItemId/SpellAbilityBankEntryId/RuneBankEntryId/ImageId`.

c) Na tabela `**CharacterRunes** (4.d)`, depois da linha `| Grau | int |`, insira:

```markdown
| SourceBankEntryId | FK → RuneBankEntries, nullable (só rastreabilidade — R0003 do Banco de Runas) |
```

d) Na seção 6.2 (NPC), depois do último item da lista que segue o parágrafo `Mesma família completa de tabelas filhas (...)` (o item `- Ganha `NomePublico`/`ImagemPublica` **não** ...`), acrescente:

```markdown
- `NpcRunes` ganha `SourceBankEntryId` (FK → RuneBankEntries, nullable), como `CharacterRunes`.
```

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Infrastructure src/RuinaRPG.Domain tests/RuinaRPG.Tests.Unit tests/RuinaRPG.Tests.Integration/Persistence "Docs/Requisitos/Requisitos - Modelo de Dados.md"
git commit -m "feat: schema do Banco de Runas (tabela, SourceBankEntryId e alvo de anexo)

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 2: API do Banco de Runas (`api/rune-bank`) e requisitos

**Files:**
- Create: `src/RuinaRPG.Contracts/Runes/CreateRuneBankEntryRequest.cs`
- Create: `src/RuinaRPG.Contracts/Runes/UpdateRuneBankEntryRequest.cs`
- Create: `src/RuinaRPG.Contracts/Runes/RuneBankEntryResponse.cs`
- Create: `src/RuinaRPG.Api/Controllers/RuneBankController.cs`
- Create: `Docs/Requisitos/Requisitos - Banco de Runas.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/RuneBankControllerTests.cs`

**Interfaces:**
- Consumes: `RuneBankEntry` e `RuinaRpgDbContext.RuneBankEntries` (Task 1).
- Produces:
  - `record CreateRuneBankEntryRequest(string Nome, string Descricao, int Grau)`, `record UpdateRuneBankEntryRequest(string Nome, string Descricao, int Grau)` e `record RuneBankEntryResponse(string Id, string Nome, string Descricao, int Grau)`, todos em `RuinaRPG.Contracts.Runes`.
  - Endpoints (todos `[Authorize(Roles = "GM")]`): `POST api/rune-bank` → 201 `RuneBankEntryResponse`; `GET api/rune-bank?nome=&grau=` → 200 `List<RuneBankEntryResponse>` (só do GM logado); `PUT api/rune-bank/{id}` → 204 / 404; `DELETE api/rune-bank/{id}` → 204 / 404.

- [ ] **Step 1: Write the failing tests**

Crie `tests/RuinaRPG.Tests.Integration/Controllers/RuneBankControllerTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Invites;
using RuinaRPG.Contracts.Runes;

namespace RuinaRPG.Tests.Integration.Controllers;

public class RuneBankControllerTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public RuneBankControllerTests(PostgresFixture postgres) => _postgres = postgres;

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
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nickname, email, "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<string> RegisterJogadorTokenAsync(string gmToken, string nickname, string email)
    {
        var codeMessage = new HttpRequestMessage(HttpMethod.Post, "/api/invite-codes");
        codeMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", gmToken);
        var codeResponse = await _client.SendAsync(codeMessage);
        var code = (await codeResponse.Content.ReadFromJsonAsync<InviteCodeResponse>())!.Code;

        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nickname, email, "Senha!123", "Senha!123", code));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<RuneBankEntryResponse> CreateAsync(string token, string nome, string descricao, int grau)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", token, new CreateRuneBankEntryRequest(nome, descricao, grau)));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!;
    }

    private async Task<List<RuneBankEntryResponse>> ListAsync(string token, string query = "")
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/rune-bank{query}", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    [Fact]
    public async Task Create_without_a_token_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/rune-bank", new CreateRuneBankEntryRequest("Runa", "Desc.", 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Create_as_the_gm_returns_201_with_the_saved_fields()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm1", "runebankgm1@teste.com");

        var created = await CreateAsync(token, "Runa do Fogo", "Queima o alvo.", 2);

        created.Nome.Should().Be("Runa do Fogo");
        created.Descricao.Should().Be("Queima o alvo.");
        created.Grau.Should().Be(2);
        created.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Every_endpoint_is_forbidden_to_a_jogador()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBankGm2", "runebankgm2@teste.com");
        var jogadorToken = await RegisterJogadorTokenAsync(gmToken, "RuneBankJogador2", "runebankjogador2@teste.com");
        var entry = await CreateAsync(gmToken, "Runa Secreta", "Só o GM vê.", 1);

        var post = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", jogadorToken, new CreateRuneBankEntryRequest("X", "Y", 1)));
        var get = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rune-bank", jogadorToken));
        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", jogadorToken, new UpdateRuneBankEntryRequest("X", "Y", 1)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", jogadorToken));

        post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        get.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        put.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        delete.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_returns_only_the_entries_of_the_authenticated_gm()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("RuneBankGmA3", "runebankgma3@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("RuneBankGmB3", "runebankgmb3@teste.com");
        var minha = await CreateAsync(tokenA, "Runa do GM A", "A.", 1);
        var alheia = await CreateAsync(tokenB, "Runa do GM B", "B.", 1);

        var body = await ListAsync(tokenA);

        body.Should().Contain(e => e.Id == minha.Id);
        body.Should().NotContain(e => e.Id == alheia.Id);
    }

    [Fact]
    public async Task List_filters_by_nome_case_insensitively_and_by_grau()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm4", "runebankgm4@teste.com");
        var fogo1 = await CreateAsync(token, "Runa do Fogo", "F.", 1);
        var fogo3 = await CreateAsync(token, "Runa do FOGO Maior", "F3.", 3);
        var gelo1 = await CreateAsync(token, "Runa do Gelo", "G.", 1);

        var porNome = await ListAsync(token, "?nome=fogo");
        porNome.Select(e => e.Id).Should().BeEquivalentTo([fogo1.Id, fogo3.Id]);

        var porGrau = await ListAsync(token, "?grau=1");
        porGrau.Select(e => e.Id).Should().BeEquivalentTo([fogo1.Id, gelo1.Id]);

        var combinado = await ListAsync(token, "?nome=fogo&grau=3");
        combinado.Select(e => e.Id).Should().BeEquivalentTo([fogo3.Id]);
    }

    [Fact]
    public async Task Update_replaces_the_fields_and_returns_204()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm5", "runebankgm5@teste.com");
        var entry = await CreateAsync(token, "Runa Velha", "Antiga.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", token, new UpdateRuneBankEntryRequest("Runa Nova", "Atual.", 4)));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var atualizada = (await ListAsync(token)).Single(e => e.Id == entry.Id);
        atualizada.Nome.Should().Be("Runa Nova");
        atualizada.Descricao.Should().Be("Atual.");
        atualizada.Grau.Should().Be(4);
    }

    [Fact]
    public async Task Update_and_delete_of_another_gms_entry_return_404()
    {
        var tokenA = await RegisterGmAndGetTokenAsync("RuneBankGmA6", "runebankgma6@teste.com");
        var tokenB = await RegisterGmAndGetTokenAsync("RuneBankGmB6", "runebankgmb6@teste.com");
        var entry = await CreateAsync(tokenA, "Runa do A", "A.", 1);

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{entry.Id}", tokenB, new UpdateRuneBankEntryRequest("Roubada", "X.", 9)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", tokenB));

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ListAsync(tokenA)).Single(e => e.Id == entry.Id).Nome.Should().Be("Runa do A");
    }

    [Fact]
    public async Task Delete_removes_the_entry_and_returns_204()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm7", "runebankgm7@teste.com");
        var entry = await CreateAsync(token, "Runa Efêmera", "Some.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{entry.Id}", token));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ListAsync(token)).Should().NotContain(e => e.Id == entry.Id);
    }

    [Fact]
    public async Task Update_and_delete_of_an_unknown_id_return_404()
    {
        var token = await RegisterGmAndGetTokenAsync("RuneBankGm8", "runebankgm8@teste.com");

        var put = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/rune-bank/{Guid.NewGuid()}", token, new UpdateRuneBankEntryRequest("X", "Y", 1)));
        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/rune-bank/{Guid.NewGuid()}", token));

        put.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RuneBankControllerTests" 2>&1 | grep -E "error CS|Passed!|Failed"`
Expected: erro de compilação — o namespace `RuinaRPG.Contracts.Runes` não existe.

- [ ] **Step 3: Create the contracts**

`src/RuinaRPG.Contracts/Runes/CreateRuneBankEntryRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Runes;

public record CreateRuneBankEntryRequest(string Nome, string Descricao, int Grau);
```

`src/RuinaRPG.Contracts/Runes/UpdateRuneBankEntryRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.Runes;

public record UpdateRuneBankEntryRequest(string Nome, string Descricao, int Grau);
```

`src/RuinaRPG.Contracts/Runes/RuneBankEntryResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Runes;

public record RuneBankEntryResponse(string Id, string Nome, string Descricao, int Grau);
```

- [ ] **Step 4: Implement the controller**

`src/RuinaRPG.Api/Controllers/RuneBankController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Runes;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Runes;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Banco de Runas do GM (Requisitos - Banco de Runas). Diferente do Banco de Magias — cujo GET
/// resolve o GM efetivo e deixa um jogador ler o banco inteiro —, tudo aqui é GM-only: o jogador só
/// enxerga as Runas que o GM anexou como públicas à campanha (CampaignCatalogController,
/// "available-runes").
/// </summary>
[ApiController]
[Route("api/rune-bank")]
[Authorize(Roles = "GM")]
public class RuneBankController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RuneBankEntryResponse>> Create(CreateRuneBankEntryRequest request)
    {
        var entry = new RuneBankEntry
        {
            Id = Guid.NewGuid(),
            GmId = CurrentUserId(),
            Nome = request.Nome,
            Descricao = request.Descricao,
            Grau = request.Grau
        };
        db.RuneBankEntries.Add(entry);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(entry));
    }

    [HttpGet]
    public async Task<ActionResult<List<RuneBankEntryResponse>>> List([FromQuery] string? nome, [FromQuery] int? grau)
    {
        var gmId = CurrentUserId();
        var query = db.RuneBankEntries.Where(e => e.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(e => EF.Functions.ILike(e.Nome, $"%{nome}%"));

        if (grau is not null)
            query = query.Where(e => e.Grau == grau);

        var entries = await query.OrderBy(e => e.Nome).ToListAsync();
        return entries.Select(ToResponse).ToList();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateRuneBankEntryRequest request)
    {
        var gmId = CurrentUserId();
        var entry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
        if (entry is null)
            return NotFound();

        entry.Nome = request.Nome;
        entry.Descricao = request.Descricao;
        entry.Grau = request.Grau;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var gmId = CurrentUserId();
        var entry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
        if (entry is null)
            return NotFound();

        db.RuneBankEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static RuneBankEntryResponse ToResponse(RuneBankEntry entry) =>
        new(entry.Id.ToString(), entry.Nome, entry.Descricao, entry.Grau);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | grep -E "error|warning|Build succeeded"` → `Build succeeded.`, 0 warnings.
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~RuneBankControllerTests" 2>&1 | grep -E "Passed!|Failed"`
Expected: `Passed!  - Failed: 0, Passed: 9`.

- [ ] **Step 6: Write the requirements document**

Crie `Docs/Requisitos/Requisitos - Banco de Runas.md`:

```markdown
> Esta página não corresponde a nenhum bloco do "[[01 - Visão Geral.canvas]]" ainda — é um subsistema novo, complementar a "[[Requisitos - Ficha de Personagem]]" (4.d) e "[[Requisitos - Ficha de NPCs]]". A Ficha de Criatura não tem Runas (ver "[[Requisitos - Ficha de Criaturas]]" R0004) e, portanto, não usa este banco.

  

> **Modelo de edição**: o banco pertence à conta do GM (não a uma Campanha específica — ver "[[Requisitos - Campanha]]" para como uma entrada dele é opcionalmente anexada e liberada numa Campanha). O GM tem acesso total a qualquer entrada, a qualquer momento. Jogadores não têm tela própria para o banco nem leitura direta dele (ver R0008): só o alcançam a partir de 4.d nas fichas que controlam, e apenas para reutilizar entradas que o GM liberou como públicas na campanha (ver R0003).

  

> **Convenção de campos**: mesma de "[[Requisitos - Ficha de Personagem]]" — valor padrão do banco de dados, placeholder em itálico quando NULL, travessão para campos não aplicáveis.

  

# **R0001** - Toda Runa criada em qualquer ficha é adicionada automaticamente ao banco.

**Descrição**: Sempre que uma Runa é criada em 4.d de uma Ficha de Personagem ou de uma Ficha de NPC, uma cópia dela é automaticamente salva neste banco geral do GM — tanto quando criada por um jogador quanto quando criada pelo GM, sem etapa de aprovação, e tanto quando montada do zero quanto quando partiu de uma entrada existente do banco (R0003). A cópia no banco é **independente**: editar a Runa na ficha de origem depois de criada não altera a cópia no banco, e vice-versa. Uma Runa que já existia numa ficha antes deste banco existir **não** é copiada retroativamente — o banco começa vazio.

  

# **R0002** - O GM pode criar uma entrada diretamente no banco.

**Descrição**: Além das entradas vindas automaticamente de fichas (R0001), o GM pode criar uma nova entrada direto no banco, sem que ela venha de nenhuma ficha específica.

  

# **R0003** - Ao adicionar uma Runa numa ficha, é possível partir de uma entrada existente do banco.

**Descrição**: Ao adicionar uma Runa em 4.d de uma Ficha de Personagem ou de NPC, o jogador/GM pode escolher entre montar do zero (ver "[[Requisitos - Ficha de Personagem]]" 4.d) ou selecionar uma entrada já existente no banco, que preenche todos os campos automaticamente. O GM pode escolher qualquer entrada do próprio banco; um jogador só pode escolher entre as entradas que o GM anexou à campanha da ficha como **públicas** (R0007). A partir da escolha, a Runa na ficha é uma cópia independente (mesmo comportamento de R0001) — editá-la depois não afeta a entrada original do banco.

  

# **R0004** - O banco deve ser listado com filtros.

**Descrição**: Uma lista exibe todas as entradas do banco, com filtros combináveis por **Nome** (contém, sem diferenciar maiúsculas de minúsculas) e **Grau** (igual).

  

# **R0005** - Campos de uma entrada do banco.

**Descrição**: Os mesmos campos de uma Runa em "[[Requisitos - Ficha de Personagem]]" 4.d: **Nome** (texto), **Descrição** (texto livre) e **Grau** (número inteiro). Nenhum campo é calculado. Como na ficha, o Grau não é limitado ao Grau do personagem.

  

# **R0006** - O GM pode editar e excluir qualquer entrada do banco.

**Descrição**: A partir da lista (R0004), o GM pode editar qualquer campo de uma entrada ou excluí-la. Excluir uma entrada do banco não afeta nenhuma ficha que já a usou como base (ver R0003) — são cópias independentes; anexos de campanha dessa entrada, porém, são removidos junto com ela.

  

# **R0007** - A cópia criada por um jogador também vira um anexo público da campanha.

> Quando quem cria a Runa (do zero ou reaproveitando outra) é um **jogador**, não o GM que gerencia a ficha, a cópia independente que R0001 já cria no banco também é anexada automaticamente à campanha daquela ficha como pública, conforme "[[Requisitos - Campanha]]" R0012. Para um NPC concedido a um jogador, a campanha é a da concessão (ver "[[Requisitos - Campanha]]" R0010).

  

# **R0008** - O banco em si é visível apenas ao GM.

**Descrição**: Diferente do "[[Requisitos - Banco de Magias e Habilidades]]" (cuja lista um jogador vinculado consegue ler por inteiro), o Banco de Runas é GM-only — listar, criar, editar e excluir entradas. O jogador nunca lê o banco privado do GM: ele só enxerga as entradas anexadas como públicas a uma campanha da qual é membro (ver "[[Requisitos - Campanha]]" R0008 e R0009).
```

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Contracts/Runes src/RuinaRPG.Api/Controllers/RuneBankController.cs tests/RuinaRPG.Tests.Integration/Controllers/RuneBankControllerTests.cs "Docs/Requisitos/Requisitos - Banco de Runas.md"
git commit -m "feat: API do Banco de Runas (GM-only) e requisitos

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 3: Integração com a Campanha — anexar, `available-runes`, visão do jogador

**Files:**
- Modify: `src/RuinaRPG.Contracts/Campaigns/AttachToCampaignRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignAttachmentsController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignCatalogController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs`
- Modify: `Docs/Requisitos/Requisitos - Campanha.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignAttachmentsControllerTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignCatalogControllerTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignPlayerViewControllerTests.cs`

**Interfaces:**
- Consumes: `RuneBankEntry`, `db.RuneBankEntries`, `CampaignAttachment.RuneBankEntryId`, `CampaignAttachmentTargetValidator.ExactlyOneSet(params string?[])` (Task 1); `RuneBankEntryResponse`, `CreateRuneBankEntryRequest`, `POST api/rune-bank` (Task 2).
- Produces:
  - `record AttachToCampaignRequest(string? ItemId, string? NpcSheetId, string? CreatureSheetId, string? SpellAbilityBankEntryId, string? ImageId, string? RuneBankEntryId = null)`.
  - `POST api/campaigns/{id}/attachments` aceita `RuneBankEntryId` (privado por padrão; 400 se a entrada não é do GM); `CampaignAttachmentResponse.Tipo == "RuneBankEntry"`, `Nome` = nome da Runa.
  - `GET api/campaigns/{campaignId}/available-runes?nome=` → `List<RuneBankEntryResponse>` só com as públicas (membro ou GM da campanha).
  - `PlayerCampaignViewResponse.AnexosPublicos` inclui `Tipo == "RuneBankEntry"` com o nome da Runa.

- [ ] **Step 1: Write the failing tests**

**`CampaignAttachmentsControllerTests.cs`** — acrescente `using RuinaRPG.Contracts.Runes;` no topo e, depois do helper `CreateBankEntryAsync`, o helper:

```csharp
    private async Task<string> CreateRuneEntryAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken,
            new CreateRuneBankEntryRequest(nome, "Descrição.", 1)));
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!.Id;
    }
```

e ao fim da classe estes testes:

```csharp
    [Fact]
    public async Task Attach_a_rune_bank_entry_defaults_to_private_and_reports_the_RuneBankEntry_type()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttRuneGm1", "attrune1@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Runa");
        var runeId = await CreateRuneEntryAsync(gmToken, "Runa do Fogo");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, null, runeId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();
        body!.Tipo.Should().Be("RuneBankEntry");
        body.Nome.Should().Be("Runa do Fogo");
        body.IsPublic.Should().BeFalse();
    }

    [Fact]
    public async Task A_rune_attachment_can_be_toggled_public_and_listed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttRuneGm2", "attrune2@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha com Runa Pública");
        var runeId = await CreateRuneEntryAsync(gmToken, "Runa da Terra");
        var attachResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, null, runeId)));
        var attachment = await attachResponse.Content.ReadFromJsonAsync<CampaignAttachmentResponse>();

        var toggle = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachment!.Id}/visibility", gmToken, true));

        toggle.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/attachments", gmToken));
        var body = await list.Content.ReadFromJsonAsync<List<CampaignAttachmentResponse>>();
        body!.Should().ContainSingle(a => a.Id == attachment.Id && a.Tipo == "RuneBankEntry" && a.IsPublic == true);
    }

    [Fact]
    public async Task Attaching_a_rune_entry_of_another_gm_returns_400()
    {
        var gmA = await RegisterGmAndGetTokenAsync("AttRuneGm3a", "attrune3a@teste.com");
        var gmB = await RegisterGmAndGetTokenAsync("AttRuneGm3b", "attrune3b@teste.com");
        var campaignId = await CreateCampaignAsync(gmA, "Campanha A");
        var runaDoB = await CreateRuneEntryAsync(gmB, "Runa do B");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmA,
            new AttachToCampaignRequest(null, null, null, null, null, runaDoB)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Attaching_a_malformed_or_unknown_rune_entry_returns_400(string runeId)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"AttRuneGm4{runeId.Length}", $"attrune4{runeId.Length}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Runa Inválida");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, null, runeId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Attaching_a_rune_entry_together_with_another_target_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("AttRuneGm5", "attrune5@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Dois Alvos");
        var runeId = await CreateRuneEntryAsync(gmToken, "Runa");
        var itemId = await CreateItemAsync(gmToken, "Corda");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(itemId, null, null, null, null, runeId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
```

**`CampaignCatalogControllerTests.cs`** — acrescente `using RuinaRPG.Contracts.Runes;` no topo e, ao fim da classe:

```csharp
    private async Task<string> CreateRuneEntryAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken,
            new CreateRuneBankEntryRequest(nome, "Descrição.", 1)));
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!.Id;
    }

    [Fact]
    public async Task AvailableRunes_returns_only_publicly_attached_entries()
    {
        var setup = await BuildMemberSetupAsync("Runes1");
        var publicRuneId = await CreateRuneEntryAsync(setup.GmToken, "Runa Pública");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, null, null, publicRuneId));
        var privateRuneId = await CreateRuneEntryAsync(setup.GmToken, "Runa Secreta");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{setup.CampaignId}/attachments", setup.GmToken,
            new AttachToCampaignRequest(null, null, null, null, null, privateRuneId)));
        await CreateRuneEntryAsync(setup.GmToken, "Runa Solta no Banco"); // nunca anexada

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-runes", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>();
        body!.Should().ContainSingle(e => e.Id == publicRuneId && e.Nome == "Runa Pública");
        body.Should().NotContain(e => e.Id == privateRuneId);
        body.Should().HaveCount(1);
    }

    [Fact]
    public async Task AvailableRunes_filters_by_nome()
    {
        var setup = await BuildMemberSetupAsync("Runes2");
        var fogo = await CreateRuneEntryAsync(setup.GmToken, "Runa do Fogo");
        var gelo = await CreateRuneEntryAsync(setup.GmToken, "Runa do Gelo");
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, null, null, fogo));
        await AttachAndPublishAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, null, null, gelo));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-runes?nome=gelo", setup.PlayerToken));

        var body = await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>();
        body!.Select(e => e.Id).Should().Equal(gelo);
    }

    [Fact]
    public async Task AvailableRunes_by_a_non_member_returns_403()
    {
        var setup = await BuildMemberSetupAsync("Runes3");
        var (_, outsiderToken) = await RegisterJogadorLinkedToAsync(setup.GmToken, "CatOutsiderRunes3", "catoutsiderrunes3@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/available-runes", outsiderToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
```

**`CampaignPlayerViewControllerTests.cs`** — acrescente `using RuinaRPG.Contracts.Runes;` no topo e, ao fim da classe:

```csharp
    [Fact]
    public async Task PlayerView_lists_a_public_rune_attachment_and_hides_a_private_one()
    {
        var setup = await BuildSetupAsync("Rune");
        var publicRune = await CreateRuneEntryAsync(setup.GmToken, "Runa Pública Rune");
        var publicAttachmentId = await AttachAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, null, null, publicRune));
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{setup.CampaignId}/attachments/{publicAttachmentId}/visibility", setup.GmToken, true));
        var privateRune = await CreateRuneEntryAsync(setup.GmToken, "Runa Privada Rune");
        await AttachAsync(setup.GmToken, setup.CampaignId, new AttachToCampaignRequest(null, null, null, null, null, privateRune));

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{setup.CampaignId}/player-view", setup.PlayerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PlayerCampaignViewResponse>();
        body!.AnexosPublicos.Should().ContainSingle(a => a.Tipo == "RuneBankEntry" && a.Nome == "Runa Pública Rune");
        body.AnexosPublicos.Should().NotContain(a => a.Nome == "Runa Privada Rune");
    }

    private async Task<string> CreateRuneEntryAsync(string gmToken, string nome)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken,
            new CreateRuneBankEntryRequest(nome, "Descrição.", 1)));
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!.Id;
    }
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignAttachmentsControllerTests|FullyQualifiedName~CampaignCatalogControllerTests|FullyQualifiedName~CampaignPlayerViewControllerTests" 2>&1 | grep -E "error CS|Passed!|Failed"`
Expected: erro de compilação — `AttachToCampaignRequest` não tem 6º parâmetro.

- [ ] **Step 3: Extend the request contract**

`src/RuinaRPG.Contracts/Campaigns/AttachToCampaignRequest.cs` (substitua):

```csharp
namespace RuinaRPG.Contracts.Campaigns;

public record AttachToCampaignRequest(string? ItemId, string? NpcSheetId, string? CreatureSheetId, string? SpellAbilityBankEntryId, string? ImageId, string? RuneBankEntryId = null);
```

(O parâmetro novo é opcional e fica no fim, então todos os `new AttachToCampaignRequest(a, b, c, d, e)` existentes continuam compilando.)

- [ ] **Step 4: Implement attach in `CampaignAttachmentsController`**

Em `Attach`:

1. Troque a chamada do validador por:

```csharp
        if (!CampaignAttachmentTargetValidator.ExactlyOneSet(request.ItemId, request.NpcSheetId, request.CreatureSheetId, request.SpellAbilityBankEntryId, request.ImageId, request.RuneBankEntryId))
            return BadRequest("Informe exatamente um alvo para o anexo.");
```

2. Troque a declaração `Guid? itemId = null, npcSheetId = null, creatureSheetId = null, bankEntryId = null, imageId = null;` por:

```csharp
        Guid? itemId = null, npcSheetId = null, creatureSheetId = null, bankEntryId = null, imageId = null, runeEntryId = null;
```

3. Entre o bloco `else if (request.SpellAbilityBankEntryId is not null) { ... }` e o `else { ... ImageId ... }` final, insira:

```csharp
        else if (request.RuneBankEntryId is not null)
        {
            if (!Guid.TryParse(request.RuneBankEntryId, out var parsed))
                return BadRequest("RuneBankEntryId inválido.");
            if (!await db.RuneBankEntries.AnyAsync(e => e.Id == parsed && e.GmId == gmId))
                return BadRequest("Entrada do Banco de Runas não encontrada.");
            runeEntryId = parsed;
        }
```

4. No inicializador `new CampaignAttachment { ... }`, depois de `SpellAbilityBankEntryId = bankEntryId,` acrescente `RuneBankEntryId = runeEntryId,`.

5. Em `ToResponseAsync`, depois do bloco `if (a.SpellAbilityBankEntryId is not null) { ... }`, insira:

```csharp
        if (a.RuneBankEntryId is not null)
        {
            var rune = await db.RuneBankEntries.FindAsync(a.RuneBankEntryId.Value);
            return new CampaignAttachmentResponse(a.Id.ToString(), "RuneBankEntry", rune!.Nome, a.IsPublic, null, null, null, null, null);
        }
```

(`ToggleVisibility` e `Remove` já funcionam para o novo tipo sem alteração.)

- [ ] **Step 5: Implement `available-runes` in `CampaignCatalogController`**

Acrescente `using RuinaRPG.Contracts.Runes;` e, depois do método `AvailableSpellAbilities`, insira:

```csharp
    [HttpGet("available-runes")]
    public async Task<ActionResult<List<RuneBankEntryResponse>>> AvailableRunes(Guid campaignId, [FromQuery] string? nome)
    {
        if (await MembershipErrorAsync(campaignId) is { } error)
            return error;

        var publicEntryIds = await db.CampaignAttachments
            .Where(a => a.CampaignId == campaignId && a.IsPublic && a.RuneBankEntryId != null)
            .Select(a => a.RuneBankEntryId!.Value)
            .ToListAsync();

        var query = db.RuneBankEntries.Where(e => publicEntryIds.Contains(e.Id));
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(e => EF.Functions.ILike(e.Nome, $"%{nome}%"));

        var entries = await query.OrderBy(e => e.Nome).ToListAsync();
        return entries.Select(e => new RuneBankEntryResponse(e.Id.ToString(), e.Nome, e.Descricao, e.Grau)).ToList();
    }
```

- [ ] **Step 6: Implement the player view**

Em `CampaignPlayerViewController`, logo depois do laço `foreach (var a in attachments.Where(a => a.IsPublic && a.SpellAbilityBankEntryId is not null)) { ... }`, insira:

```csharp
        foreach (var a in attachments.Where(a => a.IsPublic && a.RuneBankEntryId is not null))
        {
            var rune = await db.RuneBankEntries.FindAsync(a.RuneBankEntryId!.Value);
            anexosPublicos.Add(new PublicAttachmentSummary(a.Id.ToString(), "RuneBankEntry", rune!.Nome, null));
        }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | grep -E "error|warning|Build succeeded"` → 0 warnings.
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignAttachmentsControllerTests|FullyQualifiedName~CampaignCatalogControllerTests|FullyQualifiedName~CampaignPlayerViewControllerTests|FullyQualifiedName~CampaignGrantsControllerTests" 2>&1 | grep -E "Passed!|Failed"`
Expected: `Passed!` (os testes antigos seguem verdes; o teste `Grant_from_an_existing_Creature_deep_copies_traits_from_both_catalogs` é intermitente por colisão de nome em tabela global — ver memória do flake; se falhar sozinho, reexecute a classe).

- [ ] **Step 8: Update the Campanha requirements**

Em `Docs/Requisitos/Requisitos - Campanha.md` (Edit; cada trecho é único):

a) R0006: troque `# **R0006** - O GM deve poder anexar itens, fichas de criatura, fichas de NPC e entradas do Banco de Magias e Habilidades à campanha.` por `# **R0006** - O GM deve poder anexar itens, fichas de criatura, fichas de NPC e entradas do Banco de Magias e Habilidades e do Banco de Runas à campanha.` e, na descrição, troque `e entradas do "[[Requisitos - Banco de Magias e Habilidades]]".` por `e entradas do "[[Requisitos - Banco de Magias e Habilidades]]" e do "[[Requisitos - Banco de Runas]]".`

b) R0008: troque `Todo anexo (item, entrada do Banco de Magias e Habilidades ou imagem, ver R0006 e R0007)` por `Todo anexo (item, entrada do Banco de Magias e Habilidades, entrada do Banco de Runas ou imagem, ver R0006 e R0007)`.

c) R0012: troque `Uma Magia/Habilidade que um jogador cria em sua ficha (Requisitos - Ficha de Personagem R0003, Requisitos - Banco de Magias e Habilidades R0001), e uma Imagem` por `Uma Magia/Habilidade ou uma Runa que um jogador cria em sua ficha (Requisitos - Ficha de Personagem R0003, Requisitos - Banco de Magias e Habilidades R0001, Requisitos - Banco de Runas R0007), e uma Imagem`.

d) R0003 de "Requisitos - Ficha de Personagem" lista "Item, Magia/Habilidade e Imagem": não altere aqui; a Task 4 cobre.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Contracts/Campaigns src/RuinaRPG.Api/Controllers tests/RuinaRPG.Tests.Integration/Controllers "Docs/Requisitos/Requisitos - Campanha.md"
git commit -m "feat: anexar Runas do banco à campanha e liberar as públicas ao jogador

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 4: Fichas de Personagem e NPC — adicionar Runa do zero ou do banco, cópia automática e anexo do jogador

**Files:**
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/AddCharacterRuneRequest.cs`
- Modify: `src/RuinaRPG.Contracts/NpcSheets/AddNpcRuneRequest.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CharacterRunesController.cs`
- Modify: `src/RuinaRPG.Api/Controllers/NpcRunesController.cs`
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md`
- Modify: `Docs/Requisitos/Requisitos - Ficha de NPCs.md`
- Modify: `docs/superpowers/specs/2026-09-21-banco-de-runas-design.md`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterRunesControllerTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/NpcRunesControllerTests.cs`

**Interfaces:**
- Consumes: `RuneBankEntry`, `db.RuneBankEntries`, `CharacterRune/NpcRune.SourceBankEntryId`, `CampaignAttachment.RuneBankEntryId` (Task 1); `POST api/rune-bank`, `RuneBankEntryResponse` (Task 2); `AttachToCampaignRequest(..., RuneBankEntryId)`, `PUT .../attachments/{id}/visibility`, `GET campaigns/{id}/available-runes` (Task 3).
- Produces:
  - `record AddCharacterRuneRequest(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null)` e `record AddNpcRuneRequest(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null)`. (`SourceBankEntryId` fica no **fim**, opcional, para os chamadores existentes `new AddCharacterRuneRequest("Runa", "Desc", 1)` seguirem compilando.)
  - `POST api/character-sheets/{id}/runes` e `POST api/npc-sheets/{id}/runes`: exatamente um caminho (campos do zero **ou** `SourceBankEntryId`), senão 400; toda criação grava uma cópia em `RuneBankEntries` do GM da ficha; se quem cria não é o GM, a cópia também vira `CampaignAttachment` público da campanha da ficha.

- [ ] **Step 1: Write the failing Character tests**

Em `tests/RuinaRPG.Tests.Integration/Controllers/CharacterRunesControllerTests.cs`:

1. Acrescente `using RuinaRPG.Contracts.Runes;` no topo.
2. Troque o helper `SetUpSheetAsync` por este par (o antigo passa a delegar ao novo):

```csharp
    private async Task<string> SetUpSheetAsync(string gmToken, string playerId) =>
        (await SetUpSheetInCampaignAsync(gmToken, playerId)).SheetId;

    private async Task<(string SheetId, string CampaignId)> SetUpSheetInCampaignAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gmToken, new CreateCharacterSheetRequest(playerId)));
        return ((await sheetResponse.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id, campaignId);
    }

    private async Task<string> CreateRuneEntryAsync(string gmToken, string nome, string descricao, int grau)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken, new CreateRuneBankEntryRequest(nome, descricao, grau)));
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!.Id;
    }

    private async Task PublishRuneToCampaignAsync(string gmToken, string campaignId, string runeEntryId)
    {
        var attach = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, null, runeEntryId)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<List<RuneBankEntryResponse>> BankOfAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rune-bank", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    private async Task<List<RuneBankEntryResponse>> PublicRunesAsync(string token, string campaignId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-runes", token));
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }
```

3. Ao fim da classe, os testes:

```csharp
    [Fact]
    public async Task A_rune_added_by_the_gm_lands_in_the_bank_but_is_not_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm1", "runebk1@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer1", "runebkplayer1@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest("Runa do Fogo", "Queima o alvo.", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa do Fogo" && e.Descricao == "Queima o alvo." && e.Grau == 2);
        (await PublicRunesAsync(playerToken, campaignId)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_rune_added_by_the_player_lands_in_the_bank_and_is_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm2", "runebk2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer2", "runebkplayer2@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest("Runa da Água", "Cura ferimentos leves.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa da Água");
        (await PublicRunesAsync(playerToken, campaignId)).Should().ContainSingle(e => e.Nome == "Runa da Água" && e.Grau == 1);
    }

    [Fact]
    public async Task A_rune_picked_from_the_bank_copies_its_fields_and_makes_a_new_independent_bank_copy()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm3", "runebk3@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer3", "runebkplayer3@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa da Luz", "Ilumina a área.", 3);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null, entryId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CharacterRuneResponse>();
        created!.Nome.Should().Be("Runa da Luz");
        created.Descricao.Should().Be("Ilumina a área.");
        created.Grau.Should().Be(3);
        (await BankOfAsync(gmToken)).Count(e => e.Nome == "Runa da Luz").Should().Be(2); // a original + a cópia (R0001)
    }

    [Fact]
    public async Task A_player_can_only_pick_bank_entries_the_gm_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm4", "runebk4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer4", "runebkplayer4@teste.com");
        var (sheetId, campaignId) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa Reservada", "Só depois de liberada.", 1);

        var antes = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest(null, null, null, entryId)));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishRuneToCampaignAsync(gmToken, campaignId, entryId);

        var depois = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", playerToken,
            new AddCharacterRuneRequest(null, null, null, entryId)));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Adding_with_both_paths_or_with_neither_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm5", "runebk5@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer5", "runebkplayer5@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa", "Desc.", 1);

        var both = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest("Runa", "Desc.", 1, entryId)));
        var neither = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null)));
        var partial = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest("Só o nome", null, null)));

        both.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        partial.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Picking_a_malformed_or_unknown_bank_entry_returns_400(string entryId)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"RuneBkGm6{entryId.Length}", $"runebk6{entryId.Length}@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, $"RuneBkPlayer6{entryId.Length}", $"runebkplayer6{entryId.Length}@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null, entryId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Picking_another_gms_bank_entry_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("RuneBkGm7", "runebk7@teste.com");
        var otherGmToken = await RegisterGmAndGetTokenAsync("RuneBkGm7b", "runebk7b@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "RuneBkPlayer7", "runebkplayer7@teste.com");
        var (sheetId, _) = await SetUpSheetInCampaignAsync(gmToken, playerId);
        var foreignEntry = await CreateRuneEntryAsync(otherGmToken, "Runa Alheia", "Do outro GM.", 1);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/character-sheets/{sheetId}/runes", gmToken,
            new AddCharacterRuneRequest(null, null, null, foreignEntry)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
```

- [ ] **Step 2: Write the failing NPC tests**

Em `tests/RuinaRPG.Tests.Integration/Controllers/NpcRunesControllerTests.cs`:

1. Acrescente os `using`: `using RuinaRPG.Contracts.Campaigns;` e `using RuinaRPG.Contracts.Runes;` (mantenha os existentes).
2. Depois do helper `CreateSheetAsync`, acrescente:

```csharp
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

    private async Task<(string SheetId, string CampaignId)> GrantBlankNpcAsync(string gmToken, string playerId)
    {
        var campaignResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest("Campanha", "")));
        var campaignId = (await campaignResponse.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var grantResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gmToken, new GrantSheetRequest(playerId, "Npc", null)));
        return ((await grantResponse.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId, campaignId);
    }

    private async Task<string> CreateRuneEntryAsync(string gmToken, string nome, string descricao, int grau)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken, new CreateRuneBankEntryRequest(nome, descricao, grau)));
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!.Id;
    }

    private async Task PublishRuneToCampaignAsync(string gmToken, string campaignId, string runeEntryId)
    {
        var attach = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, null, null, runeEntryId)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<List<RuneBankEntryResponse>> BankOfAsync(string gmToken)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rune-bank", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    private async Task<List<RuneBankEntryResponse>> PublicRunesAsync(string token, string campaignId)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-runes", token));
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }
```

3. Ao fim da classe:

```csharp
    [Fact]
    public async Task A_rune_added_by_the_gm_lands_in_the_bank()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm1", "npcrunebk1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa do Fogo", "Queima o alvo.", 2)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa do Fogo" && e.Grau == 2);
    }

    [Fact]
    public async Task A_rune_added_by_the_player_a_npc_was_granted_to_is_also_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm2", "npcrunebk2@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcRuneBkPlayer2", "npcrunebkplayer2@teste.com");
        var (sheetId, campaignId) = await GrantBlankNpcAsync(gmToken, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest("Runa da Terra", "Endurece a pele.", 1)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(gmToken)).Should().ContainSingle(e => e.Nome == "Runa da Terra");
        (await PublicRunesAsync(playerToken, campaignId)).Should().ContainSingle(e => e.Nome == "Runa da Terra");
    }

    [Fact]
    public async Task A_rune_picked_from_the_bank_copies_its_fields()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm3", "npcrunebk3@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa da Luz", "Ilumina a área.", 3);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest(null, null, null, entryId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<NpcRuneResponse>();
        created!.Nome.Should().Be("Runa da Luz");
        created.Descricao.Should().Be("Ilumina a área.");
        created.Grau.Should().Be(3);
        (await BankOfAsync(gmToken)).Count(e => e.Nome == "Runa da Luz").Should().Be(2);
    }

    [Fact]
    public async Task A_player_can_only_pick_bank_entries_the_gm_published_to_the_campaign()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm4", "npcrunebk4@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcRuneBkPlayer4", "npcrunebkplayer4@teste.com");
        var (sheetId, campaignId) = await GrantBlankNpcAsync(gmToken, playerId);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa Reservada", "Só depois de liberada.", 1);

        var antes = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest(null, null, null, entryId)));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishRuneToCampaignAsync(gmToken, campaignId, entryId);

        var depois = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", playerToken,
            new AddNpcRuneRequest(null, null, null, entryId)));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Adding_with_both_paths_or_with_neither_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcRuneBkGm5", "npcrunebk5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        var entryId = await CreateRuneEntryAsync(gmToken, "Runa", "Desc.", 1);

        var both = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest("Runa", "Desc.", 1, entryId)));
        var neither = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest(null, null, null)));

        both.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Picking_a_malformed_or_unknown_bank_entry_returns_400(string entryId)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"NpcRuneBkGm6{entryId.Length}", $"npcrunebk6{entryId.Length}@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/npc-sheets/{sheetId}/runes", gmToken,
            new AddNpcRuneRequest(null, null, null, entryId)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
```

- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterRunesControllerTests|FullyQualifiedName~NpcRunesControllerTests" 2>&1 | grep -E "error CS|Passed!|Failed"`
Expected: erro de compilação — os requests não têm o parâmetro `SourceBankEntryId` (e `int?`).

- [ ] **Step 4: Change the request contracts**

`src/RuinaRPG.Contracts/CharacterSheets/AddCharacterRuneRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

// Exatamente um caminho: Nome + Descricao + Grau (montar do zero) OU SourceBankEntryId (partir de uma
// entrada do Banco de Runas). SourceBankEntryId fica no fim, opcional, pra não quebrar quem já monta do zero.
public record AddCharacterRuneRequest(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null);
```

`src/RuinaRPG.Contracts/NpcSheets/AddNpcRuneRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.NpcSheets;

// Mesmo formato de AddCharacterRuneRequest.
public record AddNpcRuneRequest(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null);
```

- [ ] **Step 5: Implement `CharacterRunesController.Add`**

Em `src/RuinaRPG.Api/Controllers/CharacterRunesController.cs`: acrescente `using RuinaRPG.Infrastructure.Campaigns;` e `using RuinaRPG.Infrastructure.Runes;` e substitua o método `Add` inteiro por:

```csharp
    [HttpPost]
    public async Task<ActionResult<CharacterRuneResponse>> Add(Guid sheetId, AddCharacterRuneRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        var callerId = CurrentUserId();
        if (!CharacterSheetAuthorization.CanEdit(callerId, sheet.OwnerId, campaignGmId))
            return Forbid();

        var fromScratch = request.Nome is not null && request.Descricao is not null && request.Grau is not null;
        var fromBank = request.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // both or neither
            return BadRequest("Informe exatamente um: Nome, Descricao e Grau para montar do zero, ou SourceBankEntryId.");

        string nome, descricao;
        int grau;
        Guid? sourceBankEntryId = null;

        if (fromBank)
        {
            if (!Guid.TryParse(request.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            var bankEntry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == bankEntryId && e.GmId == campaignGmId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            // O GM escolhe qualquer entrada do próprio banco; um jogador só alcança as que o GM anexou
            // à campanha da ficha como públicas (Requisitos - Banco de Runas R0003/R0008).
            if (callerId != campaignGmId
                && !await db.CampaignAttachments.AnyAsync(a => a.CampaignId == sheet.CampaignId && a.IsPublic && a.RuneBankEntryId == bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            nome = bankEntry.Nome; descricao = bankEntry.Descricao; grau = bankEntry.Grau;
            sourceBankEntryId = bankEntryId;
        }
        else
        {
            nome = request.Nome!; descricao = request.Descricao!; grau = request.Grau!.Value;
        }

        var rune = new CharacterRune { Id = Guid.NewGuid(), CharacterSheetId = sheetId, Nome = nome, Descricao = descricao, Grau = grau, SourceBankEntryId = sourceBankEntryId };
        db.CharacterRunes.Add(rune);

        // Requisitos - Banco de Runas R0001: toda criação — do zero ou a partir do banco — grava também
        // uma cópia independente no banco do GM, seja o GM ou o jogador quem criou.
        var bankCopy = new RuneBankEntry { Id = Guid.NewGuid(), GmId = campaignGmId, Nome = nome, Descricao = descricao, Grau = grau };
        db.RuneBankEntries.Add(bankCopy);

        // R0007: quando quem cria é o jogador dono (não o GM), a cópia vira anexo público da campanha
        // da ficha (Requisitos - Campanha R0012).
        if (callerId != campaignGmId)
        {
            db.CampaignAttachments.Add(new CampaignAttachment
            {
                Id = Guid.NewGuid(),
                CampaignId = sheet.CampaignId,
                RuneBankEntryId = bankCopy.Id,
                IsPublic = true
            });
        }

        await db.SaveChangesAsync();
        return Created(string.Empty, ToResponse(rune));
    }
```

- [ ] **Step 6: Implement `NpcRunesController.Add`**

Em `src/RuinaRPG.Api/Controllers/NpcRunesController.cs`: acrescente `using RuinaRPG.Infrastructure.Campaigns;` e `using RuinaRPG.Infrastructure.Runes;` e substitua o método `Add` inteiro por:

```csharp
    [HttpPost]
    public async Task<ActionResult<NpcRuneResponse>> Add(Guid sheetId, AddNpcRuneRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var callerId = CurrentUserId();
        if (!GrantedSheetAuthorization.CanEdit(callerId, sheet.OwnerId, sheet.GmId))
            return NotFound();

        var fromScratch = request.Nome is not null && request.Descricao is not null && request.Grau is not null;
        var fromBank = request.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // both or neither
            return BadRequest("Informe exatamente um: Nome, Descricao e Grau para montar do zero, ou SourceBankEntryId.");

        // A NPC sheet has no CampaignId column: the campaign of a granted NPC is resolved through the
        // grant-link CampaignAttachment (same rule NpcSpellAbilitiesController uses). Only a Jogador
        // (caller != GM) ever needs it.
        Guid? campaignId = null;
        if (callerId != sheet.GmId)
        {
            campaignId = await db.CampaignAttachments
                .Where(a => a.NpcSheetId == sheetId && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == sheet.OwnerId))
                .Select(a => (Guid?)a.CampaignId)
                .FirstOrDefaultAsync();
        }

        string nome, descricao;
        int grau;
        Guid? sourceBankEntryId = null;

        if (fromBank)
        {
            if (!Guid.TryParse(request.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            var bankEntry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == bankEntryId && e.GmId == sheet.GmId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            // Um jogador só alcança as entradas anexadas como públicas à campanha da concessão.
            if (callerId != sheet.GmId
                && (campaignId is null
                    || !await db.CampaignAttachments.AnyAsync(a => a.CampaignId == campaignId && a.IsPublic && a.RuneBankEntryId == bankEntryId)))
                return BadRequest("Entrada do banco não encontrada.");

            nome = bankEntry.Nome; descricao = bankEntry.Descricao; grau = bankEntry.Grau;
            sourceBankEntryId = bankEntryId;
        }
        else
        {
            nome = request.Nome!; descricao = request.Descricao!; grau = request.Grau!.Value;
        }

        var rune = new NpcRune { Id = Guid.NewGuid(), NpcSheetId = sheetId, Nome = nome, Descricao = descricao, Grau = grau, SourceBankEntryId = sourceBankEntryId };
        db.NpcRunes.Add(rune);

        var bankCopy = new RuneBankEntry { Id = Guid.NewGuid(), GmId = sheet.GmId, Nome = nome, Descricao = descricao, Grau = grau };
        db.RuneBankEntries.Add(bankCopy);

        if (callerId != sheet.GmId && campaignId is not null)
        {
            db.CampaignAttachments.Add(new CampaignAttachment
            {
                Id = Guid.NewGuid(),
                CampaignId = campaignId.Value,
                RuneBankEntryId = bankCopy.Id,
                IsPublic = true
            });
        }

        await db.SaveChangesAsync();
        return Created(string.Empty, ToResponse(rune));
    }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | grep -E "error|warning|Build succeeded"` → `Build succeeded.`, 0 warnings. (Se o build acusar chamadas do cliente ao construtor antigo, é esperado que **não** aconteça: o `AddCharacterRuneRequest("...", "...", int)` ainda compila. Os ajustes de cliente ficam na Task 6.)
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterRunesControllerTests|FullyQualifiedName~NpcRunesControllerTests|FullyQualifiedName~CharacterRuneAndMasteryMigrationTests|FullyQualifiedName~NpcChildTableMigrationTests" 2>&1 | grep -E "Passed!|Failed"`
Expected: `Passed!` — os testes antigos (que montam do zero) seguem verdes e os novos passam.

- [ ] **Step 8: Update the docs and the spec**

a) `Docs/Requisitos/Requisitos - Ficha de Personagem.md`, seção `### 4.d) Runas`: troque o trecho

```
Runas são um encantamento das vocações não mágicas (Campeão e Caçador). Lista incremental: o jogador adiciona uma Runa por vez, com os campos:
```

por

```
Runas são um encantamento das vocações não mágicas (Campeão e Caçador). Lista incremental: o jogador adiciona uma Runa por vez — montando do zero ou partindo de uma entrada do "[[Requisitos - Banco de Runas]]" (R0003; o jogador só escolhe entre as entradas que o GM liberou como públicas na campanha) —, com os campos:
```

e troque a linha `Uma Runa pode ser removida pelo jogador a qualquer momento.` por:

```
Uma Runa pode ser removida pelo jogador a qualquer momento. Toda Runa criada aqui (do zero ou do banco) também grava uma cópia independente no Banco de Runas do GM (R0001 do banco) e, quando quem cria é o jogador, essa cópia vira anexo público da campanha (R0007 do banco). Editar a Runa na ficha depois não altera a cópia do banco, e vice-versa.
```

b) `Docs/Requisitos/Requisitos - Ficha de Personagem.md`, bloco `# **R0003**` (Edit; cada troca é única):

- título: `# **R0003** - Os campos de Item, Magia/Habilidade e Imagem só oferecem o que a campanha liberou.` → `# **R0003** - Os campos de Item, Magia/Habilidade, Runa e Imagem só oferecem o que a campanha liberou.`
- `4.b) Magias & Habilidades (ao reaproveitar uma entrada do Banco), 5.a)-5.b)` → `4.b) Magias & Habilidades e 4.d) Runas (ao reaproveitar uma entrada do Banco), 5.a)-5.b)`
- `só listam Itens, entradas do Banco de Magias e Habilidades e Imagens que estão anexados` → `só listam Itens, entradas do Banco de Magias e Habilidades, entradas do Banco de Runas e Imagens que estão anexados`

c) `Docs/Requisitos/Requisitos - Ficha de NPCs.md`: acrescente ao fim do arquivo:

```markdown

# **R0008** - As Runas do NPC usam o Banco de Runas.

**Descrição**: A aba 4.d do NPC segue "[[Requisitos - Ficha de Personagem]]" 4.d sem alteração, inclusive a origem da Runa (do zero ou do "[[Requisitos - Banco de Runas]]") e a cópia automática para o banco do GM (R0001 do banco). O GM que gerencia o NPC escolhe qualquer entrada do próprio banco; quando o NPC foi concedido a um jogador (R0001, exceção), o jogador só escolhe entre as entradas públicas da campanha da concessão, e a Runa que ele cria também vira anexo público dessa campanha (R0007 do banco).
```

d) `docs/superpowers/specs/2026-09-21-banco-de-runas-design.md`, seção "Fichas — `POST .../runes`": troque

```
`AddCharacterRuneRequest` e `AddNpcRuneRequest` passam a ser
`(string? SourceBankEntryId, string? Nome, string? Descricao, int? Grau)`, mesmo formato dos requests de
Magia/Habilidade. Exatamente um caminho é aceito, senão 400:
```

por

```
`AddCharacterRuneRequest` e `AddNpcRuneRequest` passam a ser
`(string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId = null)` — `SourceBankEntryId` no
**fim** e opcional, para os chamadores que já montam do zero continuarem compilando. Exatamente um caminho é
aceito, senão 400:
```

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Contracts src/RuinaRPG.Api/Controllers tests/RuinaRPG.Tests.Integration/Controllers docs/superpowers/specs "Docs/Requisitos/Requisitos - Ficha de Personagem.md" "Docs/Requisitos/Requisitos - Ficha de NPCs.md"
git commit -m "feat: Runas das fichas partem do zero ou do Banco de Runas e alimentam o banco

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 5: Cliente — páginas do Banco de Runas, menu e chip

**Files:**
- Create: `src/RuinaRPG.Client/Pages/BancoDeRunas.razor`
- Create: `src/RuinaRPG.Client/Pages/BancoDeRunasForm.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`
- Modify: `src/RuinaRPG.Client/Shared/TipoChip.razor`
- Test: `tests/RuinaRPG.Tests.Client/Pages/BancoDeRunasFormTests.cs`
- Test: `tests/RuinaRPG.Tests.Client/Shared/TipoChipTests.cs`

**Interfaces:**
- Consumes: `RuinaRPG.Contracts.Runes.{CreateRuneBankEntryRequest, UpdateRuneBankEntryRequest, RuneBankEntryResponse}` e os endpoints `api/rune-bank` (Task 2).
- Produces: rotas `/banco-de-runas`, `/banco-de-runas/novo`, `/banco-de-runas/{EntryId}/editar`; `TipoChip` com `Tipo="RuneBankEntry"` exibindo "Runa".

- [ ] **Step 1: Write the failing client tests**

Crie `tests/RuinaRPG.Tests.Client/Pages/BancoDeRunasFormTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class BancoDeRunasFormTests : MudBunitContext
{
    [Fact]
    public async Task Clearing_the_required_Nome_field_blocks_the_save_call_in_edit_mode()
    {
        var putCalled = false;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "entry-1", Nome = "Runa do Fogo", Descricao = "Queima.", Grau = 1 }
                }) };
            if (request.Method == HttpMethod.Put)
            {
                putCalled = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeRunasForm>(p => p.Add(x => x.EntryId, "entry-1"));
        await Task.Delay(50); // let OnInitializedAsync populate the form

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync(""));

        await Task.Delay(700); // past the autosave debounce

        putCalled.Should().BeFalse("an empty Nome violates [Required] and must not reach the server");
    }

    [Fact]
    public async Task Editing_saves_the_changed_fields_with_a_PUT_to_the_rune_bank()
    {
        string? putPath = null;
        string? putBody = null;
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Get)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                {
                    new { Id = "entry-1", Nome = "Runa do Fogo", Descricao = "Queima.", Grau = 1 }
                }) };
            if (request.Method == HttpMethod.Put)
            {
                putPath = request.RequestUri!.AbsolutePath;
                putBody = request.Content!.ReadAsStringAsync().Result;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeRunasForm>(p => p.Add(x => x.EntryId, "entry-1"));
        await Task.Delay(50);

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Runa do Fogo Maior"));

        await Task.Delay(700);

        putPath.Should().EndWith("rune-bank/entry-1");
        putBody.Should().Contain("Runa do Fogo Maior");
    }

    [Fact]
    public async Task Create_with_a_400_from_the_server_shows_its_specific_message()
    {
        const string serverMessage = "Nome da runa inválido.";
        var http = FakeHttpMessageHandler.CreateClient(request =>
        {
            if (request.Method == HttpMethod.Post)
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(serverMessage) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeRunasForm>();
        await Task.Delay(50);

        var nome = cut.FindComponents<MudBlazor.MudTextField<string>>().Single(c => c.Instance.Label == "Nome");
        await cut.InvokeAsync(() => nome.Instance.ValueChanged.InvokeAsync("Runa do Fogo"));

        var salvar = cut.FindAll("button").Single(b => b.TextContent.Contains("Salvar"));
        await cut.InvokeAsync(() => salvar.Click());

        cut.Markup.Should().Contain(serverMessage);
    }

    [Fact]
    public async Task Editing_a_missing_entry_shows_an_error_instead_of_crashing()
    {
        var http = FakeHttpMessageHandler.CreateClient(request =>
            request.Method == HttpMethod.Get
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new List<object>()) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        Services.AddScoped(_ => http);

        var cut = Render<BancoDeRunasForm>(p => p.Add(x => x.EntryId, "does-not-exist"));
        await Task.Delay(50);

        cut.Markup.Should().Contain("Entrada não encontrada.");
    }
}
```

Em `tests/RuinaRPG.Tests.Client/Shared/TipoChipTests.cs`, acrescente ao `Theory` de rótulos conhecidos, junto dos outros `InlineData`:

```csharp
    [InlineData("RuneBankEntry", "Runa")]
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~BancoDeRunasFormTests|FullyQualifiedName~TipoChipTests" 2>&1 | grep -E "error CS|Passed!|Failed"`
Expected: erro de compilação — `BancoDeRunasForm` não existe.

- [ ] **Step 3: Implement the form page**

`src/RuinaRPG.Client/Pages/BancoDeRunasForm.razor`:

```razor
@page "/banco-de-runas/novo"
@page "/banco-de-runas/{EntryId}/editar"
@implements IDisposable
@inject HttpClient Http
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.Runes
@using System.ComponentModel.DataAnnotations
@using Microsoft.AspNetCore.Components.Forms
@using MudBlazor

<Breadcrumbs Items="@Crumbs" />

<MudText Typo="Typo.h3">@(EntryId is null ? "Nova Entrada" : "Editar Entrada")</MudText>

<DismissibleAlert @bind-Message="_errorMessage" Class="mt-3" />

<EditForm EditContext="_editContext">
    <DataAnnotationsValidator />
    @if (EntryId is not null)
    {
        <AutoSaveIndicator State="_autoSave.State" LastSavedAt="_autoSave.LastSavedAt" />
    }
    <Section Title="Runa">
        <MudTextField T="string" @bind-Value="_form.Nome" For="@(() => _form.Nome)" Label="Nome" @bind-Value:after="NotifySavedAsync" />
        <MudNumericField T="int" @bind-Value="_form.Grau" For="@(() => _form.Grau)" Label="Grau" @bind-Value:after="NotifySavedAsync" />
        <MudTextField T="string" @bind-Value="_form.Descricao" Label="Descrição" Lines="3" @bind-Value:after="NotifySavedAsync" />
    </Section>

    @if (EntryId is null)
    {
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-3" OnClick="CreateAsync">Salvar</MudButton>
    }
</EditForm>

@code {
    [Parameter] public string? EntryId { get; set; }

    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Banco de Runas", "banco-de-runas"),
        new(EntryId is null ? "Nova Entrada" : (string.IsNullOrWhiteSpace(_form.Nome) ? "Editar Entrada" : _form.Nome)),
    };

    private readonly EntryFormModel _form = new();
    // Constructed synchronously (not inside OnInitializedAsync) so it is never null on first render —
    // same reasoning as BancoDeMagiasForm: EditContext reads _form lazily at validation time.
    private readonly EditContext _editContext;
    private readonly AutoSaveCoordinator _autoSave = new();
    private string? _errorMessage;

    public BancoDeRunasForm()
    {
        _editContext = new EditContext(_form);
    }

    protected override void OnInitialized()
    {
        _autoSave.StateChanged += StateHasChangedFromAutoSave;
    }

    private void StateHasChangedFromAutoSave() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        _autoSave.StateChanged -= StateHasChangedFromAutoSave;
    }

    protected override async Task OnInitializedAsync()
    {
        if (EntryId is null)
            return;

        var response = await Http.GetAsync("rune-bank");
        var entries = await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>() ?? new();
        var existing = entries.SingleOrDefault(e => e.Id == EntryId);
        if (existing is null)
        {
            _errorMessage = "Entrada não encontrada.";
            return;
        }

        _form.Nome = existing.Nome;
        _form.Grau = existing.Grau;
        _form.Descricao = existing.Descricao;
    }

    private async Task CreateAsync()
    {
        if (!_editContext.Validate())
            return;

        var response = await Http.PostAsJsonAsync("rune-bank", new CreateRuneBankEntryRequest(_form.Nome, _form.Descricao, _form.Grau));
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = await response.Content.ReadErrorMessageAsync() ?? "Não foi possível salvar a entrada.";
            return;
        }

        Navigation.NavigateTo("/banco-de-runas");
    }

    private async Task<bool> SaveIfValidAsync()
    {
        if (!_editContext.Validate())
            return false;

        var response = await Http.PutAsJsonAsync($"rune-bank/{EntryId}", new UpdateRuneBankEntryRequest(_form.Nome, _form.Descricao, _form.Grau));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(await response.Content.ReadErrorMessageAsync() ?? "Não foi possível salvar a entrada.");

        return true;
    }

    private Task NotifySavedAsync()
    {
        // Single choke-point guard, same as BancoDeMagiasForm: in create mode the plain-field callbacks
        // must not fire a PUT to "rune-bank/" (null id). Create mode keeps its explicit "Salvar" button.
        if (EntryId is null)
            return Task.CompletedTask;

        _autoSave.NotifyChanged(SaveIfValidAsync);
        return Task.CompletedTask;
    }

    private class EntryFormModel
    {
        [Required(AllowEmptyStrings = false)]
        public string Nome { get; set; } = "";
        [Range(0, int.MaxValue)]
        public int Grau { get; set; }
        public string Descricao { get; set; } = "";
    }
}
```

- [ ] **Step 4: Implement the list page**

`src/RuinaRPG.Client/Pages/BancoDeRunas.razor`:

```razor
@page "/banco-de-runas"
@inject HttpClient Http
@inject NavigationManager Navigation
@using RuinaRPG.Contracts.Runes
@using MudBlazor

<Breadcrumbs Items="@Crumbs" />

<MudText Typo="Typo.h3">Banco de Runas</MudText>

<Section Title="Filtros">
    <MudGrid Class="mt-3">
        <MudItem xs="12" md="6">
            <MudTextField T="string" @bind-Value="_nomeFiltro" Immediate="true" @bind-Value:after="DebouncedLoadAsync" Label="Nome" />
        </MudItem>
        <MudItem xs="12" md="6">
            <MudNumericField T="int?" @bind-Value="_grauFiltro" Immediate="true" @bind-Value:after="DebouncedLoadAsync" Label="Grau" />
        </MudItem>
    </MudGrid>
</Section>

<Section Title="Runas">
    <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-3" OnClick="@(() => Navigation.NavigateTo("/banco-de-runas/novo"))">Nova Entrada</MudButton>

    <DismissibleAlert @bind-Message="_errorMessage" Class="mt-3" />

    <MudSimpleTable Dense="true" Hover="true" Class="mt-3">
        <thead>
            <tr>
                <th>Nome</th>
                <th>Grau</th>
                <th>Descrição</th>
                <th></th>
            </tr>
        </thead>
        <tbody>
            @foreach (var entry in _entries)
            {
                <tr>
                    <td>@entry.Nome</td>
                    <td>@entry.Grau</td>
                    <td>@entry.Descricao</td>
                    <td>
                        <MudButton Variant="Variant.Outlined" Color="Color.Primary" Size="Size.Small" OnClick="@(() => Navigation.NavigateTo($"/banco-de-runas/{entry.Id}/editar"))">Editar</MudButton>
                        <MudButton Variant="Variant.Outlined" Color="Color.Error" Size="Size.Small" OnClick="@(() => DeleteAsync(entry.Id))">Excluir</MudButton>
                    </td>
                </tr>
            }
        </tbody>
    </MudSimpleTable>
</Section>

@code {
    private List<RuinaRPG.Client.Shared.BreadcrumbItem> Crumbs => new()
    {
        new("Painel", "painel"),
        new("Banco de Runas"),
    };

    private string _nomeFiltro = "";
    private int? _grauFiltro;
    private List<RuneBankEntryResponse> _entries = new();
    private string? _errorMessage;

    protected override Task OnInitializedAsync() => LoadAsync();

    private CancellationTokenSource? _debounceCts;

    private async Task DebouncedLoadAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        try
        {
            await Task.Delay(300, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(_nomeFiltro))
            parametros.Add($"nome={Uri.EscapeDataString(_nomeFiltro)}");
        if (_grauFiltro is not null)
            parametros.Add($"grau={_grauFiltro}");
        var query = parametros.Count == 0 ? "" : "?" + string.Join("&", parametros);

        var response = await Http.GetAsync($"rune-bank{query}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível carregar o banco de runas.";
            return;
        }

        _entries = await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>() ?? new();
        _errorMessage = null;
    }

    private async Task DeleteAsync(string id)
    {
        var response = await Http.DeleteAsync($"rune-bank/{id}");
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = "Não foi possível excluir a entrada.";
            return;
        }

        await LoadAsync();
    }
}
```

- [ ] **Step 5: Nav link and chip**

`src/RuinaRPG.Client/Layout/NavMenu.razor` — depois da linha `<MudNavLink Href="banco-de-magias">Banco de Magias e Habilidades</MudNavLink>` insira:

```razor
                    <MudNavLink Href="banco-de-runas">Banco de Runas</MudNavLink>
```

`src/RuinaRPG.Client/Shared/TipoChip.razor`: no `switch`, depois da linha `"SpellAbilityBankEntry" => "Magia/Habilidade",` insira `"RuneBankEntry" => "Runa",`; e no comentário do topo troque `Item/SpellAbilityBankEntry/Image/NpcSheet/` por `Item/SpellAbilityBankEntry/RuneBankEntry/Image/NpcSheet/`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet build 2>&1 | grep -E "error|warning|Build succeeded"` → 0 warnings.
Run: `dotnet test tests/RuinaRPG.Tests.Client 2>&1 | grep -E "Passed!|Failed"`
Expected: `Passed!` (suíte de cliente inteira, incluindo os 4 testes novos de formulário e o novo `InlineData` do chip).

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat: páginas do Banco de Runas (lista, formulário) e link no menu do GM

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

### Task 6: Cliente — seletor de origem em 4.d (Personagem e NPC) e anexar Runa na campanha

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Fields/RuneOrigemModel.cs`
- Create: `src/RuinaRPG.Client/Shared/Fields/RuneOrigemFields.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor`
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor`
- Modify: `src/RuinaRPG.Client/Pages/CampanhaDetalhe.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/RuneOrigemFieldsTests.cs`

**Interfaces:**
- Consumes: `RuneBankEntryResponse` (Task 2); `AddCharacterRuneRequest(Nome, Descricao, Grau, SourceBankEntryId)` e `AddNpcRuneRequest(...)` (Task 4); `AttachToCampaignRequest(..., RuneBankEntryId)` (Task 3); `GET rune-bank` (GM) e `GET campaigns/{id}/available-runes` (jogador).
- Produces:
  - `RuinaRPG.Client.Shared.Fields.RuneOrigemModel { string Origem = "Zero"; string SourceBankEntryId = ""; string Nome = ""; string Descricao = ""; int Grau; bool DoBanco; void Limpar() }`.
  - `<RuneOrigemFields Model="..." BankEntries="..." />`.

- [ ] **Step 1: Write the failing tests**

Crie `tests/RuinaRPG.Tests.Client/Shared/Fields/RuneOrigemFieldsTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.Runes;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class RuneOrigemFieldsTests : MudBunitContext
{
    private static readonly RuneBankEntryResponse[] Entradas =
    [
        new("e1", "Runa do Fogo", "Queima.", 1),
        new("e2", "Runa do Gelo", "Congela.", 2),
    ];

    [Fact]
    public void The_model_starts_from_scratch_and_reports_DoBanco_only_for_the_bank_origin()
    {
        var model = new RuneOrigemModel();

        model.Origem.Should().Be("Zero");
        model.DoBanco.Should().BeFalse();

        model.Origem = "Banco";
        model.DoBanco.Should().BeTrue();
    }

    [Fact]
    public void Limpar_resets_every_field_to_the_starting_state()
    {
        var model = new RuneOrigemModel { Origem = "Banco", SourceBankEntryId = "e1", Nome = "X", Descricao = "Y", Grau = 4 };

        model.Limpar();

        model.Origem.Should().Be("Zero");
        model.SourceBankEntryId.Should().BeEmpty();
        model.Nome.Should().BeEmpty();
        model.Descricao.Should().BeEmpty();
        model.Grau.Should().Be(0);
    }

    [Fact]
    public void From_scratch_shows_the_three_rune_fields_and_no_bank_picker()
    {
        var cut = Render<RuneOrigemFields>(p => p
            .Add(x => x.Model, new RuneOrigemModel { Origem = "Zero" })
            .Add(x => x.BankEntries, Entradas));

        cut.FindComponents<MudTextField<string>>().Select(c => c.Instance.Label).Should().BeEquivalentTo("Nome", "Descrição");
        cut.FindComponents<MudNumericField<int>>().Should().ContainSingle(c => c.Instance.Label == "Grau");
        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().NotContain("e1");
    }

    [Fact]
    public void From_the_bank_lists_the_entries_and_hides_the_manual_fields()
    {
        var cut = Render<RuneOrigemFields>(p => p
            .Add(x => x.Model, new RuneOrigemModel { Origem = "Banco" })
            .Add(x => x.BankEntries, Entradas));

        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().Contain(["e1", "e2"]);
        cut.FindComponents<MudTextField<string>>().Should().BeEmpty();
        cut.FindComponents<MudNumericField<int>>().Should().BeEmpty();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~RuneOrigemFieldsTests" 2>&1 | grep -E "error CS|Passed!|Failed"`
Expected: erro de compilação — `RuneOrigemModel`/`RuneOrigemFields` não existem.

- [ ] **Step 3: Implement the model and the shared component**

`src/RuinaRPG.Client/Shared/Fields/RuneOrigemModel.cs`:

```csharp
namespace RuinaRPG.Client.Shared.Fields;

/// <summary>
/// Estado do formulário "adicionar Runa" em 4.d (Personagem e NPC): de onde a Runa vem — montada do
/// zero (Nome/Descrição/Grau) ou partindo de uma entrada do Banco de Runas (SourceBankEntryId).
/// </summary>
public class RuneOrigemModel
{
    public string Origem { get; set; } = "Zero";
    public string SourceBankEntryId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Descricao { get; set; } = "";
    public int Grau { get; set; }

    public bool DoBanco => Origem == "Banco";

    public void Limpar()
    {
        Origem = "Zero";
        SourceBankEntryId = "";
        Nome = "";
        Descricao = "";
        Grau = 0;
    }
}
```

`src/RuinaRPG.Client/Shared/Fields/RuneOrigemFields.razor`:

```razor
@using MudBlazor
@using RuinaRPG.Contracts.Runes

@* Campos do formulário de adicionar Runa em 4.d, compartilhados por FichaDePersonagem e FichaDeNpc:
   Origem (do zero / do Banco de Runas) e, conforme ela, os 3 campos da Runa ou o seletor de entrada
   do banco. O GM passa o banco inteiro dele; o jogador, só as entradas públicas da campanha. *@

<MudSelect T="string" @bind-Value="Model.Origem" Label="Origem">
    <MudSelectItem Value="@("Zero")">Montar do zero</MudSelectItem>
    <MudSelectItem Value="@("Banco")">Escolher do Banco de Runas</MudSelectItem>
</MudSelect>

@if (Model.DoBanco)
{
    <MudSelect T="string" @bind-Value="Model.SourceBankEntryId" Label="Entrada do Banco">
        <MudSelectItem Value="@("")">Escolha uma entrada</MudSelectItem>
        @foreach (var entry in BankEntries)
        {
            <MudSelectItem Value="@entry.Id">@entry.Nome (Grau @entry.Grau)</MudSelectItem>
        }
    </MudSelect>
}
else
{
    <MudTextField T="string" @bind-Value="Model.Nome" Label="Nome" />
    <MudTextField T="string" @bind-Value="Model.Descricao" Label="Descrição" Lines="3" />
    <MudNumericField T="int" @bind-Value="Model.Grau" Label="Grau" />
}

@code {
    [Parameter, EditorRequired] public RuneOrigemModel Model { get; set; } = new();
    [Parameter] public IReadOnlyList<RuneBankEntryResponse> BankEntries { get; set; } = [];
}
```

- [ ] **Step 4: Run the component tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter "FullyQualifiedName~RuneOrigemFieldsTests" 2>&1 | grep -E "Passed!|Failed"`
Expected: `Passed!  - Failed: 0, Passed: 4`.

- [ ] **Step 5: Wire `FichaDePersonagem.razor`**

1. No `EditForm Model="_runeForm" OnValidSubmit="AddRuneAsync"` da seção `Runas` (por volta da linha 592), substitua as três linhas de `MudTextField` Nome, `MudTextField` Descrição e `MudNumericField` Grau por uma só:

```razor
                <RuneOrigemFields Model="_runeForm" BankEntries="_runeBankEntries" />
```

(O botão `Adicionar Runa` logo abaixo fica como está.)

2. Acrescente `@using RuinaRPG.Contracts.Runes` junto dos outros `@using` do topo do arquivo (se ainda não houver) e, junto de `_runes`, troque as declarações (por volta da linha 859-860) para:

```csharp
    private List<CharacterRuneResponse> _runes = new();
    private List<RuneBankEntryResponse> _runeBankEntries = new();
    private readonly RuneOrigemModel _runeForm = new();
```

3. Apague a classe privada `RuneFormModel` (bloco `private class RuneFormModel { ... }` no fim do `@code`).

4. Substitua `LoadRunesAsync` e `AddRuneAsync` por:

```csharp
    private async Task LoadRunesAsync()
    {
        _runes = await Http.GetFromJsonAsync<List<CharacterRuneResponse>>($"character-sheets/{SheetId}/runes") ?? new();
        var bankUrl = _isGmCaller ? "rune-bank" : $"campaigns/{_campaignId}/available-runes";
        _runeBankEntries = await Http.GetFromJsonAsync<List<RuneBankEntryResponse>>(bankUrl) ?? new();
    }

    private async Task AddRuneAsync()
    {
        var request = _runeForm.DoBanco
            ? new AddCharacterRuneRequest(null, null, null, _runeForm.SourceBankEntryId)
            : new AddCharacterRuneRequest(_runeForm.Nome, _runeForm.Descricao, _runeForm.Grau);

        var response = await Http.PostAsJsonAsync($"character-sheets/{SheetId}/runes", request);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a runa.";
            return;
        }

        _runeForm.Limpar();
        await LoadRunesAsync();
    }
```

- [ ] **Step 6: Wire `FichaDeNpc.razor`**

Repita o mesmo com os nomes do NPC (leia o trecho atual antes de editar; estão por volta das linhas 563-566, 759, 1297-1310 e 1548):

1. No `EditForm Model="_runeForm" OnValidSubmit="AddRuneAsync"`, troque os três campos por `<RuneOrigemFields Model="_runeForm" BankEntries="_runeBankEntries" />`.
2. Acrescente `@using RuinaRPG.Contracts.Runes` no topo se faltar; troque `private readonly RuneFormModel _runeForm = new();` por:

```csharp
    private List<RuneBankEntryResponse> _runeBankEntries = new();
    private readonly RuneOrigemModel _runeForm = new();
```

3. Apague a classe privada `RuneFormModel`.
4. Substitua `LoadRunesAsync` e `AddRuneAsync` por:

```csharp
    private async Task LoadRunesAsync()
    {
        _runes = await Http.GetFromJsonAsync<List<NpcRuneResponse>>($"npc-sheets/{SheetId}/runes") ?? new();
        var bankUrl = _isGmCaller ? "rune-bank" : $"campaigns/{_campaignId}/available-runes";
        _runeBankEntries = await Http.GetFromJsonAsync<List<RuneBankEntryResponse>>(bankUrl) ?? new();
    }

    private async Task AddRuneAsync()
    {
        var request = _runeForm.DoBanco
            ? new AddNpcRuneRequest(null, null, null, _runeForm.SourceBankEntryId)
            : new AddNpcRuneRequest(_runeForm.Nome, _runeForm.Descricao, _runeForm.Grau);

        var response = await Http.PostAsJsonAsync($"npc-sheets/{SheetId}/runes", request);
        if (!response.IsSuccessStatusCode)
        {
            _errorMessage = await response.Content.ReadErrorMessageAsync() ?? "Não foi possível adicionar a runa.";
            return;
        }

        _runeForm.Limpar();
        await LoadRunesAsync();
    }
```

(Se o `LoadRunesAsync` atual do NPC usar outro nome de lista/tipo, mantenha o que já existe para `_runes` e só acrescente a linha do banco.)

- [ ] **Step 7: Attach a rune in `CampanhaDetalhe.razor`**

1. Dentro do `<MudGrid Class="mt-3 mb-3">` da aba Anexos, logo depois do `<MudItem xs="12" md="6">` da seção "Entrada do Banco de Magias" (que termina antes de `<Section Title="Imagem">`), insira:

```razor
            <MudItem xs="12" md="6">
                <Section Title="Entrada do Banco de Runas">
                    <EntityPicker @bind-Value="_attachRuneEntryId" SearchItems="SearchRuneEntriesAsync" Placeholder="Buscar runa..." />
                    <MudButton Variant="Variant.Filled" Color="Color.Primary" Class="mt-2" OnClick="AttachRuneEntryAsync">Anexar Runa</MudButton>
                </Section>
            </MudItem>
```

2. Acrescente `@using RuinaRPG.Contracts.Runes` no topo se faltar; junto de `private string _attachBankEntryId = "";` acrescente:

```csharp
    private string _attachRuneEntryId = "";
```

3. Junto de `SearchBankEntriesAsync` acrescente:

```csharp
    private async Task<List<PickerOption>> SearchRuneEntriesAsync(string query)
    {
        var response = await Http.GetAsync($"rune-bank?nome={Uri.EscapeDataString(query)}");
        if (!response.IsSuccessStatusCode) return new();
        var entries = await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>() ?? new();
        return entries.Select(e => new PickerOption(e.Id, $"{e.Nome} (Grau {e.Grau})")).ToList();
    }
```

4. Junto de `AttachBankEntryAsync` acrescente:

```csharp
    private Task AttachRuneEntryAsync()
    {
        var id = _attachRuneEntryId;
        _attachRuneEntryId = "";
        return AttachAsync(new AttachToCampaignRequest(null, null, null, null, null, id));
    }
```

(A lista "Anexos existentes" já renderiza o chip por `attachment.Tipo` — `TipoChip` mostra "Runa" — e o toggle "Público" no `else` genérico, então o novo tipo já aparece sem mais mudanças. A visão do jogador em `MinhaCampanha` também usa `TipoChip`.)

- [ ] **Step 8: Build and run all client tests**

Run: `dotnet build 2>&1 | grep -E "error|warning|Build succeeded"` → `Build succeeded.`, 0 warnings.
Run: `dotnet test tests/RuinaRPG.Tests.Client 2>&1 | grep -E "Passed!|Failed"` → `Passed!`.

- [ ] **Step 9: Commit**

```bash
git add src/RuinaRPG.Client tests/RuinaRPG.Tests.Client
git commit -m "feat: seletor de origem das Runas em 4.d e anexar Runa na campanha

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>"
```

---

## Final verification (após a Task 6)

- [ ] `dotnet build` → `Build succeeded.` com **0 Warning(s)**.
- [ ] `dotnet test tests/RuinaRPG.Tests.Unit` e `dotnet test tests/RuinaRPG.Tests.Client` → verdes.
- [ ] `dotnet test tests/RuinaRPG.Tests.Integration` → verde, ressalvado o flake conhecido (timeouts de ~100 s em classes sem relação — reexecute a classe isolada) e o teste intermitente `CampaignGrantsControllerTests.Grant_from_an_existing_Creature_deep_copies_traits_from_both_catalogs` (colisão de nome "Regeneração Bestial" em tabela global, ver memória do flake; não relacionado a Runas).
- [ ] `git grep -n "RuneFormModel"` → nenhuma ocorrência.
- [ ] `git grep -n "AddCharacterRuneRequest(_runeForm.Nome, _runeForm.Descricao, _runeForm.Grau)\b" src` → só o ramo "do zero" dentro do operador ternário.
- [ ] Conferir que nada de Criatura foi tocado: `git diff main --stat | grep -i creature` → vazio.
- [ ] Lembrete de rollout: após o deploy, `make migrate` (migration `AddRuneBank`).
