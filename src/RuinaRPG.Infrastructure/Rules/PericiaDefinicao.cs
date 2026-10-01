using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>Uma perícia do sistema (Requisitos - Auditoria de Regras R0012). Nunca é apagada de verdade — IsDeleted.</summary>
public class PericiaDefinicao
{
    public int Id { get; set; }
    /// <summary>Identificador estável usado na API e no Alvo de Artefatos; nunca muda depois de criado.</summary>
    public required string Chave { get; set; }
    public required string Nome { get; set; }
    public string? Descricao { get; set; }
    public Atributo? AtributoSugerido { get; set; }
    public bool DisponivelParaCriaturas { get; set; }
    public bool IsDeleted { get; set; }
}
