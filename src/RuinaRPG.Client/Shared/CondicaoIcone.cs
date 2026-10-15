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
        ["Marcado"] = (Icons.Material.Filled.GpsFixed, "Marcado"),
        ["Agua"] = (Icons.Material.Filled.Water, "Água"),
        ["Terra"] = (Icons.Material.Filled.Terrain, "Terra"),
        ["Vento"] = (Icons.Material.Filled.Air, "Vento"),
        ["Trovao"] = (Icons.Material.Filled.Thunderstorm, "Trovão"),
        ["Luz"] = (Icons.Material.Filled.LightMode, "Luz"),
        ["Trevas"] = (Icons.Material.Filled.DarkMode, "Trevas"),
        ["Acido"] = (Icons.Material.Filled.Opacity, "Ácido"),
        ["Magma"] = (Icons.Material.Filled.Volcano, "Magma"),
        ["Tempestade"] = (Icons.Material.Filled.Storm, "Tempestade"),
        ["Tornado"] = (Icons.Material.Filled.Tornado, "Tornado"),
        ["Inundado"] = (Icons.Material.Filled.Flood, "Inundado"),
        ["Congelado"] = (Icons.Material.Filled.SevereCold, "Congelado"),
        ["Cristalizado"] = (Icons.Material.Filled.Diamond, "Cristalizado"),
        ["Petrificado"] = (Icons.Material.Filled.Landslide, "Petrificado"),
        ["Caido"] = (Icons.Material.Filled.AirlineSeatFlat, "Caído"),
        ["Agarrado"] = (Icons.Material.Filled.PanTool, "Agarrado"),
        ["Preso"] = (Icons.Material.Filled.Lock, "Preso"),
        ["Imobilizado"] = (Icons.Material.Filled.DoNotStep, "Imobilizado"),
        ["Paralisado"] = (Icons.Material.Filled.PauseCircle, "Paralisado"),
        ["Acorrentado"] = (Icons.Material.Filled.Link, "Acorrentado"),
        ["Emaranhado"] = (Icons.Material.Filled.Grass, "Emaranhado"),
        ["Exausto"] = (Icons.Material.Filled.BatteryAlert, "Exausto"),
        ["Revigorado"] = (Icons.Material.Filled.BatteryChargingFull, "Revigorado"),
        ["Faminto"] = (Icons.Material.Filled.NoFood, "Faminto"),
        ["Sedento"] = (Icons.Material.Filled.NoDrinks, "Sedento"),
        ["Doente"] = (Icons.Material.Filled.Sick, "Doente"),
        ["Ferido"] = (Icons.Material.Filled.PersonalInjury, "Ferido"),
        ["Inconsciente"] = (Icons.Material.Filled.Hotel, "Inconsciente"),
        ["Sonolento"] = (Icons.Material.Filled.Snooze, "Sonolento"),
        ["Regenerando"] = (Icons.Material.Filled.Autorenew, "Regenerando"),
        ["Curando"] = (Icons.Material.Filled.Healing, "Curando"),
        ["Imune"] = (Icons.Material.Filled.Vaccines, "Imune"),
        ["Infestado"] = (Icons.Material.Filled.BugReport, "Infestado"),
        ["Desarmado"] = (Icons.Material.Filled.DoNotTouch, "Desarmado"),
        ["Confuso"] = (Icons.Material.Filled.QuestionMark, "Confuso"),
        ["Encantado"] = (Icons.Material.Filled.Favorite, "Encantado"),
        ["Enfurecido"] = (Icons.Material.Filled.SportsMma, "Enfurecido"),
        ["Panico"] = (Icons.Material.Filled.CrisisAlert, "Pânico"),
        ["Calmo"] = (Icons.Material.Filled.SelfImprovement, "Calmo"),
        ["Inspirado"] = (Icons.Material.Filled.Lightbulb, "Inspirado"),
        ["Focado"] = (Icons.Material.Filled.CenterFocusStrong, "Focado"),
        ["Distraido"] = (Icons.Material.Filled.CenterFocusWeak, "Distraído"),
        ["Desorientado"] = (Icons.Material.Filled.ExploreOff, "Desorientado"),
        ["Enlouquecido"] = (Icons.Material.Filled.PsychologyAlt, "Enlouquecido"),
        ["Dominado"] = (Icons.Material.Filled.Psychology, "Dominado"),
        ["Hipnotizado"] = (Icons.Material.Filled.Cyclone, "Hipnotizado"),
        ["Provocado"] = (Icons.Material.Filled.Campaign, "Provocado"),
        ["Surdo"] = (Icons.Material.Filled.HearingDisabled, "Surdo"),
        ["Mudo"] = (Icons.Material.Filled.VoiceOverOff, "Mudo"),
        ["VisaoNoEscuro"] = (Icons.Material.Filled.NightsStay, "Visão no escuro"),
        ["VisaoAgucada"] = (Icons.Material.Filled.Visibility, "Visão aguçada"),
        ["Acelerado"] = (Icons.Material.Filled.Speed, "Acelerado"),
        ["Agil"] = (Icons.Material.Filled.DirectionsRun, "Ágil"),
        ["Protegido"] = (Icons.Material.Filled.Security, "Protegido"),
        ["Resistente"] = (Icons.Material.Filled.VerifiedUser, "Resistente"),
        ["Invulneravel"] = (Icons.Material.Filled.GppGood, "Invulnerável"),
        ["Vulneravel"] = (Icons.Material.Filled.GppBad, "Vulnerável"),
        ["Exposto"] = (Icons.Material.Filled.ReportProblem, "Exposto"),
        ["DefesaQuebrada"] = (Icons.Material.Filled.RemoveModerator, "Defesa quebrada"),
        ["Protegendo"] = (Icons.Material.Filled.AddModerator, "Protegendo"),
        ["Voando"] = (Icons.Material.Filled.FlightTakeoff, "Voando"),
        ["Camuflado"] = (Icons.Material.Filled.Forest, "Camuflado"),
        ["Escondido"] = (Icons.Material.Filled.HideSource, "Escondido"),
        ["Encolhido"] = (Icons.Material.Filled.ZoomOut, "Encolhido"),
        ["Aumentado"] = (Icons.Material.Filled.ZoomIn, "Aumentado"),
        ["Transformado"] = (Icons.Material.Filled.Transform, "Transformado"),
        ["Possuido"] = (Icons.Material.Filled.SwitchAccount, "Possuído"),
        ["Sorte"] = (Icons.Material.Filled.Casino, "Sorte"),
        ["Empoderado"] = (Icons.Material.Filled.AutoFixHigh, "Empoderado"),
        ["ArmaEncantada"] = (Icons.Material.Filled.AutoFixNormal, "Arma encantada"),
        ["Dissipado"] = (Icons.Material.Filled.AutoFixOff, "Dissipado"),
        ["ManaDrenada"] = (Icons.Material.Filled.FlashOff, "Mana drenada"),
        ["Runa"] = (Icons.Material.Filled.Token, "Runa"),
        ["Selo"] = (Icons.Material.Filled.Pentagon, "Selo"),
        ["Pacto"] = (Icons.Material.Filled.Handshake, "Pacto"),
        ["Consagrado"] = (Icons.Material.Filled.Church, "Consagrado"),
        ["Condenado"] = (Icons.Material.Filled.Gavel, "Condenado"),
        ["Cacado"] = (Icons.Material.Filled.TrackChanges, "Caçado"),
        ["Concentrando"] = (Icons.Material.Filled.HourglassBottom, "Concentrando"),
        ["Carregando"] = (Icons.Material.Filled.Backpack, "Carregando")
    };

    /// <summary>As chaves que têm mapeamento, na ordem do seletor.</summary>
    public static IReadOnlyList<string> Chaves { get; } = Mapa.Keys.ToList();

    /// <summary>O ícone da chave; uma chave desconhecida cai num ícone genérico em vez de quebrar a tela.</summary>
    public static string Icone(string chave) => Mapa.TryGetValue(chave, out var m) ? m.Icone : Icons.Material.Filled.Coronavirus;

    public static string Nome(string chave) => Mapa.TryGetValue(chave, out var m) ? m.Nome : chave;
}
