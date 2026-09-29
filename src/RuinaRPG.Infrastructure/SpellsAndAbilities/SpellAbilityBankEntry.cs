using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.SpellsAndAbilities;

public class SpellAbilityBankEntry
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public SpellAbilityTipo Tipo { get; set; }
    public int Grau { get; set; }
    public int GastoEmPI { get; set; }
    public int Custo { get; set; }
    public required string Descricao { get; set; }

    /// <summary>Magia/Habilidade de Criatura — marcada pelo GM no banco, ou automaticamente quando a
    /// entrada nasce de uma Ficha de Criatura. Só serve de filtro no Banco.</summary>
    public bool DeCriatura { get; set; }
    public List<SpellAbilityBankEffect> Efeitos { get; set; } = [];

    /// <summary>Só em Passivas (Tipo = Passiva); nulo nos demais tipos.</summary>
    public CategoriaDePassiva? Categoria { get; set; }

    /// <summary>Só em Passivas; gravado como jsonb. Nulo = sem requisitos.</summary>
    public RequisitosDePassiva? Requisitos { get; set; }
}
