using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Unit.SheetLogs;

public class SheetLogMapTests
{
    private static IEnumerable<Type> EntidadesDeFicha() =>
        typeof(RuinaRpgDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.PropertyType.GetGenericArguments()[0])
            .Where(t => t.Namespace is "RuinaRPG.Infrastructure.CharacterSheets" or "RuinaRPG.Infrastructure.NpcSheets" or "RuinaRPG.Infrastructure.CreatureSheets")
            .Where(t => t.Name.StartsWith("Character") || t.Name.StartsWith("Npc") || t.Name.StartsWith("Creature"));

    [Fact]
    public void Every_sheet_entity_is_either_mapped_or_explicitly_left_out()
    {
        var semDestino = EntidadesDeFicha().Where(t => !SheetLogMap.Entidades.ContainsKey(t) && !SheetLogMap.ForaDoLog.Contains(t));
        semDestino.Select(t => t.Name).Should().BeEmpty("toda tabela de ficha precisa estar no SheetLogMap ou em ForaDoLog");
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
