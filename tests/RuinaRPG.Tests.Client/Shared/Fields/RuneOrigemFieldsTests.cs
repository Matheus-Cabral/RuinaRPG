using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.Runes;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class RuneOrigemFieldsTests : MudBunitContext
{
    private static readonly RuneBankEntryResponse[] Entradas =
    [
        new("e1", "Runa do Fogo", "Queima.", 1),
        new("e2", "Runa do Gelo", "Congela.", 2),
    ];

    [Fact]
    public void The_model_starts_from_scratch_and_reports_DoBanco_only_for_the_bank_origin()
    {
        var model = new RuneOrigemModel();

        model.Origem.Should().Be("Zero");
        model.DoBanco.Should().BeFalse();

        model.Origem = "Banco";
        model.DoBanco.Should().BeTrue();
    }

    [Fact]
    public void Limpar_resets_every_field_to_the_starting_state()
    {
        var model = new RuneOrigemModel { Origem = "Banco", SourceBankEntryId = "e1", Nome = "X", Descricao = "Y", Grau = 4 };

        model.Limpar();

        model.Origem.Should().Be("Zero");
        model.SourceBankEntryId.Should().BeEmpty();
        model.Nome.Should().BeEmpty();
        model.Descricao.Should().BeEmpty();
        model.Grau.Should().Be(0);
    }

    [Fact]
    public void From_scratch_shows_the_three_rune_fields_and_no_bank_picker()
    {
        var cut = Render<RuneOrigemFields>(p => p
            .Add(x => x.Model, new RuneOrigemModel { Origem = "Zero" })
            .Add(x => x.BankEntries, Entradas));

        cut.FindComponents<MudTextField<string>>().Select(c => c.Instance.Label).Should().BeEquivalentTo("Nome", "Descrição");
        cut.FindComponents<MudNumericField<int>>().Should().ContainSingle(c => c.Instance.Label == "Grau");
        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().NotContain("e1");
    }

    [Fact]
    public void From_the_bank_lists_the_entries_and_hides_the_manual_fields()
    {
        var cut = Render<RuneOrigemFields>(p => p
            .Add(x => x.Model, new RuneOrigemModel { Origem = "Banco" })
            .Add(x => x.BankEntries, Entradas));

        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().Contain(["e1", "e2"]);
        cut.FindComponents<MudTextField<string>>().Should().BeEmpty();
        cut.FindComponents<MudNumericField<int>>().Should().BeEmpty();
    }

    [Fact]
    public void Validar_from_scratch_rejects_a_blank_Nome()
    {
        new RuneOrigemModel { Origem = "Zero", Nome = "" }.Validar().Should().Be("Informe o nome da runa.");
    }

    [Fact]
    public void Validar_from_scratch_rejects_a_whitespace_Nome()
    {
        new RuneOrigemModel { Origem = "Zero", Nome = "   " }.Validar().Should().Be("Informe o nome da runa.");
    }

    [Fact]
    public void Validar_from_scratch_accepts_a_Nome()
    {
        new RuneOrigemModel { Origem = "Zero", Nome = "Runa do Fogo" }.Validar().Should().BeNull();
    }

    [Fact]
    public void Validar_from_the_bank_rejects_an_empty_bank_entry_id()
    {
        new RuneOrigemModel { Origem = "Banco", SourceBankEntryId = "" }.Validar().Should().Be("Escolha uma entrada do Banco de Runas.");
    }

    [Fact]
    public void Validar_from_the_bank_accepts_a_bank_entry_id()
    {
        new RuneOrigemModel { Origem = "Banco", SourceBankEntryId = "e1" }.Validar().Should().BeNull();
    }

    [Fact]
    public void Validar_from_the_bank_ignores_a_blank_Nome()
    {
        new RuneOrigemModel { Origem = "Banco", SourceBankEntryId = "e1", Nome = "" }.Validar().Should().BeNull();
    }
}
