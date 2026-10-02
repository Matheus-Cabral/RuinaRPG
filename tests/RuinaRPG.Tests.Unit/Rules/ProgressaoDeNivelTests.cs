using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using Xunit;

namespace RuinaRPG.Tests.Unit.Rules;

public class ProgressaoDeNivelTests
{
    private static readonly ColunaDeNivelDef Atributo = new(Guid.NewGuid(), "Pontos de Atributo", TipoDeColunaDeNivel.Acumulativa, ChavesDeNivel.PontosDeAtributo, 0);
    private static readonly ColunaDeNivelDef MaxPericia = new(Guid.NewGuid(), "Máx. de Perícia", TipoDeColunaDeNivel.PorNivel, ChavesDeNivel.MaxPericia, 1);
    private static readonly ColunaDeNivelDef Xp = new(Guid.NewGuid(), "XP para o próximo nível", TipoDeColunaDeNivel.PorNivel, ChavesDeNivel.XpParaProximoNivel, 2);
    private static readonly ColunaDeNivelDef Fama = new(Guid.NewGuid(), "Fama", TipoDeColunaDeNivel.Acumulativa, null, 3);

    private static ProgressaoDeNivel Tabela() => new(
        new[] { Fama, Xp, MaxPericia, Atributo },
        new[]
        {
            new LinhaDeNivel(3, "Terceira linha", new Dictionary<Guid, int?> { [Atributo.Id] = 2, [MaxPericia.Id] = null, [Xp.Id] = null }),
            new LinhaDeNivel(1, "Status de Vida Aprimorado\nStatus de Foco Aprimorado", new Dictionary<Guid, int?> { [Atributo.Id] = 9, [MaxPericia.Id] = 4, [Xp.Id] = 50, [Fama.Id] = 1 }),
            new LinhaDeNivel(2, null, new Dictionary<Guid, int?> { [Atributo.Id] = null, [Xp.Id] = 150 }),
        });

    [Fact]
    public void Orders_columns_and_rows_and_knows_the_last_level()
    {
        var t = Tabela();
        t.Colunas.Select(c => c.Nome).Should().Equal("Pontos de Atributo", "Máx. de Perícia", "XP para o próximo nível", "Fama");
        t.Linhas.Select(l => l.Nivel).Should().Equal(1, 2, 3);
        t.UltimoNivel.Should().Be(3);
    }

    [Fact]
    public void Acumulado_sums_levels_up_to_N_treating_empty_as_zero()
    {
        var t = Tabela();
        t.Acumulado(ChavesDeNivel.PontosDeAtributo, 1).Should().Be(9);
        t.Acumulado(ChavesDeNivel.PontosDeAtributo, 2).Should().Be(9);
        t.Acumulado(ChavesDeNivel.PontosDeAtributo, 3).Should().Be(11);
        t.Acumulado(ChavesDeNivel.PontosDeIgnicao, 3).Should().Be(0); // column absent
    }

    [Fact]
    public void Limite_inherits_the_nearest_lower_value_and_is_null_when_none()
    {
        var t = Tabela();
        t.Limite(ChavesDeNivel.MaxPericia, 3).Should().Be(4);
        t.Limite(ChavesDeNivel.MaxAtributo, 3).Should().BeNull();
    }

    [Fact]
    public void Xp_never_inherits_and_the_empty_last_level_is_Lvl_Max()
    {
        var t = Tabela();
        t.ValorExato(ChavesDeNivel.XpParaProximoNivel, 3).Should().BeNull();
        t.Resolver(Xp, 3).Should().BeNull();
        t.ComoXpPorNivel().Select(x => (x.Nivel, x.XpAbsoluto)).Should().Equal((1, "50"), (2, "150"), (3, "Lvl. Max"));
    }

    [Fact]
    public void Resolver_uses_the_column_type_for_custom_columns()
    {
        var t = Tabela();
        t.Resolver(Fama, 3).Should().Be(1);
        t.Resolver(MaxPericia, 2).Should().Be(4);
    }

