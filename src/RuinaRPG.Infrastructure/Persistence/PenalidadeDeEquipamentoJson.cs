using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Persistence;

/// <summary>
/// Mapeia <see cref="PenalidadeDeEquipamento"/> para uma coluna jsonb. O objeto é sempre lido e gravado
/// inteiro e nunca consultado por dentro, então um único jsonb evita colunas e tabelas-filhas para as
/// três listas de penalidade. O comparer compara pelo JSON, porque as listas do record
/// não têm igualdade por valor.
/// </summary>
internal static class PenalidadeDeEquipamentoJson
{
    private static string Serialize(PenalidadeDeEquipamento value) => JsonSerializer.Serialize(value);
    private static PenalidadeDeEquipamento Deserialize(string json) => JsonSerializer.Deserialize<PenalidadeDeEquipamento>(json)!;

    // TModel is declared nullable to match the (nullable) PenalidadeDeRequisitos property exactly — EF Core
    // skips invoking the converter for null values by default, so Serialize never sees a null v.
    public static readonly ValueConverter<PenalidadeDeEquipamento?, string> Converter =
        new(v => Serialize(v!), s => Deserialize(s));

    public static readonly ValueComparer<PenalidadeDeEquipamento?> Comparer = new(
        (a, b) => (a == null ? null : Serialize(a)) == (b == null ? null : Serialize(b)),
        v => v == null ? 0 : Serialize(v).GetHashCode(),
        v => v == null ? null : Deserialize(Serialize(v)));
}
