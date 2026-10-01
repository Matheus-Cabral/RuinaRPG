namespace RuinaRPG.Domain.CharacterSheets;

public sealed record PericiaInicial(int Id, string Chave, string Nome, bool DisponivelParaCriaturas);

/// <summary>
/// As 39 perícias com que a tabela Pericias nasce (migration AddPericias) — Id e Chave são os do
/// antigo enum Pericia, e precisam continuar iguais: colunas int, o jsonb dos requisitos de Passiva
/// e o Alvo textual dos Artefatos gravados antes da migração apontam para eles. Depois do seed, a
/// fonte da verdade é a tabela (editada na Auditoria); esta lista só serve ao seed e ao parser de
/// Históricos, que lê os nomes originais do Markdown.
/// </summary>
public static class PericiasIniciais
{
    public static IReadOnlyList<PericiaInicial> Todas { get; } =
    [
        new(0, "Acrobacia", "Acrobacia", true),
        new(1, "Alquimia", "Alquimia", false),
        new(2, "Arcano", "Arcano", false),
        new(3, "Armadilhas", "Armadilhas", false),
        new(4, "ArmasBrancas", "Armas Brancas", false),
        new(5, "ArtefatosMagicos", "Artefatos Mágicos", true),
        new(6, "Artistico", "Artístico", false),
        new(7, "Atletismo", "Atletismo", true),
        new(8, "Avaliacao", "Avaliação", false),
        new(9, "Biblioteca", "Biblioteca", false),
        new(10, "Brigar", "Brigar", true),
        new(11, "Conducao", "Condução", false),
        new(12, "Conhecimentos", "Conhecimentos", false),
        new(13, "Crime", "Crime", false),
        new(14, "EmpatiaComAnimais", "Empatia c/ Animais", true),
        new(15, "Enganacao", "Enganação", true),
        new(16, "ForcaDeVontade", "Força de Vontade", true),
        new(17, "Fortitude", "Fortitude", true),
        new(18, "Furtividade", "Furtividade", true),
        new(19, "Herborismo", "Herborismo", false),
        new(20, "Intimidacao", "Intimidação", true),
        new(21, "Intuicao", "Intuição", true),
        new(22, "Investigacao", "Investigação", true),
        new(23, "Labia", "Lábia", false),
        new(24, "Lideranca", "Liderança", false),
        new(25, "Linguistica", "Linguística", false),
        new(26, "Medicina", "Medicina", false),
        new(27, "Navegacao", "Navegação", true),
        new(28, "Ocultismo", "Ocultismo", true),
        new(29, "Oficio", "Ofício", false),
        new(30, "Percepcao", "Percepção", true),
        new(31, "Pontaria", "Pontaria", true),
        new(32, "Prontidao", "Prontidão", true),
        new(33, "Reflexos", "Reflexos", true),
        new(34, "Religiao", "Religião", false),
        new(35, "Saquear", "Saquear", false),
        new(36, "Seducao", "Sedução", true),
        new(37, "SensoComum", "Senso Comum", false),
        new(38, "Sobrevivencia", "Sobrevivência", true),
    ];

    private static readonly Dictionary<string, int> PorNome = Todas.ToDictionary(p => p.Nome, p => p.Id);

    public static int IdPorNome(string nome) => PorNome[nome];
}
