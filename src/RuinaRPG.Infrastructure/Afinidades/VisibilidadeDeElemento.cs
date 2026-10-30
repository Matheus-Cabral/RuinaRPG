namespace RuinaRPG.Infrastructure.Afinidades;

public enum OrigemDaVisibilidade
{
    Vocacao,
    Classe
}

/// <summary>
/// Um Caminho ou Sub-Elemento que uma Vocação ou Classe vê (Auditoria de Regras R0021). <see cref="Chave"/> é o
/// identificador estável: o nome do membro do enum <c>Vocacao</c> ou o nome da Classe como gravado na ficha.
/// </summary>
public class VisibilidadeDeElemento
{
    public OrigemDaVisibilidade Origem { get; set; }
    public required string Chave { get; set; }
    public int ElementoId { get; set; }
}
