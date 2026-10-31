using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Services;

/// <summary>
/// Os pontos por elemento de uma ficha de Personagem ou NPC (2.c) e o orçamento deles (R0007): gasto = soma dos
/// pontos; máximo = total do Círculo atual na tabela do Auditor (mágicas) + Afinidade Adicional. Uma conta só para o
/// cabeçalho da ficha, a lista de Afinidades e a recusa do PUT. <c>TPontos</c> é a tabela de pontos do tipo.
/// </summary>
public class AfinidadesDaFicha(RuinaRpgDbContext db, CatalogoDeAfinidadesService catalogo, GraduacaoDaFicha graduacao)
{
    /// <summary>As linhas de pontos da ficha, rastreadas, pelo id do elemento.</summary>
    public async Task<Dictionary<int, TPontos>> LinhasAsync<TPontos>(Guid sheetId) where TPontos : class, IPontosDeAfinidadeDaFicha =>
        await db.Set<TPontos>().Where(p => p.SheetId == sheetId).ToDictionaryAsync(p => p.ElementoId);

    /// <summary>Os pontos da ficha por elemento (só leitura).</summary>
    public async Task<Dictionary<int, int>> PontosAsync<TPontos>(Guid sheetId) where TPontos : class, IPontosDeAfinidadeDaFicha =>
        await db.Set<TPontos>().AsNoTracking().Where(p => p.SheetId == sheetId).ToDictionaryAsync(p => p.ElementoId, p => p.Pontos);

    /// <summary>O máximo de pontos da ficha (R0007), com o Círculo de <see cref="GraduacaoDaFicha"/>.</summary>
    public async Task<int> MaximoAsync(IFichaComAfinidades f) => await MaximoAsync(f, (await graduacao.CalcularAsync(f)).Graduacao);

    /// <summary>O máximo com o Círculo já calculado por quem chama.</summary>
    public async Task<int> MaximoAsync(IFichaComAfinidades f, int circulo) =>
        RegrasDeAfinidade.Maximo(await catalogo.CarregarAsync(), f.Vocacao, circulo, f.AfinidadeAdicional);

    /// <summary>Gasto e máximo, para o cabeçalho da ficha (VocacaoArcanaGasta/VocacaoArcanaMaxima), com o Círculo já calculado.</summary>
    public async Task<(int Gasto, int Maximo)> OrcamentoAsync<TPontos>(IFichaComAfinidades f, int circulo) where TPontos : class, IPontosDeAfinidadeDaFicha =>
        (RegrasDeAfinidade.Gasto(await PontosAsync<TPontos>(f.Id)), await MaximoAsync(f, circulo));
}
