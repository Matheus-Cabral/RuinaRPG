using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Diary;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Unit.SheetLogs;

public class SheetLogMapTests
{
    private static readonly Type[] Fichas = [typeof(CharacterSheet), typeof(NpcSheet), typeof(CreatureSheet)];

    /// <summary>Tabelas que apontam para uma ficha (ou para uma filha dela) mas não entram no log, cada uma com o motivo.</summary>
    private static readonly Dictionary<Type, string> ForaPorMotivo = new()
    {
        [typeof(SheetLogEntry)] = "é o próprio log; registrar a escrita do log nele mesmo seria recursivo",
        [typeof(EncounterParticipant)] = "estado do encontro (FK de origem SetNull); a ficha original não muda",
        [typeof(DiaryEntry)] = "o diário do personagem não entra no log (Campanha R0016)",
        [typeof(CampaignAttachment)] = "vínculo de visibilidade da campanha, não conteúdo da ficha",
    };

    // O modelo sai do DbContext sem conectar: ler Model não abre conexão.
    private static IModel Modelo()
    {
        var opcoes = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql("Host=unused").Options;
        using var db = new RuinaRpgDbContext(opcoes);
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static bool Excluida(Type t) => ForaPorMotivo.ContainsKey(t) || SheetLogMap.ForaDoLog.Contains(t);

    /// <summary>
    /// Toda entidade com FK (obrigatória ou opcional, por navegação ou sombra) para uma ficha e, de forma transitiva,
    /// toda com FK para uma dessas que entra no log (netas, como os Efeitos de uma Magia; o que pende de uma exclusão fica de fora com ela): uma neta nova também não pode passar batida.
    /// </summary>
    private static IReadOnlyCollection<Type> EntidadesDeFicha()
    {
        var modelo = Modelo();
        var achadas = new HashSet<Type>();
        bool cresceu;
        do
        {
            cresceu = false;
            foreach (var e in modelo.GetEntityTypes())
            {
                if (achadas.Contains(e.ClrType)) continue;
                var alvos = e.GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType).ToList();
                if (alvos.Any(t => Fichas.Contains(t) || (achadas.Contains(t) && !Excluida(t)))) { achadas.Add(e.ClrType); cresceu = true; }
            }
        } while (cresceu);
        foreach (var f in Fichas) achadas.Add(f); // as próprias fichas também precisam estar no mapa
        return achadas;
    }

    [Fact]
    public void Every_sheet_entity_is_either_mapped_or_explicitly_left_out()
    {
        var semDestino = EntidadesDeFicha().Where(t => !SheetLogMap.Entidades.ContainsKey(t) && !SheetLogMap.ForaDoLog.Contains(t) && !ForaPorMotivo.ContainsKey(t));
        semDestino.Select(t => t.Name).Should().BeEmpty("toda tabela ligada a uma ficha precisa estar no SheetLogMap, em ForaDoLog ou em ForaPorMotivo");
    }

    [Fact]
    public void An_exclusion_is_never_also_mapped_and_still_points_at_a_sheet()
    {
        var achadas = EntidadesDeFicha();
        foreach (var (t, motivo) in ForaPorMotivo)
        {
            motivo.Should().NotBeNullOrWhiteSpace(t.Name);
            SheetLogMap.Entidades.Should().NotContainKey(t);
            achadas.Should().Contain(t, $"{t.Name} deixou de apontar para uma ficha: tire da lista de exclusões");
        }
        foreach (var t in SheetLogMap.ForaDoLog) achadas.Should().Contain(t);
    }

    [Fact]
    public void Tables_that_hang_off_no_sheet_are_not_swept_in()
    {
        var achadas = EntidadesDeFicha().Select(t => t.Name).ToList();
        achadas.Should().NotContain(["Condicao", "CondicaoModificador", "CondicaoDesabilitacao",
            "EncounterParticipantCondicao", "EncounterLogEntry", "Encounter"]);
    }

    [Fact]
    public void Every_scalar_property_of_a_mapped_entity_is_labelled_ignored_or_long_text()
    {
        var faltando = new List<string>();
        foreach (var (tipo, mapa) in SheetLogMap.Entidades)
            foreach (var p in tipo.GetProperties().Where(p => p.PropertyType == typeof(string) || p.PropertyType.IsValueType
                         || Nullable.GetUnderlyingType(p.PropertyType) is not null || p.Name == "Requisitos"))
                if (!mapa.Campos.ContainsKey(p.Name) && !mapa.Ignorados.Contains(p.Name) && !mapa.TextosLongos.Contains(p.Name))
                    faltando.Add($"{tipo.Name}.{p.Name}");
        faltando.Should().BeEmpty();
    }

