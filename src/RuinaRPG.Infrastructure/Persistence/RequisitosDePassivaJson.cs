using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.Persistence;

/// <summary>
/// Mapeia <see cref="RequisitosDePassiva"/> para uma coluna jsonb. O objeto é sempre lido e gravado
/// inteiro e nunca consultado por dentro, então um único jsonb evita ~11 colunas + 3 tabelas-filhas em
/// cada uma das 4 tabelas de Magia/Habilidade. O comparer compara pelo JSON, porque as listas do record
/// não têm igualdade por valor.
/// </summary>
internal static class RequisitosDePassivaJson
{
    private static string Serialize(RequisitosDePassiva value) => JsonSerializer.Serialize(value);
    private static RequisitosDePassiva Deserialize(string json) => JsonSerializer.Deserialize<RequisitosDePassiva>(json)!;

    // TModel is declared nullable to match the (nullable) Requisitos property exactly — EF Core
    // skips invoking the converter for null values by default, so Serialize never sees a null v.
    public static readonly ValueConverter<RequisitosDePassiva?, string> Converter =
        new(v => Serialize(v!), s => Deserialize(s));

    public static readonly ValueComparer<RequisitosDePassiva?> Comparer = new(
        (a, b) => (a == null ? null : Serialize(a)) == (b == null ? null : Serialize(b)),
        v => v == null ? 0 : Serialize(v).GetHashCode(),
        v => v == null ? null : Deserialize(Serialize(v)));
}
