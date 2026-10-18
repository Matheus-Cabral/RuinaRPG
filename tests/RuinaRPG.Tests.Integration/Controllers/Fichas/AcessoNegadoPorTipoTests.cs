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
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public async Task Acesso_negado_tem_o_status_do_tipo(string tipo, string metodo, string sufixo, int status)
    {
        var sheetId = await CriarFichaDeOutroMestreAsync(tipo);
        var intruso = await RegisterGmAndGetTokenAsync($"intruso{Guid.NewGuid():N}"[..20], $"{Guid.NewGuid():N}@x.com");
        var corpo = metodo == "PUT" ? new { Gasto = 1, AtributoEscolhido = (string?)null } : null;
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
