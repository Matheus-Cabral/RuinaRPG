namespace RuinaRPG.Infrastructure.Rules.Niveis;

/// <summary>Célula da Tabela de Níveis (nível x coluna); Valor nulo = célula vazia.</summary>
public class ValorDeNivel
{
    public int Nivel { get; set; }
    public Guid ColunaId { get; set; }
    public int? Valor { get; set; }
}
