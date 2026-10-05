using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class ItemFormModelTests
{
    private static RequisitosDePassivaDto Requisitos() => new(
        Nivel: 5, Vocacao: "Guerreiro", Classe: "Lutador", Linhagem: "Humano", Variante: "Sol", Graduacao: 2,
        CoracaoDeMana: true, Afinidade: "Fogo", Estrela: "Alfa", HistoricoId: "hist-1",
        Atributos: [new RequisitoMinimoDto("Forca", 3)],
        SubAtributos: [new RequisitoMinimoDto("Iniciativa", 2)],
        Pericias: [new RequisitoMinimoDto("Atletismo", 4)]);

    private static PenalidadeDeEquipamentoDto Penalidade() => new(
        Atributos: [new PenalidadeLinhaDto("Destreza", 1)],
        SubAtributos: [new PenalidadeLinhaDto("Movimentacao", 2)],
        Pericias: [new PenalidadeLinhaDto("Furtividade", 3)],
        Texto: "Barulhenta");

    public static IEnumerable<object[]> RequestsPorTipo()
    {
        yield return [new CreateItemRequest("ItemGeral", "Poção", 1.5m, 10, "img-1", "Consumível", "Cura", null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, 2.5m)];
        yield return [new CreateItemRequest("Arma", "Espada", 3m, 100, "img-2", "Arma|Espada", "Afiada", "C", "Duas mãos", "2d6", 7, "19-20", 2, "Cortante",
            null, null, null, null, null, null, null, null, null, Requisitos(), Penalidade())];
        yield return [new CreateItemRequest("Armadura", "Couraça", 8m, 200, "img-3", null, "Pesada", "B", null, null, null, null, null, null,
            "Pesada", 5, 2, 1, null, null, null, null, null, Requisitos(), Penalidade())];
        yield return [new CreateItemRequest("Escudo", "Broquel", 2m, 50, null, null, null, "D", null, null, null, null, null, null,
            "Leve", null, null, null, 3, null, null, null, null, Requisitos(), Penalidade())];
        yield return [new CreateItemRequest("Artefato", "Anel", 0.1m, 500, "img-4", null, "Mágico", null, null, null, null, null, null, null,
            null, null, null, null, null, "Pericia", "Atletismo", 2, null, Requisitos(), Penalidade())];
    }

    [Theory, MemberData(nameof(RequestsPorTipo))]
    public void FromRequest_then_ToCreateRequest_round_trips_every_field_of_each_type(CreateItemRequest request)
    {
        var result = ItemFormModel.FromRequest(request).ToCreateRequest();

        result.Should().BeEquivalentTo(request);
    }

    [Fact]
    public void PossuiRequisitos_is_true_only_when_the_request_has_requirements_or_a_penalty()
    {
        CreateItemRequest Base(RequisitosDePassivaDto? r, PenalidadeDeEquipamentoDto? p) => new("Arma", "Espada", 1m, 1, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, null, Requisitos: r, PenalidadeDeRequisitos: p);

        ItemFormModel.FromRequest(Base(null, null)).PossuiRequisitos.Should().BeFalse();
        ItemFormModel.FromRequest(Base(Requisitos(), null)).PossuiRequisitos.Should().BeTrue();
        ItemFormModel.FromRequest(Base(null, Penalidade())).PossuiRequisitos.Should().BeTrue();
    }

    [Fact]
    public void ToCreateRequest_sends_null_requirements_and_penalty_when_PossuiRequisitos_is_false()
    {
        var model = new ItemFormModel { Tipo = "Arma", Nome = "Espada", PossuiRequisitos = false };
        model.Requisitos = RequisitosFormModel.FromDto(Requisitos());
        model.Penalidade = PenalidadeFormModel.FromDto(Penalidade());

        var create = model.ToCreateRequest();
        var update = model.ToUpdateRequest();

        create.Requisitos.Should().BeNull();
        create.PenalidadeDeRequisitos.Should().BeNull();
        update.Requisitos.Should().BeNull();
        update.PenalidadeDeRequisitos.Should().BeNull();
    }

    [Fact]
    public void ToUpdateRequest_carries_the_same_values_without_the_Tipo()
    {
        var request = new CreateItemRequest("Armadura", "Couraça", 8m, 200, "img-3", "Armadura|Pesada", "Pesada", "B", null, null, null, null, null, null,
            "Pesada", 5, 2, 1, null, null, null, null, 1m, Requisitos(), Penalidade());

        var update = ItemFormModel.FromRequest(request).ToUpdateRequest();

        update.Should().BeEquivalentTo(new UpdateItemRequest("Couraça", 8m, 200, "img-3", "Armadura|Pesada", "Pesada", "B", null, null, null, null, null, null,
            "Pesada", 5, 2, 1, null, null, null, null, 1m, Requisitos(), Penalidade()));
    }
}
