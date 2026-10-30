namespace RuinaRPG.Infrastructure.Afinidades;

/// <summary>Máximo de pontos de afinidade de um Círculo (Auditoria de Regras R0018).</summary>
public class TotalDeAfinidadePorCirculo
{
    public int Circulo { get; set; }
    public int Total { get; set; }
}
