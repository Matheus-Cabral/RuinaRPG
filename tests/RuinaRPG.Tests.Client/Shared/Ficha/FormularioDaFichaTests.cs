using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Contracts.Images;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// O formulário auto-salvo das três fichas (E, Task 10): um tipo só, que a página cria, carrega da ficha e grava, e que as
/// abas recebem. Aqui ficam as regras dele: as mensagens de validação de cada tipo, os "Aplicar" de XP e de PI do
/// Personagem e a troca de imagem.
/// </summary>
public class FormularioDaFichaTests
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    private static TipoDeFichaCliente Tipo(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private static List<string> Mensagens(FormularioDaFicha form, string propriedade)
    {
        var resultados = new List<ValidationResult>();
        var valor = typeof(FormularioDaFicha).GetProperty(propriedade)!.GetValue(form);
        Validator.TryValidateProperty(valor, new ValidationContext(form) { MemberName = propriedade }, resultados);
        return resultados.Select(r => r.ErrorMessage!).ToList();
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public void The_form_knows_its_kind(string tipo) =>
        new FormularioDaFicha(Tipo(tipo)).Tipo.Should().BeSameAs(Tipo(tipo));

    // A mensagem do PI usa o rótulo do campo no tipo ("Pontos de Ignição Atual" ou "Pontos de Ignição").
    [Theory]
    [InlineData("Personagem", "Informe os Pontos de Ignição Atual.")]
    [InlineData("NPC", "Informe os Pontos de Ignição Atual.")]
    [InlineData("Criatura", "Informe os Pontos de Ignição.")]
    public void A_blank_pontos_de_ignicao_says_the_label_of_its_kind(string tipo, string mensagem)
    {
        var form = new FormularioDaFicha(Tipo(tipo)) { PontosDeIgnicaoAtual = null };

        Mensagens(form, nameof(FormularioDaFicha.PontosDeIgnicaoAtual)).Should().Equal(mensagem);
    }

    [Fact]
    public void Filled_pontos_de_ignicao_is_valid() =>
        Mensagens(new FormularioDaFicha(TipoDeFichaCliente.Criatura) { PontosDeIgnicaoAtual = 0 }, nameof(FormularioDaFicha.PontosDeIgnicaoAtual)).Should().BeEmpty();

    [Theory]
    [InlineData(nameof(FormularioDaFicha.Nivel), "Informe o Nível.")]
    [InlineData(nameof(FormularioDaFicha.ExperienciaAtual), "Informe a Experiência Atual.")]
    [InlineData(nameof(FormularioDaFicha.NucleosRankF), "Informe os Núcleos de Rank F.")]
    [InlineData(nameof(FormularioDaFicha.NucleosRankS), "Informe os Núcleos de Rank S.")]
    [InlineData(nameof(FormularioDaFicha.VitalidadeAtual), "Informe a Vitalidade.")]
    [InlineData(nameof(FormularioDaFicha.FocoAtual), "Informe o Foco.")]
    [InlineData(nameof(FormularioDaFicha.AdrenalinaAtual), "Informe a Adrenalina.")]
    [InlineData(nameof(FormularioDaFicha.EstresseAtual), "Informe o Estresse.")]
    public void A_blank_required_field_has_the_same_message_on_every_kind(string propriedade, string mensagem)
    {
        foreach (var tipo in new[] { "Personagem", "NPC", "Criatura" })
        {
            var form = new FormularioDaFicha(Tipo(tipo));
            typeof(FormularioDaFicha).GetProperty(propriedade)!.SetValue(form, null);
            Mensagens(form, propriedade).Should().Equal(mensagem);
        }
    }

    [Fact]
    public void The_defaults_are_the_ones_of_the_old_page_forms()
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Npc);

        form.Nivel.Should().Be(1);
        form.GraduacaoLabel.Should().Be("Grau");
        form.Cobertura.Should().Be("Nenhuma");
        form.Abate.Should().BeNull();
    }

    // ---- "Aplicar" do XP (Personagem): soma com sinal, nunca abaixo de 0; vazio ou 0 não salva ----

    [Theory]
    [InlineData(120, 15, 135)]
    [InlineData(120, -20, 100)]
    [InlineData(10, -50, 0)]
    [InlineData(null, 5, 5)]
    public void Adding_xp_sums_the_signed_delta_never_below_zero(int? atual, int delta, int esperado)
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Personagem) { ExperienciaAtual = atual };

        form.SomarExperiencia(delta).Should().BeTrue();

        form.ExperienciaAtual.Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Adding_no_xp_changes_nothing_and_asks_no_save(int? delta)
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Personagem) { ExperienciaAtual = 120 };

        form.SomarExperiencia(delta).Should().BeFalse();

        form.ExperienciaAtual.Should().Be(120);
    }

    // ---- "Aplicar" do PI de bônus manual (Personagem) ----

    [Theory]
    [InlineData(2, 3, 5)]
    [InlineData(2, -5, -3)]
    public void Adding_manual_pi_sums_the_signed_delta(int atual, int delta, int esperado)
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Personagem) { PontosDeIgnicaoBonusManual = atual };

        form.SomarBonusManualDePi(delta).Should().BeTrue();

        form.PontosDeIgnicaoBonusManual.Should().Be(esperado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Adding_no_manual_pi_changes_nothing_and_asks_no_save(int? delta)
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Personagem) { PontosDeIgnicaoBonusManual = 2 };

        form.SomarBonusManualDePi(delta).Should().BeFalse();

        form.PontosDeIgnicaoBonusManual.Should().Be(2);
    }

    // ---- imagem ----

    private static readonly List<ImageSummaryResponse> Galeria = new()
    {
        new("img1", "/img/um.png", DateTime.UtcNow),
        new("img2", "/img/dois.png", DateTime.UtcNow),
    };

    [Fact]
    public void Picking_a_gallery_image_takes_its_url()
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Npc) { ImageId = "img1", ImageUrl = "/img/um.png" };

        form.EscolherImagem("img2", Galeria);

        form.ImageId.Should().Be("img2");
        form.ImageUrl.Should().Be("/img/dois.png");
    }

    [Fact]
    public void Picking_an_image_missing_from_the_gallery_keeps_the_id_without_url()
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Npc);

        form.EscolherImagem("outra", Galeria);

        form.ImageId.Should().Be("outra");
        form.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void Clearing_the_pick_or_removing_the_image_empties_both()
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Criatura) { ImageId = "img1", ImageUrl = "/img/um.png" };
        form.EscolherImagem(null, Galeria);
        form.ImageId.Should().BeNull();
        form.ImageUrl.Should().BeNull();

        form.ImageId = "img1";
        form.ImageUrl = "/img/um.png";
        form.RemoverImagem();
        form.ImageId.Should().BeNull();
        form.ImageUrl.Should().BeNull();
    }

    [Fact]
    public void An_uploaded_image_becomes_the_sheet_image()
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Personagem);

        form.UsarImagem("img-nova", "/img/nova.png");

        form.ImageId.Should().Be("img-nova");
        form.ImageUrl.Should().Be("/img/nova.png");
    }

    // ---- os Atuais como vieram (o servidor só grava o que mudou) ----

    [Fact]
    public void The_loaded_currents_are_kept_apart_from_later_edits()
    {
        var form = new FormularioDaFicha(TipoDeFichaCliente.Personagem) { VitalidadeAtual = 20, FocoAtual = 10, AdrenalinaAtual = 3, EstresseAtual = 1 };

        form.GuardarAtuaisCarregados();
        form.VitalidadeAtual = 5;

        (form.VitalidadeAtualCarregada, form.FocoAtualCarregado, form.AdrenalinaAtualCarregada, form.EstresseAtualCarregado).Should().Be((20, 10, 3, 1));
    }
}
