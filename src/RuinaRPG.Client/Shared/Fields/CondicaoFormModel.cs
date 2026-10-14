namespace RuinaRPG.Client.Shared.Fields;

/// <summary>Uma linha de modificador em edição. Os valores são os nomes dos enums, como a API recebe; Especifico vazio = "Todos/Todas".</summary>
public class CondicaoLinhaForm
{
    public string Alvo { get; set; } = "Atributo";
    public string Especifico { get; set; } = "Forca";
    public string TipoDeValor { get; set; } = "Fixo";
    public int Valor { get; set; } = 1;
    public string? Momento { get; set; }
}

/// <summary>A Condição em edição no formulário de Auditoria → Condições; Id nulo = nova.</summary>
public class CondicaoFormModel
{
    public string? Id { get; set; }
    public string Nome { get; set; } = "";
    public string Descricao { get; set; } = "";
    public string Icone { get; set; } = "Marcado";
    public int? DuracaoPadraoTurnos { get; set; }
    public List<CondicaoLinhaForm> Linhas { get; set; } = new();
    public IReadOnlyCollection<string> Categorias { get; set; } = new List<string>();
}
