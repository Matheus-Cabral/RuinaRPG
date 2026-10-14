using RuinaRPG.Domain.Condicoes;

namespace RuinaRPG.Infrastructure.Condicoes;

public class CondicaoModificador
{
    public Guid Id { get; set; }
    public Guid CondicaoId { get; set; }
    public int Ordem { get; set; }
    public AlvoDeCondicao Alvo { get; set; }
    public string? AlvoEspecifico { get; set; }
    public TipoDeValorDeCondicao TipoDeValor { get; set; }
    public int Valor { get; set; }
    public MomentoDeCondicao? Momento { get; set; }
}
