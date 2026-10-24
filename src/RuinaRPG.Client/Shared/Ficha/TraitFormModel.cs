namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>O "Adicionar Característica" de Informações Básicas: a característica do catálogo e a especificação, se exigida.</summary>
public class TraitFormModel
{
    public string TraitId { get; set; } = "";
    public string? Especificacao { get; set; }
}
