using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class PassivaRequisitosEvaluatorTests
{
    private static FichaParaRequisitos Personagem(
        int nivel = 5, Vocacao? vocacao = Vocacao.Feiticeiro, string? classe = "Elementalista",
        Linhagem? linhagem = Linhagem.Humano, Variante? variante = Variante.Sinir, int graduacao = 3,
        bool coracao = true, AfinidadeElemental? afinidade = AfinidadeElemental.Fogo, Estrela? estrela = Estrela.Liora,
        Guid? historicoId = null,
        Dictionary<Atributo, int>? atributos = null, Dictionary<SubAtributo, int>? subAtributos = null,
        Dictionary<Pericia, int?>? pericias = null) =>
        new(true, nivel, vocacao, classe, linhagem, variante, graduacao, coracao, afinidade, estrela, historicoId,
            atributos ?? Enum.GetValues<Atributo>().ToDictionary(a => a, _ => 3),
            subAtributos ?? Enum.GetValues<SubAtributo>().ToDictionary(s => s, _ => 3),
            pericias ?? Enum.GetValues<Pericia>().ToDictionary(p => p, _ => (int?)3));

    // Criatura: sem identidade de personagem; só Força/Vigor/Agilidade/Destreza/Astúcia; só algumas Perícias.
    private static FichaParaRequisitos Criatura(int nivel = 5, AfinidadeElemental? afinidade = AfinidadeElemental.Fogo) =>
        new(false, nivel, null, null, null, null, 0, false, afinidade, null, null,
            new Dictionary<Atributo, int> { [Atributo.Forca] = 3, [Atributo.Vigor] = 3, [Atributo.Agilidade] = 3, [Atributo.Destreza] = 3, [Atributo.Astucia] = 3 },
            Enum.GetValues<SubAtributo>().ToDictionary(s => s, _ => 3),
            new Dictionary<Pericia, int?> { [Pericia.Atletismo] = 3 });

    [Fact]
    public void Null_requisitos_have_no_pendencias() =>
        PassivaRequisitosEvaluator.Pendencias(null, Personagem(), null).Should().BeEmpty();

    [Fact]
    public void Empty_requisitos_have_no_pendencias() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva(), Personagem(vocacao: null, classe: null), null).Should().BeEmpty();

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Nivel_is_a_minimum(int exigido, bool cumpre) =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Nivel = exigido }, Personagem(nivel: 5), null)
            .Should().BeEquivalentTo(cumpre ? Array.Empty<string>() : [$"Nível {exigido}"]);

    [Fact]
    public void Vocacao_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Vocacao = Vocacao.Campeao }, Personagem(), null)
            .Should().Equal("Vocação: Campeão");

    [Fact]
    public void Empty_vocacao_on_a_personagem_fails_a_vocacao_requisito() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Vocacao = Vocacao.Feiticeiro }, Personagem(vocacao: null), null)
            .Should().Equal("Vocação: Feiticeiro");

    [Fact]
    public void Classe_compares_trimmed_and_case_insensitive()
    {
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Classe = " elementalista " }, Personagem(), null).Should().BeEmpty();
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Classe = "Duelista" }, Personagem(), null).Should().Equal("Classe: Duelista");
    }

    [Fact]
    public void Linhagem_and_variante_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Linhagem = Linhagem.Econos, Variante = Variante.Alora }, Personagem(), null)
            .Should().Equal("Linhagem: Ecônos", "Variante: Alóra");

    [Fact]
    public void Graduacao_is_a_minimum() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Graduacao = 4 }, Personagem(graduacao: 3), null)
            .Should().Equal("Grau/Círculo 4");

    [Fact]
    public void Coracao_de_mana_true_is_required_false_is_not_a_requisito()
    {
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { CoracaoDeMana = true }, Personagem(coracao: false), null).Should().Equal("Coração de Mana");
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { CoracaoDeMana = false }, Personagem(coracao: true), null).Should().BeEmpty();
    }

    [Fact]
    public void Afinidade_and_estrela_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Afinidade = AfinidadeElemental.Agua, Estrela = Estrela.Sadir }, Personagem(), null)
            .Should().Equal("Afinidade: Água", "Estrela: Sadir");

    [Fact]
    public void Historico_must_match_and_uses_the_given_name()
    {
        var exigido = Guid.NewGuid();
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = exigido }, Personagem(historicoId: Guid.NewGuid()), "Nobre")
            .Should().Equal("Histórico: Nobre");
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = exigido }, Personagem(historicoId: exigido), "Nobre")
            .Should().BeEmpty();
    }

    [Fact]
    public void Historico_that_no_longer_exists_reads_removido() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = Guid.NewGuid() }, Personagem(), null)
            .Should().Equal("Histórico: (removido)");

    [Fact]
    public void Each_attribute_subattribute_and_pericia_in_the_lists_is_a_minimum_over_the_total()
    {
        var requisitos = new RequisitosDePassiva
        {
            Atributos = [new(Atributo.Forca, 4), new(Atributo.Agilidade, 3)],
            SubAtributos = [new(SubAtributo.Iniciativa, 5)],
            Pericias = [new(Pericia.Atletismo, 2), new(Pericia.ArmasBrancas, 7)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Personagem(), null)
            .Should().Equal("Força ≥ 4", "Iniciativa ≥ 5", "Armas Brancas ≥ 7");
    }

    [Fact]
    public void A_pericia_without_a_total_fails() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Pericias = [new(Pericia.Atletismo, 0)] },
                Personagem(pericias: new Dictionary<Pericia, int?> { [Pericia.Atletismo] = null }), null)
            .Should().Equal("Atletismo ≥ 0");

    [Fact]
    public void Criatura_ignores_requisitos_on_fields_it_does_not_have()
    {
        var requisitos = new RequisitosDePassiva
        {
            Vocacao = Vocacao.Bruxo, Classe = "X", Linhagem = Linhagem.Humano, Variante = Variante.Sinir, Graduacao = 9,
            CoracaoDeMana = true, Estrela = Estrela.Sadir, HistoricoId = Guid.NewGuid(),
            Atributos = [new(Atributo.Instinto, 99), new(Atributo.Vontade, 99), new(Atributo.Influencia, 99)],
            Pericias = [new(Pericia.Medicina, 99)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Criatura(), "Nobre").Should().BeEmpty();
    }

    [Fact]
    public void Criatura_still_checks_nivel_afinidade_shared_attributes_subattributes_and_its_pericias()
    {
        var requisitos = new RequisitosDePassiva
        {
            Nivel = 6, Afinidade = AfinidadeElemental.Gelo,
            Atributos = [new(Atributo.Forca, 4)], SubAtributos = [new(SubAtributo.DefesaNatural, 4)], Pericias = [new(Pericia.Atletismo, 4)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Criatura(), null)
            .Should().Equal("Nível 6", "Afinidade: Gelo", "Força ≥ 4", "Defesa Natural ≥ 4", "Atletismo ≥ 4");
    }

    [Theory]
    [InlineData(CategoriaDePassiva.Livre, "Passiva Livre")]
    [InlineData(CategoriaDePassiva.Vocacional, "Passiva Vocacional")]
    [InlineData(CategoriaDePassiva.DeClasse, "Passiva de Classe")]
    public void Categoria_labels(CategoriaDePassiva categoria, string label) =>
        RequisitoLabels.Categoria(categoria).Should().Be(label);
}
