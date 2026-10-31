using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Domain.Condicoes;

public static class CondicaoValidator
{
    private static readonly string[] SubAtributos =
    [
        SubAtributoAlvo.Iniciativa, SubAtributoAlvo.Movimentacao, SubAtributoAlvo.EsquivaNatural,
        SubAtributoAlvo.DefesaNatural, SubAtributoAlvo.ReducaoFisica, SubAtributoAlvo.ReducaoMagica,
        SubAtributoAlvo.Adrenalina
    ];

    /// <summary>
    /// Null when valid; otherwise the Portuguese message for the first problem. <paramref name="elementoExiste"/> diz se o
    /// alvo específico de Afinidade/Dano elemental é o id (em texto) de um elemento do catálogo que não é Caminho.
    /// </summary>
    public static string? Validar(string nome, IReadOnlyList<ModificadorDeCondicao> modificadores, IReadOnlyList<CategoriaDesabilitavel> desabilitacoes, int? duracaoPadraoTurnos, Func<string, bool> periciaExiste, Func<string, bool> elementoExiste)
    {
        if (string.IsNullOrWhiteSpace(nome))
            return "O nome da condição é obrigatório.";
        if (modificadores.Count == 0 && desabilitacoes.Count == 0)
            return "A condição precisa de ao menos um modificador ou uma desabilitação.";
        if (duracaoPadraoTurnos is < 1)
            return "A duração padrão deve ser de pelo menos 1 turno.";
        if (desabilitacoes.Distinct().Count() != desabilitacoes.Count)
            return "A mesma categoria não pode ser desabilitada duas vezes.";

        foreach (var m in modificadores)
        {
            if ((m.Alvo == AlvoDeCondicao.RecursoAtual) != m.Momento.HasValue)
                return "O momento é obrigatório para o valor atual de um recurso e não se aplica aos demais alvos.";
            var erroValor = ValidarValor(m);
            if (erroValor is not null)
                return erroValor;
            var erroAlvo = ValidarAlvoEspecifico(m, periciaExiste, elementoExiste);
            if (erroAlvo is not null)
                return erroAlvo;
        }
        return null;
    }

    /// <summary>
    /// Os limites do valor de uma linha, pelo tipo dela; null quando válido. Vale também para o valor que o GM ajusta ao
    /// aplicar a Condição no encontro.
    /// </summary>
    public static string? ValidarValor(ModificadorDeCondicao m)
    {
        if (m.Valor == 0)
            return "O valor de um modificador não pode ser zero.";
        if (m.TipoDeValor == TipoDeValorDeCondicao.Percentual && m.Valor is < -100 or > 1000)
            return "Um modificador percentual deve estar entre -100 e 1000.";
        return null;
    }

    private static string? ValidarAlvoEspecifico(ModificadorDeCondicao m, Func<string, bool> periciaExiste, Func<string, bool> elementoExiste)
    {
        var especifico = m.AlvoEspecifico;
        var vazio = string.IsNullOrWhiteSpace(especifico);
        switch (m.Alvo)
        {
            case AlvoDeCondicao.Atributo:
                return !vazio && Nome<Atributo>(especifico!) ? null : "O atributo do modificador é inválido.";
            case AlvoDeCondicao.Pericia:
            case AlvoDeCondicao.Maestria:
                return vazio || periciaExiste(especifico!) ? null : "A perícia do modificador não existe.";
            case AlvoDeCondicao.SubAtributo:
                return !vazio && SubAtributos.Contains(especifico!) ? null : "O sub-atributo do modificador é inválido.";
            case AlvoDeCondicao.RecursoTotal:
            case AlvoDeCondicao.RecursoAtual:
                return !vazio && Nome<RecursoDeCondicao>(especifico!) ? null : "O recurso do modificador é inválido.";
            case AlvoDeCondicao.AfinidadeElemental:
            case AlvoDeCondicao.DanoElemental:
                return vazio || elementoExiste(especifico!) ? null : "O elemento do modificador é inválido.";
            case AlvoDeCondicao.Dano:
                return vazio || Nome<TipoDeDano>(especifico!) ? null : "O tipo de dano do modificador é inválido.";
            default:
                return "O alvo do modificador é inválido.";
        }
    }

    // Enum.IsDefined(Type, string) only matches names, so a numeric string such as "1" is rejected.
    private static bool Nome<T>(string valor) where T : struct, Enum => Enum.GetNames<T>().Contains(valor);
}
