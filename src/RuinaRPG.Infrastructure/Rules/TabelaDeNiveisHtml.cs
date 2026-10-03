using System.Text;
using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// HTML do Livro de Regras para a Tabela de Níveis (Auditoria): uma tabela "Nível | Bônus" (o que cada
/// nível concede, um item por linha, exatamente <see cref="ProgressaoDeNivel.LinhasDeBonus"/>) e, abaixo,
/// "Limites e progressão" (só quando o Auditor liga a opção) com só as colunas "Por nível" que têm algum valor. Nomes de coluna e texto livre
/// vêm do Auditor, então tudo é codificado em HTML.
/// </summary>
public static class TabelaDeNiveisHtml
{
    private const string Vazio = "—";

    // Só <, >, &, " e ' são escapados — WebUtility/HtmlEncoder transformariam acentos e "+" em entidades numéricas.
    private static string Codificar(string texto) => texto
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    public static string Montar(ProgressaoDeNivel tabela, bool mostrarLimites)
    {
        var html = new StringBuilder("<table class=\"tabela-de-niveis tabela-de-niveis-bonus\"><thead><tr><th>Nível</th><th>Bônus</th></tr></thead><tbody>");
        foreach (var linha in tabela.Linhas)
        {
            var bonus = tabela.LinhasDeBonus(linha.Nivel);
            html.Append("<tr><td>").Append(linha.Nivel).Append("</td><td>")
                .Append(bonus.Count == 0 ? Vazio : string.Join("<br />", bonus.Select(b => Codificar(b))))
                .Append("</td></tr>");
        }
        html.Append("</tbody></table>");

        if (!mostrarLimites)
            return html.ToString();

        var limites = tabela.Colunas
            .Where(c => c.Tipo == TipoDeColunaDeNivel.PorNivel && tabela.Linhas.Any(l => l.Valores.GetValueOrDefault(c.Id) is not null))
            .ToList();
        if (limites.Count == 0)
            return html.ToString();

        html.Append("<h3>Limites e progressão</h3><table class=\"tabela-de-niveis tabela-de-niveis-limites\"><thead><tr><th>Nível</th>");
        foreach (var c in limites)
            html.Append("<th>").Append(Codificar(ChavesDeNivel.RotuloParaJogador(c.ChaveDeSistema, c.Nome))).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var linha in tabela.Linhas)
        {
            html.Append("<tr><td>").Append(linha.Nivel).Append("</td>");
            foreach (var c in limites)
                html.Append("<td>").Append(linha.Valores.GetValueOrDefault(c.Id)?.ToString() ?? Vazio).Append("</td>");
            html.Append("</tr>");
        }
        html.Append("</tbody></table>");
        return html.ToString();
    }
}