    [Fact]
    public void Only_the_diary_free_history_images_are_left_out()
    {
        SheetLogMap.ForaDoLog.Select(t => t.Name).Should().BeEquivalentTo(
            new[] { "CharacterSheetHistoriaImage", "NpcSheetHistoriaImage" });
    }

    [Fact]
    public void The_resource_fields_carry_their_categoria_on_the_three_sheets()
    {
        foreach (var tipo in new[] { typeof(CharacterSheet), typeof(NpcSheet), typeof(CreatureSheet) })
        {
            var c = SheetLogMap.Entidades[tipo].Categorias;
            c["VitalidadeAtual"].Should().Be(SheetLogCategoria.RecursoVFA);
            c["FocoAtual"].Should().Be(SheetLogCategoria.RecursoVFA);
            c["AdrenalinaAtual"].Should().Be(SheetLogCategoria.RecursoVFA);
        }
        SheetLogMap.Entidades[typeof(CharacterSheet)].Categorias["EstresseAtual"].Should().Be(SheetLogCategoria.Estresse);
        SheetLogMap.Entidades[typeof(NpcSheet)].Categorias["EstresseAtual"].Should().Be(SheetLogCategoria.Estresse);
    }

    [Fact]
    public void A_spell_effect_points_to_its_spell_ability_as_parent()
    {
        var m = SheetLogMap.Entidades[typeof(CharacterSpellAbilityEffect)];
        m.ParentType.Should().Be(typeof(CharacterSpellAbility));
        m.ParentIdProperty.Should().Be("CharacterSpellAbilityId");
    }

    [Fact]
    public void Every_property_named_by_a_map_entry_exists_on_the_entity()
    {
        var inexistentes = new List<string>();
        foreach (var (tipo, m) in SheetLogMap.Entidades)
        {
            var nomes = new[] { m.SheetIdProperty, m.ParentIdProperty, m.ItemNomeProperty, m.ItemRefProperty }
                .Concat(m.Campos.Keys).Concat(m.Ignorados).Concat(m.TextosLongos).Concat(m.Categorias.Keys);
            inexistentes.AddRange(nomes.Where(n => n is not null && tipo.GetProperty(n) is null).Select(n => $"{tipo.Name}.{n}"));
        }
        inexistentes.Should().BeEmpty();
    }

    [Fact]
    public void A_child_row_names_its_sheet_or_its_parent_and_a_sheet_row_names_neither()
    {
        foreach (var (tipo, m) in SheetLogMap.Entidades)
        {
            if (m.IsSheet) { m.SheetIdProperty.Should().BeNull(tipo.Name); m.ParentType.Should().BeNull(tipo.Name); }
            else if (m.ParentType is not null) { m.ParentIdProperty.Should().NotBeNull(tipo.Name); m.SheetIdProperty.Should().BeNull(tipo.Name); }
            else m.SheetIdProperty.Should().NotBeNull(tipo.Name);
            m.Secao.Should().NotBeNullOrWhiteSpace(tipo.Name);
        }
    }

    [Fact]
    public void Every_entity_is_mapped_under_the_kind_of_its_sheet()
    {
        foreach (var (tipo, m) in SheetLogMap.Entidades)
        {
            var esperado = tipo.Name.StartsWith("Character") ? SheetLogKind.Character
                : tipo.Name.StartsWith("Npc") ? SheetLogKind.Npc : SheetLogKind.Creature;
            m.Kind.Should().Be(esperado, tipo.Name);
        }
    }

    [Fact]
    public void The_armor_slot_column_is_labelled_Slot_de_Armadura()
    {
        foreach (var tipo in new[] { typeof(CharacterArmorSlot), typeof(NpcArmorSlot), typeof(CreatureArmorSlot) })
            SheetLogMap.Entidades[tipo].Campos["Slot"].Should().Be("Slot de Armadura");
    }

    [Fact]
    public void The_chosen_attribute_of_a_skill_is_labelled_Atributo_escolhido()
    {
        foreach (var tipo in new[] { typeof(CharacterSkill), typeof(NpcSkill), typeof(CreatureSkill) })
            SheetLogMap.Entidades[tipo].Campos["AtributoEscolhido"].Should().Be("Atributo escolhido");
    }

    [Fact]
    public void Every_long_text_has_a_label()
    {
        foreach (var (tipo, m) in SheetLogMap.Entidades)
            foreach (var p in m.TextosLongos)
                SheetLogMap.RotulosDeTextoLongo.Should().ContainKey(p, $"{tipo.Name}.{p} precisa de rótulo");
        SheetLogMap.RotulosDeTextoLongo["Historia"].Should().Be("História");
        SheetLogMap.RotulosDeTextoLongo["Descricao"].Should().Be("Descrição");
    }
}
