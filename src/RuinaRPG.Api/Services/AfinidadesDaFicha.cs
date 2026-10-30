using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Os pontos por elemento de uma ficha de Personagem ou NPC (2.c) e o orçamento deles (R0007): gasto = soma dos
/// pontos; máximo = total do Círculo atual na tabela do Auditor (mágicas) + Afinidade Adicional. Uma conta só para o
/// cabeçalho da ficha, a lista de Afinidades e a recusa do PUT. <c>TPontos</c> é a tabela de pontos do tipo.
/// </summary>
public class AfinidadesDaFicha(RuinaRpgDbContext db, CatalogoDeAfinidadesService catalogo, ITabelaDeNiveis tabelaDeNiveis, IRulesDataProvider rules)
{
    /// <summary>As linhas de pontos da ficha, rastreadas, pelo id do elemento.</summary>
    public async Task<Dictionary<int, TPontos>> LinhasAsync<TPontos>(Guid sheetId) where TPontos : class, IPontosDeAfinidadeDaFicha =>
        await db.Set<TPontos>().Where(p => p.SheetId == sheetId).ToDictionaryAsync(p => p.ElementoId);

    /// <summary>Os pontos da ficha por elemento (só leitura).</summary>
    public async Task<Dictionary<int, int>> PontosAsync<TPontos>(Guid sheetId) where TPontos : class, IPontosDeAfinidadeDaFicha =>
        await db.Set<TPontos>().AsNoTracking().Where(p => p.SheetId == sheetId).ToDictionaryAsync(p => p.ElementoId, p => p.Pontos);

    /// <summary>O Círculo (Grau, nas marciais) da ficha, o mesmo de 1.b; sem Vocação, 0.</summary>
    public async Task<int> CirculoAsync(IFichaComAfinidades f)
    {
        if (f.Vocacao is not { } vocacao)
            return 0;
        var tabela = await tabelaDeNiveis.ObterAsync();
        var eapAtual = EapCalculator.Compute(f.Nivel, f.NucleosRankF, f.NucleosRankE, f.NucleosRankD, f.NucleosRankC, f.NucleosRankB, f.NucleosRankA, f.NucleosRankS, tabela.ComoEapPorNivel());
        return GraduacaoCalculator.Compute(vocacao, eapAtual, f.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);
    }

    /// <summary>O máximo de pontos da ficha (R0007).</summary>
    public async Task<int> MaximoAsync(IFichaComAfinidades f) =>
        RegrasDeAfinidade.Maximo(await catalogo.CarregarAsync(), f.Vocacao, await CirculoAsync(f), f.AfinidadeAdicional);

    /// <summary>Gasto e máximo, para o cabeçalho da ficha (VocacaoArcanaGasta/VocacaoArcanaMaxima).</summary>
    public async Task<(int Gasto, int Maximo)> OrcamentoAsync<TPontos>(IFichaComAfinidades f) where TPontos : class, IPontosDeAfinidadeDaFicha =>
        (RegrasDeAfinidade.Gasto(await PontosAsync<TPontos>(f.Id)), await MaximoAsync(f));
}
