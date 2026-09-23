using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public class SubcategoriaOption
{
    public Guid Id { get; set; }
    public ItemTipo Tipo { get; set; }
    public SubcategoriaFacet Facet { get; set; }
    public required string Valor { get; set; }
    public bool IsDeleted { get; set; }
}
