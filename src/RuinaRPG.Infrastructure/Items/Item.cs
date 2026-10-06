namespace RuinaRPG.Infrastructure.Items;

public abstract class Item
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public required string Nome { get; set; }
    public decimal Peso { get; set; }
    public int Preco { get; set; }
    public Guid? ImageId { get; set; }
    public string? Descricao { get; set; }

    /// <summary>Requisitos do equipamento (Arma, Armadura, Escudo, Artefato); null = sem requisitos. Item Geral nunca tem. jsonb.</summary>
    public RuinaRPG.Domain.SpellsAndAbilities.RequisitosDePassiva? Requisitos { get; set; }

    /// <summary>Penalidade aplicada enquanto os Requisitos não são cumpridos; null = nenhuma. jsonb.</summary>
    public RuinaRPG.Domain.Items.PenalidadeDeEquipamento? PenalidadeDeRequisitos { get; set; }
}
