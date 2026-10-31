namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// As Classes de cada Vocação, de "Relação de Vocacão e Classes.md", com o texto exato que a ficha grava em
/// <c>SubVocacao</c> (a Classe é texto livre no servidor). A chave é o nome do membro do enum <see cref="Vocacao"/>.
/// É a "tabela atual" de Classes enquanto Vocações e Classes não são criadas pelo Auditor.
/// </summary>
public static class ClassesPorVocacao
{
    public static readonly IReadOnlyDictionary<string, string[]> Todas = new Dictionary<string, string[]>
    {
        ["Campeao"] = ["Cavalheiro", "Duelista", "Paladino", "Lamina Holística", "Guardião"],
        ["Cacador"] = ["Arqueiro", "Domador", "Assassino"],
        ["Feiticeiro"] = ["Aeromante", "Biomante", "Fluxomante", "Piromante", "Sábio"],
        ["Adepto"] = ["Paladino", "Arauto", "Trovador", "Eremita"],
        ["Bruxo"] = ["Ecomante", "Hemomante", "Osteomante", "Nexomante", "Cultista"],
    };

    /// <summary>Se o texto é uma Classe de alguma Vocação.</summary>
    public static bool Existe(string classe) => Todas.Values.Any(cs => cs.Contains(classe, StringComparer.Ordinal));
}
