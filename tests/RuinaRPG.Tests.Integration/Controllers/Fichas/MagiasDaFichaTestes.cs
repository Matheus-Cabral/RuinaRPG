using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
// Pedido e resposta de Magia/Habilidade da ficha têm o mesmo JSON nos três tipos.
using AdicionarMagia = RuinaRPG.Contracts.CharacterSheets.AddCharacterSpellAbilityRequest;
using MagiaLida = RuinaRPG.Contracts.CharacterSheets.CharacterSpellAbilityResponse;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

/// <summary>Magias e Habilidades da ficha (<c>/spell-abilities</c>) e o débito de Pontos de Ignição, nos três tipos.</summary>
public abstract class MagiasDaFichaTestes<TTipo>(ApiFixture fixture) : TestesDeFicha<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    /// <summary>SourceBankEntryId da linha de Magia da ficha, lido da tabela do tipo.</summary>
    protected abstract IQueryable<Guid?> OrigemNoBanco(RuinaRpgDbContext db, Guid magiaId);

    protected static AdicionarMagia BolaDeFogoFromScratch() => new(
        null, "Bola de Fogo", "Magia", 3, "Uma explosão de fogo.",
        [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)]);

    protected static UpdateSheetSpellAbilityRequest BolaDeFogoEditada() => new(
        "Bola de Fogo Maior", "Magia", 3, "Explosão ampliada.",
        [new SpellAbilityEffectRequest("Dano", 4, 8)]);

    protected async Task<List<SpellAbilityEntryResponse>> GetBankAsync(string gmToken)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/spell-ability-bank", gmToken));
        return (await response.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>())!;
    }

    private async Task<string> CreateBankEntryAsync(string gmToken, string nome)
    {
        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", gmToken,
            new CreateSpellAbilityEntryRequest(nome, "Magia", 1, "Descrição.", [])));
        return (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
    }

    private async Task PublishBankEntryAsync(string gmToken, string campaignId, string entryId)
    {
        var attach = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/attachments", gmToken,
            new AttachToCampaignRequest(null, null, null, entryId, null)));
        var attachmentId = (await attach.Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/campaigns/{campaignId}/attachments/{attachmentId}/visibility", gmToken, true));
    }

    private async Task<Guid?> SourceBankEntryIdAsync(string sheetSpellAbilityId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return await OrigemNoBanco(db, Guid.Parse(sheetSpellAbilityId)).SingleAsync();
    }

    protected async Task<MagiaLida> AddAsync(string token, FichaDeTeste f, AdicionarMagia request) =>
        (await (await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), token, request)))
            .Content.ReadFromJsonAsync<MagiaLida>())!;

    [Fact]
    public async Task AddFromScratch_creates_the_sheet_entry_and_an_independent_bank_copy()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.Token, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<MagiaLida>();
        body!.Nome.Should().Be("Bola de Fogo");
        body.Tipo.Should().Be("Magia");
        body.Grau.Should().Be(3);
        body.GastoEmPI.Should().Be(14); // 8 + 6
        body.Custo.Should().Be(18); // ceil(14 * 1.25) = 18
        body.Efeitos.Should().HaveCount(2);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spell-abilities"), f.Token));
        var list = await listResponse.Content.ReadFromJsonAsync<List<MagiaLida>>();
        list!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");

        var bank = await GetBankAsync(f.GmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.Tipo == "Magia" && e.Grau == 3 && e.GastoEmPI == 14 && e.Custo == 18);
    }

    // AddFromScratch_editing_the_sheet_copy_afterward_does_not_change_the_bank_copy: this plan
    // doesn't build a PUT for spell abilities — Ficha de Personagem's 4.b describes add/remove,
    // not edit-in-place — so there is no endpoint to exercise for this case; intentionally omitted.

    [Fact]
    public async Task AddFromBankEntry_copies_every_field_and_reuses_the_bank_entry_without_duplicating_it()
    {
        var f = await FichaDeJogadorAsync();
        await SetPiAsync(f, 1000);

        var bankCreateResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/spell-ability-bank", f.GmToken,
            new CreateSpellAbilityEntryRequest("Cura Leve", "Habilidade", 1, "Restaura um pouco de vida.",
                [new SpellAbilityEffectRequest("Dano", 2, 4), new SpellAbilityEffectRequest("Cura", null, 2)])));
        var bankEntry = await bankCreateResponse.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>();

        // O jogador só alcança entradas que o GM liberou como públicas na campanha da ficha.
        await PublishBankEntryAsync(f.GmToken, f.CampaignId!, bankEntry!.Id);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.Token,
            new AdicionarMagia(bankEntry!.Id, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var sheetCopy = await response.Content.ReadFromJsonAsync<MagiaLida>();
        sheetCopy!.Nome.Should().Be(bankEntry.Nome);
        sheetCopy.Tipo.Should().Be(bankEntry.Tipo);
        sheetCopy.Grau.Should().Be(bankEntry.Grau);
        sheetCopy.Descricao.Should().Be(bankEntry.Descricao);
        sheetCopy.GastoEmPI.Should().Be(bankEntry.GastoEmPI);
        sheetCopy.Custo.Should().Be(bankEntry.Custo);
        sheetCopy.Efeitos.Should().BeEquivalentTo(bankEntry.Efeitos);

        var bank = await GetBankAsync(f.GmToken);
        bank.Should().ContainSingle().Which.Id.Should().Be(bankEntry.Id); // escolher do banco não gera uma segunda entrada
        (await SourceBankEntryIdAsync(sheetCopy.Id)).Should().Be(Guid.Parse(bankEntry.Id));
    }

    [Fact]
    public async Task Delete_removes_only_the_sheet_copy()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 1000);
        var addResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.Token, BolaDeFogoFromScratch()));
        var entryId = (await addResponse.Content.ReadFromJsonAsync<MagiaLida>())!.Id;

        (await GetBankAsync(f.GmToken)).Should().ContainSingle();

        var deleteResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"spell-abilities/{entryId}"), f.Token));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var listResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spell-abilities"), f.Token));
        var list = await listResponse.Content.ReadFromJsonAsync<List<MagiaLida>>();
        list!.Should().NotContain(e => e.Id == entryId);

        (await GetBankAsync(f.GmToken)).Should().ContainSingle(); // untouched — independent copies
    }

    [Fact]
    public async Task Add_without_either_SourceBankEntryId_or_from_scratch_fields_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.Token,
            new AdicionarMagia(null, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Add_by_someone_without_access_returns_the_denied_status()
    {
        var f = await NovaFichaAsync();
        var otherToken = await Tipo.CriarIntrusoAsync(this, f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), otherToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(Tipo.StatusNegado);
    }

    [Fact]
    public async Task AddFromScratch_by_the_player_auto_attaches_the_bank_copy_as_public()
    {
        var f = await FichaDeJogadorAsync();
        await SetPiAsync(f, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.Token, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{f.CampaignId}/available-spell-abilities", f.Token));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().ContainSingle(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task Add_from_scratch_with_an_effect_missing_a_prerequisite_returns_400()
    {
        var f = await NovaFichaAsync();

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.Token,
            new AdicionarMagia(null, "Cura Sem Dano", "Magia", 1, "Descrição.",
                new List<SpellAbilityEffectRequest> { new("Cura", null, 2) })));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Segurança: uma entrada do Banco de Magias é copiada para a ficha só se for do GM da ficha e, para um
    // jogador, só se o GM a anexou como pública à campanha da ficha (Requisitos - Ficha de Personagem R0003).
    [Fact]
    public async Task AddFromBankEntry_by_a_player_requires_the_entry_to_be_public_in_the_campaign()
    {
        var f = await FichaDeJogadorAsync();
        var entryId = await CreateBankEntryAsync(f.GmToken, "Magia Reservada");

        var antes = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.Token,
            new AdicionarMagia(entryId, null, null, null, null, null)));
        antes.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await PublishBankEntryAsync(f.GmToken, f.CampaignId!, entryId);

        var depois = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.Token,
            new AdicionarMagia(entryId, null, null, null, null, null)));
        depois.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddFromBankEntry_never_accepts_another_gms_entry_not_even_for_the_gm()
    {
        var f = await NovaFichaAsync();
        var otherGmToken = await OutroMestreAsync();
        var foreignEntryId = await CreateBankEntryAsync(otherGmToken, "Magia do Outro GM");

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken,
            new AdicionarMagia(foreignEntryId, null, null, null, null, null)));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddFromScratch_links_the_sheet_entry_to_the_bank_entry_it_created()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken, BolaDeFogoFromScratch()));
        var sheetCopy = await response.Content.ReadFromJsonAsync<MagiaLida>();

        var bankEntry = (await GetBankAsync(f.GmToken)).Should().ContainSingle().Subject;
        (await SourceBankEntryIdAsync(sheetCopy!.Id)).Should().Be(Guid.Parse(bankEntry.Id));
        bankEntry.DeCriatura.Should().Be(Tipo.MagiasDeCriatura);
    }

    [Fact]
    public async Task An_entry_built_from_scratch_is_flagged_CriadaNaFicha_and_one_from_the_bank_is_not()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 100);

        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken, BolaDeFogoFromScratch()));
        var bankId = (await GetBankAsync(f.GmToken)).Single(e => e.Nome == "Bola de Fogo").Id;
        await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken,
            new AdicionarMagia(bankId, null, null, null, null, null)));

        var list = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spell-abilities"), f.GmToken)))
            .Content.ReadFromJsonAsync<List<MagiaLida>>())!;
        list.Should().HaveCount(2);
        list.Count(e => e.CriadaNaFicha).Should().Be(1);
    }

    [Fact]
    public async Task Update_by_the_owner_changes_a_from_scratch_entry_and_leaves_the_bank_copy_alone()
    {
        var f = await FichaDeJogadorAsync();
        await SetPiAsync(f, 100);
        var criada = await AddAsync(f.Token, f, BolaDeFogoFromScratch());

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"spell-abilities/{criada.Id}"), f.Token, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MagiaLida>();
        body!.Nome.Should().Be("Bola de Fogo Maior");
        body.Descricao.Should().Be("Explosão ampliada.");
        body.GastoEmPI.Should().Be(8);
        body.Custo.Should().Be(10); // ceil(8 * 1.25)
        body.Efeitos.Should().ContainSingle(e => e.EfeitoNome == "Dano");
        body.CriadaNaFicha.Should().BeTrue();
        (await GetBankAsync(f.GmToken)).Should().ContainSingle(e => e.Nome == "Bola de Fogo" && e.GastoEmPI == 14);
    }

    [Fact]
    public async Task Update_refuses_an_entry_that_came_from_the_bank()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 100);
        await AddAsync(f.GmToken, f, BolaDeFogoFromScratch());
        var bankId = (await GetBankAsync(f.GmToken)).Single(e => e.Nome == "Bola de Fogo").Id;
        var doBanco = await AddAsync(f.GmToken, f, new AdicionarMagia(bankId, null, null, null, null, null));

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"spell-abilities/{doBanco.Id}"), f.GmToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Só Magias e Habilidades montadas na ficha podem ser editadas.");
    }

    [Fact]
    public async Task Update_validates_like_a_from_scratch_add()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 100);
        var criada = await AddAsync(f.GmToken, f, BolaDeFogoFromScratch());
        var url = Url(f, $"spell-abilities/{criada.Id}");

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, f.GmToken, BolaDeFogoEditada() with { Tipo = "Passiva" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, f.GmToken, BolaDeFogoEditada() with { Tipo = "Inexistente" }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, f.GmToken,
            BolaDeFogoEditada() with { Efeitos = [new SpellAbilityEffectRequest("Efeito Que Não Existe", 1, 1)] }))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Outro Mestre: 403 no Personagem, 404 no NPC/Criatura.</summary>
    [Fact]
    public async Task Update_by_someone_who_cannot_edit_the_sheet_is_refused_and_an_unknown_id_is_404()
    {
        var f = await NovaFichaAsync();
        var outroGm = await OutroMestreAsync();
        await SetPiAsync(f, 100);
        var criada = await AddAsync(f.GmToken, f, BolaDeFogoFromScratch());

        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"spell-abilities/{criada.Id}"), outroGm, BolaDeFogoEditada())))
            .StatusCode.Should().Be(Tipo.StatusNegado);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"spell-abilities/{Guid.NewGuid()}"), f.GmToken, BolaDeFogoEditada())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Add_is_refused_when_the_Gasto_em_PI_exceeds_the_balance_even_for_the_gm()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 13);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken, BolaDeFogoFromScratch()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Pontos de Ignição insuficientes: custa 14 e a ficha tem 13.");
        (await GetPiAsync(f)).Should().Be(13);
        (await GetBankAsync(f.GmToken)).Should().NotContain(e => e.Nome == "Bola de Fogo", "uma criação recusada não deixa cópia no banco");
    }

    [Fact]
    public async Task Add_with_exactly_enough_debits_the_balance_to_zero_and_Delete_refunds_it()
    {
        var f = await FichaDeJogadorAsync();
        await SetPiAsync(f, 14);

        var criada = await AddAsync(f.Token, f, BolaDeFogoFromScratch());
        (await GetPiAsync(f)).Should().Be(0);

        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"spell-abilities/{criada.Id}"), f.Token)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetPiAsync(f)).Should().Be(14);
    }

    [Fact]
    public async Task Update_settles_only_the_difference_in_both_directions()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 14);
        var criada = await AddAsync(f.GmToken, f, BolaDeFogoFromScratch()); // gasto 14, saldo 0
        var url = Url(f, $"spell-abilities/{criada.Id}");

        // Só o nome muda (diferença 0): aceito mesmo com saldo zero.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, f.GmToken,
            new UpdateSheetSpellAbilityRequest("Outro Nome", "Magia", 3, "Uma explosão de fogo.",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(f)).Should().Be(0);

        // Custo cai de 14 para 8: devolve 6.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, f.GmToken, BolaDeFogoEditada()))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(f)).Should().Be(6);

        // Custo sobe de 8 para 14: precisa de 6, tem 6.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, f.GmToken,
            new UpdateSheetSpellAbilityRequest("Bola de Fogo", "Magia", 3, "x",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await GetPiAsync(f)).Should().Be(0);

        // Voltar a 8 e tentar subir para 14 com saldo 5: recusado, nada muda.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, f.GmToken, BolaDeFogoEditada()))).EnsureSuccessStatusCode();
        await SetPiAsync(f, 5);
        var recusada = await Client.SendAsync(AuthedRequest(HttpMethod.Put, url, f.GmToken,
            new UpdateSheetSpellAbilityRequest("Bola de Fogo", "Magia", 3, "x",
                [new SpellAbilityEffectRequest("Dano", 4, 8), new SpellAbilityEffectRequest("Alcance", 2, 6)])));
        recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await recusada.Content.ReadAsStringAsync()).Should().Contain("Pontos de Ignição insuficientes: custa 6 e a ficha tem 5.");
        (await GetPiAsync(f)).Should().Be(5);
    }

    [Fact]
    public async Task Update_refuses_a_from_scratch_entry_whose_stored_Tipo_does_not_consume_PI()
    {
        // Uma Racial montada na ficha é CriadaNaFicha mas nunca foi debitada: convertê-la em Magia por
        // aqui custaria quase nada e a remoção devolveria o gasto inteiro.
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 100);
        var racial = await AddAsync(f.GmToken, f, BolaDeFogoFromScratch() with { Tipo = "Racial" });
        var saldo = await GetPiAsync(f);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Put, Url(f, $"spell-abilities/{racial.Id}"), f.GmToken, BolaDeFogoEditada()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Só Magias e Habilidades montadas na ficha podem ser editadas.");
        var list = (await (await Client.SendAsync(AuthedRequest(HttpMethod.Get, Url(f, "spell-abilities"), f.GmToken)))
            .Content.ReadFromJsonAsync<List<MagiaLida>>())!;
        list.Should().ContainSingle(e => e.Id == racial.Id && e.Nome == "Bola de Fogo" && e.Tipo == "Racial" && e.GastoEmPI == 14 && e.Efeitos.Count == 2);
        (await GetPiAsync(f)).Should().Be(saldo);
    }

    [Fact]
    public async Task A_stale_sheet_form_cannot_erase_a_debit()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 14);
        await AddAsync(f.GmToken, f, BolaDeFogoFromScratch());
        (await GetPiAsync(f)).Should().Be(0);

        // O formulário da ficha, aberto antes do débito, ainda traz 14 no campo de PI.
        (await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{Tipo.Rota}/{f.SheetId}", f.GmToken,
            Tipo.AtualizacaoComPi(14)))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetPiAsync(f)).Should().Be(0);
    }
}

