using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Runes;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Runes;
// Pedido e resposta de Runa têm o mesmo JSON no Personagem e no NPC.
using AdicionarRuna = RuinaRPG.Contracts.CharacterSheets.AddCharacterRuneRequest;
using RunaLida = RuinaRPG.Contracts.CharacterSheets.CharacterRuneResponse;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>
/// Runas da ficha (<c>/runes</c>), que só Personagem e NPC têm. <c>FichaDeJogadorAsync</c> é a ficha do
/// Jogador no Personagem e um NPC concedido a ele; <c>NovaFichaAsync</c> no NPC é o NPC do próprio Mestre.
/// </summary>
public abstract class RunasDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTestePersonagemOuNpc, new()
{
    /// <summary>SourceBankEntryId da Runa da ficha, lido da tabela do tipo.</summary>
    protected abstract IQueryable<Guid?> OrigemNoBanco(RuinaRpgDbContext db, Guid runaId);

    private async Task<string> CreateRuneEntryAsync(string gmToken, string nome, string descricao, int grau, string? tipo = null)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", gmToken, new CreateRuneBankEntryRequest(nome, descricao, grau, null, tipo, Disciplina: "Adicao")));
        return (await response.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!.Id;
    }

    private async Task PublicarNaCampanhaAsync(string gmToken, string campaignId, AttachToCampaignRequest anexo)
    {
        var attach = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken, anexo));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private Task PublishRuneToCampaignAsync(string gmToken, string campaignId, string runeEntryId) =>
        PublicarNaCampanhaAsync(gmToken, campaignId, new AttachToCampaignRequest(null, null, null, null, null, runeEntryId));

    private Task PublishImageToCampaignAsync(string gmToken, string campaignId, string imageId) =>
        PublicarNaCampanhaAsync(gmToken, campaignId, new AttachToCampaignRequest(null, null, null, null, imageId, null));

    private async Task<List<RuneBankEntryResponse>> BankOfAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/rune-bank", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    private async Task<List<RuneBankEntryResponse>> PublicRunesAsync(string token, string campaignId)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{campaignId}/available-runes", token));
        return (await response.Content.ReadFromJsonAsync<List<RuneBankEntryResponse>>())!;
    }

    private async Task<List<RunaLida>> RunesOfAsync(FichaDeTeste f, string token)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "runes"), token));
        return (await response.Content.ReadFromJsonAsync<List<RunaLida>>())!;
    }

    private Task<HttpResponseMessage> AddAsync(FichaDeTeste f, string token, AdicionarRuna request) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "runes"), token, request));

    private async Task<(string Id, string Url)> UploadImageAsync(string token, string? campaignId = null)
    {
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(fileContent, "file", "test.png");
        if (campaignId is not null)
            content.Add(new StringContent(campaignId), "campaignId");
        var message = new HttpRequestMessage(HttpMethod.Post, "/api/images") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await Client.SendAsync(message);
        var body = await response.Content.ReadFromJsonAsync<RuinaRPG.Contracts.Images.ImageUploadResponse>();
        return (body!.Id, body.Url);
    }

    private async Task<Guid?> SourceBankEntryIdAsync(string runeId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return await OrigemNoBanco(db, Guid.Parse(runeId)).SingleAsync();
    }

    [Fact]
    public async Task Add_a_valid_rune_returns_201()
    {
        var f = await NovaFichaAsync();

        var response = await AddAsync(f, f.Token, new AdicionarRuna("Runa do Fogo", "Queima o alvo.", 1, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await RunesOfAsync(f, f.Token);
        body.Should().ContainSingle(r => r.Nome == "Runa do Fogo" && r.Descricao == "Queima o alvo." && r.Grau == 1);
    }

    [Fact]
    public async Task List_returns_the_runes_on_the_sheet()
    {
        var f = await NovaFichaAsync();

        await AddAsync(f, f.Token, new AdicionarRuna("Runa da Terra", "Endurece a pele.", 2, Disciplina: "Adicao"));

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "runes"), f.Token));
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await listResponse.Content.ReadFromJsonAsync<List<RunaLida>>();
        body!.Should().ContainSingle(r => r.Nome == "Runa da Terra" && r.Descricao == "Endurece a pele." && r.Grau == 2);
    }

    [Fact]
    public async Task Delete_an_existing_rune_returns_204_and_it_no_longer_appears_on_list()
    {
        var f = await NovaFichaAsync();

        var addResponse = await AddAsync(f, f.Token, new AdicionarRuna("Runa da Água", "Cura ferimentos leves.", 3, Disciplina: "Adicao"));
        var added = await addResponse.Content.ReadFromJsonAsync<RunaLida>();

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"runes/{added!.Id}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await RunesOfAsync(f, f.Token)).Should().NotContain(r => r.Id == added.Id);
    }

    [Fact]
    public async Task Add_by_someone_without_access_returns_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);

        var response = await AddAsync(f, otherToken, new AdicionarRuna("Runa do Vento", "Aumenta velocidade.", 1, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task A_rune_on_the_sheet_is_read_only_there_is_no_update_endpoint()
    {
        var f = await NovaFichaAsync();
        var addResponse = await AddAsync(f, f.Token, new AdicionarRuna("Runa do Fogo", "Queima o alvo.", 1, Disciplina: "Adicao"));
        var added = await addResponse.Content.ReadFromJsonAsync<RunaLida>();

        var update = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"runes/{added!.Id}"), f.Token,
            new { Nome = "Runa do Gelo", Descricao = "Congela.", Grau = 2 }));

        update.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    // Cobre também o antigo NPC A_rune_added_by_the_gm_lands_in_the_bank (mesmas afirmações e mais estas).
    [Fact]
    public async Task A_rune_added_by_the_gm_lands_in_the_bank_but_is_not_published_to_the_campaign()
    {
        var f = await FichaDeJogadorAsync();

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna("Runa do Fogo", "Queima o alvo.", 2, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(f.GmToken)).Should().ContainSingle(e => e.Nome == "Runa do Fogo" && e.Descricao == "Queima o alvo." && e.Grau == 2);
        (await PublicRunesAsync(f.Token, f.CampaignId!)).Should().BeEmpty();
    }

    // Cobre também o antigo NPC A_rune_added_by_the_player_a_npc_was_granted_to_is_also_published_to_the_campaign.
    [Fact]
    public async Task A_rune_added_by_the_player_lands_in_the_bank_and_is_published_to_the_campaign()
    {
        var f = await FichaDeJogadorAsync();

        var response = await AddAsync(f, f.Token, new AdicionarRuna("Runa da Água", "Cura ferimentos leves.", 1, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await BankOfAsync(f.GmToken)).Should().ContainSingle(e => e.Nome == "Runa da Água");
        (await PublicRunesAsync(f.Token, f.CampaignId!)).Should().ContainSingle(e => e.Nome == "Runa da Água" && e.Grau == 1);
    }

    [Fact]
    public async Task A_rune_picked_from_the_bank_copies_its_fields_without_duplicating_the_bank_entry()
    {
        var f = await NovaFichaAsync();
        var entryId = await CreateRuneEntryAsync(f.GmToken, "Runa da Luz", "Ilumina a área.", 3);

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, entryId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<RunaLida>();
        created!.Nome.Should().Be("Runa da Luz");
        created.Descricao.Should().Be("Ilumina a área.");
        created.Grau.Should().Be(3);
        (await BankOfAsync(f.GmToken)).Should().ContainSingle().Which.Id.Should().Be(entryId); // escolher do banco não duplica (R0003)
        (await SourceBankEntryIdAsync(created.Id)).Should().Be(Guid.Parse(entryId));
    }

    // ---- Tipo opcional (Arcana / Negra) ----

    [Fact]
    public async Task A_rune_from_scratch_with_a_tipo_returns_it_and_the_bank_copy_carries_it()
    {
        var f = await FichaDeJogadorAsync();

        var response = await AddAsync(f, f.Token, new AdicionarRuna("Runa Sombria", "Escura.", 2, null, null, "Negra", Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<RunaLida>())!.Tipo.Should().Be("Negra");
        (await RunesOfAsync(f, f.Token)).Should().ContainSingle(r => r.Nome == "Runa Sombria" && r.Tipo == "Negra");
        (await BankOfAsync(f.GmToken)).Should().ContainSingle(e => e.Nome == "Runa Sombria" && e.Tipo == "Negra");
        (await PublicRunesAsync(f.Token, f.CampaignId!)).Should().ContainSingle(e => e.Nome == "Runa Sombria" && e.Tipo == "Negra");
    }

    [Fact]
    public async Task A_rune_without_tipo_has_a_null_tipo()
    {
        var f = await NovaFichaAsync();

        await AddAsync(f, f.Token, new AdicionarRuna("Runa Comum", "Sem tipo.", 1, null, null, "", Disciplina: "Adicao"));

        (await RunesOfAsync(f, f.Token)).Should().ContainSingle(r => r.Nome == "Runa Comum" && r.Tipo == null);
    }

    [Fact]
    public async Task A_rune_with_an_unknown_tipo_returns_400_and_creates_nothing()
    {
        var f = await NovaFichaAsync();

        var response = await AddAsync(f, f.Token, new AdicionarRuna("Runa", "D.", 1, null, null, "Branca", Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Tipo de Runa desconhecido.");
        (await RunesOfAsync(f, f.Token)).Should().BeEmpty();
        (await BankOfAsync(f.GmToken)).Should().BeEmpty();
    }

    /// <summary>Era o caso do NPC: o Mestre escolhe do banco.</summary>
    [Fact]
    public async Task A_rune_picked_from_the_bank_copies_the_tipo()
    {
        var f = await NovaFichaAsync();
        var entryId = await CreateRuneEntryAsync(f.GmToken, "Runa Arcana da Luz", "Ilumina.", 1, "Arcana");

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, entryId));

        (await response.Content.ReadFromJsonAsync<RunaLida>())!.Tipo.Should().Be("Arcana");
        (await RunesOfAsync(f, f.GmToken)).Should().ContainSingle(r => r.Tipo == "Arcana");
    }

    /// <summary>Era o caso do Personagem (<c>A_rune_picked_from_the_bank_copies_the_tipo</c>): Mestre e Jogador escolhem do banco.</summary>
    [Fact]
    public async Task A_rune_picked_from_the_bank_by_the_gm_and_by_the_player_copies_the_tipo()
    {
        var f = await FichaDeJogadorAsync();
        var entryId = await CreateRuneEntryAsync(f.GmToken, "Runa Arcana da Luz", "Ilumina.", 1, "Arcana");
        await PublishRuneToCampaignAsync(f.GmToken, f.CampaignId!, entryId);

        var doGm = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, entryId));
        var doJogador = await AddAsync(f, f.Token, new AdicionarRuna(null, null, null, entryId));

        (await doGm.Content.ReadFromJsonAsync<RunaLida>())!.Tipo.Should().Be("Arcana");
        (await doJogador.Content.ReadFromJsonAsync<RunaLida>())!.Tipo.Should().Be("Arcana");
        (await RunesOfAsync(f, f.GmToken)).Should().OnlyContain(r => r.Tipo == "Arcana");
    }

    [Fact]
    public async Task Available_runes_for_a_player_include_the_tipo()
    {
        var f = await FichaDeJogadorAsync();
        var entryId = await CreateRuneEntryAsync(f.GmToken, "Runa Pública", "D.", 1, "Negra");
        await PublishRuneToCampaignAsync(f.GmToken, f.CampaignId!, entryId);

        (await PublicRunesAsync(f.Token, f.CampaignId!)).Should().ContainSingle(e => e.Id == entryId && e.Tipo == "Negra");
    }

    [Fact]
    public async Task A_player_can_only_pick_bank_entries_the_gm_published_to_the_campaign()
    {
        var f = await FichaDeJogadorAsync();
        var entryId = await CreateRuneEntryAsync(f.GmToken, "Runa Reservada", "Só depois de liberada.", 1);

        var antes = await AddAsync(f, f.Token, new AdicionarRuna(null, null, null, entryId));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishRuneToCampaignAsync(f.GmToken, f.CampaignId!, entryId);

        var depois = await AddAsync(f, f.Token, new AdicionarRuna(null, null, null, entryId));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Adding_with_both_paths_or_with_neither_returns_400()
    {
        var f = await NovaFichaAsync();
        var entryId = await CreateRuneEntryAsync(f.GmToken, "Runa", "Desc.", 1);

        var both = await AddAsync(f, f.GmToken, new AdicionarRuna("Runa", "Desc.", 1, entryId));
        var neither = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null));
        var partial = await AddAsync(f, f.GmToken, new AdicionarRuna("Só o nome", null, null));

        both.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        neither.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        partial.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task Picking_a_malformed_or_unknown_bank_entry_returns_400(string entryId)
    {
        var f = await NovaFichaAsync();

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, entryId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Picking_another_gms_bank_entry_returns_400()
    {
        var f = await NovaFichaAsync();
        var otherGmToken = await OutroMestreAsync();
        var foreignEntry = await CreateRuneEntryAsync(otherGmToken, "Runa Alheia", "Do outro GM.", 1);

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, foreignEntry));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---- Imagem opcional da Runa ----

    [Fact]
    public async Task A_rune_from_scratch_with_the_callers_own_image_returns_201_and_the_list_has_the_ImageUrl()
    {
        var f = await FichaDeJogadorAsync();
        var (imageId, imageUrl) = await UploadImageAsync(f.Token, f.CampaignId);

        var response = await AddAsync(f, f.Token, new AdicionarRuna("Runa Ilustrada", "Tem imagem.", 1, null, imageId, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<RunaLida>())!.ImageUrl.Should().Be(imageUrl);
        (await RunesOfAsync(f, f.Token)).Should().ContainSingle(r => r.Nome == "Runa Ilustrada" && r.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task A_rune_without_image_has_a_null_ImageUrl()
    {
        var f = await NovaFichaAsync();

        await AddAsync(f, f.Token, new AdicionarRuna("Runa Nua", "Sem imagem.", 1, null, "", Disciplina: "Adicao"));

        (await RunesOfAsync(f, f.Token)).Should().ContainSingle(r => r.Nome == "Runa Nua" && r.ImageUrl == null);
    }

    [Fact]
    public async Task The_automatic_bank_copy_carries_the_image()
    {
        var f = await NovaFichaAsync();
        var (imageId, imageUrl) = await UploadImageAsync(f.GmToken);

        await AddAsync(f, f.GmToken, new AdicionarRuna("Runa do GM", "Com imagem.", 2, null, imageId, Disciplina: "Adicao"));

        (await BankOfAsync(f.GmToken)).Should().ContainSingle(e => e.Nome == "Runa do GM" && e.ImageId == imageId && e.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task A_jogador_can_use_an_image_the_gm_published_in_the_campaign()
    {
        var f = await FichaDeJogadorAsync();
        var (imageId, imageUrl) = await UploadImageAsync(f.GmToken);
        await PublishImageToCampaignAsync(f.GmToken, f.CampaignId!, imageId);

        var response = await AddAsync(f, f.Token, new AdicionarRuna("Runa Liberada", "Imagem do GM.", 1, null, imageId, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await RunesOfAsync(f, f.Token)).Should().ContainSingle(r => r.ImageUrl == imageUrl);
    }

    [Fact]
    public async Task A_jogador_cannot_use_a_non_public_image_of_the_gm()
    {
        var f = await FichaDeJogadorAsync();
        var (naoAnexada, _) = await UploadImageAsync(f.GmToken);
        var (privada, _) = await UploadImageAsync(f.GmToken);
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{f.CampaignId}/attachments", f.GmToken,
            new AttachToCampaignRequest(null, null, null, null, privada, null))); // anexada, mas privada

        var semAnexo = await AddAsync(f, f.Token, new AdicionarRuna("Runa", "X.", 1, null, naoAnexada, Disciplina: "Adicao"));
        var comAnexoPrivado = await AddAsync(f, f.Token, new AdicionarRuna("Runa", "X.", 1, null, privada, Disciplina: "Adicao"));

        semAnexo.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        comAnexoPrivado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RunesOfAsync(f, f.Token)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_public_image_of_another_campaign_is_not_usable()
    {
        var f = await FichaDeJogadorAsync();
        var otherCampaignId = await CreateCampaignAsync(f.GmToken, "Outra Campanha");
        var (imageId, _) = await UploadImageAsync(f.GmToken);
        await PublishImageToCampaignAsync(f.GmToken, otherCampaignId, imageId);

        var response = await AddAsync(f, f.Token, new AdicionarRuna("Runa", "X.", 1, null, imageId, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000001")]
    public async Task A_malformed_or_unknown_image_id_returns_400(string imageId)
    {
        var f = await NovaFichaAsync();

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna("Runa", "X.", 1, null, imageId, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_rune_picked_from_the_bank_inherits_the_entrys_image()
    {
        var f = await NovaFichaAsync();
        var (imageId, imageUrl) = await UploadImageAsync(f.GmToken);
        var entryResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", f.GmToken,
            new CreateRuneBankEntryRequest("Runa Herdeira", "Herda a imagem.", 1, imageId, Disciplina: "Adicao")));
        var entry = (await entryResponse.Content.ReadFromJsonAsync<RuneBankEntryResponse>())!;

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, entry.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<RunaLida>())!.ImageUrl.Should().Be(imageUrl);
        (await RunesOfAsync(f, f.GmToken)).Should().ContainSingle(r => r.Nome == "Runa Herdeira" && r.ImageUrl == imageUrl);
        (await BankOfAsync(f.GmToken)).Should().ContainSingle(e => e.Nome == "Runa Herdeira").Which.ImageId.Should().Be(imageId);
    }

    [Fact]
    public async Task Picking_from_the_bank_together_with_an_image_id_returns_400()
    {
        var f = await NovaFichaAsync();
        var (imageId, _) = await UploadImageAsync(f.GmToken);
        var entryId = await CreateRuneEntryAsync(f.GmToken, "Runa", "Desc.", 1);

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, entryId, imageId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_rune_added_from_scratch_is_linked_to_the_bank_entry_it_created()
    {
        var f = await NovaFichaAsync();

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna("Runa Nova", "Criada do zero.", 1, null, Disciplina: "Adicao"));
        var created = (await response.Content.ReadFromJsonAsync<RunaLida>())!;

        var bankEntry = (await BankOfAsync(f.GmToken)).Should().ContainSingle().Subject;
        (await SourceBankEntryIdAsync(created.Id)).Should().Be(Guid.Parse(bankEntry.Id));
    }

    [Fact]
    public async Task Add_from_scratch_requires_a_disciplina_and_returns_it()
    {
        var f = await NovaFichaAsync();

        var semDisciplina = await AddAsync(f, f.GmToken, new AdicionarRuna("Runa", "Desc.", 1));
        var comDisciplina = await AddAsync(f, f.GmToken, new AdicionarRuna("Runa", "Desc.", 1, Disciplina: "Alteracao"));

        semDisciplina.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await semDisciplina.Content.ReadAsStringAsync()).Should().Contain("Disciplina é obrigatória.");
        comDisciplina.StatusCode.Should().Be(HttpStatusCode.Created);
        (await comDisciplina.Content.ReadFromJsonAsync<RunaLida>())!.Disciplina.Should().Be("Alteracao");
    }

    [Fact]
    public async Task Add_from_scratch_rejects_an_unknown_disciplina()
    {
        var f = await NovaFichaAsync();

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna("Runa", "Desc.", 1, Disciplina: "Bruxaria"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Disciplina de Runa desconhecida.");
    }

    [Fact]
    public async Task Add_from_scratch_copies_the_disciplina_to_the_automatic_bank_entry()
    {
        var f = await NovaFichaAsync();

        await AddAsync(f, f.GmToken, new AdicionarRuna("Runa Copiada", "Desc.", 1, Disciplina: "Emissao"));

        var bank = await BankOfAsync(f.GmToken);
        bank.Single(e => e.Nome == "Runa Copiada").Disciplina.Should().Be("Emissao");
    }

    [Fact]
    public async Task Add_from_the_bank_copies_the_entrys_disciplina_and_ignores_the_requests()
    {
        var f = await NovaFichaAsync();
        var entry = await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/rune-bank", f.GmToken,
            new CreateRuneBankEntryRequest("Do Banco", "Desc.", 1, null, null, "Manifestacao")))).Content.ReadFromJsonAsync<RuneBankEntryResponse>();

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, entry!.Id, Disciplina: "Adicao"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<RunaLida>())!.Disciplina.Should().Be("Manifestacao");
    }

    [Fact]
    public async Task Add_from_a_legacy_bank_entry_without_disciplina_is_accepted_and_stays_without()
    {
        var f = await NovaFichaAsync();
        var entryId = Guid.NewGuid();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var gmId = await db.Users.Where(u => u.NormalizedEmail == f.GmEmail.ToUpperInvariant()).Select(u => u.Id).SingleAsync();
            db.RuneBankEntries.Add(new RuneBankEntry { Id = entryId, GmId = gmId, Nome = "Antiga", Descricao = "Pré-1.4.3", Grau = 1 });
            await db.SaveChangesAsync();
        }

        var response = await AddAsync(f, f.GmToken, new AdicionarRuna(null, null, null, entryId.ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.Content.ReadFromJsonAsync<RunaLida>())!.Disciplina.Should().BeNull();
    }
}

public sealed class RunasDaFichaTestesPersonagem(ApiFixture fixture) : RunasDaFichaTestes<TipoDeTestePersonagem>(fixture)
{
    protected override IQueryable<Guid?> OrigemNoBanco(RuinaRpgDbContext db, Guid runaId) =>
        db.CharacterRunes.Where(r => r.Id == runaId).Select(r => r.SourceBankEntryId);
}

public sealed class RunasDaFichaTestesNpc(ApiFixture fixture) : RunasDaFichaTestes<TipoDeTesteNpc>(fixture)
{
    protected override IQueryable<Guid?> OrigemNoBanco(RuinaRpgDbContext db, Guid runaId) =>
        db.NpcRunes.Where(r => r.Id == runaId).Select(r => r.SourceBankEntryId);
}
