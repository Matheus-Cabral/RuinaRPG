using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Escudo : Item
{
    public CategoriaProtecao? Categoria { get; set; }
    public int? BonusDefesa { get; set; }
    public string? Penalidade { get; set; }
    public int? RequisitoVigor { get; set; }
    public int? DurabilidadeMaxima { get; set; }
}
