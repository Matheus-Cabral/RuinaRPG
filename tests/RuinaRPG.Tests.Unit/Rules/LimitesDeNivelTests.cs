using FluentAssertions;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.Rules;

public class LimitesDeNivelTests
{
    [Theory]
    [InlineData(2, 3, null, true)]
    [InlineData(2, 3, 3, true)]
    [InlineData(2, 4, 3, false)]
    [InlineData(5, 4, 3, true)]   // lowering an over-cap value is allowed
    [InlineData(5, 5, 3, true)]   // unchanged over-cap value is allowed
    [InlineData(5, 6, 3, false)]
    public void Gasto(int atual, int novo, int? limite, bool permitido) =>
        (LimitesDeNivel.Gasto("Força", atual, novo, limite, 7) is null).Should().Be(permitido);

    [Fact]
    public void Gasto_message_names_the_target_cap_and_level() =>
        LimitesDeNivel.Gasto("Atletismo", 0, 9, 8, 5).Should().Be("Atletismo não pode passar de 8 pontos no nível 5.");

    private static readonly CategoriaDePassiva Livre = CategoriaDePassiva.Livre, Vocacional = CategoriaDePassiva.Vocacional, DeClasse = CategoriaDePassiva.DeClasse;

    /// <summary>Tabela de um nível só; limite nulo = coluna inteiramente vazia (sem limite).</summary>
    private static ProgressaoDeNivel Tabela(int? livres = null, int? vocacionais = null, int? deClasse = null, int? coringas = null)
    {
        var valores = new List<(string, int)>();
        if (livres is { } l) valores.Add((ChavesDeNivel.MaxPassivasLivres, l));
        if (vocacionais is { } v) valores.Add((ChavesDeNivel.MaxPassivasVocacionais, v));
        if (deClasse is { } c) valores.Add((ChavesDeNivel.MaxPassivasDeClasse, c));
        if (coringas is { } k) valores.Add((ChavesDeNivel.MaxPassivasCoringa, k));
        return TabelaDeNiveisDeTeste.Criar(TabelaDeNiveisDeTeste.Nivel(1, valores.ToArray()));
    }

    private static bool Permite(CategoriaDePassiva categoria, CategoriaDePassiva[] naFicha, ProgressaoDeNivel tabela) =>
        LimitesDeNivel.Passivas(categoria, naFicha, tabela, 1) is null;

    [Fact]
    public void Passivas_without_wildcards_keep_the_per_category_limit()
    {
        Permite(Livre, [], Tabela(livres: 1)).Should().BeTrue();
        Permite(Livre, [Livre], Tabela(livres: 1)).Should().BeFalse();
        Permite(Livre, [], Tabela(livres: 0)).Should().BeFalse();
        Permite(Vocacional, [Livre], Tabela(livres: 1, vocacionais: 1)).Should().BeTrue();
    }

    [Fact]
    public void Passivas_of_a_category_with_an_entirely_empty_column_are_unlimited_and_never_use_a_wildcard()
    {
        Permite(Livre, [Livre, Livre, Livre], Tabela()).Should().BeTrue();
        // Vocacional já gastou a única coringa; Livre (coluna vazia) segue liberada.
        Permite(Livre, [Vocacional, Livre, Livre], Tabela(vocacionais: 0, coringas: 1)).Should().BeTrue();
        // Passivas de categoria sem limite não contam como excedente.
        Permite(Vocacional, [Livre, Livre, Livre], Tabela(vocacionais: 0, coringas: 1)).Should().BeTrue();
    }

    [Fact]
    public void A_wildcard_slot_accepts_a_passiva_of_any_category_once_its_own_limit_is_full()
    {
        var t = Tabela(livres: 1, vocacionais: 0, deClasse: 0, coringas: 1);
        Permite(Livre, [Livre], t).Should().BeTrue();
        Permite(Vocacional, [Livre], t).Should().BeTrue();
        Permite(DeClasse, [Livre], t).Should().BeTrue();
    }

    [Fact]
    public void Wildcards_are_shared_across_categories_and_run_out()
    {
        var t = Tabela(livres: 1, vocacionais: 0, deClasse: 0, coringas: 1);
        Permite(Livre, [Livre, Vocacional], t).Should().BeFalse();    // a coringa já foi para a Vocacional
        Permite(DeClasse, [Livre, Livre], t).Should().BeFalse();      // a coringa já foi para a segunda Livre
        Permite(DeClasse, [Livre, Livre], Tabela(livres: 1, deClasse: 0, coringas: 2)).Should().BeTrue();
    }

    [Fact]
    public void A_sheet_already_above_its_limits_can_still_add_where_the_category_has_room()
    {
        // Três Livres com limite 1 e nenhuma coringa (a tabela foi editada depois): Vocacional ainda cabe.
        var t = Tabela(livres: 1, vocacionais: 1, coringas: 0);
        Permite(Vocacional, [Livre, Livre, Livre], t).Should().BeTrue();
        Permite(Livre, [Livre, Livre, Livre], t).Should().BeFalse();
    }

    [Theory]
    [InlineData(CategoriaDePassiva.Livre, "Livre(s)")]
    [InlineData(CategoriaDePassiva.Vocacional, "Vocacional(is)")]
    [InlineData(CategoriaDePassiva.DeClasse, "De Classe")]
    public void Passivas_message_without_wildcards_is_the_category_limit(CategoriaDePassiva categoria, string rotulo)
    {
        var t = Tabela(livres: 2, vocacionais: 2, deClasse: 2);
        LimitesDeNivel.Passivas(categoria, [categoria, categoria], t, 1).Should().Be($"O nível 1 permite no máximo 2 Passiva(s) {rotulo}.");
    }

    [Fact]
    public void Passivas_message_with_wildcards_says_they_are_used_up() =>
        LimitesDeNivel.Passivas(Livre, [Livre, Livre], Tabela(livres: 1, coringas: 1), 1)
            .Should().Be("O nível 1 permite no máximo 1 Passiva(s) Livre(s), e as 1 vaga(s) de Habilidade Passiva já estão em uso.");
}
