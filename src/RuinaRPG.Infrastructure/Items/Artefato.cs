using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Artefato : Item
{
    public string? Subcategoria { get; set; }
    public TipoDeAlvo? TipoDeAlvo { get; set; }
    public string? Alvo { get; set; }
    public int? Valor { get; set; }
}
