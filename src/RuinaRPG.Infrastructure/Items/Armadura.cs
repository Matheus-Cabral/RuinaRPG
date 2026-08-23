using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Armadura : Item
{
    public CategoriaProtecao? Categoria { get; set; }
    public int? Defesa { get; set; }
    public int? RF { get; set; }
    public int? RM { get; set; }
    public string? Penalidade { get; set; }
    public int? RequisitoVigor { get; set; }
    public int? DurabilidadeMaxima { get; set; }
}
