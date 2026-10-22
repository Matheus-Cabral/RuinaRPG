namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>O formulário "Adicionar Item" do Inventário.</summary>
public sealed class InventoryFormModel
{
    public string ItemId { get; set; } = "";
    public int Qtd { get; set; } = 1;
}

/// <summary>O formulário "Adicionar Espólio".</summary>
public sealed class SpoilFormModel
{
    public string ItemId { get; set; } = "";
    public int Qtd { get; set; } = 1;
    public int DT { get; set; }
}

/// <summary>O formulário "Adicionar Artefato".</summary>
public sealed class ArtifactFormModel
{
    public string ArtifactItemId { get; set; } = "";
}

/// <summary>O formulário "Adicionar Afeição".</summary>
public sealed class AffectionFormModel
{
    public string Nome { get; set; } = "";
    public int Favorabilidade { get; set; }
}
