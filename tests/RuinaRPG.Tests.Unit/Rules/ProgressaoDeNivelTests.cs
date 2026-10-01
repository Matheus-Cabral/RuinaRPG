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
        t.ComoLevelBonus().Single(b => b.Nivel == 3).BonusText.Should().Be("Pontos de Atributo: +2<br>Terceira linha");
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
