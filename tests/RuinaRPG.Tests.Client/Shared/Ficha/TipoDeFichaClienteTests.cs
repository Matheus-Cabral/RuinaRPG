using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// O tipo de ficha do lado do cliente: o prefixo da API e os fatos que as abas compartilhadas perguntam. Os
/// valores vêm do inventário de diferenças do E (docs/superpowers/specs/2026-10-19-e-inventario-de-diferencas.md,
/// "Proposed capability flags", aprovado pelo usuário em 2026-10-22).
/// </summary>
public class TipoDeFichaClienteTests
{
    private static TipoDeFichaCliente Tipo(string nome) => nome switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        "Criatura" => TipoDeFichaCliente.Criatura,
        _ => throw new ArgumentOutOfRangeException(nameof(nome)),
    };

    [Theory]
    [InlineData("Personagem", "character-sheets")]
    [InlineData("NPC", "npc-sheets")]
    [InlineData("Criatura", "creature-sheets")]
    public void Nome_and_Rota_per_kind(string nome, string rota)
    {
        Tipo(nome).Nome.Should().Be(nome);
        Tipo(nome).Rota.Should().Be(rota);
    }

    [Theory]
    [InlineData("Personagem", "character-sheets/s1")]
    [InlineData("NPC", "npc-sheets/s1")]
    [InlineData("Criatura", "creature-sheets/s1")]
    public void Url_without_suffix_is_the_sheet_address(string nome, string esperada) =>
        Tipo(nome).Url("s1").Should().Be(esperada);

    [Theory]
    [InlineData("Personagem", "character-sheets/s1/weapons/w1")]
    [InlineData("NPC", "npc-sheets/s1/weapons/w1")]
    [InlineData("Criatura", "creature-sheets/s1/weapons/w1")]
    public void Url_with_suffix_appends_it_after_a_slash(string nome, string esperada) =>
        Tipo(nome).Url("s1", "weapons/w1").Should().Be(esperada);

    [Theory]
    [InlineData(nameof(TipoDeFichaCliente.TemDiario), true, false, false)]
    [InlineData(nameof(TipoDeFichaCliente.TemAntecedentes), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.EditaNivelAManual), false, true, true)]
    [InlineData(nameof(TipoDeFichaCliente.TemOrcamentoDePericias), true, false, false)]
    [InlineData(nameof(TipoDeFichaCliente.TemEspolios), false, false, true)]
    [InlineData(nameof(TipoDeFichaCliente.TemInventario), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.TemRunas), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.TemAfinidades), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.TemTracosRaciais), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.EditaMaestriaNaLinha), true, false, false)]
    [InlineData(nameof(TipoDeFichaCliente.EditaAfeicaoNaLinha), true, false, false)]
    [InlineData(nameof(TipoDeFichaCliente.TemArmaManual), false, false, true)]
    [InlineData(nameof(TipoDeFichaCliente.CondicoesSoSeConcedida), false, true, true)]
    [InlineData(nameof(TipoDeFichaCliente.TemLinhagemEVocacao), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.TemSinaEEstresse), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.MostraProgressaoDoNivel), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.CadastraItemNoPicker), false, true, true)]
    [InlineData(nameof(TipoDeFichaCliente.MostraPesoDasArmas), true, true, false)]
    [InlineData(nameof(TipoDeFichaCliente.CriaCaracteristicaNoPicker), false, false, true)]
    public void Flag_per_kind(string flag, bool personagem, bool npc, bool criatura)
    {
        Ler<bool>(TipoDeFichaCliente.Personagem, flag).Should().Be(personagem);
        Ler<bool>(TipoDeFichaCliente.Npc, flag).Should().Be(npc);
        Ler<bool>(TipoDeFichaCliente.Criatura, flag).Should().Be(criatura);
    }

    [Theory]
    [InlineData(nameof(TipoDeFichaCliente.TipoDaNotificacao), "character", "npc", "creature")]
    [InlineData(nameof(TipoDeFichaCliente.Titulo), "Ficha de Personagem", "Ficha de NPC", "Ficha de Criatura")]
    [InlineData(nameof(TipoDeFichaCliente.RotuloDoFoco), "Foco (PF)", "Foco (PF)", "Arcana (PF)")]
    [InlineData(nameof(TipoDeFichaCliente.RotuloDosPontosDeIgnicao), "Pontos de Ignição Atual", "Pontos de Ignição Atual", "Pontos de Ignição")]
    [InlineData(nameof(TipoDeFichaCliente.UrlDeBuscaDeCaracteristicas), "traits", "traits", "creature-exclusive-traits")]
    public void Text_member_per_kind(string membro, string personagem, string npc, string criatura)
    {
        Ler<string>(TipoDeFichaCliente.Personagem, membro).Should().Be(personagem);
        Ler<string>(TipoDeFichaCliente.Npc, membro).Should().Be(npc);
        Ler<string>(TipoDeFichaCliente.Criatura, membro).Should().Be(criatura);
    }

    // A2 (decidido em 2026-10-22): a ajuda do XP por abate e assistência existe no NPC ("só o GM vê"); a Criatura mostra
    // os números sem ajuda, e o Personagem não tem Abate.
    [Theory]
    [InlineData(nameof(TipoDeFichaCliente.AjudaDoXpPorAbate), null, "XP que o NPC concede aos jogadores ao ser derrotado: Abate para quem deu o golpe final, Assistência para quem ajudou. Calculado a partir da Experiência atual do NPC; só o GM vê.", null)]
    // A1 (decidido: fica por tipo): o diálogo da Característica Racial fala com o jogador no Personagem e do NPC no NPC.
    [InlineData(nameof(TipoDeFichaCliente.TituloDaEscolhaRacial), "Escolha suas Características Raciais", "Escolha a Característica Racial do NPC", null)]
    [InlineData(nameof(TipoDeFichaCliente.RecebeAutomaticamente), "Você também recebe automaticamente:", "O NPC também recebe automaticamente:", null)]
    public void Optional_text_member_per_kind(string membro, string? personagem, string? npc, string? criatura)
    {
        Ler<string?>(TipoDeFichaCliente.Personagem, membro).Should().Be(personagem);
        Ler<string?>(TipoDeFichaCliente.Npc, membro).Should().Be(npc);
        Ler<string?>(TipoDeFichaCliente.Criatura, membro).Should().Be(criatura);
    }

    // Nenhum fato fica sem teste: cada propriedade pública de TipoDeFichaCliente aparece num InlineData acima.
    [Fact]
    public void Every_member_is_pinned_by_a_test()
    {
        var testados = typeof(TipoDeFichaClienteTests).GetMethods()
            .SelectMany(m => m.GetCustomAttributes(typeof(InlineDataAttribute), false).Cast<InlineDataAttribute>())
            .Where(a => a.GetData(null!).First() is object[] { Length: > 0 } valores && valores[0] is string)
            .Select(a => (string)a.GetData(null!).First()[0])
            .ToHashSet();
        var membros = typeof(TipoDeFichaCliente).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(n => n is not ("Nome" or "Rota" or "EqualityContract"));

        membros.Should().OnlyContain(n => testados.Contains(n));
    }

    private static T Ler<T>(TipoDeFichaCliente tipo, string membro) =>
        (T)typeof(TipoDeFichaCliente).GetProperty(membro)!.GetValue(tipo)!;
}
