using FluentAssertions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class EfeitoPrerequisiteResolverTests
{
    private static EfeitoRegra Regra(string nome, int grau, params string[][] preRequisitos) => new(
        nome, grau, TipoDeCusto.Fixo, CustoFixo: 1, CustoPorUnidade: null, QuantidadeDerivadaDeEfeito: null,
        MaxUnidades: null, MaxEscalaPorGrau: false, MaxContandoAPartirDoGrau: null,
        CustoAlternativo: null, CustoAlternativoAPartirDoGrau: null, PreRequisitos: preRequisitos);

    private static readonly IReadOnlyDictionary<string, string> SemEscolhas = new Dictionary<string, string>();

    [Fact]
    public void A_group_with_a_single_missing_candidate_resolves_by_itself()
    {
        var duracao = Regra("Duração", 1);
        var alvo = Regra("Cura", 1, ["Dano"]);
        var dano = Regra("Dano", 1);
        var catalogo = new[] { duracao, alvo, dano };

        var resultado = EfeitoPrerequisiteResolver.Resolver(alvo, catalogo, new HashSet<string>(), grauDaMagia: 1, SemEscolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { dano });
    }

    [Fact]
    public void Nothing_to_resolve_when_every_prerequisite_is_already_present()
    {
        var alvo = Regra("Cura", 1, ["Dano"]);
        var dano = Regra("Dano", 1);
        var catalogo = new[] { alvo, dano };

        var resultado = EfeitoPrerequisiteResolver.Resolver(alvo, catalogo, new HashSet<string> { "Dano" }, grauDaMagia: 1, SemEscolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        resultado.ParaAutoAdicionar.Should().BeEmpty();
    }

    [Fact]
    public void A_group_with_more_than_one_Grau_eligible_candidate_is_reported_as_ambiguous()
    {
        var duracao = Regra("Duração", 1);
        var congelar = Regra("Congelar", 3, ["Duração"]);
        var enraizar = Regra("Enraizar", 2, ["Duração"]);
        var atordoamento = Regra("Atordoamento", 8, ["Duração"]);
        var detrito = Regra("Detrito", 4, ["Duração"], ["Atordoamento", "Congelar", "Enraizar"]);
        var catalogo = new[] { duracao, congelar, enraizar, atordoamento, detrito };

        var resultado = EfeitoPrerequisiteResolver.Resolver(detrito, catalogo, new HashSet<string>(), grauDaMagia: 4, SemEscolhas);

        // Atordoamento (Grau 8) não é elegível numa Magia de Grau 4 — só Congelar/Enraizar entram na escolha.
        resultado.GrupoAmbiguo.Should().BeEquivalentTo(new[] { "Congelar", "Enraizar" }, o => o.WithStrictOrdering());
        // O grupo [Duração] (único candidato) já foi resolvido antes do grupo ambíguo ser encontrado.
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { duracao });
    }

    [Fact]
    public void A_group_with_only_one_Grau_eligible_candidate_resolves_without_asking_even_if_the_group_has_more_members()
    {
        var duracao = Regra("Duração", 1);
        var congelar = Regra("Congelar", 3, ["Duração"]);
        var atordoamento = Regra("Atordoamento", 8, ["Duração"]);
        var alvo = Regra("Alvo", 3, ["Atordoamento", "Congelar"]);
        var catalogo = new[] { duracao, congelar, atordoamento, alvo };

        // Grau da Magia = 3: Atordoamento (Grau 8) não é elegível, só Congelar sobra — resolve sozinho.
        var resultado = EfeitoPrerequisiteResolver.Resolver(alvo, catalogo, new HashSet<string>(), grauDaMagia: 3, SemEscolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { duracao, congelar });
    }

    [Fact]
    public void An_answered_ambiguous_group_resolves_using_the_forced_choice_and_recurses_into_it()
    {
        var duracao = Regra("Duração", 1);
        var congelar = Regra("Congelar", 3, ["Duração"]);
        var enraizar = Regra("Enraizar", 2, ["Duração"]);
        var detrito = Regra("Detrito", 4, ["Congelar", "Enraizar"]);
        var catalogo = new[] { duracao, congelar, enraizar, detrito };
        var escolhas = new Dictionary<string, string> { ["Congelar|Enraizar"] = "Congelar" };

        var resultado = EfeitoPrerequisiteResolver.Resolver(detrito, catalogo, new HashSet<string>(), grauDaMagia: 4, escolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        // Congelar (a escolha forçada) exige Duração — resolvida recursivamente antes de Congelar.
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { duracao, congelar }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Recursion_orders_a_chosen_candidates_own_missing_prerequisite_before_the_candidate()
    {
        var duracao = Regra("Duração", 1);
        var enraizar = Regra("Enraizar", 2, ["Duração"]);
        var alvo = Regra("Alvo", 3, ["Enraizar"]);
        var catalogo = new[] { duracao, enraizar, alvo };

        var resultado = EfeitoPrerequisiteResolver.Resolver(alvo, catalogo, new HashSet<string>(), grauDaMagia: 3, SemEscolhas);

        resultado.GrupoAmbiguo.Should().BeNull();
        resultado.ParaAutoAdicionar.Should().BeEquivalentTo(new[] { duracao, enraizar }, o => o.WithStrictOrdering());
    }
}
