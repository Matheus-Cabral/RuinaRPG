# Notificação de Nota Secreta Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quando o GM publica uma Nota Secreta, cada jogador destinatário recebe, em qualquer página, um aviso clicável com som, e passa a ver um contador persistente de notas não lidas.

**Architecture:** `DiaryEntryRecipients` ganha `ReadAt`; dois endpoints novos dão a contagem de não lidas e marcam uma campanha como lida. Um `NotificationHub` SignalR endereçado por usuário empurra `SecretNoteReceived`/`SecretNotesChanged`. No client, um serviço scoped `SecretNoteNotifier` guarda os contadores e a conexão (atrás de `INotificationConnection`, para ser testável em bUnit); o `MainLayout` o inicia para Jogador, mostra o snackbar e toca o som.

**Tech Stack:** ASP.NET Core 8, EF Core/Npgsql, SignalR, Blazor WebAssembly, MudBlazor 9.9.0, xUnit, bUnit 2.9, FluentAssertions, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-10-09-notificacao-de-nota-secreta-design.md`

## Global Constraints

- Branch `release/1.4.4`. Não mexer em `AppVersionInfo` (já está em 1.4.4).
- `dotnet build` termina com **0 warnings, 0 errors**; warning novo é falha.
- TDD obrigatório (Técnico R0011): teste falhando primeiro, depois o mínimo de código.
- `dotnet test` exige Docker rodando (Testcontainers). Há um flake conhecido de timeout (~4%) nos testes de integração: um teste que falhar por timeout deve ser rodado de novo **sozinho** (`--filter FullyQualifiedName~NomeDoTeste`) antes de ser tratado como falha real.
- Texto de interface em português do Brasil; nomes de teste e comentários de código em inglês, como no restante do código.
- Ícones do MudBlazor (`Icons.Material.Filled.*`), nunca emojis.
- O texto da Nota Secreta nunca trafega em evento do hub.
- Quem não é destinatário não recebe evento nem contagem (Campanha R0011).
- Cada commit termina com `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Fazer `git push` depois de cada commit.

## Review Focus

1. Nome de campanha com HTML (`<b>x</b>`) no snackbar: deve aparecer como texto, não ser interpretado. Teste na Task 4.
2. `StartAsync` chamado duas vezes em sequência (inicialização do layout + primeira navegação): deve abrir uma única conexão. Teste na Task 3.
3. Hub indisponível ou lançando exceção: a Nota Secreta é gravada e a API responde 201 mesmo assim. Teste na Task 2.
4. `GET secret-notes/unread` respondendo erro na partida: o notifier fica com contagem zero, conecta mesmo assim e não propaga exceção ao layout. Teste na Task 3.
5. Nota chega com o jogador já na aba Notas Secretas daquela campanha: a lista recarrega e o contador termina em zero. Teste na Task 5.

---

### Task 1: `ReadAt`, contagem de não lidas e marcar como lida (API)

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/Diary/DiaryEntryRecipient.cs`
- Create: migration `AddReadAtToDiaryEntryRecipients` em `src/RuinaRPG.Infrastructure/Persistence/Migrations/`
- Create: `src/RuinaRPG.Contracts/Diary/UnreadSecretNotesResponse.cs`
- Modify: `src/RuinaRPG.Api/Controllers/CampaignPlayerViewController.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/SecretNoteUnreadTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Persistence/SecretNoteReadAtMigrationTests.cs`

**Interfaces:**
- Produces: `record UnreadSecretNotesResponse(string CampaignId, string CampaignName, int Count)` em `RuinaRPG.Contracts.Diary`.
- Produces: `GET /api/secret-notes/unread` → `List<UnreadSecretNotesResponse>` (só campanhas com `Count > 0`).
- Produces: `POST /api/campaigns/{campaignId}/secret-notes/mark-read` → `204`.
- Produces: `DiaryEntryRecipient.ReadAt` (`DateTime?`, nulo = não lida).

- [ ] **Step 1: Escrever os testes de integração dos endpoints (falhando)**

Criar `tests/RuinaRPG.Tests.Integration/Controllers/SecretNoteUnreadTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;

namespace RuinaRPG.Tests.Integration.Controllers;

