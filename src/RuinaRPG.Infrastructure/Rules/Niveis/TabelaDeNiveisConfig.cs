namespace RuinaRPG.Infrastructure.Rules.Niveis;

/// <summary>
/// Configuração da Tabela de Níveis: linha única (Id sempre 1; sem linha = tudo no padrão).
/// MostrarLimitesNoLivro decide se o Livro de Regras exibe a tabela "Limites e progressão".
/// </summary>
public class TabelaDeNiveisConfig
{
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;
    public bool MostrarLimitesNoLivro { get; set; }
}
