using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Afinidades da ficha por pontos (Ficha de Personagem 2.c e R0007; Personagem e NPC): a lista de elementos com
/// gasto e máximo, e o PUT dos pontos e da Experiência de um elemento. A rota
/// (<c>api/&lt;tipo&gt;-sheets/{sheetId}/afinidades</c>) fica na subclasse de cada tipo; quem é recusado pelas regras
/// é um fato do tipo (<see cref="ITipoDeFicha{TSheet}.AfinidadesRecusamForaDasRegras"/>).
/// </summary>
public abstract class AfinidadesDaFichaController<TSheet, TPontos>(
    RuinaRpgDbContext db, ITipoDeFicha<TSheet> tipo, AfinidadesDaFicha afinidades, CatalogoDeAfinidadesService catalogo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFichaComAfinidades
    where TPontos : class, IPontosDeAfinidadeDaFicha, new()
{
    [HttpGet]
    public async Task<ActionResult<AfinidadesDaFichaResponse>> Listar(Guid sheetId)
    {
        if ((await FichaParaLeituraAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        return await RespostaAsync(ficha, await catalogo.CarregarAsync());
    }

    /// <summary>
    /// Grava os pontos e a Experiência de um elemento. Um valor inválido (elemento fora do catálogo, negativo) é
    /// recusado para todos; as regras de 2.c só para quem o tipo diz (o GM de um NPC não). Mudar só a Experiência
    /// nunca é recusado pelas regras (os pontos não mudam).
    /// </summary>
    [HttpPut("{elementoId:int}")]
    public async Task<ActionResult<AfinidadesDaFichaResponse>> Atualizar(Guid sheetId, int elementoId, AtualizarAfinidadeRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var c = await catalogo.CarregarAsync();
        if (RegrasDeAfinidade.MotivoDeValorInvalido(c, elementoId, request.Pontos, request.Experiencia) is { } invalido)
            return BadRequest(invalido);

        var aplicaRegras = Tipo.AfinidadesRecusamForaDasRegras(ficha, CurrentUserId());
        // Dois PUTs simultâneos no mesmo elemento novo criam a mesma linha: o segundo bate no índice único
        // (SheetId, ElementoId) e repete uma vez, relendo as linhas — aí a do outro já existe e vira atualização.
        for (var tentativa = 1; ; tentativa++)
        {
            var linhas = await afinidades.LinhasAsync<TPontos>(sheetId);
            var linha = linhas.GetValueOrDefault(elementoId);
            if (await MotivoParaRecusarAsync(c, ficha, aplicaRegras, Pontos(linhas), elementoId, request.Pontos) is { } recusa)
                return BadRequest(recusa);

            Gravar(linha, sheetId, elementoId, request);
            try
            {
                await db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (linha is null && tentativa == 1)
            {
                db.ChangeTracker.Clear();
            }
        }

        return await RespostaAsync(ficha, c);
    }

    /// <summary>
    /// As regras de 2.c — inclusive não gravar num elemento que a lista esconde — só para quem o tipo diz. O GM de um
    /// NPC grava mesmo num elemento escondido, e a linha passa a aparecer na lista, marcada: nenhuma linha fica oculta.
    /// </summary>
    private async Task<string?> MotivoParaRecusarAsync(CatalogoDeAfinidades c, TSheet ficha, bool aplicaRegras, Dictionary<int, int> pontos, int elementoId, int novoValor) =>
        aplicaRegras
            ? RegrasDeAfinidade.MotivoParaRecusarMudanca(c, ficha.Vocacao, ficha.SubVocacao, pontos, elementoId, novoValor, await afinidades.MaximoAsync(ficha))
            : null;

    /// <summary>Uma linha zerada e sem Experiência sai da tabela: só ficam os elementos em que a ficha tem algo.</summary>
    private void Gravar(TPontos? linha, Guid sheetId, int elementoId, AtualizarAfinidadeRequest request)
    {
        var vazia = request.Pontos == 0 && request.Experiencia is null;
        if (linha is null)
        {
            if (!vazia)
                db.Set<TPontos>().Add(new TPontos { Id = Guid.NewGuid(), SheetId = sheetId, ElementoId = elementoId, Pontos = request.Pontos, Experiencia = request.Experiencia });
            return;
        }
        if (vazia)
        {
            db.Set<TPontos>().Remove(linha);
            return;
        }
        linha.Pontos = request.Pontos;
        linha.Experiencia = request.Experiencia;
    }

    private async Task<AfinidadesDaFichaResponse> RespostaAsync(TSheet ficha, CatalogoDeAfinidades c)
    {
        var linhas = await afinidades.LinhasAsync<TPontos>(ficha.Id);
        var pontos = Pontos(linhas);
        var efeitos = await Tipo.CondicoesAsync(ficha);

        var elementos = RegrasDeAfinidade.Elementos(c, ficha.Vocacao, ficha.SubVocacao, pontos)
            .Select(e => new ElementoDaFichaResponse(e.Elemento.Id, e.Elemento.Nome, e.Elemento.Tipo.ToString(), e.Elemento.Teto, e.Pontos,
                linhas.GetValueOrDefault(e.Elemento.Id)?.Experiencia, e.Bloqueado, e.Motivo, efeitos.NaAfinidade(e.Elemento.Id, e.Pontos) ?? e.Pontos))
            .ToList();

        return new AfinidadesDaFichaResponse(RegrasDeAfinidade.Gasto(pontos), await afinidades.MaximoAsync(ficha),
            RegrasDeAfinidade.SecaoVisivel(ficha.Vocacao, ficha.AfinidadeAdicional, ficha.PossuiCoracaoDeMana), elementos);
    }

    private static Dictionary<int, int> Pontos(Dictionary<int, TPontos> linhas) => linhas.ToDictionary(l => l.Key, l => l.Value.Pontos);
}
