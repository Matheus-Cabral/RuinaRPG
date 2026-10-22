using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;
using System.Reflection;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// Os records da Criatura que continuam separados (E.2) viram os records compartilhados que as abas mostram.
/// Nada no fio muda: o adaptador só copia campo a campo, com <c>DurabilidadeMaximo</c> → <c>DurabilidadeMaxima</c>
/// e valores neutros para o que a Criatura não tem (<c>Peso</c> 0, <c>ContaHistorico</c> false, <c>IsRacial</c> false).
/// </summary>
public class AdaptadorDeCriaturaTests
{
    private static readonly List<string> Requisitos = new() { "Vigor 2" };
    private static readonly List<string> Pendentes = new() { "Vigor 2" };
    private static readonly List<string> Penalidade = new() { "Esquiva −1" };

    private static CreatureWeaponResponse ArmaDoCatalogo() => new(
        Id: "w1", ItemId: "i1", Nome: "Garra de Ferro", TipoDeDano: "Cortante", Dados: "1d6", Dano: 2, Alcance: 1, Critico: "20", Rank: "D",
        IsEquipped: true, DurabilidadeAtual: 7, DurabilidadeMaximo: 9, ImageUrl: "/img/g.png", Descricao: "Afiada.", Inquebravel: true,
        Requisitos: Requisitos, RequisitosPendentes: Pendentes, Penalidade: Penalidade, OutrasPenalidades: "Barulhenta", DesabilitadoPor: "Atordoado");

    private static CreatureArmorSlotResponse Slot() => new(
        Slot: "Superior", ItemId: "a1", Nome: "Couro", Categoria: "Leve", Defesa: 2, RF: 1, RM: 0, Peso: 4.5m, DurabilidadeAtual: 3, DurabilidadeMaximo: 5,
        ImageUrl: "/img/c.png", Descricao: "Curtido.", Inquebravel: true, Requisitos: Requisitos, RequisitosPendentes: Pendentes, Penalidade: Penalidade,
        OutrasPenalidades: "Range", DesabilitadoPor: "Enfraquecido");

    private static CreatureSkillResponse Pericia() => new("Atletismo", 3, 2, "Vigor", 5, "Atletismo", "Correr e saltar.");

    private static CreatureTraitResponse Caracteristica(string id) => new(id, "tr-" + id, "Faro", "Fareja longe.", 2, "Positiva", "sangue", true);

    // ---- cada campo copiado (por reflexão: um campo novo num dos dois records quebra este teste) ----

    // Campo da Criatura → campo compartilhado, onde o nome muda.
    private static readonly Dictionary<string, string> Renomeados = new() { ["DurabilidadeMaximo"] = "DurabilidadeMaxima" };

    public static TheoryData<string> Mapeamentos => new() { "Arma", "Slot", "Pericia", "Caracteristica" };

    [Theory]
    [MemberData(nameof(Mapeamentos))]
    public void Every_creature_field_is_copied_to_the_shared_record(string mapeamento)
    {
        var (origem, destino, neutros) = Mapear(mapeamento);

        foreach (var campo in origem.GetType().GetProperties().Where(p => p.Name != "EqualityContract"))
        {
            var nomeDestino = Renomeados.GetValueOrDefault(campo.Name, campo.Name);
            var alvo = destino.GetType().GetProperty(nomeDestino);
            alvo.Should().NotBeNull($"{origem.GetType().Name}.{campo.Name} precisa de um lugar em {destino.GetType().Name}");
            alvo!.GetValue(destino).Should().BeEquivalentTo(campo.GetValue(origem), $"{campo.Name} → {nomeDestino}");
        }

        // O que só o record compartilhado tem fica neutro.
        var copiados = origem.GetType().GetProperties().Select(p => Renomeados.GetValueOrDefault(p.Name, p.Name)).ToHashSet();
        var soDoDestino = destino.GetType().GetProperties().Select(p => p.Name).Where(n => !copiados.Contains(n) && n != "EqualityContract").ToList();
        soDoDestino.Should().BeEquivalentTo(neutros.Keys);
        foreach (var (nome, valor) in neutros)
            destino.GetType().GetProperty(nome)!.GetValue(destino).Should().Be(valor, nome);
    }

    private static (object Origem, object Destino, Dictionary<string, object> Neutros) Mapear(string mapeamento) => mapeamento switch
    {
        "Arma" => (ArmaDoCatalogo(), AdaptadorDeCriatura.ParaArma(ArmaDoCatalogo()), new() { ["Peso"] = 0m }),
        "Slot" => (Slot(), AdaptadorDeCriatura.ParaSlotDeArmadura(Slot()), new()),
        "Pericia" => (Pericia(), AdaptadorDeCriatura.ParaPericia(Pericia()), new() { ["ContaHistorico"] = false }),
        "Caracteristica" => (Caracteristica("t1"), AdaptadorDeCriatura.ParaCaracteristica(Caracteristica("t1")), new() { ["IsRacial"] = false }),
        _ => throw new ArgumentOutOfRangeException(nameof(mapeamento)),
    };

