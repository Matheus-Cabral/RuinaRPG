using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class PassivaRequisitosEvaluatorTests
{
    // O avaliador recebe o nome da Perícia de fora (a tabela Pericias vive na Infrastructure) — aqui, os nomes do seed.
    private static string NomeDaPericia(int id) => PericiasIniciais.Todas.Single(p => p.Id == id).Nome;

    private static FichaParaRequisitos Personagem(
        int nivel = 5, Vocacao? vocacao = Vocacao.Feiticeiro, string? classe = "Elementalista",
        Linhagem? linhagem = Linhagem.Humano, Variante? variante = Variante.Sinir, int graduacao = 3,
        bool coracao = true, AfinidadeElemental? afinidade = AfinidadeElemental.Fogo, Estrela? estrela = Estrela.Liora,
        Guid? historicoId = null,
        Dictionary<Atributo, int>? atributos = null, Dictionary<SubAtributo, int>? subAtributos = null,
        Dictionary<int, int?>? pericias = null) =>
        new(true, nivel, vocacao, classe, linhagem, variante, graduacao, coracao, afinidade, estrela, historicoId,
            atributos ?? Enum.GetValues<Atributo>().ToDictionary(a => a, _ => 3),
            subAtributos ?? Enum.GetValues<SubAtributo>().ToDictionary(s => s, _ => 3),
            pericias ?? PericiasIniciais.Todas.ToDictionary(p => p.Id, _ => (int?)3));

    // Criatura: sem identidade de personagem; só Força/Vigor/Agilidade/Destreza/Astúcia; só algumas Perícias.
    private static FichaParaRequisitos Criatura(int nivel = 5, AfinidadeElemental? afinidade = AfinidadeElemental.Fogo) =>
        new(false, nivel, null, null, null, null, 0, false, afinidade, null, null,
            new Dictionary<Atributo, int> { [Atributo.Forca] = 3, [Atributo.Vigor] = 3, [Atributo.Agilidade] = 3, [Atributo.Destreza] = 3, [Atributo.Astucia] = 3 },
            Enum.GetValues<SubAtributo>().ToDictionary(s => s, _ => 3),
            new Dictionary<int, int?> { [7 /* Atletismo */] = 3 });

    [Fact]
    public void Null_requisitos_have_no_pendencias() =>
        PassivaRequisitosEvaluator.Pendencias(null, Personagem(), null, NomeDaPericia).Should().BeEmpty();

    [Fact]
    public void Empty_requisitos_have_no_pendencias() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva(), Personagem(vocacao: null, classe: null), null, NomeDaPericia).Should().BeEmpty();

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Nivel_is_a_minimum(int exigido, bool cumpre) =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Nivel = exigido }, Personagem(nivel: 5), null, NomeDaPericia)
            .Should().BeEquivalentTo(cumpre ? Array.Empty<string>() : [$"Nível {exigido}"]);

    [Fact]
    public void Vocacao_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Vocacao = Vocacao.Campeao }, Personagem(), null, NomeDaPericia)
            .Should().Equal("Vocação: Campeão");

    [Fact]
    public void Empty_vocacao_on_a_personagem_fails_a_vocacao_requisito() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Vocacao = Vocacao.Feiticeiro }, Personagem(vocacao: null), null, NomeDaPericia)
            .Should().Equal("Vocação: Feiticeiro");

    [Fact]
    public void Classe_compares_trimmed_and_case_insensitive()
    {
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Classe = " elementalista " }, Personagem(), null, NomeDaPericia).Should().BeEmpty();
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Classe = "Duelista" }, Personagem(), null, NomeDaPericia).Should().Equal("Classe: Duelista");
    }

    [Fact]
    public void Linhagem_and_variante_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Linhagem = Linhagem.Econos, Variante = Variante.Alora }, Personagem(), null, NomeDaPericia)
            .Should().Equal("Linhagem: Ecônos", "Variante: Alóra");

    [Fact]
    public void Graduacao_is_a_minimum() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Graduacao = 4 }, Personagem(graduacao: 3), null, NomeDaPericia)
            .Should().Equal("Grau/Círculo 4");

    [Fact]
    public void Coracao_de_mana_true_is_required_false_is_not_a_requisito()
    {
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { CoracaoDeMana = true }, Personagem(coracao: false), null, NomeDaPericia).Should().Equal("Coração de Mana");
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { CoracaoDeMana = false }, Personagem(coracao: true), null, NomeDaPericia).Should().BeEmpty();
    }

    [Fact]
    public void Afinidade_and_estrela_must_match() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Afinidade = AfinidadeElemental.Agua, Estrela = Estrela.Sadir }, Personagem(), null, NomeDaPericia)
            .Should().Equal("Afinidade: Água", "Estrela: Sadir");

    [Fact]
    public void Historico_must_match_and_uses_the_given_name()
    {
        var exigido = Guid.NewGuid();
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = exigido }, Personagem(historicoId: Guid.NewGuid()), "Nobre", NomeDaPericia)
            .Should().Equal("Histórico: Nobre");
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = exigido }, Personagem(historicoId: exigido), "Nobre", NomeDaPericia)
            .Should().BeEmpty();
    }

    [Fact]
    public void Historico_that_no_longer_exists_reads_removido() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { HistoricoId = Guid.NewGuid() }, Personagem(), null, NomeDaPericia)
            .Should().Equal("Histórico: (removido)");

    [Fact]
    public void Each_attribute_subattribute_and_pericia_in_the_lists_is_a_minimum_over_the_total()
    {
        var requisitos = new RequisitosDePassiva
        {
            Atributos = [new(Atributo.Forca, 4), new(Atributo.Agilidade, 3)],
            SubAtributos = [new(SubAtributo.Iniciativa, 5)],
            Pericias = [new(7 /* Atletismo */, 2), new(4 /* ArmasBrancas */, 7)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Personagem(), null, NomeDaPericia)
            .Should().Equal("Força ≥ 4", "Iniciativa ≥ 5", "Armas Brancas ≥ 7");
    }

    [Fact]
    public void A_pericia_without_a_total_fails() =>
        PassivaRequisitosEvaluator.Pendencias(new RequisitosDePassiva { Pericias = [new(7 /* Atletismo */, 0)] },
                Personagem(pericias: new Dictionary<int, int?> { [7 /* Atletismo */] = null }), null, NomeDaPericia)
            .Should().Equal("Atletismo ≥ 0");

    [Fact]
    public void Criatura_ignores_requisitos_on_fields_it_does_not_have()
    {
        var requisitos = new RequisitosDePassiva
        {
            Vocacao = Vocacao.Bruxo, Classe = "X", Linhagem = Linhagem.Humano, Variante = Variante.Sinir, Graduacao = 9,
            CoracaoDeMana = true, Estrela = Estrela.Sadir, HistoricoId = Guid.NewGuid(),
            Atributos = [new(Atributo.Instinto, 99), new(Atributo.Vontade, 99), new(Atributo.Influencia, 99)],
            Pericias = [new(26 /* Medicina */, 99)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Criatura(), "Nobre", NomeDaPericia).Should().BeEmpty();
    }

    [Fact]
    public void Criatura_still_checks_nivel_afinidade_shared_attributes_subattributes_and_its_pericias()
    {
        var requisitos = new RequisitosDePassiva
        {
            Nivel = 6, Afinidade = AfinidadeElemental.Gelo,
            Atributos = [new(Atributo.Forca, 4)], SubAtributos = [new(SubAtributo.DefesaNatural, 4)], Pericias = [new(7 /* Atletismo */, 4)],
        };

        PassivaRequisitosEvaluator.Pendencias(requisitos, Criatura(), null, NomeDaPericia)
            .Should().Equal("Nível 6", "Afinidade: Gelo", "Força ≥ 4", "Defesa Natural ≥ 4", "Atletismo ≥ 4");
    }

    [Theory]
    [InlineData(CategoriaDePassiva.Livre, "Passiva Livre")]
    [InlineData(CategoriaDePassiva.Vocacional, "Passiva Vocacional")]
    [InlineData(CategoriaDePassiva.DeClasse, "Passiva de Classe")]
    public void Categoria_labels(CategoriaDePassiva categoria, string label) =>
        RequisitoLabels.Categoria(categoria).Should().Be(label);

    [Fact]
    public void Descrever_is_empty_without_requisitos()
    {
        PassivaRequisitosEvaluator.Descrever(null, null, _ => "x").Should().BeEmpty();
        PassivaRequisitosEvaluator.Descrever(new RequisitosDePassiva(), null, _ => "x").Should().BeEmpty();
    }

    [Fact]
    public void Descrever_lists_every_filled_requisito_in_field_order_with_the_sheet_labels()
    {
        var requisitos = new RequisitosDePassiva
        {
            Nivel = 10, Vocacao = Vocacao.Campeao, Classe = " Paladino ", Linhagem = Linhagem.Econos, Graduacao = 3, CoracaoDeMana = true,
            Afinidade = AfinidadeElemental.Agua, HistoricoId = Guid.NewGuid(),
            Atributos = [new RequisitoDeAtributo(Atributo.Forca, 4)],
            SubAtributos = [new RequisitoDeSubAtributo(SubAtributo.Movimentacao, 6)],
            Pericias = [new RequisitoDePericia(7, 2)],
        };

        PassivaRequisitosEvaluator.Descrever(requisitos, "Nobre", id => id == 7 ? "Atletismo" : null).Should().Equal(
            "Nível 10", "Vocação: Campeão", "Classe: Paladino", "Linhagem: Ecônos", "Grau/Círculo 3", "Coração de Mana",
            "Afinidade: Água", "Histórico: Nobre", "Força ≥ 4", "Movimentação ≥ 6", "Atletismo ≥ 2");
    }

    [Fact]
    public void Descrever_omits_a_removed_pericia_and_flags_a_removed_historico()
    {
        var requisitos = new RequisitosDePassiva { HistoricoId = Guid.NewGuid(), CoracaoDeMana = false, Pericias = [new RequisitoDePericia(7, 2)] };

        PassivaRequisitosEvaluator.Descrever(requisitos, null, _ => null).Should().Equal("Histórico: (removido)");
    }
}
