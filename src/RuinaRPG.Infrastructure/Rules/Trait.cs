using RuinaRPG.Domain.Enums;

namespace RuinaRPG.Infrastructure.Rules;

public class Trait
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    public int Custo { get; set; }
    public Polaridade Polaridade { get; set; }
    public bool RequerEspecificacao { get; set; }
}
