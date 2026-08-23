using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Items;

public class Arma : Item
{
    public string? Subcategoria { get; set; }
    public Tier? Tier { get; set; }
    public Empunhadura? Empunhadura { get; set; }
    public string? Dados { get; set; }
    public int? Dano { get; set; }
    public string? Critico { get; set; }
    public int? Alcance { get; set; }
    public TipoDeDano? TipoDeDano { get; set; }
    public string? RequisitoAtributo { get; set; }
    public int? DurabilidadeMaxima { get; set; }
}
