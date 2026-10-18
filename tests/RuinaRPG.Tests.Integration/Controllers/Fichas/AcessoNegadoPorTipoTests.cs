using System.Net.Http.Json;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Controllers.Fichas;

// Spec D, Review Focus 1–2: o código genérico mantém o status de acesso negado de cada tipo
// (Personagem 403, NPC/Criatura 404) e checa a chave de Perícia antes da ficha.
public class AcessoNegadoPorTipoTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    public static TheoryData<string, string, string, int> Casos => new()
    {
        // tipo, método, sufixo da rota, status esperado para um usuário sem acesso
        { "character", "GET", "skills", 403 }, { "npc", "GET", "skills", 404 }, { "creature", "GET", "skills", 404 },
        { "character", "PUT", "skills/Atletismo", 403 }, { "npc", "PUT", "skills/Atletismo", 404 }, { "creature", "PUT", "skills/Atletismo", 404 },
        { "character", "GET", "attributes", 403 }, { "npc", "GET", "attributes", 404 }, { "creature", "GET", "attributes", 404 },
        { "character", "GET", "masteries", 403 }, { "npc", "GET", "masteries", 404 }, { "creature", "GET", "masteries", 404 },
        { "character", "PUT", "attributes/Forca", 403 }, { "npc", "PUT", "attributes/Forca", 404 }, { "creature", "PUT", "attributes/Forca", 404 },
        { "character", "GET", "attributes/budget", 403 }, { "npc", "GET", "attributes/budget", 404 }, { "creature", "GET", "attributes/budget", 404 },
        { "character", "POST", "masteries", 403 }, { "npc", "POST", "masteries", 404 }, { "creature", "POST", "masteries", 404 },
        { "character", "DELETE", $"masteries/{Guid.Empty}", 403 }, { "npc", "DELETE", $"masteries/{Guid.Empty}", 404 }, { "creature", "DELETE", $"masteries/{Guid.Empty}", 404 },
        { "character", "PUT", $"masteries/{Guid.Empty}", 403 },
        { "character", "GET", "weapons", 403 }, { "npc", "GET", "weapons", 404 }, { "creature", "GET", "weapons", 404 },
        { "character", "GET", "armor-slots", 403 }, { "npc", "GET", "armor-slots", 404 }, { "creature", "GET", "armor-slots", 404 },
        { "character", "GET", "shields", 403 }, { "npc", "GET", "shields", 404 }, { "creature", "GET", "shields", 404 },
        { "character", "POST", "weapons", 403 }, { "npc", "POST", "weapons", 404 }, { "creature", "POST", "weapons", 404 },
        { "character", "PUT", "armor-slots/Capacete", 403 }, { "npc", "PUT", "armor-slots/Capacete", 404 }, { "creature", "PUT", "armor-slots/Capacete", 404 },
        { "character", "DELETE", $"shields/{Guid.Empty}", 403 }, { "npc", "DELETE", $"shields/{Guid.Empty}", 404 }, { "creature", "DELETE", $"shields/{Guid.Empty}", 404 },
        { "character", "GET", "inventory", 403 }, { "npc", "GET", "inventory", 404 }, { "creature", "GET", "spoils", 404 },
        { "character", "GET", "artifacts", 403 }, { "npc", "GET", "artifacts", 404 }, { "creature", "GET", "artifacts", 404 },
        { "character", "GET", "affections", 403 }, { "npc", "GET", "affections", 404 }, { "creature", "GET", "affections", 404 },
        { "character", "GET", "traits", 403 }, { "npc", "GET", "traits", 404 }, { "creature", "GET", "traits", 404 },
        { "character", "GET", "racial-traits/pending", 403 }, { "npc", "GET", "racial-traits/pending", 404 },
        { "character", "POST", "inventory", 403 }, { "npc", "POST", "inventory", 404 }, { "creature", "POST", "spoils", 404 },
        { "character", "DELETE", $"inventory/{Guid.Empty}", 403 }, { "npc", "DELETE", $"inventory/{Guid.Empty}", 404 }, { "creature", "DELETE", $"spoils/{Guid.Empty}", 404 },
        { "character", "POST", "artifacts", 403 }, { "npc", "POST", "artifacts", 404 }, { "creature", "POST", "artifacts", 404 },
        { "character", "DELETE", $"affections/{Guid.Empty}", 403 }, { "npc", "DELETE", $"affections/{Guid.Empty}", 404 }, { "creature", "DELETE", $"affections/{Guid.Empty}", 404 },
        { "character", "PUT", $"affections/{Guid.Empty}", 403 },
        { "character", "POST", "traits", 403 }, { "npc", "POST", "traits", 404 }, { "creature", "POST", "traits", 404 },
        { "character", "DELETE", $"traits/{Guid.Empty}", 403 }, { "npc", "DELETE", $"traits/{Guid.Empty}", 404 }, { "creature", "DELETE", $"traits/{Guid.Empty}", 404 },
        { "character", "POST", "racial-traits/resolve", 403 }, { "npc", "POST", "racial-traits/resolve", 404 },
        // Magias e Habilidades: o Personagem nega os dois GET com 404 (e não 403) — divergência acidental mantida.
        { "character", "GET", "spell-abilities", 404 }, { "npc", "GET", "spell-abilities", 404 }, { "creature", "GET", "spell-abilities", 404 },
        { "character", "GET", "spell-abilities/passivas-disponiveis", 404 }, { "npc", "GET", "spell-abilities/passivas-disponiveis", 404 }, { "creature", "GET", "spell-abilities/passivas-disponiveis", 404 },
        { "character", "POST", "spell-abilities", 403 }, { "npc", "POST", "spell-abilities", 404 }, { "creature", "POST", "spell-abilities", 404 },
        { "character", "PUT", $"spell-abilities/{Guid.Empty}", 403 }, { "npc", "PUT", $"spell-abilities/{Guid.Empty}", 404 }, { "creature", "PUT", $"spell-abilities/{Guid.Empty}", 404 },
        { "character", "DELETE", $"spell-abilities/{Guid.Empty}", 403 }, { "npc", "DELETE", $"spell-abilities/{Guid.Empty}", 404 }, { "creature", "DELETE", $"spell-abilities/{Guid.Empty}", 404 },
    };

    // Um corpo só, válido para todo PUT/POST da tabela (campos que a rota não usa são ignorados no binding).
    private static readonly object Corpo = new
    {
        Gasto = 1, AtributoEscolhido = (string?)null, Bonus = 0, TemMaestria = false,
        Nome = "Golpe", Pericia = "Atletismo", Atributo = "Forca", GastoMaestria = 0, ItemId = Guid.Empty.ToString(),
        Qtd = 1, DT = 0, ArtifactItemId = Guid.Empty.ToString(), Favorabilidade = 0, TraitId = Guid.Empty.ToString(), GratuitaTraitNome = "Golpe",
        Tipo = "Magia", Grau = 1, Descricao = "Golpe", Efeitos = Array.Empty<object>(),
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task Acesso_negado_tem_o_status_do_tipo(string tipo, string metodo, string sufixo, int status)
    {
        var sheetId = await CriarFichaDeOutroMestreAsync(tipo);
        var intruso = await RegisterGmAndGetTokenAsync($"intruso{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@x.com");
        var corpo = metodo is "PUT" or "POST" ? Corpo : null;
        var resposta = await Client.SendAsync(AuthedRequest(new HttpMethod(metodo), $"/api/{tipo}-sheets/{sheetId}/{sufixo}", intruso, corpo));
        Assert.Equal(status, (int)resposta.StatusCode);
    }

    [Theory]
    [InlineData("character")]
    [InlineData("npc")]
    [InlineData("creature")]
    public async Task Pericia_desconhecida_em_ficha_alheia_e_404(string tipo)
    {
        var sheetId = await CriarFichaDeOutroMestreAsync(tipo);
        var intruso = await RegisterGmAndGetTokenAsync($"intruso{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@x.com");
        var resposta = await Client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/{tipo}-sheets/{sheetId}/skills/nao-existe", intruso, new { Gasto = 1 }));
        Assert.Equal(404, (int)resposta.StatusCode);
    }

    /// <summary>Uma ficha do tipo pedido, de um Mestre recém-criado (o intruso nunca tem acesso a ela).</summary>
    private async Task<string> CriarFichaDeOutroMestreAsync(string tipo)
    {
        var dono = await RegisterGmAndGetTokenAsync($"dono{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@x.com");
        switch (tipo)
        {
            case "character":
                var (jogadorId, _) = await RegisterJogadorLinkedToAsync(dono, $"jog{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@x.com");
                var campanhaId = await CreateCampaignAsync(dono, "Campanha");
                await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanhaId}/members", dono, new AddCampaignMemberRequest(jogadorId)));
                var ficha = await Client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campanhaId}/character-sheets", dono, new CreateCharacterSheetRequest(jogadorId)));
                return (await ficha.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
            case "npc":
                var npc = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/npc-sheets", dono));
                return (await npc.Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id;
            case "creature":
                var criatura = await Client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/creature-sheets", dono));
                return (await criatura.Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id;
            default:
                throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null);
        }
    }
}
