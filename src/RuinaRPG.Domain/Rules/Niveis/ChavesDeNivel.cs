using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Domain.Rules.Niveis;

public enum TipoDeColunaDeNivel { Acumulativa, PorNivel }

public static class ChavesDeNivel
{
    public const string PontosDeAtributo = "PontosDeAtributo", PontosDePericia = "PontosDePericia",
        EspacosDeCaracteristica = "EspacosDeCaracteristica", PontosDeIgnicao = "PontosDeIgnicao",
        EspacosDeMaestria = "EspacosDeMaestria", PontosDeMaestria = "PontosDeMaestria",
        MaxAtributo = "MaxAtributo", MaxPericia = "MaxPericia",
        MaxPassivasLivres = "MaxPassivasLivres", MaxPassivasVocacionais = "MaxPassivasVocacionais", MaxPassivasDeClasse = "MaxPassivasDeClasse",
        XpParaProximoNivel = "XpParaProximoNivel", EapBase = "EapBase";

    public sealed record Definicao(string Chave, string Nome, TipoDeColunaDeNivel Tipo, int Ordem);

    public static IReadOnlyList<Definicao> Sistema { get; } = new[]
    {
        new Definicao(PontosDeAtributo, "Pontos de Atributo", TipoDeColunaDeNivel.Acumulativa, 0),
        new Definicao(PontosDePericia, "Pontos de Perícia", TipoDeColunaDeNivel.Acumulativa, 1),
        new Definicao(EspacosDeCaracteristica, "Espaços de Característica", TipoDeColunaDeNivel.Acumulativa, 2),
        new Definicao(PontosDeIgnicao, "Pontos de Ignição", TipoDeColunaDeNivel.Acumulativa, 3),
        new Definicao(EspacosDeMaestria, "Espaços de Maestria", TipoDeColunaDeNivel.Acumulativa, 4),
        new Definicao(PontosDeMaestria, "Pontos de Maestria", TipoDeColunaDeNivel.Acumulativa, 5),
        new Definicao(MaxAtributo, "Máx. de Atributo", TipoDeColunaDeNivel.PorNivel, 6),
        new Definicao(MaxPericia, "Máx. de Perícia", TipoDeColunaDeNivel.PorNivel, 7),
        new Definicao(MaxPassivasLivres, "Máx. Passivas Livres", TipoDeColunaDeNivel.PorNivel, 8),
        new Definicao(MaxPassivasVocacionais, "Máx. Passivas Vocacionais", TipoDeColunaDeNivel.PorNivel, 9),
        new Definicao(MaxPassivasDeClasse, "Máx. Passivas De Classe", TipoDeColunaDeNivel.PorNivel, 10),
        new Definicao(XpParaProximoNivel, "XP para o próximo nível", TipoDeColunaDeNivel.PorNivel, 11),
        new Definicao(EapBase, "EAP base", TipoDeColunaDeNivel.PorNivel, 12),
    };

    public static bool SemHeranca(string? chave) => chave is XpParaProximoNivel or EapBase;

    public static string MaxPassivas(CategoriaDePassiva categoria) => categoria switch
    {
        CategoriaDePassiva.Livre => MaxPassivasLivres,
        CategoriaDePassiva.Vocacional => MaxPassivasVocacionais,
        CategoriaDePassiva.DeClasse => MaxPassivasDeClasse,
        _ => throw new ArgumentOutOfRangeException(nameof(categoria)),
    };
}
