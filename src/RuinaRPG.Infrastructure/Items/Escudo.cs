using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Escudo : Item
{
    public string? Subcategoria { get; set; }
    public RankDeItem? Rank { get; set; }
    public CategoriaProtecao? Categoria { get; set; }
    public int? BonusDefesa { get; set; }
}
