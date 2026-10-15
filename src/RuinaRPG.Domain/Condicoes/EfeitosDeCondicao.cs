using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Domain.Condicoes;

/// <summary>
/// Converte as Condições ativas em ajustes nos números de uma ficha. Não limita o resultado
/// (quem chama decide o piso de cada valor).
/// </summary>
public sealed class EfeitosDeCondicao
{
    public static readonly EfeitosDeCondicao Nenhum = new([]);

    private readonly IReadOnlyList<CondicaoAtiva> _ativas;

    public EfeitosDeCondicao(IReadOnlyList<CondicaoAtiva> ativas) => _ativas = ativas;

    public bool Vazio => _ativas.Count == 0;

    /// <summary>
    /// floor((valorBase + fixos) × (100 + percentuais) / 100), com piso matemático (rumo a menos infinito).
    /// A soma dos percentuais nunca passa de −100: várias reduções juntas zeram o valor, não invertem o sinal.
    /// A linha casa quando o Alvo é igual e o AlvoEspecifico é nulo ou igual ao pedido (ignora caixa);
    /// um pedido sem alvo específico só casa com linhas gerais. Linhas de RecursoAtual nunca entram.
    /// Uma linha de Maestria restrita (<see cref="ModificadorDeCondicao.Maestrias"/>) só casa quando
    /// <paramref name="linhaDaFicha"/> é uma das Maestrias escolhidas.
    /// </summary>
    public int Ajustar(AlvoDeCondicao alvo, string? alvoEspecifico, int valorBase, Guid? linhaDaFicha = null)
    {
        if (alvo == AlvoDeCondicao.RecursoAtual)
            return valorBase;

        long fixos = 0, percentuais = 0;
        foreach (var linha in _ativas.SelectMany(a => a.Modificadores))
        {
            if (!Casa(linha, alvo, alvoEspecifico, linhaDaFicha))
                continue;
            if (linha.TipoDeValor == TipoDeValorDeCondicao.Fixo)
                fixos += linha.Valor;
            else
                percentuais += linha.Valor;
        }

        if (percentuais == 0)
            return (int)(valorBase + fixos);
        percentuais = Math.Max(percentuais, -100);
        return (int)PisDaDivisao((valorBase + fixos) * (100 + percentuais), 100);
    }

    /// <summary>O mesmo resultado de <see cref="Ajustar"/>, como o delta a somar ao valor base.</summary>
    public int Delta(AlvoDeCondicao alvo, string? alvoEspecifico, int valorBase) =>
        Ajustar(alvo, alvoEspecifico, valorBase) - valorBase;

    /// <summary>Nome da primeira Condição que desabilita o item; null se nenhuma.</summary>
    public string? DesabilitadoPor(CategoriaDesabilitavel categoria, Guid itemId)
    {
        foreach (var condicao in _ativas)
        {
            if (condicao.Desabilitacoes.TryGetValue(categoria, out var itens) && (itens.Count == 0 || itens.Contains(itemId)))
                return condicao.Nome;
        }
        return null;
    }

    /// <param name="nomeDaMaestria">O nome de uma linha de Maestria da ficha; null se não a conhece.</param>
    public IReadOnlyList<ExplicacaoDeCondicao> Explicar(Func<string, string>? nomeDaPericia = null, Func<Guid, string?>? nomeDaMaestria = null) =>
        _ativas.Select(c => new ExplicacaoDeCondicao(c.Nome, LinhasDe(c, nomeDaPericia ?? (k => k), nomeDaMaestria ?? (_ => null)))).ToList();

    public IReadOnlyList<(CondicaoAtiva Condicao, ModificadorDeCondicao Linha)> AjustesDeRecursoAtual(MomentoDeCondicao momento) =>
        _ativas
            .SelectMany(c => c.Modificadores.Select(l => (Condicao: c, Linha: l)))
            .Where(x => x.Linha.Alvo == AlvoDeCondicao.RecursoAtual && x.Linha.Momento == momento)
            .ToList();

