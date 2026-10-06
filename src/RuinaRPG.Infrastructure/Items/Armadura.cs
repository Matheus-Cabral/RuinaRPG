using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Armadura : Item
{
    public string? Subcategoria { get; set; }
    public RankDeItem? Rank { get; set; }
    public CategoriaProtecao? Categoria { get; set; }
    public int? Defesa { get; set; }
    public int? RF { get; set; }
    public int? RM { get; set; }
}
