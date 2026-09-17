using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class VocacaoEscolaMapTests
{
    [Fact]
    public void Feiticeiro_can_pick_Dobra_and_Transmutacao_but_not_the_other_schools()
    {
        VocacaoEscolaMap.PodeEscolherElemento(Vocacao.Feiticeiro, Elemento.Fogo).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Feiticeiro, SubElemento.Gelo).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Feiticeiro, SubElemento.Curar).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Feiticeiro, SubElemento.Necromancia).Should().BeFalse();
    }

    [Fact]
    public void Adepto_can_pick_Dobra_and_Consagracao_but_not_the_other_schools()
    {
        VocacaoEscolaMap.PodeEscolherElemento(Vocacao.Adepto, Elemento.Terra).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Adepto, SubElemento.Vida).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Adepto, SubElemento.Necromancia).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Adepto, SubElemento.Raio).Should().BeFalse();
    }

    [Fact]
    public void Bruxo_can_pick_Dobra_and_Maculacao_but_not_the_other_schools()
    {
        VocacaoEscolaMap.PodeEscolherElemento(Vocacao.Bruxo, Elemento.Ar).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Bruxo, SubElemento.Necromancia).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Bruxo, SubElemento.Purificar).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(Vocacao.Bruxo, SubElemento.Flora).Should().BeFalse();
    }

    [Theory]
    [InlineData(Vocacao.Campeao)]
    [InlineData(Vocacao.Cacador)]
    public void Non_magic_Vocacoes_can_pick_nothing(Vocacao vocacao)
    {
        VocacaoEscolaMap.PodeEscolherElemento(vocacao, Elemento.Fogo).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(vocacao, SubElemento.Curar).Should().BeFalse();
    }

    [Fact]
    public void A_null_Vocacao_can_pick_nothing()
    {
        VocacaoEscolaMap.PodeEscolherElemento(null, Elemento.Fogo).Should().BeFalse();
        VocacaoEscolaMap.PodeEscolherSubElemento(null, SubElemento.Curar).Should().BeFalse();
    }

    [Fact]
    public void PodeEscolherAfinidade_resolves_an_Elemento_shaped_value_correctly()
    {
        // AfinidadeElemental.Fogo compartilha o nome de membro com Elemento.Fogo.
        VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao.Feiticeiro, AfinidadeElemental.Fogo).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao.Campeao, AfinidadeElemental.Fogo).Should().BeFalse();
    }

    [Fact]
    public void PodeEscolherAfinidade_resolves_a_SubElemento_shaped_value_correctly()
    {
        // AfinidadeElemental.Necromancia compartilha o nome de membro com SubElemento.Necromancia.
        VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao.Bruxo, AfinidadeElemental.Necromancia).Should().BeTrue();
        VocacaoEscolaMap.PodeEscolherAfinidade(Vocacao.Adepto, AfinidadeElemental.Necromancia).Should().BeFalse();
    }
}
