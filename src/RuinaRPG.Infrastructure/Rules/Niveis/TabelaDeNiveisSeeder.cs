using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Domain.Rules.ReferenceData;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules.Niveis;

/// <summary>
/// Garante as colunas de sistema e, só com a tabela vazia, semeia as linhas a partir do Markdown da
/// Tabela de Níveis e das tabelas de XP/EAP. Nunca sobrescreve: o Auditor de Regras passa a ser o dono.
/// </summary>
public static class TabelaDeNiveisSeeder
{
    public static async Task<int> SeedAsync(RuinaRpgDbContext db, string niveisMarkdown,
        IReadOnlyList<XpPorNivel> xp, IReadOnlyList<EapPorNivel> eap)
    {
        var criadas = 0;
        var colunas = await db.ColunasDeNivel.Where(c => c.ChaveDeSistema != null).ToListAsync();
        foreach (var def in ChavesDeNivel.Sistema)
        {
            if (colunas.Any(c => c.ChaveDeSistema == def.Chave)) continue;
            var nova = new ColunaDeNivel { Id = Guid.NewGuid(), Nome = def.Nome, Tipo = def.Tipo, ChaveDeSistema = def.Chave, Ordem = def.Ordem };
            db.ColunasDeNivel.Add(nova);
            colunas.Add(nova);
            criadas++;
        }
        await db.SaveChangesAsync();

        if (await db.NiveisProgressao.AnyAsync()) return criadas;

        Guid IdDe(string chave) => colunas.Single(c => c.ChaveDeSistema == chave).Id;
        var xpPorNivel = xp.ToDictionary(x => x.Nivel);
        var eapPorNivel = eap.ToDictionary(e => e.Nivel);

        foreach (var lb in NivelBonusParser.Parse(niveisMarkdown))
        {
            var extracao = NivelBonusExtractor.Extrair(lb.BonusText);
            db.NiveisProgressao.Add(new NivelProgressao { Nivel = lb.Nivel, OutrosBonus = string.Join("\n", extracao.Restante) });
            foreach (var (chave, valor) in extracao.Valores)
                db.ValoresDeNivel.Add(new ValorDeNivel { Nivel = lb.Nivel, ColunaId = IdDe(chave), Valor = valor });

            if (xpPorNivel.TryGetValue(lb.Nivel, out var x) && int.TryParse(x.XpAbsoluto, out var xpValor))
                db.ValoresDeNivel.Add(new ValorDeNivel { Nivel = lb.Nivel, ColunaId = IdDe(ChavesDeNivel.XpParaProximoNivel), Valor = xpValor });
            if (eapPorNivel.TryGetValue(lb.Nivel, out var e))
                db.ValoresDeNivel.Add(new ValorDeNivel { Nivel = lb.Nivel, ColunaId = IdDe(ChavesDeNivel.EapBase), Valor = e.ValorAbsoluto });
            criadas++;
        }
        await db.SaveChangesAsync();
        return criadas;
    }
}