public class SecretNoteUnreadTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public SecretNoteUnreadTests(PostgresFixture postgres) => _postgres = postgres;

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

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<string> RegisterGmAsync()
    {
        var nick = Unique("Gm");
        var response = await _client.PostAsJsonAsync("/api/auth/register/gm",
            new RegisterGmRequest(nick, $"{nick}@t.com", "Senha!123", "Senha!123"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<(string Id, string Token)> RegisterJogadorAsync(string gmToken)
    {
        var codeResponse = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invite-codes", gmToken));
        var code = (await codeResponse.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var nick = Unique("Jg");
        var response = await _client.PostAsJsonAsync("/api/auth/register/jogador",
            new RegisterJogadorRequest(nick, $"{nick}@t.com", "Senha!123", "Senha!123", code));
        var tokens = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/auth/me", tokens.AccessToken));
        return ((await me.Content.ReadFromJsonAsync<MeResponse>())!.Id, tokens.AccessToken);
    }

    private async Task<string> CreateCampaignWithMembersAsync(string gmToken, string nome, params string[] memberIds)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/campaigns", gmToken, new CreateCampaignRequest(nome, "")));
        var campaignId = (await response.Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        foreach (var memberId in memberIds)
            (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(memberId))))
                .EnsureSuccessStatusCode();
        return campaignId;
    }

    private async Task CreateNoteAsync(string gmToken, string campaignId, params string[] recipientIds) =>
        (await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/secret-notes", gmToken,
            new CreateSecretNoteRequest("Pista.", recipientIds.ToList(), [])))).EnsureSuccessStatusCode();

    private async Task<List<UnreadSecretNotesResponse>> UnreadAsync(string token)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/secret-notes/unread", token));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<UnreadSecretNotesResponse>>())!;
    }

    [Fact]
    public async Task Unread_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/secret-notes/unread");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unread_counts_notes_per_campaign_for_the_recipient_only()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var (otherId, otherToken) = await RegisterJogadorAsync(gmToken);
        var campanhaA = await CreateCampaignWithMembersAsync(gmToken, "Campanha A", recipientId, otherId);
        var campanhaB = await CreateCampaignWithMembersAsync(gmToken, "Campanha B", recipientId);
        await CreateNoteAsync(gmToken, campanhaA, recipientId);
        await CreateNoteAsync(gmToken, campanhaA, recipientId);
        await CreateNoteAsync(gmToken, campanhaB, recipientId);

        (await UnreadAsync(recipientToken)).Should().BeEquivalentTo(new[]
        {
            new UnreadSecretNotesResponse(campanhaA, "Campanha A", 2),
            new UnreadSecretNotesResponse(campanhaB, "Campanha B", 1),
        });
        (await UnreadAsync(otherToken)).Should().BeEmpty();
        (await UnreadAsync(gmToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task MarkRead_clears_only_that_campaign_for_the_caller_and_is_idempotent()
    {
        var gmToken = await RegisterGmAsync();
        var (aId, aToken) = await RegisterJogadorAsync(gmToken);
        var (bId, bToken) = await RegisterJogadorAsync(gmToken);
        var campanhaA = await CreateCampaignWithMembersAsync(gmToken, "Campanha A", aId, bId);
        var campanhaB = await CreateCampaignWithMembersAsync(gmToken, "Campanha B", aId);
        await CreateNoteAsync(gmToken, campanhaA, aId, bId);
        await CreateNoteAsync(gmToken, campanhaB, aId);

        var first = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanhaA}/secret-notes/mark-read", aToken));
        var second = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanhaA}/secret-notes/mark-read", aToken));

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await UnreadAsync(aToken)).Should().BeEquivalentTo(new[] { new UnreadSecretNotesResponse(campanhaB, "Campanha B", 1) });
        (await UnreadAsync(bToken)).Should().BeEquivalentTo(new[] { new UnreadSecretNotesResponse(campanhaA, "Campanha A", 1) });
    }

    [Fact]
    public async Task A_note_created_after_MarkRead_counts_as_unread_again()
    {
        var gmToken = await RegisterGmAsync();
        var (playerId, playerToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Campanha", playerId);
        await CreateNoteAsync(gmToken, campanha, playerId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/secret-notes/mark-read", playerToken));

        await CreateNoteAsync(gmToken, campanha, playerId);

        (await UnreadAsync(playerToken)).Should().BeEquivalentTo(new[] { new UnreadSecretNotesResponse(campanha, "Campanha", 1) });
    }

    [Fact]
    public async Task MarkRead_by_a_non_member_returns_403_and_an_unknown_campaign_404()
    {
        var gmToken = await RegisterGmAsync();
        var (_, outsiderToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Campanha");

        var forbidden = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/secret-notes/mark-read", outsiderToken));
        var notFound = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{Guid.NewGuid()}/secret-notes/mark-read", outsiderToken));

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

Se `AddCampaignMemberRequest` tiver outra assinatura, conferir em `src/RuinaRPG.Contracts/Campaigns/` e em `CampaignsControllerTests.cs` como os testes existentes adicionam membro, e ajustar o helper.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SecretNoteUnreadTests`
Expected: erro de compilação, `UnreadSecretNotesResponse` não existe.

- [ ] **Step 3: Contrato, coluna e endpoints**

Criar `src/RuinaRPG.Contracts/Diary/UnreadSecretNotesResponse.cs`:

```csharp
namespace RuinaRPG.Contracts.Diary;

public record UnreadSecretNotesResponse(string CampaignId, string CampaignName, int Count);
```

Em `src/RuinaRPG.Infrastructure/Diary/DiaryEntryRecipient.cs`, acrescentar a propriedade:

```csharp
    // Campanha R0015: null = the recipient hasn't opened the Notas Secretas tab since this note arrived.
    public DateTime? ReadAt { get; set; }
```

Em `CampaignPlayerViewController.cs`, logo depois de `ListSecretNotes`:

```csharp
    /// <summary>
    /// Campanha R0015: how many Notas Secretas the caller hasn't read yet, per campaign. Only the
    /// caller's own DiaryEntryRecipients rows are counted, so a non-recipient (or a GM) gets an
    /// empty list — R0011's "doesn't even know the note exists" still holds.
    /// </summary>
    [HttpGet("~/api/secret-notes/unread")]
    public async Task<ActionResult<List<UnreadSecretNotesResponse>>> ListUnreadSecretNotes()
    {
        var callerId = CurrentUserId();
        var counts = await db.DiaryEntryRecipients
            .Where(r => r.UserId == callerId && r.ReadAt == null)
            .Join(db.DiaryEntries.Where(d => d.IsSecretNote && d.CampaignId != null), r => r.DiaryEntryId, d => d.Id, (r, d) => d.CampaignId!.Value)
            .GroupBy(campaignId => campaignId)
            .Select(g => new { CampaignId = g.Key, Count = g.Count() })
            .ToListAsync();

        var campaignIds = counts.Select(c => c.CampaignId).ToList();
        var names = await db.Campaigns.Where(c => campaignIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nome);
        return counts
            .Select(c => new UnreadSecretNotesResponse(c.CampaignId.ToString(), names[c.CampaignId], c.Count))
            .OrderBy(c => c.CampaignName)
            .ToList();
    }

    [HttpPost("~/api/campaigns/{campaignId}/secret-notes/mark-read")]
    public async Task<IActionResult> MarkSecretNotesRead(Guid campaignId)
    {
        var callerId = CurrentUserId();
        var campaign = await db.Campaigns.FindAsync(campaignId);
        if (campaign is null)
            return NotFound();

        var isMember = campaign.GmId == callerId || await db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == callerId);
        if (!isMember)
            return Forbid();

        var noteIds = db.DiaryEntries.Where(d => d.CampaignId == campaignId && d.IsSecretNote).Select(d => d.Id);
        var now = DateTime.UtcNow;
        await db.DiaryEntryRecipients
            .Where(r => r.UserId == callerId && r.ReadAt == null && noteIds.Contains(r.DiaryEntryId))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.ReadAt, now));
        return NoContent();
    }
```

- [ ] **Step 4: Gerar a migration e acrescentar o backfill**

Run:
```bash
dotnet ef migrations add AddReadAtToDiaryEntryRecipients --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

No arquivo gerado, dentro de `Up`, depois do `AddColumn`:

```csharp
            // Campanha R0015: notes that already existed before the unread counter must not show
            // up as unread on the first deploy — stamp them as read at their own creation time.
            migrationBuilder.Sql("""
                UPDATE "DiaryEntryRecipients" r
                SET "ReadAt" = d."CreatedAt"
                FROM "DiaryEntries" d
                WHERE d."Id" = r."DiaryEntryId";
                """);
```

- [ ] **Step 5: Rodar os testes dos endpoints**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SecretNoteUnreadTests`
Expected: 5 passed.

- [ ] **Step 6: Teste do backfill da migration**

Criar `tests/RuinaRPG.Tests.Integration/Persistence/SecretNoteReadAtMigrationTests.cs` (mesmo padrão de `AfinidadeSegundaEssenciaMigrationTests.cs`, incluindo o helper `SchemaInsert`):

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class SecretNoteReadAtMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public SecretNoteReadAtMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migration_stamps_pre_existing_recipients_as_read_at_the_note_creation_time()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var anterior = db.Database.GetMigrations().Single(m => m.EndsWith("_AddTabelaDeAfinidades"));
        await migrator.MigrateAsync(anterior);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@readat.com", Email = "gm@readat.com", Nickname = "ReadAtGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "p@readat.com", Email = "p@readat.com", Nickname = "ReadAtPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaignId = Guid.NewGuid();
        var noteId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        // Raw SQL: the entities already carry columns this point of the schema doesn't have yet.
        await SchemaInsert.AtCurrentSchemaAsync(db, "Campaigns", new() { ["Id"] = campaignId, ["GmId"] = gm.Id, ["Nome"] = "Teste", ["Descricao"] = "" });
        await SchemaInsert.AtCurrentSchemaAsync(db, "DiaryEntries", new() { ["Id"] = noteId, ["AuthorUserId"] = gm.Id, ["CampaignId"] = campaignId, ["IsSecretNote"] = true, ["Texto"] = "Pista antiga.", ["CreatedAt"] = createdAt });
        await SchemaInsert.AtCurrentSchemaAsync(db, "DiaryEntryRecipients", new() { ["DiaryEntryId"] = noteId, ["UserId"] = player.Id });

        await migrator.MigrateAsync();

        var recipient = await db.DiaryEntryRecipients.AsNoTracking().SingleAsync(r => r.DiaryEntryId == noteId);
        recipient.ReadAt.Should().Be(createdAt);
    }
}
```

Como a implementação já existe (a migration foi gerada no Step 4, pois o EF precisa da propriedade para gerá-la), provar que o teste morde: comentar temporariamente o `migrationBuilder.Sql(...)`, rodar e ver falhar com `ReadAt` nulo, e então restaurar.

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~SecretNoteReadAtMigrationTests`
Expected: FAIL sem o `Sql`, PASS com ele.

- [ ] **Step 7: Build limpo, testes de Nota Secreta existentes e commit**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignsControllerTests|FullyQualifiedName~SecretNote"` → todos passam.

```bash
git add -A src tests
git commit -m "feat: contagem de Notas Secretas não lidas e marcar como lidas (API)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 2: `NotificationHub` e eventos de Nota Secreta (API)

**Files:**
- Create: `src/RuinaRPG.Contracts/Notifications/SecretNoteNotification.cs`
- Create: `src/RuinaRPG.Contracts/Notifications/NotificationEvents.cs`
- Create: `src/RuinaRPG.Api/Hubs/NotificationHub.cs`
- Create: `src/RuinaRPG.Api/Hubs/SubClaimUserIdProvider.cs`
- Modify: `src/RuinaRPG.Api/Program.cs` (registro do `IUserIdProvider`, `MapHub`)
- Modify: `src/RuinaRPG.Api/Controllers/CampaignsController.cs` (`CreateSecretNote`, `UpdateSecretNote`, `DeleteSecretNote`, `Delete` da campanha)
- Test: `tests/RuinaRPG.Tests.Integration/Hubs/NotificationHubTests.cs`

**Interfaces:**
- Consumes: `GET /api/secret-notes/unread`, `POST .../secret-notes/mark-read`, `UnreadSecretNotesResponse` (Task 1).
- Produces: `record SecretNoteNotification(string CampaignId, string CampaignName)` em `RuinaRPG.Contracts.Notifications`.
- Produces: `static class NotificationEvents { const string SecretNoteReceived = "SecretNoteReceived"; const string SecretNotesChanged = "SecretNotesChanged"; }` em `RuinaRPG.Contracts.Notifications`.
- Produces: hub em `/hubs/notifications`, autenticado, sem métodos chamáveis pelo cliente. `SecretNoteReceived` leva um `SecretNoteNotification`; `SecretNotesChanged` não leva payload.

- [ ] **Step 1: Escrever os testes do hub (falhando)**

Criar `tests/RuinaRPG.Tests.Integration/Hubs/NotificationHubTests.cs`. Copiar de `SecretNoteUnreadTests.cs` (Task 1) o esqueleto da classe (`IClassFixture<PostgresFixture>, IAsyncLifetime`, `InitializeAsync`, `DisposeAsync`) e os helpers `Unique`, `AuthedRequest`, `RegisterGmAsync`, `RegisterJogadorAsync`, `CreateCampaignWithMembersAsync`, `UnreadAsync`. Acrescentar:

```csharp
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Contracts.Notifications;
```

```csharp
    private async Task<string> CreateNoteAsync(string gmToken, string campaignId, params string[] recipientIds)
    {
        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/secret-notes", gmToken,
            new CreateSecretNoteRequest("Pista.", recipientIds.ToList(), [])));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SecretNoteResponse>())!.Id;
    }

    private async Task<HubConnection> ConnectAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    /// <summary>Records every event a connection receives, so a test can assert both arrival and absence.</summary>
    private sealed class Inbox
    {
        private readonly TaskCompletionSource _any = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<SecretNoteNotification> Received { get; } = new();
        public int Changed { get; private set; }

        public Inbox(HubConnection connection)
        {
            connection.On<SecretNoteNotification>(NotificationEvents.SecretNoteReceived, n => { Received.Add(n); _any.TrySetResult(); });
            connection.On(NotificationEvents.SecretNotesChanged, () => { Changed++; _any.TrySetResult(); });
        }

        public async Task<bool> WaitForAnyAsync(TimeSpan timeout) =>
            await Task.WhenAny(_any.Task, Task.Delay(timeout)) == _any.Task;
    }

    private static readonly TimeSpan Arrives = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaysSilent = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Recipient_receives_SecretNoteReceived_and_a_non_recipient_member_receives_nothing()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var (otherId, otherToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId, otherId);
        await using var recipientConnection = await ConnectAsync(recipientToken);
        await using var otherConnection = await ConnectAsync(otherToken);
        var recipientInbox = new Inbox(recipientConnection);
        var otherInbox = new Inbox(otherConnection);

        await CreateNoteAsync(gmToken, campanha, recipientId);

        (await recipientInbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        recipientInbox.Received.Should().ContainSingle().Which.Should().Be(new SecretNoteNotification(campanha, "Ruína"));
        (await otherInbox.WaitForAnyAsync(StaysSilent)).Should().BeFalse();
    }

    [Fact]
    public async Task A_recipient_listed_twice_is_notified_once()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        await using var connection = await ConnectAsync(recipientToken);
        var inbox = new Inbox(connection);

        await CreateNoteAsync(gmToken, campanha, recipientId, recipientId);

        (await inbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        await Task.Delay(StaysSilent);
        inbox.Received.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_notifies_only_the_added_recipient_and_keeps_the_read_state_of_the_existing_one()
    {
        var gmToken = await RegisterGmAsync();
        var (existingId, existingToken) = await RegisterJogadorAsync(gmToken);
        var (addedId, addedToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", existingId, addedId);
        var noteId = await CreateNoteAsync(gmToken, campanha, existingId);
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/secret-notes/mark-read", existingToken));
        await using var existingConnection = await ConnectAsync(existingToken);
        await using var addedConnection = await ConnectAsync(addedToken);
        var existingInbox = new Inbox(existingConnection);
        var addedInbox = new Inbox(addedConnection);

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campanha}/secret-notes/{noteId}", gmToken,
            new UpdateSecretNoteRequest("Pista revista.", [existingId, addedId], [])));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await addedInbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        addedInbox.Received.Should().ContainSingle().Which.Should().Be(new SecretNoteNotification(campanha, "Ruína"));
        (await existingInbox.WaitForAnyAsync(StaysSilent)).Should().BeFalse();
        (await UnreadAsync(addedToken)).Should().ContainSingle().Which.Count.Should().Be(1);
        (await UnreadAsync(existingToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Update_that_only_changes_the_text_notifies_nobody()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        var noteId = await CreateNoteAsync(gmToken, campanha, recipientId);
        await using var connection = await ConnectAsync(recipientToken);
        var inbox = new Inbox(connection);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campanha}/secret-notes/{noteId}", gmToken,
            new UpdateSecretNoteRequest("Só o texto mudou.", [recipientId], [])));

        (await inbox.WaitForAnyAsync(StaysSilent)).Should().BeFalse();
    }

    [Fact]
    public async Task Update_that_removes_a_recipient_sends_them_SecretNotesChanged()
    {
        var gmToken = await RegisterGmAsync();
        var (keptId, _) = await RegisterJogadorAsync(gmToken);
        var (removedId, removedToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", keptId, removedId);
        var noteId = await CreateNoteAsync(gmToken, campanha, keptId, removedId);
        await using var connection = await ConnectAsync(removedToken);
        var inbox = new Inbox(connection);

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campanha}/secret-notes/{noteId}", gmToken,
            new UpdateSecretNoteRequest("Pista.", [keptId], [])));

        (await inbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        inbox.Changed.Should().Be(1);
        inbox.Received.Should().BeEmpty();
        (await UnreadAsync(removedToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_a_note_sends_SecretNotesChanged_and_drops_the_unread_count()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        var noteId = await CreateNoteAsync(gmToken, campanha, recipientId);
        await using var connection = await ConnectAsync(recipientToken);
        var inbox = new Inbox(connection);

        await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campanha}/secret-notes/{noteId}", gmToken));

        (await inbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        inbox.Changed.Should().Be(1);
        (await UnreadAsync(recipientToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Deleting_the_campaign_sends_SecretNotesChanged_to_its_note_recipients()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        await CreateNoteAsync(gmToken, campanha, recipientId);
        await using var connection = await ConnectAsync(recipientToken);
        var inbox = new Inbox(connection);

        var delete = await _client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campanha}", gmToken));

        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await inbox.WaitForAnyAsync(Arrives)).Should().BeTrue();
        inbox.Changed.Should().Be(1);
        (await UnreadAsync(recipientToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Note_is_still_created_when_the_hub_throws()
    {
        var gmToken = await RegisterGmAsync();
        var (recipientId, recipientToken) = await RegisterJogadorAsync(gmToken);
        var campanha = await CreateCampaignWithMembersAsync(gmToken, "Ruína", recipientId);
        await using var brokenFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton<IHubContext<NotificationHub>>(new ThrowingHubContext())));
        using var brokenClient = brokenFactory.CreateClient();

        var response = await brokenClient.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanha}/secret-notes", gmToken,
            new CreateSecretNoteRequest("Pista.", [recipientId], [])));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await UnreadAsync(recipientToken)).Should().ContainSingle().Which.Count.Should().Be(1);
    }

    private sealed class ThrowingHubContext : IHubContext<NotificationHub>
    {
        public IHubClients Clients => throw new InvalidOperationException("hub down");
        public IGroupManager Groups => throw new InvalidOperationException("hub down");
    }

    [Fact]
    public async Task Anonymous_connection_is_refused()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, "/hubs/notifications"), options =>
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        await FluentActions.Awaiting(() => connection.StartAsync()).Should().ThrowAsync<HttpRequestException>();
    }
