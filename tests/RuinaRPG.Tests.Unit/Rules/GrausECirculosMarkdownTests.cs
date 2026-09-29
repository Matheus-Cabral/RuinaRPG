using FluentAssertions;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class GrausECirculosMarkdownTests
{
    private const string Md =
        "Intro.\n\n# 1º GRAU / CÍRCULO I\n\n## Efeitos Básicos\n\nDano...\n\n## Cura\n\nTexto da Cura.\n\n" +
        "# 2º GRAU / CÍRCULO II\n\n## Libra\n\nTexto da Libra.\n\n## Aceleração\n\nTexto.\n";

    private const string Bloco = "## Chama Viva\n\n**Gasto:** 3 PI.\n\nEnvolve.\n";

    [Theory]
    [InlineData("Cura", true)]
    [InlineData("cura", true)]
    [InlineData("CURA", true)]
    [InlineData("Cúra", true)]
    [InlineData("Cura (Maior)", false)]    // only "Libra (…)" shares a heading
    [InlineData("Dano (Fogo)", false)]     // only the exact basic names share Efeitos Básicos
    [InlineData("Cur", false)]
    [InlineData("Aceleracao", true)]       // accent-insensitive
    [InlineData("Libra (Arcana)", true)]   // shares ## Libra
    [InlineData("Dano", true)]             // shares ## Efeitos Básicos
    [InlineData("Chama Viva", false)]
    public void Contem(string nome, bool esperado) => GrausECirculosMarkdown.Contem(Md, nome).Should().Be(esperado);

    [Fact]
    public void Inserir_appends_at_the_end_of_the_grau_section()
    {
        var md = GrausECirculosMarkdown.Inserir(Md, 1, Bloco);
        md.Should().Contain("Texto da Cura.\n\n## Chama Viva\n\n**Gasto:** 3 PI.\n\nEnvolve.\n\n# 2º GRAU / CÍRCULO II");
    }

    [Fact]
    public void Inserir_in_the_last_section_appends_at_the_end() =>
        GrausECirculosMarkdown.Inserir(Md, 2, Bloco).Should().EndWith("Texto.\n\n" + Bloco);

    [Fact]
    public void Inserir_creates_a_missing_grau_section() =>
        GrausECirculosMarkdown.Inserir(Md, 4, Bloco).Should().EndWith("\n\n# 4º GRAU / CÍRCULO IV\n\n" + Bloco);

    [Fact]
    public void Inserir_does_nothing_when_already_present() =>
        GrausECirculosMarkdown.Inserir(Md, 1, "## Cura\n\nOutro.\n").Should().Be(Md);

    [Fact]
    public void Substituir_same_grau_replaces_only_that_block()
    {
        var md = GrausECirculosMarkdown.Substituir(Md, "Cura", 1, "## Cura\n\nNovo texto.\n");
        md.Should().Contain("## Cura\n\nNovo texto.\n\n# 2º GRAU");
        md.Should().NotContain("Texto da Cura.");
        md.Should().Contain("## Efeitos Básicos\n\nDano...");
    }

    [Fact]
    public void Substituir_moves_to_another_grau_and_renames()
    {
        var md = GrausECirculosMarkdown.Substituir(Md, "Cura", 2, "## Cura Maior\n\nNovo.\n");
        md.Should().NotContain("## Cura\n");
        md.Should().EndWith("Texto.\n\n## Cura Maior\n\nNovo.\n");
    }

    [Fact]
    public void Substituir_when_missing_inserts() =>
        GrausECirculosMarkdown.Substituir(Md, "Chama Viva", 1, Bloco).Should().Contain(Bloco);

    [Fact]
    public void Remover_removes_only_the_block()
    {
        var md = GrausECirculosMarkdown.Remover(Md, "Cura");
        md.Should().NotContain("## Cura");
        md.Should().Contain("Dano...\n\n# 2º GRAU");
    }

    [Theory]
    [InlineData("Dano")]
    [InlineData("Libra (Vitalidade)")]
    public void Shared_blocks_are_never_replaced_or_removed(string nome)
    {
        GrausECirculosMarkdown.EhBlocoCompartilhado(nome).Should().BeTrue();
        GrausECirculosMarkdown.Remover(Md, nome).Should().Be(Md);
        GrausECirculosMarkdown.Substituir(Md, nome, 1, "## Dano\n\nY.\n").Should().Be(Md);
    }

    [Theory]
    [InlineData("Cura (Maior)")]
    [InlineData("Dano (Fogo)")]
    [InlineData("Alcance Longo")]
    public void Names_that_only_resemble_a_shared_block_are_not_shared(string nome) =>
        GrausECirculosMarkdown.EhBlocoCompartilhado(nome).Should().BeFalse();

    [Theory]
    [InlineData("Libra (Qualquer)")]
    [InlineData("libra (arcana)")]
    [InlineData("Duração")]
    [InlineData("duracao")]
    public void Libra_variants_and_exact_basics_are_shared(string nome) =>
        GrausECirculosMarkdown.EhBlocoCompartilhado(nome).Should().BeTrue();

    [Fact]
    public void Parenthetical_name_does_not_touch_the_base_block()
    {
        GrausECirculosMarkdown.Remover(Md, "Cura (Maior)").Should().Be(Md);

        var substituido = GrausECirculosMarkdown.Substituir(Md, "Cura (Maior)", 1, "## Cura (Maior)\n\nNovo.\n");
        substituido.Should().Contain("## Cura\n\nTexto da Cura.");
        substituido.Should().Contain("## Cura (Maior)\n\nNovo.\n");

        var inserido = GrausECirculosMarkdown.Inserir(Md, 1, "## Cura (Maior)\n\nNovo.\n");
        inserido.Should().Contain("## Cura\n\nTexto da Cura.\n\n## Cura (Maior)\n\nNovo.\n\n# 2º GRAU");
    }

    [Fact]
    public void Case_and_accent_variants_still_locate_the_block() =>
        GrausECirculosMarkdown.Remover(Md, "CÚRA").Should().NotContain("## Cura");

    [Fact]
    public void Renaming_a_shared_name_inserts_the_new_block()
    {
        var md = GrausECirculosMarkdown.Substituir(Md, "Libra (Arcana)", 2, "## Mana Libra\n\nNovo.\n");
        md.Should().Contain("## Libra\n\nTexto da Libra.");
        md.Should().EndWith("Texto.\n\n## Mana Libra\n\nNovo.\n");
    }

    [Fact]
    public void Renaming_a_shared_name_to_another_shared_name_changes_nothing() =>
        GrausECirculosMarkdown.Substituir(Md, "Libra (Arcana)", 1, "## Libra (Foco)\n\nNovo.\n").Should().Be(Md);

    [Theory]
    [InlineData("cura", "Cura", true)]
    [InlineData(" Cúra ", "CURA", true)]
    [InlineData("Cura (Maior)", "Cura", false)]
    public void Nomes_equivalentes(string a, string b, bool esperado) =>
        GrausECirculosMarkdown.NomesEquivalentes(a, b).Should().Be(esperado);

    [Fact]
    public void Escaped_hash_heading_matches_the_raw_name()
    {
        var md = GrausECirculosMarkdown.Inserir(Md, 1, "## \\# Chama\n\nTexto.\n");
        GrausECirculosMarkdown.Contem(md, "# Chama").Should().BeTrue();
        GrausECirculosMarkdown.Remover(md, "# Chama").Should().Be(Md);
    }
}