    private static bool Casa(ModificadorDeCondicao linha, AlvoDeCondicao alvo, string? alvoEspecifico, Guid? linhaDaFicha) =>
        linha.Alvo == alvo
        && (linha.AlvoEspecifico is null
            || string.Equals(linha.AlvoEspecifico, alvoEspecifico, StringComparison.OrdinalIgnoreCase))
        && (linha.Maestrias is null || (linhaDaFicha is { } id && linha.Maestrias.Contains(id)));

    // Divisão com piso verdadeiro: o "/" do C# trunca rumo a zero.
    internal static long PisDaDivisao(long numerador, long divisor)
    {
        var q = numerador / divisor;
        return numerador % divisor != 0 && (numerador < 0) != (divisor < 0) ? q - 1 : q;
    }

    private static List<string> LinhasDe(CondicaoAtiva condicao, Func<string, string> nomeDaPericia, Func<Guid, string?> nomeDaMaestria)
    {
        var linhas = condicao.Modificadores.Select(m => TextoDaLinha(m, nomeDaPericia, nomeDaMaestria)).ToList();

        var categorias = condicao.Desabilitacoes.OrderBy(d => d.Key).Select(d =>
            d.Value.Count == 0 ? RotulosPt.Plural(d.Key) : $"{RotulosPt.Plural(d.Key)} (itens escolhidos)").ToList();
        if (categorias.Count > 0)
            linhas.Add($"Desabilita: {string.Join(", ", categorias)}");
        return linhas;
    }

    private static string TextoDaLinha(ModificadorDeCondicao m, Func<string, string> nomeDaPericia, Func<Guid, string?> nomeDaMaestria)
    {
        var valor = Formatar(m);
        var e = m.AlvoEspecifico;
        if (m.Alvo == AlvoDeCondicao.Maestria && m.Maestrias is { } escolhidas)
        {
            // Restrita na aplicação: cita as Maestrias da ficha pelo nome (em ordem alfabética), quando os conhece.
            var nomes = escolhidas.Select(nomeDaMaestria).OfType<string>().Order(StringComparer.CurrentCulture).ToList();
            return nomes.Count == 0 ? $"Maestrias escolhidas {valor}"
                : $"{(nomes.Count == 1 ? "Maestria" : "Maestrias")} {string.Join(", ", nomes)} {valor}";
        }
        return m.Alvo switch
        {
            AlvoDeCondicao.Atributo => $"{RotulosPt.Atributo(e)} {valor}",
            AlvoDeCondicao.Pericia => e is null ? $"Todas as Perícias {valor}" : $"{nomeDaPericia(e)} {valor}",
            AlvoDeCondicao.Maestria => e is null ? $"Todas as Maestrias {valor}" : $"Maestria de {nomeDaPericia(e)} {valor}",
            AlvoDeCondicao.SubAtributo => $"{e} {valor}",
            AlvoDeCondicao.RecursoTotal => $"{RotulosPt.Recurso(e)} total {valor}",
            // O percentual de um valor atual é uma fração do total do recurso (DuracaoDeCondicao.ValorDoAjuste).
            AlvoDeCondicao.RecursoAtual => $"{RotulosPt.Recurso(e)} atual {valor}{(m.TipoDeValor == TipoDeValorDeCondicao.Percentual ? " do total" : "")} {(m.Momento == MomentoDeCondicao.ACadaVez ? "a cada vez" : "ao aplicar")}",
            AlvoDeCondicao.AfinidadeElemental => e is null ? $"Todas as Afinidades {valor}" : $"Afinidade de {RotulosPt.Elemento(e)} {valor}",
            AlvoDeCondicao.DanoElemental => e is null ? $"Todo dano elemental {valor}" : $"Dano de {RotulosPt.Elemento(e)} {valor}",
            AlvoDeCondicao.Dano => e is null ? $"Dano {valor}" : $"Dano {RotulosPt.TipoDeDano(e)} {valor}",
            _ => $"{e} {valor}"
        };
    }

    private static string Formatar(ModificadorDeCondicao m)
    {
        var sinal = m.Valor < 0 ? "−" : "+";
        var sufixo = m.TipoDeValor == TipoDeValorDeCondicao.Percentual ? "%" : "";
        return $"{sinal}{Math.Abs(m.Valor)}{sufixo}";
    }

