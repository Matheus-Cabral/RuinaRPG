using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;

namespace RuinaRPG.Client.Shared;

/// <summary>Uma escolha de um select: o <see cref="Valor"/> que a API recebe e o <see cref="Rotulo"/> em português.</summary>
public record OpcaoDeCondicao(string Valor, string Rotulo);

/// <summary>
/// As escolhas do formulário de Condições (Alvo, "específico", tipo de valor, momento, categorias).
/// Os valores são os nomes dos enums, como a API espera; os rótulos são em português com acento.
/// </summary>
public static class CondicaoAlvos
{
    public static IReadOnlyList<OpcaoDeCondicao> Alvos { get; } =
    [
        new(nameof(AlvoDeCondicao.Atributo), "Atributo"),
        new(nameof(AlvoDeCondicao.Pericia), "Perícia"),
        new(nameof(AlvoDeCondicao.SubAtributo), "Sub-atributo"),
        new(nameof(AlvoDeCondicao.RecursoTotal), "Total (máximo) de um recurso"),
        new(nameof(AlvoDeCondicao.RecursoAtual), "Valor atual de um recurso (dano ou cura por vez)"),
        new(nameof(AlvoDeCondicao.AfinidadeElemental), "Afinidade elemental"),
        new(nameof(AlvoDeCondicao.DanoElemental), "Dano elemental dos ataques"),
        new(nameof(AlvoDeCondicao.Dano), "Modificador de dano dos ataques"),
        new(nameof(AlvoDeCondicao.Maestria), "Maestria")
    ];

    public static IReadOnlyList<OpcaoDeCondicao> Tipos { get; } =
    [
        new(nameof(TipoDeValorDeCondicao.Fixo), "Fixo"),
        new(nameof(TipoDeValorDeCondicao.Percentual), "Percentual")
    ];

    public static IReadOnlyList<OpcaoDeCondicao> Momentos { get; } =
    [
        new(nameof(MomentoDeCondicao.AoAplicar), "Ao aplicar"),
        new(nameof(MomentoDeCondicao.ACadaVez), "A cada vez do participante")
    ];

    public static IReadOnlyList<OpcaoDeCondicao> Categorias { get; } =
    [
        new(nameof(CategoriaDesabilitavel.Arma), "Armas"),
        new(nameof(CategoriaDesabilitavel.Armadura), "Armaduras"),
        new(nameof(CategoriaDesabilitavel.Escudo), "Escudos"),
        new(nameof(CategoriaDesabilitavel.Artefato), "Artefatos"),
        new(nameof(CategoriaDesabilitavel.Magia), "Magias"),
        new(nameof(CategoriaDesabilitavel.Habilidade), "Habilidades"),
        new(nameof(CategoriaDesabilitavel.Runa), "Runas"),
        new(nameof(CategoriaDesabilitavel.Passiva), "Passivas")
    ];

    private static readonly OpcaoDeCondicao[] SubAtributos =
        new[]
        {
            SubAtributoAlvo.Iniciativa, SubAtributoAlvo.Movimentacao, SubAtributoAlvo.EsquivaNatural,
            SubAtributoAlvo.DefesaNatural, SubAtributoAlvo.ReducaoFisica, SubAtributoAlvo.ReducaoMagica,
            SubAtributoAlvo.Adrenalina
        }.Select(s => new OpcaoDeCondicao(s, s)).ToArray();

    private static readonly OpcaoDeCondicao[] Recursos =
        Enum.GetNames<RecursoDeCondicao>().Select(r => new OpcaoDeCondicao(r, r)).ToArray();

    private static readonly OpcaoDeCondicao[] Elementos =
    [
        new(nameof(Elemento.Ar), "Ar"), new(nameof(Elemento.Agua), "Água"),
        new(nameof(Elemento.Fogo), "Fogo"), new(nameof(Elemento.Terra), "Terra")
    ];

    private static readonly OpcaoDeCondicao[] TiposDeDano =
        Enum.GetNames<RuinaRPG.Domain.Items.TipoDeDano>().Select(t => new OpcaoDeCondicao(t, t)).ToArray();

    /// <summary>Atributo, sub-atributo e recurso exigem uma escolha; os demais aceitam "Todos/Todas" (valor vazio).</summary>
    public static bool ExigeEspecifico(AlvoDeCondicao alvo) =>
        alvo is AlvoDeCondicao.Atributo or AlvoDeCondicao.SubAtributo or AlvoDeCondicao.RecursoTotal or AlvoDeCondicao.RecursoAtual;

    /// <summary>As escolhas do select "específico" para o Alvo; a escolha geral, quando existe, é a primeira, com valor vazio.</summary>
    public static IReadOnlyList<OpcaoDeCondicao> Especificos(AlvoDeCondicao alvo, IEnumerable<PericiaResponse> pericias) => alvo switch
    {
        AlvoDeCondicao.Atributo => Enum.GetNames<Atributo>().Select(a => new OpcaoDeCondicao(a, AtributoDisplay.Label(a))).ToList(),
        AlvoDeCondicao.Pericia or AlvoDeCondicao.Maestria =>
            [new OpcaoDeCondicao("", "Todas"), .. pericias.Select(p => new OpcaoDeCondicao(p.Chave, p.Nome))],
        AlvoDeCondicao.SubAtributo => SubAtributos,
        AlvoDeCondicao.RecursoTotal or AlvoDeCondicao.RecursoAtual => Recursos,
        AlvoDeCondicao.AfinidadeElemental or AlvoDeCondicao.DanoElemental => [new OpcaoDeCondicao("", "Todos"), .. Elementos],
        AlvoDeCondicao.Dano => [new OpcaoDeCondicao("", "Todos"), .. TiposDeDano],
        _ => []
    };
}