```

Para o teste anônimo, conferir como `EncounterHubTests.cs` escreve o equivalente (há um comentário longo sobre `AccessTokenProvider` lá) e usar a mesma forma de asserção.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~NotificationHubTests`
Expected: erro de compilação, `RuinaRPG.Contracts.Notifications` e `NotificationHub` não existem.

- [ ] **Step 3: Contratos, hub e registro**

`src/RuinaRPG.Contracts/Notifications/SecretNoteNotification.cs`:

```csharp
namespace RuinaRPG.Contracts.Notifications;

/// <summary>Campanha R0015: payload of SecretNoteReceived. Never carries the note's text.</summary>
public record SecretNoteNotification(string CampaignId, string CampaignName);
```

`src/RuinaRPG.Contracts/Notifications/NotificationEvents.cs`:

```csharp
namespace RuinaRPG.Contracts.Notifications;

/// <summary>Event names of the notifications hub, shared by the API (sender) and the client (listener).</summary>
public static class NotificationEvents
{
    public const string SecretNoteReceived = "SecretNoteReceived";
    public const string SecretNotesChanged = "SecretNotesChanged";
}
```

`src/RuinaRPG.Api/Hubs/NotificationHub.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RuinaRPG.Api.Hubs;

/// <summary>
/// Server-to-client only: controllers push per-user events through IHubContext (Clients.Users),
/// so there is nothing for a client to invoke and no group to join.
/// </summary>
[Authorize]
public class NotificationHub : Hub;
```

`src/RuinaRPG.Api/Hubs/SubClaimUserIdProvider.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR;

namespace RuinaRPG.Api.Hubs;

/// <summary>
/// SignalR's default provider reads ClaimTypes.NameIdentifier, but Program.cs keeps the JWT claim
/// types as JwtTokenService wrote them (MapInboundClaims = false), so the user id lives in "sub".
/// Without this, Clients.User(...) would never match any connection.
/// </summary>
public class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
}
```

Em `Program.cs`: logo depois de `builder.Services.AddSignalR();`

```csharp
builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();
```

e logo depois de `app.MapHub<EncounterHub>("/hubs/encounters");`

```csharp
app.MapHub<NotificationHub>("/hubs/notifications");
```

Acrescentar `using Microsoft.AspNetCore.SignalR;` se ainda não houver.

- [ ] **Step 4: Enviar os eventos em `CampaignsController`**

Construtor passa a ser:

```csharp
public class CampaignsController(RuinaRpgDbContext db, UserManager<ApplicationUser> userManager, IHubContext<NotificationHub> notifications, ILogger<CampaignsController> logger) : ControllerBase
```

com os usings `Microsoft.AspNetCore.SignalR`, `RuinaRPG.Api.Hubs`, `RuinaRPG.Contracts.Notifications`.

Helpers privados (junto dos demais helpers de Nota Secreta):

```csharp
    /// <summary>
    /// Campanha R0015. The note is already saved when this runs — a hub failure must not fail the
    /// request (same reasoning as CharacterSheetsController's ParticipantsChanged push): the
    /// recipient still gets the unread counter on their next load.
    /// </summary>
    private async Task NotifySecretNoteReceivedAsync(Guid campaignId, IEnumerable<Guid> userIds)
    {
        var ids = userIds.Select(id => id.ToString()).ToList();
        if (ids.Count == 0)
            return;

        try
        {
            var nome = await db.Campaigns.Where(c => c.Id == campaignId).Select(c => c.Nome).SingleAsync();
            await notifications.Clients.Users(ids).SendAsync(NotificationEvents.SecretNoteReceived, new SecretNoteNotification(campaignId.ToString(), nome));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push SecretNoteReceived for campaign {CampaignId}", campaignId);
        }
    }

    private async Task NotifySecretNotesChangedAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Select(id => id.ToString()).ToList();
        if (ids.Count == 0)
            return;

        try
        {
            await notifications.Clients.Users(ids).SendAsync(NotificationEvents.SecretNotesChanged);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push SecretNotesChanged");
        }
    }
```

Conferir como `CharacterSheetsController` (perto da linha 321) captura e registra a falha do hub e usar o mesmo tipo de exceção e nível de log que ele usa, se forem diferentes dos acima.

`CreateSecretNote`: entre `await db.SaveChangesAsync();` e o `return`:

```csharp
        await NotifySecretNoteReceivedAsync(campaignId, recipientIds.Distinct());
```

`UpdateSecretNote`: substituir o bloco de destinatários por

