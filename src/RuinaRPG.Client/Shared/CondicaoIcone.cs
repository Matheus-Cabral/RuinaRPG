using MudBlazor;
using RuinaRPG.Contracts.Condicoes;

namespace RuinaRPG.Client.Shared;

/// <summary>
/// Liga cada chave de ícone de Condição (<see cref="CondicaoIcones.Chaves"/>, a lista que a API aceita)
/// a um ícone MudBlazor e a um nome em português para o seletor. Só exibição: o que viaja e é guardado
/// é a chave.
/// </summary>
public static class CondicaoIcone
{
    private static readonly Dictionary<string, (string Icone, string Nome)> Mapa = new()
    {
        ["Veneno"] = (Icons.Material.Filled.Science, "Veneno"),
        ["Fogo"] = (Icons.Material.Filled.LocalFireDepartment, "Fogo"),
        ["Gelo"] = (Icons.Material.Filled.AcUnit, "Gelo"),
        ["Atordoado"] = (Icons.Material.Filled.BlurOn, "Atordoado"),
        ["Sangramento"] = (Icons.Material.Filled.WaterDrop, "Sangramento"),
        ["Escudo"] = (Icons.Material.Filled.Shield, "Escudo"),
        ["Lentidao"] = (Icons.Material.Filled.SlowMotionVideo, "Lentidão"),
        ["Cegueira"] = (Icons.Material.Filled.VisibilityOff, "Cegueira"),
        ["Medo"] = (Icons.Material.Filled.SentimentVeryDissatisfied, "Medo"),
        ["Bencao"] = (Icons.Material.Filled.AutoAwesome, "Bênção"),
        ["Maldicao"] = (Icons.Material.Filled.HeartBroken, "Maldição"),
        ["Sono"] = (Icons.Material.Filled.Bedtime, "Sono"),
        ["Enraizado"] = (Icons.Material.Filled.Park, "Enraizado"),
        ["Enfraquecido"] = (Icons.Material.Filled.TrendingDown, "Enfraquecido"),
        ["Fortalecido"] = (Icons.Material.Filled.FitnessCenter, "Fortalecido"),
        ["Queimadura"] = (Icons.Material.Filled.Whatshot, "Queimadura"),
        ["Eletrificado"] = (Icons.Material.Filled.Bolt, "Eletrificado"),
        ["Silenciado"] = (Icons.Material.Filled.VolumeOff, "Silenciado"),
        ["Invisivel"] = (Icons.Material.Filled.PersonOff, "Invisível"),
        ["Marcado"] = (Icons.Material.Filled.GpsFixed, "Marcado")
    };

    /// <summary>As chaves que têm mapeamento, na ordem do seletor.</summary>
    public static IReadOnlyList<string> Chaves { get; } = Mapa.Keys.ToList();

    /// <summary>O ícone da chave; uma chave desconhecida cai num ícone genérico em vez de quebrar a tela.</summary>
    public static string Icone(string chave) => Mapa.TryGetValue(chave, out var m) ? m.Icone : Icons.Material.Filled.Coronavirus;

    public static string Nome(string chave) => Mapa.TryGetValue(chave, out var m) ? m.Nome : chave;
}
