namespace RuinaRPG.Infrastructure.Rules.Niveis;

/// <summary>Uma linha (nível) da Tabela de Níveis. Os valores numéricos ficam em <see cref="ValorDeNivel"/>.</summary>
public class NivelProgressao
{
    public int Nivel { get; set; }
    public string? OutrosBonus { get; set; }
}