```csharp
        var existingRecipients = await db.DiaryEntryRecipients.Where(r => r.DiaryEntryId == noteId).ToListAsync();
        var newRecipientIds = recipientIds.Distinct().ToList();
        var removedRecipients = existingRecipients.Where(r => !newRecipientIds.Contains(r.UserId)).ToList();
        var addedRecipientIds = newRecipientIds.Where(id => existingRecipients.All(r => r.UserId != id)).ToList();
        db.DiaryEntryRecipients.RemoveRange(removedRecipients);
        foreach (var recipientId in addedRecipientIds)
            db.DiaryEntryRecipients.Add(new DiaryEntryRecipient { DiaryEntryId = noteId, UserId = recipientId });
```

e, entre `await db.SaveChangesAsync();` e `return NoContent();`:

```csharp
        await NotifySecretNoteReceivedAsync(campaignId, addedRecipientIds);
        await NotifySecretNotesChangedAsync(removedRecipients.Select(r => r.UserId));
```

`DeleteSecretNote`: antes de `db.DiaryEntries.Remove(note);`

```csharp
        var recipientIds = await db.DiaryEntryRecipients.Where(r => r.DiaryEntryId == noteId).Select(r => r.UserId).ToListAsync();
```

e depois do `SaveChangesAsync`:

```csharp
        await NotifySecretNotesChangedAsync(recipientIds);
```

`Delete` da campanha (o método que faz `db.Campaigns.Remove(campaign);`): antes do `Remove`

```csharp
        var noteRecipientIds = await db.DiaryEntryRecipients
            .Where(r => db.DiaryEntries.Any(d => d.Id == r.DiaryEntryId && d.CampaignId == campaign.Id && d.IsSecretNote))
            .Select(r => r.UserId)
            .Distinct()
            .ToListAsync();
```

e depois do `SaveChangesAsync`:

```csharp
        await NotifySecretNotesChangedAsync(noteRecipientIds);
```

- [ ] **Step 5: Rodar os testes do hub**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter FullyQualifiedName~NotificationHubTests`
Expected: 9 passed.

- [ ] **Step 6: Build, regressão e commit**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CampaignsControllerTests|FullyQualifiedName~SecretNote|FullyQualifiedName~HubTests"` → todos passam.

```bash
git add -A src tests
git commit -m "feat: hub de notificações avisa os destinatários de Notas Secretas

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 3: `SecretNoteNotifier`, conexão e som (Client)

**Files:**
- Create: `src/RuinaRPG.Client/Services/INotificationConnection.cs`
- Create: `src/RuinaRPG.Client/Services/SignalRNotificationConnection.cs`
- Create: `src/RuinaRPG.Client/Services/SecretNoteNotifier.cs`
- Create: `src/RuinaRPG.Client/wwwroot/js/notificationSound.js`
- Create: `src/RuinaRPG.Client/wwwroot/audio/harp_notification.mp3` (cópia de `Docs/harp_notification.mp3`)
- Modify: `src/RuinaRPG.Client/wwwroot/index.html`, `src/RuinaRPG.Client/Program.cs`
- Create: `tests/RuinaRPG.Tests.Client/Shared/FakeNotificationConnection.cs`
- Modify: `tests/RuinaRPG.Tests.Client/MudBunitContext.cs`
- Test: `tests/RuinaRPG.Tests.Client/Services/SecretNoteNotifierTests.cs`

**Interfaces:**
- Consumes: `UnreadSecretNotesResponse` (Task 1); `SecretNoteNotification`, `NotificationEvents` (Task 2); `GET secret-notes/unread`, `POST campaigns/{id}/secret-notes/mark-read` (caminhos relativos ao `HttpClient`, cuja base já termina em `/api/`).
- Produces:

```csharp
public interface INotificationConnection : IAsyncDisposable
{
    bool IsActive { get; }
    Task StartAsync(Func<SecretNoteNotification, Task> onReceived, Func<Task> onChanged);
    Task StopAsync();
}