    // ---- os casos que a Review Focus nomeia ----

    [Fact]
    public void Weapon_DurabilidadeMaximo_becomes_DurabilidadeMaxima()
    {
        var arma = AdaptadorDeCriatura.ParaArma(ArmaDoCatalogo());

        arma.DurabilidadeMaxima.Should().Be(9);
        arma.DurabilidadeAtual.Should().Be(7);
    }

    [Fact]
    public void Armor_slot_DurabilidadeMaximo_becomes_DurabilidadeMaxima()
    {
        AdaptadorDeCriatura.ParaSlotDeArmadura(Slot()).DurabilidadeMaxima.Should().Be(5);
    }

    [Fact]
    public void Manual_weapon_keeps_its_null_ItemId_and_has_no_weight()
    {
        // O ataque natural (manual), como o servidor o manda: sem Item do Catálogo, sem Alcance/Crítico/Rank/Durabilidade.
        var manual = new CreatureWeaponResponse("w2", null, "Mordida", "Perfurante", "1d4", 1, null, null, null, false, null, null, null, null);

        var arma = AdaptadorDeCriatura.ParaArma(manual);

        arma.ItemId.Should().BeNull();
        arma.Nome.Should().Be("Mordida");
        arma.TipoDeDano.Should().Be("Perfurante");
        arma.Dados.Should().Be("1d4");
        arma.Dano.Should().Be(1);
        arma.Alcance.Should().BeNull();
        arma.Critico.Should().BeNull();
        arma.Rank.Should().BeNull();
        arma.Peso.Should().Be(0m);
        arma.Inquebravel.Should().BeFalse();
        arma.Requisitos.Should().BeNull();
    }

    [Fact]
    public void An_empty_armor_slot_stays_empty()
    {
        var vazio = new CreatureArmorSlotResponse("Capacete", null, null, null, null, null, null, null, null, null, null, null);

        AdaptadorDeCriatura.ParaSlotDeArmadura(vazio).Should().Be(new ArmorSlotResponse("Capacete", null, null, null, null, null, null, null, null, null, null, null));
    }

    [Fact]
    public void Trait_list_maps_both_lists_and_keeps_the_totals()
    {
        var lista = new CreatureTraitsListResponse(
            new() { Caracteristica("t1"), Caracteristica("t2") }, 4, new() { Caracteristica("t3") }, -1, 6, 2);

        var compartilhada = AdaptadorDeCriatura.ParaCaracteristicas(lista);

        compartilhada.Positivas.Select(t => t.Id).Should().Equal("t1", "t2");
        compartilhada.Negativas.Select(t => t.Id).Should().Equal("t3");
        compartilhada.Positivas.Should().OnlyContain(t => !t.IsRacial);
        compartilhada.TotalPositivas.Should().Be(4);
        compartilhada.TotalNegativas.Should().Be(-1);
        compartilhada.PontosDisponiveis.Should().Be(6);
        compartilhada.PontosDisponiveisNegativas.Should().Be(2);
    }

    // Cada record da Criatura que continua separado e que uma aba compartilhada mostra tem o seu Para….
    // Um corpo sem as listas (o JSON pode omiti-las) passa como veio: a aba mostra nada, como a página de antes mostrava.
    [Fact]
    public void A_trait_list_without_rows_keeps_them_missing()
    {
        var lista = new CreatureTraitsListResponse(null!, 0, null!, 0, 0, 0);

        var compartilhada = AdaptadorDeCriatura.ParaCaracteristicas(lista);

        compartilhada.Positivas.Should().BeNull();
        compartilhada.Negativas.Should().BeNull();
    }

    [Fact]
    public void There_is_one_mapping_per_creature_record_a_shared_tab_shows()
    {
        typeof(AdaptadorDeCriatura).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => (De: m.GetParameters().Single().ParameterType.Name, Para: m.ReturnType.Name))
            .Should().BeEquivalentTo(new[]
            {
                (De: nameof(CreatureWeaponResponse), Para: nameof(WeaponResponse)),
                (De: nameof(CreatureArmorSlotResponse), Para: nameof(ArmorSlotResponse)),
                (De: nameof(CreatureSkillResponse), Para: nameof(SkillResponse)),
                (De: nameof(CreatureTraitResponse), Para: nameof(TraitDaFichaResponse)),
                (De: nameof(CreatureTraitsListResponse), Para: nameof(TraitsListResponse)),
            });
    }
}
