using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Infrastructure.Rules.Niveis;

/// <summary>Uma coluna da Tabela de Níveis: de sistema (ChaveDeSistema preenchida) ou criada pelo Auditor.</summary>
public class ColunaDeNivel
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public TipoDeColunaDeNivel Tipo { get; set; }
    public string? ChaveDeSistema { get; set; }
    public int Ordem { get; set; }
    public bool IsDeleted { get; set; }
}