public class SecretNoteNotifier(HttpClient http, INotificationConnection connection)
{
    public IReadOnlyDictionary<string, int> UnreadByCampaign { get; }
    public int TotalUnread { get; }
    public int UnreadFor(string campaignId);
    public event Action? Changed;
    public event Action<SecretNoteNotification>? Received;
    public Task StartAsync();
    public Task StopAsync();
    public Task MarkCampaignReadAsync(string campaignId);
}
```

- Produces (testes): `FakeNotificationConnection` com `StartCount`, `StopCount`, `StartShouldThrow`, `RaiseReceivedAsync(SecretNoteNotification)`, `RaiseChangedAsync()`; e `MudBunitContext.NotificationConnection` (a instância registrada), além de `SecretNoteNotifier` registrado como scoped em todo teste bUnit.
- Produces (JS): `window.ruinaNotificationSound.play()`.

- [ ] **Step 1: Fake da conexão e testes do notifier (falhando)**

`tests/RuinaRPG.Tests.Client/Shared/FakeNotificationConnection.cs`:

```csharp
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Stand-in for the SignalR connection (which bUnit can't host): records starts/stops and lets a
/// test play the server's part by raising the two hub events.
/// </summary>
public sealed class FakeNotificationConnection : INotificationConnection
{
    private Func<SecretNoteNotification, Task>? _onReceived;
    private Func<Task>? _onChanged;

    public bool IsActive { get; private set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }
    public bool StartShouldThrow { get; set; }

    public Task StartAsync(Func<SecretNoteNotification, Task> onReceived, Func<Task> onChanged)
    {
        StartCount++;
        if (StartShouldThrow)
            throw new HttpRequestException("hub unreachable");

        _onReceived = onReceived;
        _onChanged = onChanged;
        IsActive = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopCount++;
        IsActive = false;
        return Task.CompletedTask;
    }

    public Task RaiseReceivedAsync(SecretNoteNotification notification) => _onReceived!(notification);

    public Task RaiseChangedAsync() => _onChanged!();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

`tests/RuinaRPG.Tests.Client/Services/SecretNoteNotifierTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class SecretNoteNotifierTests
{
    private readonly FakeNotificationConnection _connection = new();
    private readonly List<HttpRequestMessage> _requests = new();
    private List<UnreadSecretNotesResponse> _unread = new();
    private HttpStatusCode _unreadStatus = HttpStatusCode.OK;

    private SecretNoteNotifier CreateNotifier() => new(FakeHttpMessageHandler.CreateClient(request =>
    {
        _requests.Add(request);
        if (request.RequestUri!.AbsolutePath.EndsWith("secret-notes/unread"))
            return new HttpResponseMessage(_unreadStatus) { Content = JsonContent.Create(_unread) };
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }), _connection);

    [Fact]
    public async Task Start_loads_the_unread_counts_and_opens_the_connection()
    {
        _unread = new() { new("c1", "Ruína", 2), new("c2", "Outra", 1) };
        var notifier = CreateNotifier();
        var changed = 0;
        notifier.Changed += () => changed++;

        await notifier.StartAsync();

        notifier.TotalUnread.Should().Be(3);
        notifier.UnreadFor("c1").Should().Be(2);
        notifier.UnreadFor("unknown").Should().Be(0);
        _connection.StartCount.Should().Be(1);
        changed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Starting_twice_opens_a_single_connection()
    {
        var notifier = CreateNotifier();

        await Task.WhenAll(notifier.StartAsync(), notifier.StartAsync());
        await notifier.StartAsync();

        _connection.StartCount.Should().Be(1);
    }

    [Fact]
    public async Task Start_survives_a_failing_unread_request_and_still_connects()
    {
        _unreadStatus = HttpStatusCode.InternalServerError;
        var notifier = CreateNotifier();

        await notifier.StartAsync();

        notifier.TotalUnread.Should().Be(0);
        _connection.StartCount.Should().Be(1);
    }

    [Fact]
    public async Task Start_swallows_a_connection_failure_and_a_later_start_retries()
    {
        var notifier = CreateNotifier();
        _connection.StartShouldThrow = true;

        await notifier.StartAsync();
        _connection.StartShouldThrow = false;
        await notifier.StartAsync();

        _connection.StartCount.Should().Be(2);
        _connection.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_received_note_increments_its_campaign_and_raises_both_events()
    {
        _unread = new() { new("c1", "Ruína", 1) };
        var notifier = CreateNotifier();
        await notifier.StartAsync();
        var received = new List<SecretNoteNotification>();
        var changed = 0;
        notifier.Received += received.Add;
        notifier.Changed += () => changed++;

        await _connection.RaiseReceivedAsync(new SecretNoteNotification("c1", "Ruína"));
        await _connection.RaiseReceivedAsync(new SecretNoteNotification("c9", "Nova"));

        notifier.UnreadFor("c1").Should().Be(2);
        notifier.UnreadFor("c9").Should().Be(1);
        received.Should().Equal(new SecretNoteNotification("c1", "Ruína"), new SecretNoteNotification("c9", "Nova"));
        changed.Should().Be(2);
    }

    [Fact]
    public async Task SecretNotesChanged_reloads_the_counts_without_raising_Received()
    {
        _unread = new() { new("c1", "Ruína", 2) };
        var notifier = CreateNotifier();
        await notifier.StartAsync();
        var received = 0;
        notifier.Received += _ => received++;
        _unread = new();

        await _connection.RaiseChangedAsync();

        notifier.TotalUnread.Should().Be(0);
        received.Should().Be(0);
    }

    [Fact]
    public async Task MarkCampaignRead_posts_and_clears_only_that_campaign()
    {
        _unread = new() { new("c1", "Ruína", 2), new("c2", "Outra", 1) };
        var notifier = CreateNotifier();
        await notifier.StartAsync();

        await notifier.MarkCampaignReadAsync("c1");

        _requests.Should().Contain(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("campaigns/c1/secret-notes/mark-read"));
        notifier.UnreadFor("c1").Should().Be(0);
        notifier.TotalUnread.Should().Be(1);
    }

    [Fact]
    public async Task Stop_closes_the_connection_and_clears_the_counts()
    {
        _unread = new() { new("c1", "Ruína", 2) };
        var notifier = CreateNotifier();
        await notifier.StartAsync();

        await notifier.StopAsync();

        _connection.StopCount.Should().Be(1);
        notifier.TotalUnread.Should().Be(0);
    }
}
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~SecretNoteNotifierTests`
Expected: erro de compilação, `INotificationConnection` e `SecretNoteNotifier` não existem.

- [ ] **Step 3: Implementar a interface, o notifier e a conexão SignalR**

`src/RuinaRPG.Client/Services/INotificationConnection.cs`:

```csharp
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Client.Services;

/// <summary>
/// The realtime channel behind SecretNoteNotifier. An interface only so the notifier can be
/// tested without a SignalR server — SignalRNotificationConnection is the one real implementation.
/// </summary>
public interface INotificationConnection : IAsyncDisposable
{
    /// <summary>True while connected, connecting or auto-reconnecting.</summary>
    bool IsActive { get; }

    /// <summary><paramref name="onChanged"/> also runs after an automatic reconnect, since events may have been missed.</summary>
    Task StartAsync(Func<SecretNoteNotification, Task> onReceived, Func<Task> onChanged);

    Task StopAsync();
}
```

`src/RuinaRPG.Client/Services/SecretNoteNotifier.cs`:

```csharp
using System.Net.Http.Json;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Client.Services;

/// <summary>
/// Campanha R0015: the player's unread Notas Secretas, per campaign, kept live by the
/// notifications hub. Scoped — one per app instance — so the layout, the nav menu and the
/// campaign pages all read the same counters.
/// </summary>
public class SecretNoteNotifier(HttpClient http, INotificationConnection connection)
{
    private Dictionary<string, int> _unread = new();
    private Task? _starting;

    public IReadOnlyDictionary<string, int> UnreadByCampaign => _unread;
    public int TotalUnread => _unread.Values.Sum();
    public int UnreadFor(string campaignId) => _unread.GetValueOrDefault(campaignId);

    /// <summary>The counters changed. May be raised off the renderer's context — use InvokeAsync.</summary>
    public event Action? Changed;

    /// <summary>A note arrived in real time (never raised for the initial load or a silent refresh).</summary>
    public event Action<SecretNoteNotification>? Received;

    /// <summary>
    /// Idempotent, and safe to call on every navigation: MainLayout does exactly that, which is
    /// also what brings a connection that gave up reconnecting back to life.
    /// </summary>
    public Task StartAsync()
    {
        if (connection.IsActive)
            return Task.CompletedTask;
        if (_starting is { IsCompleted: false })
            return _starting;
        return _starting = StartCoreAsync();
    }

    private async Task StartCoreAsync()
    {
        await RefreshAsync();
        try
        {
            await connection.StartAsync(OnReceivedAsync, RefreshAsync);
        }
        catch (Exception)
        {
            // No realtime channel right now (offline, API restarting). The counters above are
            // still valid, and the next StartAsync retries.
        }
    }

    public async Task StopAsync()
    {
        if (!connection.IsActive && _unread.Count == 0)
            return;

        await connection.StopAsync();
        _unread = new();
        Changed?.Invoke();
    }

    public async Task MarkCampaignReadAsync(string campaignId)
    {
        var response = await http.PostAsync($"campaigns/{campaignId}/secret-notes/mark-read", null);
        if (!response.IsSuccessStatusCode)
            return;

        if (_unread.Remove(campaignId))
            Changed?.Invoke();
    }

    private async Task RefreshAsync()
    {
        var response = await http.GetAsync("secret-notes/unread");
        if (!response.IsSuccessStatusCode)
            return;

        var unread = await response.Content.ReadFromJsonAsync<List<UnreadSecretNotesResponse>>() ?? new();
        _unread = unread.ToDictionary(u => u.CampaignId, u => u.Count);
        Changed?.Invoke();
    }

    private Task OnReceivedAsync(SecretNoteNotification notification)
    {
        _unread[notification.CampaignId] = UnreadFor(notification.CampaignId) + 1;
        Changed?.Invoke();
        Received?.Invoke(notification);
        return Task.CompletedTask;
    }
}
```

Se o teste `Starting_twice_opens_a_single_connection` expuser uma corrida (o fake fica ativo de forma síncrona, então não deve), a correção é no notifier, não no teste.

`src/RuinaRPG.Client/Services/SignalRNotificationConnection.cs`:

```csharp
using Microsoft.AspNetCore.SignalR.Client;
using RuinaRPG.Contracts.Notifications;

namespace RuinaRPG.Client.Services;

public sealed class SignalRNotificationConnection(HttpClient http, AuthStateService authState) : INotificationConnection
{
    private HubConnection? _connection;

    public bool IsActive => _connection is { State: not HubConnectionState.Disconnected };

    public async Task StartAsync(Func<SecretNoteNotification, Task> onReceived, Func<Task> onChanged)
    {
        await StopAsync();

        // Same URL/token wiring as GerenciadorDeEncontros.razor's encounter hub connection.
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(http.BaseAddress!, "/"), "hubs/notifications"), options =>
            {
                options.AccessTokenProvider = async () => await authState.GetAccessTokenAsync();
            })
            .WithAutomaticReconnect()
            .Build();
        connection.On(NotificationEvents.SecretNoteReceived, onReceived);
        connection.On(NotificationEvents.SecretNotesChanged, onChanged);
        connection.Reconnected += _ => onChanged();

        _connection = connection;
        await connection.StartAsync();
    }

    public async Task StopAsync()
    {
        if (_connection is null)
            return;

        var connection = _connection;
        _connection = null;
        await connection.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
```

- [ ] **Step 4: Rodar os testes do notifier**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~SecretNoteNotifierTests`
Expected: 8 passed.

- [ ] **Step 5: Som, DI do app e DI dos testes bUnit**

```bash
mkdir -p src/RuinaRPG.Client/wwwroot/audio
cp Docs/harp_notification.mp3 src/RuinaRPG.Client/wwwroot/audio/harp_notification.mp3
```

`src/RuinaRPG.Client/wwwroot/js/notificationSound.js`:

```js
window.ruinaNotificationSound = {
    // Browsers reject play() until the user has interacted with the page (autoplay policy). The
    // visual notification is shown regardless, so a blocked sound is swallowed rather than surfaced.
    play: function () {
        var audio = new Audio('audio/harp_notification.mp3');
        var playing = audio.play();
        if (playing && playing.catch) {
            playing.catch(function () { });
        }
    }
};
```

Em `index.html`, depois de `<script src="js/richTextEditor.js"></script>`:

```html
    <script src="js/notificationSound.js"></script>
```

Em `src/RuinaRPG.Client/Program.cs`, depois do `AddScoped` do `HttpClient` (última linha de serviços):

```csharp
builder.Services.AddScoped<INotificationConnection, SignalRNotificationConnection>();
builder.Services.AddScoped<SecretNoteNotifier>();
```

Em `tests/RuinaRPG.Tests.Client/MudBunitContext.cs`, acrescentar a propriedade e os registros no construtor (ao lado do `PericiaCatalogo`), com `using RuinaRPG.Tests.Client.Shared;`:

```csharp
    /// <summary>The fake behind every test's SecretNoteNotifier — raise hub events through it.</summary>
    protected FakeNotificationConnection NotificationConnection { get; } = new();
```

```csharp
        // Same registrations as the app's Program.cs: MainLayout, NavMenu and the player's campaign
        // pages inject SecretNoteNotifier. The connection is a fake — bUnit can't host SignalR.
        Services.AddSingleton<INotificationConnection>(NotificationConnection);
        Services.AddScoped<SecretNoteNotifier>();
```

- [ ] **Step 6: Build, suíte do client e commit**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test tests/RuinaRPG.Tests.Client` → todos passam.

```bash
git add -A src tests
git commit -m "feat(client): SecretNoteNotifier, conexão de notificações e som de harpa

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 4: Aviso global no `MainLayout` e contador no menu (Client)

**Files:**
- Modify: `src/RuinaRPG.Client/Layout/MainLayout.razor`
- Modify: `src/RuinaRPG.Client/Layout/NavMenu.razor`
- Test: `tests/RuinaRPG.Tests.Client/Layout/MainLayoutTests.cs`, `tests/RuinaRPG.Tests.Client/Layout/NavMenuTests.cs`

**Interfaces:**
- Consumes: `SecretNoteNotifier` (`StartAsync`, `StopAsync`, `TotalUnread`, `UnreadByCampaign`, `Changed`, `Received`), `FakeNotificationConnection`, `MudBunitContext.NotificationConnection`, `window.ruinaNotificationSound.play` (Task 3); `SecretNoteNotification` (Task 2); `MeResponse(Id, Nickname, Role, IsRulesAuditor, PendingChangelogVersion, MustChangePassword)`.
- Produces: navegação do aviso para `campanhas/{campaignId}/jogador?aba=notas` (a Task 5 faz a página honrar `aba=notas`).

- [ ] **Step 1: Testes do `MainLayout` (falhando)**

Em `MainLayoutTests.cs`, acrescentar (reaproveitando `RegisterCommonServices`, `RenderLayout` e `UseViewport` que já existem; usings novos: `Microsoft.JSInterop`, `RuinaRPG.Contracts.Diary`, `RuinaRPG.Contracts.Notifications`):

```csharp
    private HttpClient HttpFor(string role, List<UnreadSecretNotesResponse>? unread = null) => FakeHttpMessageHandler.CreateClient(request =>
        request.RequestUri!.AbsolutePath.EndsWith("secret-notes/unread")
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(unread ?? new()) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new MeResponse("u1", "Nick", role, false, null, false)) });

    [Fact]
    public void Starts_the_notifier_for_a_Jogador()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador"));
        var cut = RenderLayout();
        cut.WaitForAssertion(() => NotificationConnection.StartCount.Should().Be(1));
    }

    [Fact]
    public async Task Does_not_start_the_notifier_for_a_GM()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("GM"));
        RenderLayout();
        await Task.Delay(100);

        NotificationConnection.StartCount.Should().Be(0);
    }

    [Fact]
    public async Task A_received_note_shows_a_clickable_snackbar_and_plays_the_sound()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador"));
        var cut = RenderLayout();
        cut.WaitForAssertion(() => NotificationConnection.IsActive.Should().BeTrue());

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("c1", "Ruína"));

        cut.WaitForAssertion(() => cut.Find(".mud-snackbar").TextContent.Should().Contain("Nova nota secreta em Ruína"));
        JSInterop.Invocations.Should().Contain(i => i.Identifier == "ruinaNotificationSound.play");

        await cut.InvokeAsync(() => cut.Find(".mud-snackbar").Click());

        Services.GetRequiredService<NavigationManager>().Uri.Should().EndWith("campanhas/c1/jogador?aba=notas");
    }

    [Fact]
    public async Task Snackbar_shows_a_campaign_name_with_markup_as_plain_text()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador"));
        var cut = RenderLayout();
        cut.WaitForAssertion(() => NotificationConnection.IsActive.Should().BeTrue());

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("c1", "<b id=\"injected\">Ruína</b>"));

        cut.WaitForAssertion(() => cut.Find(".mud-snackbar").TextContent.Should().Contain("<b id=\"injected\">Ruína</b>"));
        cut.FindAll("#injected").Should().BeEmpty();
    }

    [Fact]
    public void App_bar_shows_the_unread_total_linking_to_the_single_campaign_with_unread_notes()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador", new() { new("c1", "Ruína", 3) }));
        var cut = RenderLayout();

        cut.WaitForAssertion(() =>
        {
            var indicator = cut.Find("[aria-label='Notas secretas não lidas']");
            indicator.GetAttribute("href").Should().Be("campanhas/c1/jogador?aba=notas");
            cut.Find(".rr-unread-indicator").TextContent.Should().Contain("3");
        });
    }

    [Fact]
    public void App_bar_indicator_links_to_Minhas_Campanhas_when_several_campaigns_have_unread_notes()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador", new() { new("c1", "Ruína", 1), new("c2", "Outra", 1) }));
        var cut = RenderLayout();

        cut.WaitForAssertion(() =>
            cut.Find("[aria-label='Notas secretas não lidas']").GetAttribute("href").Should().Be("minhas-campanhas"));
    }

    [Fact]
    public async Task App_bar_has_no_indicator_without_unread_notes()
    {
        UseViewport(Breakpoint.Lg);
        RegisterCommonServices(HttpFor("Jogador"));
        var cut = RenderLayout();
        await Task.Delay(100);

        cut.FindAll("[aria-label='Notas secretas não lidas']").Should().BeEmpty();
    }