    [Fact]
    public void LinhasDeBonus_lists_nonzero_acumulativas_then_the_free_text_lines()
    {
        var t = Tabela();
        t.LinhasDeBonus(1).Should().Equal("Pontos de Atributo: +9", "Fama: +1", "Status de Vida Aprimorado", "Status de Foco Aprimorado");
        t.LinhasDeBonus(2).Should().BeEmpty();
        t.LinhasDeBonus(3).Should().Equal("Pontos de Atributo: +2", "Terceira linha");
    }

    private static ProgressaoDeNivel TabelaAcumulada(params (int Nivel, string? Outros, int? Atributo, int? Fama)[] linhas) => new(
        new[] { Fama, Atributo },
        linhas.Select(l => new LinhaDeNivel(l.Nivel, l.Outros, new Dictionary<Guid, int?> { [Atributo.Id] = l.Atributo, [Fama.Id] = l.Fama })).ToList());

    [Fact]
    public void LinhasDeBonusAcumuladas_sums_the_same_column_across_levels_into_one_line()
    {
        var t = TabelaAcumulada((1, null, 9, null), (2, null, 9, null), (3, null, 1, null));
        t.LinhasDeBonusAcumuladas(1, 3).Should().Equal("Pontos de Atributo: +10");
    }

    [Fact]
    public void LinhasDeBonusAcumuladas_keeps_column_order_and_omits_zero_sums()
    {
        var t = TabelaAcumulada((1, null, 2, 1), (2, null, 3, -1), (3, null, null, null));
        t.LinhasDeBonusAcumuladas(0, 3).Should().Equal("Pontos de Atributo: +5");
        t.LinhasDeBonusAcumuladas(0, 1).Should().Equal("Pontos de Atributo: +2", "Fama: +1");
    }

    [Fact]
    public void LinhasDeBonusAcumuladas_shows_repeated_free_text_once_with_the_count_after_the_numeric_lines()
    {
        var t = TabelaAcumulada(
            (1, "Status de Vocação de Vida/Foco\nOutro", 1, null),
            (2, "Status de Vocação de Vida/Foco", null, null),
            (3, "  Status de Vocação de Vida/Foco \nUnico", 1, null));
        t.LinhasDeBonusAcumuladas(0, 3).Should().Equal(
            "Pontos de Atributo: +2", "Status de Vocação de Vida/Foco (×3)", "Outro", "Unico");
    }

    [Fact]
    public void LinhasDeBonusAcumuladas_keeps_distinct_texts_in_first_appearance_order_and_is_case_sensitive()
    {
        var t = TabelaAcumulada((1, "B", null, null), (2, "A\nB", null, null), (3, "a", null, null));
        t.LinhasDeBonusAcumuladas(0, 3).Should().Equal("B (×2)", "A", "a");
    }

    [Fact]
    public void LinhasDeBonusAcumuladas_for_a_single_level_equals_LinhasDeBonus_and_excludes_the_lower_bound()
    {
        var t = Tabela();
        t.LinhasDeBonusAcumuladas(0, 1).Should().Equal(t.LinhasDeBonus(1));
        t.LinhasDeBonusAcumuladas(2, 3).Should().Equal(t.LinhasDeBonus(3));
        t.LinhasDeBonusAcumuladas(3, 3).Should().BeEmpty();
    }

    private static ProgressaoDeNivel TabelaComUltimo(int? xpDoPenultimo, int? xpDoUltimo)
    {
        var xp = new ColunaDeNivelDef(Guid.NewGuid(), "XP", TipoDeColunaDeNivel.PorNivel, ChavesDeNivel.XpParaProximoNivel, 0);
        return new ProgressaoDeNivel(new[] { xp }, new[]
        {
            new LinhaDeNivel(1, null, new Dictionary<Guid, int?> { [xp.Id] = 50 }),
            new LinhaDeNivel(2, null, new Dictionary<Guid, int?> { [xp.Id] = xpDoPenultimo }),
            new LinhaDeNivel(3, null, new Dictionary<Guid, int?> { [xp.Id] = xpDoUltimo }),
        });
    }

