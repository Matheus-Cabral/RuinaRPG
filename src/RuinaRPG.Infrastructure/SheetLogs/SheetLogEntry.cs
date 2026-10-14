namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>Uma linha do log de ficha (Campanha R0016). Exatamente uma das três FKs de ficha é preenchida.</summary>
public class SheetLogEntry
{
    public Guid Id { get; set; }
    public Guid? CharacterSheetId { get; set; }
    public Guid? NpcSheetId { get; set; }
    public Guid? CreatureSheetId { get; set; }
    public DateTime OccurredAt { get; set; }
    public Guid ActorUserId { get; set; }
    public required string ActorNickname { get; set; }
    public bool ActorIsGm { get; set; }
    public SheetLogCategoria Categoria { get; set; }
    public SheetLogAcao Acao { get; set; }
    public required string Secao { get; set; }
    public string? Campo { get; set; }
    public string? ItemNome { get; set; }
    public string? ValorAntes { get; set; }
    public string? ValorDepois { get; set; }
    /// <summary>Escrita que o servidor fez por conta própria dentro da ação do autor (corte do Atual por uma Condição, expiração).</summary>
    public bool Automatico { get; set; }
}
