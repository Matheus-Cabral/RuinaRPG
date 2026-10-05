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
    /// <summary>Imagem opcional (só na origem "do zero"); vazio = sem imagem.</summary>
    public string ImageId { get; set; } = "";
    /// <summary>"Arcana", "Negra" ou "" (sem tipo). Só vale na origem "do zero"; do banco o tipo vem da entrada.</summary>
    public string Tipo { get; set; } = "";
    /// <summary>Nome do enum DisciplinaDeRuna ("Adicao"...) ou "" (não escolhida). Obrigatória na origem "do zero".</summary>
    public string Disciplina { get; set; } = "";

    /// <summary>O Tipo no formato do contrato: null quando sem tipo.</summary>
    public string? TipoOuNulo => string.IsNullOrEmpty(Tipo) ? null : Tipo;

    public bool DoBanco => Origem == "Banco";

    /// <summary>Mensagem de erro em português se o formulário não pode ser enviado; null se está válido.</summary>
    public string? Validar()
    {
        if (DoBanco)
            return string.IsNullOrWhiteSpace(SourceBankEntryId) ? "Escolha uma entrada do Banco de Runas." : null;

        if (string.IsNullOrWhiteSpace(Nome))
            return "Informe o nome da runa.";

        return string.IsNullOrEmpty(Disciplina) ? "Escolha a Disciplina da runa." : null;
    }

    public void Limpar()
    {
        Origem = "Zero";
        SourceBankEntryId = "";
        Nome = "";
        Descricao = "";
        Grau = 0;
        ImageId = "";
        Tipo = "";
        Disciplina = "";
    }
}