    /// <summary>Nomes em português (com acento) dos enums que o texto das Condições cita.</summary>
    private static class RotulosPt
    {
        private static readonly Dictionary<Atributo, string> Atributos = new()
        {
            [CharacterSheets.Atributo.Forca] = "Força",
            [CharacterSheets.Atributo.Agilidade] = "Agilidade",
            [CharacterSheets.Atributo.Destreza] = "Destreza",
            [CharacterSheets.Atributo.Vigor] = "Vigor",
            [CharacterSheets.Atributo.Astucia] = "Astúcia",
            [CharacterSheets.Atributo.Instinto] = "Instinto",
            [CharacterSheets.Atributo.Vontade] = "Vontade",
            [CharacterSheets.Atributo.Influencia] = "Influência"
        };

        private static readonly Dictionary<Elemento, string> Elementos = new()
        {
            [CharacterSheets.Elemento.Ar] = "Ar",
            [CharacterSheets.Elemento.Agua] = "Água",
            [CharacterSheets.Elemento.Fogo] = "Fogo",
            [CharacterSheets.Elemento.Terra] = "Terra"
        };

        private static readonly Dictionary<TipoDeDano, string> TiposDeDano = new()
        {
            [Items.TipoDeDano.Cortante] = "Cortante",
            [Items.TipoDeDano.Perfurante] = "Perfurante",
            [Items.TipoDeDano.Contundente] = "Contundente",
            [Items.TipoDeDano.Arcano] = "Arcano"
        };

        private static readonly Dictionary<CategoriaDesabilitavel, string> Categorias = new()
        {
            [CategoriaDesabilitavel.Arma] = "Armas",
            [CategoriaDesabilitavel.Armadura] = "Armaduras",
            [CategoriaDesabilitavel.Escudo] = "Escudos",
            [CategoriaDesabilitavel.Artefato] = "Artefatos",
            [CategoriaDesabilitavel.Magia] = "Magias",
            [CategoriaDesabilitavel.Habilidade] = "Habilidades",
            [CategoriaDesabilitavel.Runa] = "Runas",
            [CategoriaDesabilitavel.Passiva] = "Passivas"
        };

        public static string Plural(CategoriaDesabilitavel categoria) => Categorias[categoria];

        public static string Atributo(string? nome) =>
            Enum.TryParse<Atributo>(nome, out var a) && Atributos.TryGetValue(a, out var t) ? t : nome ?? "";

        public static string Elemento(string nome) =>
            Enum.TryParse<Elemento>(nome, out var e) && Elementos.TryGetValue(e, out var t) ? t : nome;

        public static string TipoDeDano(string nome) =>
            Enum.TryParse<TipoDeDano>(nome, out var d) && TiposDeDano.TryGetValue(d, out var t) ? t : nome;

        // Vitalidade, Foco, Adrenalina e Estresse já são escritos em português sem acento.
        public static string Recurso(string? nome) => nome ?? "";
    }
}

public static class DuracaoDeCondicao
{
    /// <summary>Turnos que faltam; null quando a Condição não tem prazo. Aplicada na preparação (turno 0) conta a partir do Turno 1.</summary>
    public static int? TurnosRestantes(int aplicadaNoTurno, int? duracao, int turnoAtual) =>
        duracao is null ? null : Math.Max(0, Math.Max(aplicadaNoTurno, 1) + duracao.Value - turnoAtual);

    public static bool Expirou(int aplicadaNoTurno, int? duracao, int turnoAtual) =>
        duracao is not null && turnoAtual >= Math.Max(aplicadaNoTurno, 1) + duracao.Value;

    /// <summary>O valor de uma linha de RecursoAtual: o fixo, ou o piso de total × percentual / 100.</summary>
    public static int ValorDoAjuste(ModificadorDeCondicao linha, int totalDoRecurso) =>
        linha.TipoDeValor == TipoDeValorDeCondicao.Fixo
            ? linha.Valor
            : (int)EfeitosDeCondicao.PisDaDivisao((long)totalDoRecurso * linha.Valor, 100);
}
