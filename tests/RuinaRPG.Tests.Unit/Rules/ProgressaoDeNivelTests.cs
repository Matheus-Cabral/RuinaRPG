using FluentAssertions;
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
        t.LinhasDeBonus(1).Should().Equal("+9 Pontos de Atributo", "+1 Fama", "Status de Vida Aprimorado", "Status de Foco Aprimorado");
        t.LinhasDeBonus(2).Should().BeEmpty();
        t.ComoLevelBonus().Single(b => b.Nivel == 3).BonusText.Should().Be("+2 Pontos de Atributo<br>Terceira linha");
    }
}
