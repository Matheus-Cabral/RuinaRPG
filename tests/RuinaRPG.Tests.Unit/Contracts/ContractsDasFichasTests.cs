using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using RuinaRPG.Contracts.CharacterSheets;

namespace RuinaRPG.Tests.Unit.Contracts;

/// <summary>
/// E.2: um record de Contracts que é igual entre os tipos de ficha (fora o nome do tipo) vive uma vez só,
/// em <c>RuinaRPG.Contracts.Fichas</c>. Este teste falha quando sobram gêmeos por tipo.
/// </summary>
public class ContractsDasFichasTests
{
    private static readonly string[] NamespacesDasFichas =
    [
        "RuinaRPG.Contracts.CharacterSheets",
        "RuinaRPG.Contracts.NpcSheets",
        "RuinaRPG.Contracts.CreatureSheets",
        "RuinaRPG.Contracts.Fichas",
    ];

    private static readonly NullabilityInfoContext Nulabilidade = new();

    [Fact]
    public void Each_per_kind_record_left_has_no_identical_twin()
    {
        var records = typeof(CharacterSheetResponse).Assembly.GetTypes()
            .Where(t => t.IsClass && NamespacesDasFichas.Contains(t.Namespace) && t.GetMethod("<Clone>$") is not null)
            .ToList();
        records.Should().NotBeEmpty();

        var gemeos = records
            .GroupBy(t => NomeNormalizado(t.Name) + Assinatura(t, []))
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" = ", g.Select(t => $"{t.Namespace!.Split('.').Last()}.{t.Name}").Order(StringComparer.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();

        string.Join(Environment.NewLine, gemeos).Should().BeEmpty("records iguais entre os tipos de ficha devem ser um só, em RuinaRPG.Contracts.Fichas");
    }

    private static string NomeNormalizado(string nome) =>
        Regex.Replace(nome, "Character|Npc|Creature|DaFicha", "");

    /// <summary>Parâmetros do construtor primário: nome, tipo (com nulabilidade, records comparados por estrutura) e padrão.</summary>
    private static string Assinatura(Type record, HashSet<Type> visitados)
    {
        if (!visitados.Add(record)) return "(ciclo)";
        var ctor = record.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
        var partes = ctor.GetParameters().Select(p =>
        {
            var nulo = Nulabilidade.Create(p).ReadState == NullabilityState.Nullable ? "?" : "";
            var padrao = p.HasDefaultValue ? $"={p.DefaultValue ?? "null"}" : "";
            return $"{p.Name}:{Tipo(p.ParameterType, visitados)}{nulo}{padrao}";
        });
        return "(" + string.Join(",", partes) + ")";
    }

    private static string Tipo(Type tipo, HashSet<Type> visitados)
    {
        if (tipo.IsGenericType)
            return tipo.Name + "<" + string.Join(",", tipo.GetGenericArguments().Select(a => Tipo(a, visitados))) + ">";
        if (NamespacesDasFichas.Contains(tipo.Namespace) && tipo.GetMethod("<Clone>$") is not null)
            return NomeNormalizado(tipo.Name) + Assinatura(tipo, [.. visitados]);
        return tipo.FullName ?? tipo.Name;
    }
}
