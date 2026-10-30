using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.Afinidades;

/// <summary>
/// Converte as linhas de Afinidade do formato antigo (2.1.0) em pontos por elemento do catálogo.
/// Cada Elemento/Caminho fica com o maior valor em que aparecia; cada Sub-Elemento soma os valores das suas
/// linhas, nunca abaixo de 0 (o gasto da ficha não muda); a Experiência vai ao Sub-Elemento da linha ou, sem ele, à Essência 1,
/// e duas linhas para o mesmo elemento ficam com a maior.
/// </summary>
public static class ConversaoDeLinhas
{
    public static IReadOnlyDictionary<int, (int Pontos, int? Experiencia)> Converter(IEnumerable<LinhaDeAfinidade> linhas)
    {
        var pontos = new Dictionary<int, int>();
        var experiencia = new Dictionary<int, int>();
        var somaDeSub = new Dictionary<int, int>();

        foreach (var l in linhas)
        {
            int? essencia1 = l.Elemento is { } el ? IdDe(el) : null;
            if (essencia1 is { } e1) Maior(pontos, e1, l.ElementoValor ?? 0);
            if (l.SegundaEssencia is { } s) Maior(pontos, IdDe(s), l.SegundaEssenciaValor ?? 0);

            int? sub = l.SubElemento is { } se ? IdDe(se) : null;
            if (sub is { } sid) somaDeSub[sid] = somaDeSub.GetValueOrDefault(sid) + (l.SubElementoValor ?? 0);

            if (l.Experiencia is { } exp && (sub ?? essencia1) is { } destino)
                Maior(experiencia, destino, exp);
        }

        foreach (var (id, valor) in somaDeSub) pontos[id] = Math.Max(0, valor);
        foreach (var id in experiencia.Keys) pontos.TryAdd(id, 0);

        return pontos
            .Where(p => p.Value > 0 || experiencia.ContainsKey(p.Key))
            .ToDictionary(p => p.Key, p => (p.Value, experiencia.TryGetValue(p.Key, out var x) ? (int?)x : null));
    }

    private static void Maior(Dictionary<int, int> d, int id, int valor) =>
        d[id] = Math.Max(d.GetValueOrDefault(id), valor);

    // Ids do catálogo (= AfinidadeElemental; Mundano = 18).
    private static int IdDe(Elemento e) => e switch
    {
        Elemento.Terra => 0, Elemento.Agua => 1, Elemento.Fogo => 2, Elemento.Ar => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(e))
    };

    private static int IdDe(EssenciaBasica e) => e switch
    {
        EssenciaBasica.Ar => 3, EssenciaBasica.Agua => 1, EssenciaBasica.Fogo => 2, EssenciaBasica.Terra => 0,
        EssenciaBasica.Alma => 9, EssenciaBasica.Vida => 14, EssenciaBasica.Mundano => 18,
        _ => throw new ArgumentOutOfRangeException(nameof(e))
    };

    private static int IdDe(SubElemento s) => s switch
    {
        SubElemento.Gelo => 4, SubElemento.Flora => 5, SubElemento.Ferro => 6, SubElemento.Raio => 7,
        SubElemento.Prever => 8, SubElemento.Alma => 9, SubElemento.Purificar => 10, SubElemento.Ecomancia => 11,
        SubElemento.Hemomancia => 12, SubElemento.Curar => 13, SubElemento.Vida => 14, SubElemento.Aprimorar => 15,
        SubElemento.Necromancia => 16, SubElemento.Invocacao => 17,
        _ => throw new ArgumentOutOfRangeException(nameof(s))
    };
}
