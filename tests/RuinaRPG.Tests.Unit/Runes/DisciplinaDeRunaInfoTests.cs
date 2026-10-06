using FluentAssertions;
using RuinaRPG.Domain.Runes;

namespace RuinaRPG.Tests.Unit.Runes;

public class DisciplinaDeRunaInfoTests
{
    [Theory]
    [InlineData(DisciplinaDeRuna.Adicao, "Adição", "Influencia o corpo do usuário")]
    [InlineData(DisciplinaDeRuna.Alteracao, "Alteração", "Influencia objetos inanimados")]
    [InlineData(DisciplinaDeRuna.Emissao, "Emissão", "Influencia alvos inanimados")]
    [InlineData(DisciplinaDeRuna.Manifestacao, "Manifestação", "Manifesta a aura do usuário")]
    public void Each_disciplina_has_its_label_and_description(DisciplinaDeRuna disciplina, string rotulo, string descricao)
    {
        DisciplinaDeRunaInfo.Rotulo(disciplina).Should().Be(rotulo);
        DisciplinaDeRunaInfo.Descricao(disciplina).Should().Be(descricao);
    }

    [Fact]
    public void Todas_lists_the_four_in_declaration_order() =>
        DisciplinaDeRunaInfo.Todas.Should().Equal(DisciplinaDeRuna.Adicao, DisciplinaDeRuna.Alteracao, DisciplinaDeRuna.Emissao, DisciplinaDeRuna.Manifestacao);
}
