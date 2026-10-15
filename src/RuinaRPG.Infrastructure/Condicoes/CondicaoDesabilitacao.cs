using RuinaRPG.Domain.Condicoes;

namespace RuinaRPG.Infrastructure.Condicoes;

public class CondicaoDesabilitacao
{
    public Guid Id { get; set; }
    public Guid CondicaoId { get; set; }
    public CategoriaDesabilitavel Categoria { get; set; }
}