/// <summary>NPC e Criatura: o Mestre na sua própria ficha, fora de campanha.</summary>
public abstract class MagiasDeNpcOuCriaturaTestes<TTipo>(ApiFixture fixture) : MagiasDaFichaTestes<TTipo>(fixture) where TTipo : ITipoDeTeste, new()
{
    [Fact]
    public async Task AddFromScratch_by_the_gm_on_their_own_sheet_does_not_auto_attach()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var bank = await GetBankAsync(f.GmToken);
        bank.Should().ContainSingle(e => e.Nome == "Bola de Fogo"); // the bank copy still happens (R0001) — just no CampaignAttachment
    }
}

public sealed class MagiasDaFichaTestesPersonagem(ApiFixture fixture) : MagiasDaFichaTestes<TipoDeTestePersonagem>(fixture)
{
    protected override IQueryable<Guid?> OrigemNoBanco(RuinaRpgDbContext db, Guid magiaId) =>
        db.CharacterSpellAbilities.Where(e => e.Id == magiaId).Select(e => e.SourceBankEntryId);

    [Fact]
    public async Task AddFromScratch_by_the_gm_does_not_auto_attach()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 1000);

        var response = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken, BolaDeFogoFromScratch()));
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var availableResponse = await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{f.CampaignId}/available-spell-abilities", f.GmToken));
        var available = await availableResponse.Content.ReadFromJsonAsync<List<SpellAbilityEntryResponse>>();
        available!.Should().NotContain(e => e.Nome == "Bola de Fogo");
    }

    [Fact]
    public async Task Add_from_the_bank_also_checks_and_debits()
    {
        var f = await NovaFichaAsync();
        await SetPiAsync(f, 20);
        await AddAsync(f.GmToken, f, BolaDeFogoFromScratch()); // 20 - 14 = 6
        var bankId = (await GetBankAsync(f.GmToken)).Single(e => e.Nome == "Bola de Fogo").Id;

        var recusada = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken,
            new AdicionarMagia(bankId, null, null, null, null, null)));
        recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await SetPiAsync(f, 14);
        var aceita = await Client.SendAsync(AuthedRequest(HttpMethod.Post, Url(f, "spell-abilities"), f.GmToken,
            new AdicionarMagia(bankId, null, null, null, null, null)));
        aceita.StatusCode.Should().Be(HttpStatusCode.Created);
        (await GetPiAsync(f)).Should().Be(0);
    }

    [Fact]
    public async Task Deleting_an_entry_that_was_never_debited_still_refunds_its_Gasto_em_PI()
    {
        var f = await NovaFichaAsync();
        var legadaId = Guid.NewGuid();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.CharacterSpellAbilities.Add(new RuinaRPG.Infrastructure.CharacterSheets.CharacterSpellAbility
            {
                Id = legadaId, SheetId = Guid.Parse(f.SheetId), Nome = "Antiga", Tipo = RuinaRPG.Domain.SpellsAndAbilities.SpellAbilityTipo.Magia,
                Grau = 1, GastoEmPI = 9, Custo = 12, Descricao = "anterior à 1.5.0",
            });
            await db.SaveChangesAsync();
        }
        await SetPiAsync(f, 2);

        (await Client.SendAsync(AuthedRequest(HttpMethod.Delete, Url(f, $"spell-abilities/{legadaId}"), f.GmToken))).EnsureSuccessStatusCode();

        (await GetPiAsync(f)).Should().Be(11);
    }
}

public sealed class MagiasDaFichaTestesNpc(ApiFixture fixture) : MagiasDeNpcOuCriaturaTestes<TipoDeTesteNpc>(fixture)
{
    protected override IQueryable<Guid?> OrigemNoBanco(RuinaRpgDbContext db, Guid magiaId) =>
        db.NpcSpellAbilities.Where(e => e.Id == magiaId).Select(e => e.SourceBankEntryId);
}

public sealed class MagiasDaFichaTestesCriatura(ApiFixture fixture) : MagiasDeNpcOuCriaturaTestes<TipoDeTesteCriatura>(fixture)
{
    protected override IQueryable<Guid?> OrigemNoBanco(RuinaRpgDbContext db, Guid magiaId) =>
        db.CreatureSpellAbilities.Where(e => e.Id == magiaId).Select(e => e.SourceBankEntryId);
}