```

Se a marcação real do MudBlazor 9.9.0 usar outra classe para o snackbar ou renderizar o `MudIconButton` com `Href` de outra forma, ajustar o **seletor** no teste para o elemento real; as asserções (texto, navegação ao clicar, destino do link, nome como texto puro) não mudam.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~MainLayoutTests`
Expected: os 7 testes novos falham (notifier nunca inicia, não há snackbar nem indicador); os antigos seguem passando.

- [ ] **Step 3: Implementar no `MainLayout.razor`**

Diretivas novas no topo:

```razor
@using Microsoft.JSInterop
@using RuinaRPG.Client.Services
@using RuinaRPG.Contracts.Notifications
@inject SecretNoteNotifier Notifier
@inject ISnackbar Snackbar
@inject IJSRuntime JS
```

Na `MudAppBar`, entre `<MudSpacer />` e `<ThemeToggle />`:

```razor
            @if (Notifier.TotalUnread > 0)
            {
                @* Campanha R0015: lives in the app bar (not only in the drawer) so the counter stays
                   visible with the drawer collapsed on desktop and closed on mobile. *@
                <MudBadge Content="@Notifier.TotalUnread" Color="Color.Error" Overlap="true" Class="rr-unread-indicator mr-2">
                    <MudIconButton Icon="@Icons.Material.Filled.MarkEmailUnread" Color="Color.Inherit" Href="@UnreadHref"
                                    title="Notas secretas não lidas" aria-label="Notas secretas não lidas" />
                </MudBadge>
            }
```

No `@code`:

```csharp
    // One campaign with unread notes → straight to its Notas Secretas tab; several → the list,
    // where each campaign card shows its own count.
    private string UnreadHref => Notifier.UnreadByCampaign.Count == 1
        ? NotasHref(Notifier.UnreadByCampaign.Keys.Single())
        : "minhas-campanhas";

    private static string NotasHref(string campaignId) => $"campanhas/{campaignId}/jogador?aba=notas";
```

`OnInitializedAsync` passa a assinar os eventos antes da primeira chamada a `auth/me`:

```csharp
    protected override async Task OnInitializedAsync()
    {
        _isLandingRoute = IsLandingRoute(Navigation.Uri);
        Navigation.LocationChanged += OnLocationChanged;
        Notifier.Changed += OnNotifierChanged;
        Notifier.Received += OnSecretNoteReceived;
        await RefreshMustChangePasswordAsync();
    }
```

`RefreshMustChangePasswordAsync` passa a ser `RefreshSessionAsync` (renomear também as duas chamadas), mantendo o comentário existente acima dele e acrescentando o do notifier:

```csharp
    private async Task RefreshSessionAsync()
    {
        var response = await Http.GetAsync("auth/me");
        var me = response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<MeResponse>() : null;
        _mustChangePassword = me?.MustChangePassword == true;

        // Campanha R0015: only a Jogador receives Notas Secretas. Running this on every navigation
        // is what starts the notifier after login, stops it after logout, and restarts a
        // connection that gave up reconnecting — StartAsync/StopAsync are both idempotent.
        if (me?.Role == "Jogador")
            await Notifier.StartAsync();
        else
            await Notifier.StopAsync();
    }

    private void OnNotifierChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnSecretNoteReceived(SecretNoteNotification notification) => _ = InvokeAsync(async () =>
    {
        // RenderFragment overload on purpose: the string overload renders its message as markup,
        // and the campaign name is GM-typed text.
        Snackbar.Add(
            builder => builder.AddContent(0, $"Nova nota secreta em {notification.CampaignName}"),
            Severity.Info,
            options =>
            {
                options.Icon = Icons.Material.Filled.MarkEmailUnread;
                options.OnClick = _ =>
                {
                    Navigation.NavigateTo(NotasHref(notification.CampaignId));
                    return Task.CompletedTask;
                };
            });
        await JS.InvokeVoidAsync("ruinaNotificationSound.play");
    });
```

`Dispose`:

```csharp
    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
        Notifier.Changed -= OnNotifierChanged;
        Notifier.Received -= OnSecretNoteReceived;
    }
```

Se `ISnackbar.Add` do MudBlazor 9.9.0 não tiver a sobrecarga `(RenderFragment, Severity, Action<SnackbarOptions>)` com esses nomes, usar a sobrecarga equivalente que aceita `RenderFragment` (conferir em `~/.nuget/packages/mudblazor/9.9.0/lib/net8.0/MudBlazor.xml`); não usar a sobrecarga de `string`.

- [ ] **Step 4: Rodar os testes do `MainLayout`**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~MainLayoutTests`
Expected: todos passam.

- [ ] **Step 5: Teste do contador no `NavMenu` (falhando)**

Em `NavMenuTests.cs` (usings novos: `RuinaRPG.Contracts.Diary`, `RuinaRPG.Contracts.Notifications`):

```csharp
    private IRenderedComponent<CascadingAuthenticationState> RenderForJogador(List<UnreadSecretNotesResponse> unread)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("jogador");
        authContext.SetRoles("Jogador");
        Services.AddBlazoredLocalStorage();
        Services.AddScoped<AuthStateService>();
        Services.AddScoped<TokenAuthenticationStateProvider>();
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(unread) }));

        return Render<CascadingAuthenticationState>(p => p.AddChildContent<NavMenu>());
    }

    [Fact]
    public async Task Minhas_Campanhas_link_shows_the_unread_total_and_follows_live_changes()
    {
        var cut = RenderForJogador(new() { new("c1", "Ruína", 2) });
        var notifier = Services.GetRequiredService<SecretNoteNotifier>();
        cut.FindAll(".rr-unread-chip").Should().BeEmpty();

        await cut.InvokeAsync(() => notifier.StartAsync());
        cut.WaitForAssertion(() => cut.Find(".rr-unread-chip").TextContent.Trim().Should().Be("2"));

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("c1", "Ruína"));
        cut.WaitForAssertion(() => cut.Find(".rr-unread-chip").TextContent.Trim().Should().Be("3"));

        await cut.InvokeAsync(() => notifier.MarkCampaignReadAsync("c1"));
        cut.WaitForAssertion(() => cut.FindAll(".rr-unread-chip").Should().BeEmpty());
    }
