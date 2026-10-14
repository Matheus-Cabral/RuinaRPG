using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RuinaRPG.Infrastructure.Persistence;

/// <summary>
/// Conversor e comparer para uma propriedade jsonb que é uma coleção tipada (lista ou dicionário). O comparer
/// compara pelo JSON e clona por ida e volta, para o EF enxergar mudanças feitas por dentro da coleção.
/// Enums viajam como texto.
/// </summary>
internal static class JsonbConversao<T> where T : class
{
    private static readonly JsonSerializerOptions Opcoes = new() { Converters = { new JsonStringEnumConverter() } };

    private static string Serialize(T value) => JsonSerializer.Serialize(value, Opcoes);
    private static T Deserialize(string json) => JsonSerializer.Deserialize<T>(json, Opcoes)!;

    public static readonly ValueConverter<T, string> Converter = new(v => Serialize(v), s => Deserialize(s));

    public static readonly ValueComparer<T> Comparer = new(
        (a, b) => Serialize(a!) == Serialize(b!),
        v => Serialize(v).GetHashCode(),
        v => Deserialize(Serialize(v)));
}
