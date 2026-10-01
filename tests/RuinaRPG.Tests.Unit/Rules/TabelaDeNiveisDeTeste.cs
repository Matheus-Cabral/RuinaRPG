using RuinaRPG.Domain.Rules.Niveis;

namespace RuinaRPG.Tests.Unit.Rules;

/// <summary>Monta uma ProgressaoDeNivel pequena, só com as colunas de sistema, para os testes de calculadoras.</summary>
public static class TabelaDeNiveisDeTeste
{
    public sealed record Linha(int Nivel, (string Chave, int Valor)[] Valores);

    public static Linha Nivel(int nivel, params (string Chave, int Valor)[] valores) => new(nivel, valores);

    public static ProgressaoDeNivel Criar(params Linha[] linhas)
    {
        var colunas = ChavesDeNivel.Sistema
            .Select(d => new ColunaDeNivelDef(Guid.NewGuid(), d.Nome, d.Tipo, d.Chave, d.Ordem))
            .ToList();
        var porChave = colunas.ToDictionary(c => c.ChaveDeSistema!, c => c.Id);

        return new ProgressaoDeNivel(colunas, linhas
            .Select(l => new LinhaDeNivel(l.Nivel, null, l.Valores.ToDictionary(v => porChave[v.Chave], v => (int?)v.Valor)))
            .ToList());
    }
}