```

Se a forma de autorizar como Jogador usada acima conflitar com a que os demais testes deste arquivo usam (`BunitAuthorizationService`), seguir a do arquivo, desde que o link "Minhas Campanhas" (dentro de `AuthorizeView Roles="Jogador"`) seja renderizado.

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~NavMenuTests`
Expected: o teste novo falha (`.rr-unread-chip` não existe).

- [ ] **Step 6: Implementar no `NavMenu.razor`**

No topo: `@implements IDisposable` e `@inject SecretNoteNotifier Notifier`.

Substituir o link de Minhas Campanhas por:

```razor
                    <MudNavLink Href="minhas-campanhas" Icon="@Icons.Material.Filled.Groups" IconColor="Color.Primary">
                        Minhas Campanhas
                        @if (Notifier.TotalUnread > 0)
                        {
                            <MudChip T="string" Size="Size.Small" Color="Color.Error" Class="rr-unread-chip ml-2">@Notifier.TotalUnread</MudChip>
                        }
                    </MudNavLink>
```

No `@code`:

```csharp
    protected override void OnInitialized() => Notifier.Changed += OnNotifierChanged;

    private void OnNotifierChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Notifier.Changed -= OnNotifierChanged;
```

- [ ] **Step 7: Suíte do client, build e commit**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test tests/RuinaRPG.Tests.Client` → todos passam (inclusive os testes antigos de `NavMenu`, que leem o texto dos links com o contador em zero).

```bash
git add -A src tests
git commit -m "feat(client): aviso com som de Nota Secreta em qualquer página e contador de não lidas

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 5: Páginas do jogador — aba Notas Secretas e contadores por campanha (Client)

**Files:**
- Modify: `src/RuinaRPG.Client/Pages/MinhaCampanha.razor`
- Modify: `src/RuinaRPG.Client/Pages/MinhasCampanhas.razor`
- Test: `tests/RuinaRPG.Tests.Client/Pages/MinhaCampanhaTests.cs`
- Test: `tests/RuinaRPG.Tests.Client/Pages/MinhasCampanhasTests.cs` (criar se não existir)

**Interfaces:**
- Consumes: `SecretNoteNotifier` (`UnreadFor`, `MarkCampaignReadAsync`, `Changed`, `Received`, `StartAsync`), `MudBunitContext.NotificationConnection` (Task 3); `SecretNoteNotification` (Task 2); URL `campanhas/{id}/jogador?aba=notas` (Task 4).

- [ ] **Step 1: Testes de `MinhaCampanha` (falhando)**

Em `MinhaCampanhaTests.cs` (usings novos: `Microsoft.AspNetCore.Components`, `RuinaRPG.Client.Services`, `RuinaRPG.Contracts.Diary`, `RuinaRPG.Contracts.Notifications`):

```csharp
    private readonly List<HttpRequestMessage> _requests = new();
    private List<SecretNoteResponse> _notes = new();
    private List<UnreadSecretNotesResponse> _unread = new();

    private void RegisterJogadorBackend()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("jogador");
        authContext.SetRoles("Jogador");
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            _requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("player-view"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PlayerCampaignViewResponse(new(), new(), new())) };
            if (path.EndsWith("secret-notes/unread"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_unread) };
            if (path.EndsWith("secret-notes/mark-read"))
            {
                _unread = new();
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(_notes) };
        }));
    }

    private int MarkReadCalls => _requests.Count(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("campaigns/campaign-1/secret-notes/mark-read"));

    private static string ActiveTab(IRenderedComponent<MinhaCampanha> cut) => cut.Find("div.mud-tab-active").TextContent;

    [Fact]
    public void Opens_on_the_Notas_Secretas_tab_and_marks_the_campaign_read_when_aba_is_notas()
    {
        RegisterJogadorBackend();
        Services.GetRequiredService<NavigationManager>().NavigateTo("campanhas/campaign-1/jogador?aba=notas");

        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));

        cut.WaitForAssertion(() =>
        {
            ActiveTab(cut).Should().Contain("Notas Secretas");
            MarkReadCalls.Should().Be(1);
        });
    }

    [Fact]
    public async Task Without_the_aba_parameter_it_opens_on_the_first_tab_and_marks_nothing_read()
    {
        RegisterJogadorBackend();

        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        await Task.Delay(100);

        ActiveTab(cut).Should().Contain("Minhas Fichas");
        MarkReadCalls.Should().Be(0);
    }

    [Fact]
    public async Task Notas_Secretas_tab_shows_the_unread_count_and_clears_it_when_opened()
    {
        RegisterJogadorBackend();
        _unread = new() { new("campaign-1", "Ruína", 2) };
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync();
        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        IElement NotasTab() => cut.FindAll("div.mud-tab").Single(e => e.TextContent.Contains("Notas Secretas"));
        cut.WaitForAssertion(() => NotasTab().TextContent.Should().Contain("2"));

        await cut.InvokeAsync(() => NotasTab().Click());

        cut.WaitForAssertion(() =>
        {
            MarkReadCalls.Should().Be(1);
            NotasTab().TextContent.Trim().Should().Be("Notas Secretas");
        });
    }

    [Fact]
    public async Task A_note_arriving_while_on_the_Notas_Secretas_tab_reloads_the_list_and_ends_read()
    {
        RegisterJogadorBackend();
        Services.GetRequiredService<NavigationManager>().NavigateTo("campanhas/campaign-1/jogador?aba=notas");
        var notifier = Services.GetRequiredService<SecretNoteNotifier>();
        await notifier.StartAsync();
        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        cut.WaitForAssertion(() => MarkReadCalls.Should().Be(1));
        _notes = new() { new SecretNoteResponse("n1", "Você percebe algo estranho.", DateTime.UtcNow, new(), new()) };

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("campaign-1", "Ruína"));

        cut.WaitForAssertion(() =>
        {
            cut.Markup.Should().Contain("Você percebe algo estranho.");
            MarkReadCalls.Should().Be(2);
            notifier.UnreadFor("campaign-1").Should().Be(0);
        });
    }

    [Fact]
    public async Task A_note_for_another_campaign_does_not_mark_this_one_read()
    {
        RegisterJogadorBackend();
        Services.GetRequiredService<NavigationManager>().NavigateTo("campanhas/campaign-1/jogador?aba=notas");
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync();
        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        cut.WaitForAssertion(() => MarkReadCalls.Should().Be(1));

        await NotificationConnection.RaiseReceivedAsync(new SecretNoteNotification("other", "Outra"));
        await Task.Delay(100);

        MarkReadCalls.Should().Be(1);
    }

    [Fact]
    public void Navigating_to_aba_notas_while_already_on_the_page_switches_to_the_tab()
    {
        RegisterJogadorBackend();
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("campanhas/campaign-1/jogador");
        var cut = Render<MinhaCampanha>(p => p.Add(x => x.CampaignId, "campaign-1"));
        cut.WaitForAssertion(() => ActiveTab(cut).Should().Contain("Minhas Fichas"));

        cut.InvokeAsync(() => navigation.NavigateTo("campanhas/campaign-1/jogador?aba=notas"));

        cut.WaitForAssertion(() =>
        {
            ActiveTab(cut).Should().Contain("Notas Secretas");
            MarkReadCalls.Should().Be(1);
        });
    }
```

`IElement` vem de `AngleSharp.Dom`. Se a classe da aba ativa no MudBlazor 9.9.0 não for `mud-tab-active`, ajustar o seletor de `ActiveTab` para a marcação real.

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~MinhaCampanhaTests`
Expected: os 6 testes novos falham; o teste antigo de Anexos Públicos segue passando.

- [ ] **Step 3: Implementar em `MinhaCampanha.razor`**

Topo:

```razor
@page "/campanhas/{CampaignId}/jogador"
@implements IDisposable
@inject HttpClient Http
@inject NavigationManager Navigation
@inject SecretNoteNotifier Notifier
@using Microsoft.AspNetCore.Components.Routing
@using RuinaRPG.Client.Services
@using RuinaRPG.Contracts.Campaigns
@using RuinaRPG.Contracts.Diary
@using RuinaRPG.Contracts.Notifications
@using MudBlazor
```

Abas:

```razor
<MudTabs Class="mt-3" ActivePanelIndex="_activeTab" ActivePanelIndexChanged="OnTabChangedAsync">
```

```razor
    <MudTabPanel Text="Notas Secretas" BadgeData="@NotasBadge" BadgeColor="Color.Error">