    [Fact]
    public void Xp_filled_on_the_last_level_is_ignored_and_the_level_never_exceeds_the_last()
    {
        var t = TabelaComUltimo(150, 999);
        t.ComoXpPorNivel().Last().XpAbsoluto.Should().Be("Lvl. Max");
        NivelCalculator.Compute(1_000_000, t.ComoXpPorNivel()).Should().Be(3);
    }

    [Fact]
    public void Adding_a_level_only_unlocks_it_once_the_previous_levels_xp_is_filled()
    {
        NivelCalculator.Compute(1_000_000, TabelaComUltimo(null, null).ComoXpPorNivel()).Should().Be(2);
        NivelCalculator.Compute(1_000_000, TabelaComUltimo(150, null).ComoXpPorNivel()).Should().Be(3);
    }

    private static ProgressaoDeNivel Passivas(params (int Nivel, int Valor)[] celulas) =>
        TabelaDeNiveisDeTeste.Criar(Enumerable.Range(1, 50)
            .Select(n => TabelaDeNiveisDeTeste.Nivel(n, celulas.Where(c => c.Nivel == n).Select(c => (ChavesDeNivel.MaxPassivasLivres, c.Valor)).ToArray())).ToArray());

    [Fact]
    public void LimiteAcumulado_is_null_while_the_whole_column_is_empty()
    {
        var t = Passivas();
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 1).Should().BeNull();
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 50).Should().BeNull();
    }

    [Fact]
    public void LimiteAcumulado_sums_the_column_up_to_the_level_once_any_level_has_a_value()
    {
        var t = Passivas((10, 1), (30, 1), (50, 1));
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 9).Should().Be(0);
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 10).Should().Be(1);
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 29).Should().Be(1);
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 30).Should().Be(2);
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 49).Should().Be(2);
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 50).Should().Be(3);
    }

    [Fact]
    public void LimiteAcumulado_treats_a_column_of_only_zeros_as_a_limit_of_zero_and_ignores_empty_cells()
    {
        var t = Passivas((2, 0), (5, 2));
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 1).Should().Be(0);
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 4).Should().Be(0);
        t.LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 5).Should().Be(2);
    }

    [Fact]
    public void LimiteAcumulado_is_null_when_the_column_does_not_exist()
    {
        Tabela().LimiteAcumulado(ChavesDeNivel.MaxPassivasLivres, 3).Should().BeNull();
    }

    [Fact]
    public void The_three_passiva_system_columns_are_additive_and_named_Passivas()
    {
        var porChave = ChavesDeNivel.Sistema.ToDictionary(d => d.Chave);
        porChave[ChavesDeNivel.MaxPassivasLivres].Should().Match<ChavesDeNivel.Definicao>(d => d.Tipo == TipoDeColunaDeNivel.Acumulativa && d.Nome == "Passivas Livres");
        porChave[ChavesDeNivel.MaxPassivasVocacionais].Should().Match<ChavesDeNivel.Definicao>(d => d.Tipo == TipoDeColunaDeNivel.Acumulativa && d.Nome == "Passivas Vocacionais");
        porChave[ChavesDeNivel.MaxPassivasDeClasse].Should().Match<ChavesDeNivel.Definicao>(d => d.Tipo == TipoDeColunaDeNivel.Acumulativa && d.Nome == "Passivas De Classe");
        porChave[ChavesDeNivel.MaxAtributo].Tipo.Should().Be(TipoDeColunaDeNivel.PorNivel);
        porChave[ChavesDeNivel.MaxPericia].Tipo.Should().Be(TipoDeColunaDeNivel.PorNivel);
    }
}
