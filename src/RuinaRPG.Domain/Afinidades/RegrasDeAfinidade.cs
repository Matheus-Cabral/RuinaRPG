using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.Afinidades;

public sealed record ElementoNaFicha(ElementoDoCatalogo Elemento, int Pontos, bool Bloqueado, string? Motivo);

/// <summary>Regras puras de Afinidades por pontos (Ficha de Personagem 2.c; Auditoria de Regras R0018–R0021).</summary>
public static class RegrasDeAfinidade
{
    public static int Gasto(IReadOnlyDictionary<int, int> pontos) => pontos.Values.Sum();

    /// <summary>Mágicas: total do Círculo + Afinidade Adicional; marciais e sem Vocação: só a Adicional.</summary>
    public static int Maximo(CatalogoDeAfinidades c, Vocacao? v, int circulo, int afinidadeAdicional) =>
        (VocacaoArcanaCalculator.EhMagica(v) ? c.TotalPorCirculo.GetValueOrDefault(circulo) : 0) + afinidadeAdicional;

    public static bool SecaoVisivel(Vocacao? v, int afinidadeAdicional, bool coracaoDeMana) =>
        VocacaoArcanaCalculator.EhMagica(v) || afinidadeAdicional > 0 || coracaoDeMana;

    public static IReadOnlyList<ElementoNaFicha> Elementos(
        CatalogoDeAfinidades c, Vocacao? v, string? classe, IReadOnlyDictionary<int, int> pontos)
    {
        var resultado = new List<ElementoNaFicha>();
        foreach (var e in c.Elementos)
        {
            var p = pontos.GetValueOrDefault(e.Id);
            var visivel = Visivel(c, v, classe, e);
            if (!visivel && p == 0) continue;

            var motivo = !visivel ? "Indisponível para esta Vocação/Classe"
                : MotivoDeRequisito(c, e, pontos)
                  ?? (e.Teto is { } teto && p > teto ? $"Acima do teto ({teto})" : null);
            resultado.Add(new ElementoNaFicha(e, p, motivo is not null, motivo));
        }
        return resultado;
    }

    /// <summary>Null = permitido.</summary>
    public static string? MotivoParaRecusarMudanca(
        CatalogoDeAfinidades c, Vocacao? v, string? classe, IReadOnlyDictionary<int, int> pontos,
        int elementoId, int novoValor, int maximo)
    {
        var elemento = c.Elementos.FirstOrDefault(e => e.Id == elementoId);
        if (elemento is null) return "Elemento inexistente.";
        if (novoValor < 0) return "O valor não pode ser negativo.";

        var atual = pontos.GetValueOrDefault(elementoId);
        if (novoValor == atual) return null;

        if (novoValor < atual)
        {
            foreach (var x in c.Cruzamentos.Where(x => x.ElementoA == elementoId || x.ElementoB == elementoId))
            {
                var minimo = x.ElementoA == elementoId ? x.MinimoA : x.MinimoB;
                if (novoValor < minimo && pontos.GetValueOrDefault(x.SubElemento) > 0)
                {
                    var sub = NomeDe(c, x.SubElemento);
                    return $"{sub} depende de {elemento.Nome}: zere {sub} antes.";
                }
            }
            return null;
        }

        if (!Visivel(c, v, classe, elemento)) return $"{elemento.Nome} não está disponível para esta Vocação/Classe.";
        if (MotivoDeRequisito(c, elemento, pontos) is { } bloqueio) return bloqueio;
        if (elemento.Teto is { } teto && novoValor > teto) return $"{elemento.Nome} tem teto de {teto} pontos.";
        if (Gasto(pontos) - atual + novoValor > maximo) return $"Pontos acima do máximo de {maximo}.";
        return null;
    }

    private static bool Visivel(CatalogoDeAfinidades c, Vocacao? v, string? classe, ElementoDoCatalogo e) =>
        e.Tipo == TipoDeElemento.Base
        || (v is not null && !VocacaoArcanaCalculator.EhMagica(v))
        || c.Visibilidade.Any(x => x.ElementoId == e.Id
            && ((x.Vocacao is not null && v is not null && x.Vocacao == v.ToString())
                || (x.Classe is not null && classe is not null && x.Classe == classe)));

    private static string? MotivoDeRequisito(CatalogoDeAfinidades c, ElementoDoCatalogo e, IReadOnlyDictionary<int, int> pontos)
    {
        if (e.Tipo != TipoDeElemento.SubElemento) return null;
        var cruzamentos = c.Cruzamentos.Where(x => x.SubElemento == e.Id).ToList();
        if (cruzamentos.Count == 0) return null;
        var naoCumprido = cruzamentos.Where(x =>
            pontos.GetValueOrDefault(x.ElementoA) < x.MinimoA || pontos.GetValueOrDefault(x.ElementoB) < x.MinimoB).ToList();
        if (naoCumprido.Count < cruzamentos.Count) return null;
        var primeiro = naoCumprido[0];
        return $"Precisa de {primeiro.MinimoA} em {NomeDe(c, primeiro.ElementoA)} e {primeiro.MinimoB} em {NomeDe(c, primeiro.ElementoB)}";
    }

    private static string NomeDe(CatalogoDeAfinidades c, int id) =>
        c.Elementos.FirstOrDefault(e => e.Id == id)?.Nome ?? id.ToString();
}