```

`@code`:

```csharp
    // Position of the "Notas Secretas" MudTabPanel above — keep in sync if the tabs are reordered.
    private const int NotasTabIndex = 2;

    [Parameter] public string CampaignId { get; set; } = "";

    private PlayerCampaignViewResponse _view = new(new(), new(), new());
    private List<SecretNoteResponse> _secretNotes = new();
    private string? _errorMessage;
    private int _activeTab;

    // MudTabPanel hides its badge when BadgeData is null.
    private object? NotasBadge => Notifier.UnreadFor(CampaignId) is > 0 and var count ? count : null;

    protected override async Task OnInitializedAsync()
    {
        Notifier.Changed += OnNotifierChanged;
        Notifier.Received += OnSecretNoteReceived;
        Navigation.LocationChanged += OnLocationChanged;
        await Task.WhenAll(LoadAsync(), LoadSecretNotesAsync());
        await ApplyAbaFromUriAsync();
    }

    // Campanha R0015: the notification links here with ?aba=notas. Read off the URI (rather than
    // a [SupplyParameterFromQuery] parameter) so that clicking the notification while already on
    // this page — a navigation that re-creates nothing — still switches tabs, via LocationChanged.
    private async Task ApplyAbaFromUriAsync()
    {
        var aba = System.Web.HttpUtility.ParseQueryString(new Uri(Navigation.Uri).Query)["aba"];
        if (aba == "notas")
            await OnTabChangedAsync(NotasTabIndex);
    }

    private async Task OnTabChangedAsync(int index)
    {
        _activeTab = index;
        if (index == NotasTabIndex)
            await Notifier.MarkCampaignReadAsync(CampaignId);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => _ = InvokeAsync(async () =>
    {
        await ApplyAbaFromUriAsync();
        StateHasChanged();
    });

    private void OnNotifierChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnSecretNoteReceived(SecretNoteNotification notification) => _ = InvokeAsync(async () =>
    {
        if (notification.CampaignId != CampaignId)
            return;

        await LoadSecretNotesAsync();
        // Already looking at the tab: the new note is on screen, so it is read.
        if (_activeTab == NotasTabIndex)
            await Notifier.MarkCampaignReadAsync(CampaignId);
        StateHasChanged();
    });

    public void Dispose()
    {
        Notifier.Changed -= OnNotifierChanged;
        Notifier.Received -= OnSecretNoteReceived;
        Navigation.LocationChanged -= OnLocationChanged;
    }
```

`LoadAsync` e `LoadSecretNotesAsync` ficam como estão.

- [ ] **Step 4: Rodar os testes de `MinhaCampanha`**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~MinhaCampanhaTests`
Expected: todos passam.

- [ ] **Step 5: Teste de `MinhasCampanhas` (falhando)**

Em `tests/RuinaRPG.Tests.Client/Pages/MinhasCampanhasTests.cs` (criar a classe se o arquivo não existir; se existir, acrescentar o teste). Conferir a assinatura de `CampaignResponse` em `src/RuinaRPG.Contracts/Campaigns/` e completar os argumentos do construtor de acordo:

```csharp
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Pages;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Tests.Client.Shared;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RuinaRPG.Tests.Client.Pages;

public class MinhasCampanhasTests : MudBunitContext
{
    /// <summary>Campanha R0015: each campaign card shows how many Notas Secretas are still unread there.</summary>
    [Fact]
    public async Task Campaign_card_shows_its_unread_secret_notes_and_only_where_there_are_some()
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("jogador");
        authContext.SetRoles("Jogador");
        var campaigns = new List<CampaignResponse>
        {
            new("c1", "Ruína", "", null),
            new("c2", "Outra", "", null),
        };
        var unread = new List<UnreadSecretNotesResponse> { new("c1", "Ruína", 2) };
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
            request.RequestUri!.AbsolutePath.EndsWith("secret-notes/unread")
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(unread) }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(campaigns) }));
        await Services.GetRequiredService<SecretNoteNotifier>().StartAsync();

        var cut = Render<MinhasCampanhas>();

        cut.WaitForAssertion(() =>
        {
            var chips = cut.FindAll(".rr-unread-chip");
            chips.Should().ContainSingle();
            chips[0].TextContent.Should().Contain("2 notas secretas não lidas");
            chips[0].Closest(".mud-card")!.TextContent.Should().Contain("Ruína");
        });
    }
}
```

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~MinhasCampanhasTests`
Expected: FAIL, `.rr-unread-chip` não existe.

- [ ] **Step 6: Implementar em `MinhasCampanhas.razor`**

Topo: `@implements IDisposable`, `@inject SecretNoteNotifier Notifier`, `@using RuinaRPG.Client.Services`.

Dentro do `MudCardContent`, depois da descrição:

```razor
                    @if (Notifier.UnreadFor(campaign.Id) is > 0 and var unread)
                    {
                        <MudChip T="string" Size="Size.Small" Color="Color.Error" Icon="@Icons.Material.Filled.MarkEmailUnread" Class="rr-unread-chip mt-2">
                            @(unread == 1 ? "1 nota secreta não lida" : $"{unread} notas secretas não lidas")
                        </MudChip>
                    }
```

`@code`: trocar `OnInitializedAsync` e acrescentar:

```csharp
    protected override Task OnInitializedAsync()
    {
        Notifier.Changed += OnNotifierChanged;
        return LoadAsync();
    }

    private void OnNotifierChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Notifier.Changed -= OnNotifierChanged;
```

- [ ] **Step 7: Suíte do client, build e commit**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test tests/RuinaRPG.Tests.Client` → todos passam.

```bash
git add -A src tests
git commit -m "feat(client): aba Notas Secretas abre pelo aviso, marca como lida e mostra não lidas por campanha

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

### Task 6: Requisitos, changelog da 1.4.4 e verificação final

**Files:**
- Modify: `Docs/Requisitos/Requisitos - Campanha.md` (novo R0015 no fim)
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md` (tabela `DiaryEntryRecipients`)
- Modify: `Docs/Requisitos/Requisitos - Técnico.md` (hub de notificações)
- Modify: `src/RuinaRPG.Client/Shared/ChangelogDialog.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/ChangelogDialogTests.cs`

**Interfaces:**
- Consumes: comportamento entregue pelas Tasks 1 a 5.

- [ ] **Step 1: Teste do changelog (falhando)**

Em `ChangelogDialogTests.cs`, no teste `Rendered_list_contains_the_section_labels_of_1_4_4`, acrescentar à asserção existente o rótulo novo:

```csharp
        cut.Markup.Should().Contain("Notas Secretas:");
```

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ChangelogDialogTests`
Expected: FAIL nesse teste.

- [ ] **Step 2: Item novo no `ChangelogDialog.razor`**

Na lista da versão 1.4.4, como primeiro `<li>`:

```razor
            <li><b>Notas Secretas:</b> o jogador é avisado na hora, com som, quando o Mestre envia uma Nota Secreta para ele, em qualquer página; o aviso leva direto à nota, e um contador mostra as que ainda não foram lidas.</li>
```

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~ChangelogDialogTests`
Expected: todos passam. Se o diálogo usar outro termo que não "Mestre" para o GM nos demais itens, usar o termo já adotado ali.

- [ ] **Step 3: R0015 em `Requisitos - Campanha.md`**

Acrescentar ao fim do arquivo, seguindo o formato dos requisitos vizinhos (título `#`, `**Descrição**`):

```markdown
# **R0015** - O jogador deve ser notificado quando recebe uma Nota Secreta.

**Descrição**: Quando o GM publica uma Nota Secreta (R0011), cada jogador destinatário que estiver com o sistema aberto recebe, em qualquer página, um aviso visual e um aviso sonoro (`harp_notification.mp3`). O aviso visual informa a campanha de origem e é clicável: leva à aba Notas Secretas da tela do jogador naquela campanha (R0009). O texto da nota não aparece no aviso.

O jogador também vê um contador de Notas Secretas não lidas: o total na barra superior e no item "Minhas Campanhas" do menu, a quantidade de cada campanha na lista de Minhas Campanhas, e a quantidade da campanha na própria aba Notas Secretas. Uma nota deixa de ser não lida quando o jogador abre a aba Notas Secretas daquela campanha — todas as notas dele ali passam a lidas de uma vez. O contador cobre quem não estava com o sistema aberto no momento do envio; o aviso sonoro não é reproduzido depois.

Se o GM editar uma Nota Secreta e acrescentar um destinatário, esse jogador é notificado como se a nota fosse nova; os destinatários que já existiam não são notificados de novo e conservam o estado de leitura. Editar apenas o texto ou as imagens não notifica ninguém. Excluir a nota, ou remover um destinatário, apenas corrige o contador de quem a perdeu.

Vale a mesma restrição de R0011: quem não é destinatário não recebe aviso nem contagem.

> O navegador pode bloquear o aviso sonoro enquanto o jogador não tiver interagido com a página; o aviso visual aparece de qualquer forma.
```

Conferir se o arquivo tem alguma lista ou índice de requisitos que precise citar o R0015, e se algum outro documento (por exemplo o preâmbulo de `Requisitos - Ficha de Personagem.md`) lista os requisitos da Campanha por número.

- [ ] **Step 4: `Modelo de Dados` e `Técnico`**

Em `Requisitos - Modelo de Dados.md`, na tabela **DiaryEntryRecipients**, acrescentar a linha (no mesmo estilo das demais tabelas do arquivo para colunas anuláveis):

```markdown
| ReadAt | timestamp NULL — nulo = não lida ([[Requisitos - Campanha]] R0015) |
```

Em `Requisitos - Técnico.md`, localizar onde o SignalR e o hub de encontros são descritos (`grep -n -i "signalr\|hubs" "Docs/Requisitos/Requisitos - Técnico.md"`) e acrescentar, no mesmo trecho, que existe um segundo hub, `/hubs/notifications`, autenticado para qualquer usuário, só de servidor para cliente, endereçado por usuário (claim `sub`), usado para os avisos de Nota Secreta ([[Requisitos - Campanha]] R0015). Não criar requisito novo para isso; estender o texto existente.

- [ ] **Step 5: Verificação final da branch**

Run: `dotnet build` → 0 warnings, 0 errors.
Run: `dotnet test` → suíte inteira. Testes de integração que falharem por timeout são rodados de novo um a um; qualquer falha que persista sozinha é falha real e deve ser corrigida antes do commit.

- [ ] **Step 6: Commit**

```bash
git add -A Docs src tests
git commit -m "docs: Campanha R0015 (notificação de Nota Secreta) e novidade no diálogo da 1.4.4

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```
