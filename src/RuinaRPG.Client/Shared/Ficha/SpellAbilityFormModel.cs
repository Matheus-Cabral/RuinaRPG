using RuinaRPG.Client.Shared.Fields;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// O formulário "adicionar Magia/Habilidade" da aba Magias &amp; Habilidades (as três fichas): do zero
/// (Nome/Tipo/Grau/Descrição/Efeitos) ou partindo de uma entrada do Banco de Magias.
/// </summary>
public class SpellAbilityFormModel
{
    public string Origem { get; set; } = "Zero";
    public string SourceBankEntryId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Tipo { get; set; } = "Magia";
    public int Grau { get; set; }
    public string Descricao { get; set; } = "";
    public List<EfeitoLinha> Efeitos { get; set; } = new();
}
