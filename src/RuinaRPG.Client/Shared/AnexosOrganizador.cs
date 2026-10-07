using System.Globalization;
using RuinaRPG.Contracts.Campaigns;

namespace RuinaRPG.Client.Shared;

/// <summary>
/// Um anexo de campanha como a lista agrupada o enxerga — a forma comum à lista do GM
/// (CampaignAttachmentResponse) e à do jogador (PublicAttachmentSummary). Publico é null quando a
/// tela não distingue público de privado (a do jogador, que só recebe o que é público).
/// </summary>
public record AnexoView(string Id, string Tipo, string? Nome, string? ImageUrl, AttachmentFacets? Facets, bool? Publico);

public record GrupoDeAnexos(string Tipo, string Titulo, IReadOnlyList<AnexoView> Anexos);

/// <summary>Os critérios de filtro de um grupo; null (ou Nome em branco) = critério desligado.</summary>
public class AnexoFiltro
{
    public string? Nome { get; set; }
    public bool? Publico { get; set; }
    public string? ItemTipo { get; set; }
    public string? Subcategoria { get; set; }
    public string? EntradaTipo { get; set; }
    public int? Grau { get; set; }
    public string? Disciplina { get; set; }

    public bool Ativo => !string.IsNullOrWhiteSpace(Nome) || Publico is not null || ItemTipo is not null
        || Subcategoria is not null || EntradaTipo is not null || Grau is not null || Disciplina is not null;
}

/// <summary>
/// Campanha R0006/R0009: os anexos da campanha aparecem agrupados por tipo, numa ordem fixa, em ordem
/// alfabética dentro do grupo, e cada grupo é filtrável pelos critérios do seu tipo.
/// </summary>
public static class AnexosOrganizador
{
    public const string TipoImagem = "Image";

    private static readonly (string Tipo, string Titulo)[] Ordem =
    [
        ("Item", "Itens"), ("SpellAbilityBankEntry", "Magias/Habilidades"), ("RuneBankEntry", "Runas"),
        (TipoImagem, "Imagens"), ("NpcSheet", "NPCs"), ("CreatureSheet", "Criaturas"),
    ];

    // Mesma escolha de PassivasDoLivroOrganizador: cultura invariante com IgnoreNonSpace, que o ICU
    // embarcado no Blazor WebAssembly atende sem depender de um pt-BR que pode não estar lá.
    private static readonly CompareInfo Comparador = CultureInfo.InvariantCulture.CompareInfo;
    private const CompareOptions SemCaixaNemAcento = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static IReadOnlyList<GrupoDeAnexos> Agrupar(IEnumerable<AnexoView> anexos)
    {
        var porTipo = anexos.GroupBy(a => a.Tipo).ToDictionary(g => g.Key, g => g.ToList());

        // Um Tipo que esta lista ainda não conhece ganha um grupo próprio no fim, em vez de sumir.
        var desconhecidos = porTipo.Keys.Where(t => Ordem.All(o => o.Tipo != t)).Order().Select(t => (Tipo: t, Titulo: t));

        return Ordem.Concat(desconhecidos)
            .Where(o => porTipo.ContainsKey(o.Tipo))
            .Select(o => new GrupoDeAnexos(o.Tipo, o.Titulo, Ordenar(o.Tipo, porTipo[o.Tipo])))
            .ToList();
    }

    // Uma Imagem não tem nome próprio (o que vem em Nome é o caminho do arquivo), então a galeria
    // fica na ordem em que as imagens foram anexadas. Nos demais grupos, quem não tem nome (NPC ou
    // Criatura só com a imagem pública) vai para o fim.
    private static IReadOnlyList<AnexoView> Ordenar(string tipo, List<AnexoView> anexos) =>
        tipo == TipoImagem
            ? anexos
            : anexos.OrderBy(a => a.Nome is null)
                .ThenBy(a => a.Nome, Comparer<string?>.Create((x, y) => Comparador.Compare(x, y, SemCaixaNemAcento)))
                .ToList();

    public static IReadOnlyList<AnexoView> Filtrar(IEnumerable<AnexoView> anexos, AnexoFiltro filtro) =>
        anexos.Where(a =>
            (string.IsNullOrWhiteSpace(filtro.Nome) || (a.Nome is not null && Comparador.IndexOf(a.Nome, filtro.Nome.Trim(), SemCaixaNemAcento) >= 0))
            && (filtro.Publico is null || a.Publico == filtro.Publico)
            && (filtro.ItemTipo is null || a.Facets?.ItemTipo == filtro.ItemTipo)
            && (filtro.Subcategoria is null || a.Facets?.Subcategoria == filtro.Subcategoria)
            && (filtro.EntradaTipo is null || a.Facets?.EntradaTipo == filtro.EntradaTipo)
            && (filtro.Grau is null || a.Facets?.Grau == filtro.Grau)
            && (filtro.Disciplina is null || a.Facets?.Disciplina == filtro.Disciplina))
        .ToList();

    /// <summary>Os valores distintos de uma faceta presentes no grupo — são as opções do seu filtro.</summary>
    public static IReadOnlyList<string> Opcoes(IEnumerable<AnexoView> anexos, Func<AttachmentFacets, string?> faceta) =>
        anexos.Select(a => a.Facets is null ? null : faceta(a.Facets))
            .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!)
            .Distinct().OrderBy(v => v, Comparer<string>.Create((x, y) => Comparador.Compare(x, y, SemCaixaNemAcento)))
            .ToList();

    public static IReadOnlyList<int> Opcoes(IEnumerable<AnexoView> anexos, Func<AttachmentFacets, int?> faceta) =>
        anexos.Select(a => a.Facets is null ? null : faceta(a.Facets))
            .Where(v => v is not null).Select(v => v!.Value)
            .Distinct().Order().ToList();
}
