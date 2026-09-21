namespace RuinaRPG.Client.Shared.Fields;

/// <summary>
/// Estado do formulário "adicionar Runa" em 4.d (Personagem e NPC): de onde a Runa vem — montada do
/// zero (Nome/Descrição/Grau) ou partindo de uma entrada do Banco de Runas (SourceBankEntryId).
/// </summary>
public class RuneOrigemModel
{
    public string Origem { get; set; } = "Zero";
    public string SourceBankEntryId { get; set; } = "";
    public string Nome { get; set; } = "";
    public string Descricao { get; set; } = "";
    public int Grau { get; set; }

    public bool DoBanco => Origem == "Banco";

    public void Limpar()
    {
        Origem = "Zero";
        SourceBankEntryId = "";
        Nome = "";
        Descricao = "";
        Grau = 0;
    }
}
