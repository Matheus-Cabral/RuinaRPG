using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Services;

/// <summary>
/// As Condições ativas de uma ficha: as de todo participante ligado a ela em encontros em andamento, não vencidas,
/// todas somadas. Uma consulta por ficha por requisição (o resultado fica guardado neste serviço, que é scoped).
/// NPC e Criatura do GM (sem dono) nunca têm Condição na ficha: as de um participante deles só valem no resumo do
/// encontro, por <see cref="DoParticipanteAsync"/>, que nenhum endpoint de ficha chama.
/// </summary>
public class CondicoesAtivasDaFicha(RuinaRpgDbContext db, IPericiaCatalogo pericias)
{
    private enum Origem { Personagem, Npc, Criatura, Participante }

    private static readonly IReadOnlyList<Aplicada> Nenhuma = [];

    private readonly Dictionary<(Origem, Guid), IReadOnlyList<Aplicada>> _porOrigem = [];

    private sealed record Aplicada(EncounterParticipantCondicao Condicao, int CurrentRound);

    public async Task<EfeitosDeCondicao> ParaPersonagemAsync(Guid sheetId) => Efeitos(await AtivasAsync(Origem.Personagem, sheetId));

    public async Task<EfeitosDeCondicao> ParaNpcAsync(NpcSheet sheet) =>
        sheet.OwnerId is null ? EfeitosDeCondicao.Nenhum : Efeitos(await AtivasAsync(Origem.Npc, sheet.Id));

    public async Task<EfeitosDeCondicao> ParaCriaturaAsync(CreatureSheet sheet) =>
        sheet.OwnerId is null ? EfeitosDeCondicao.Nenhum : Efeitos(await AtivasAsync(Origem.Criatura, sheet.Id));

    /// <summary>Só para o resumo do participante no encontro: vale também para NPC/Criatura do GM.</summary>
    public async Task<EfeitosDeCondicao> DoParticipanteAsync(Guid participantId) => Efeitos(await AtivasAsync(Origem.Participante, participantId));

    public async Task<List<CondicaoAtivaNaFichaResponse>> ListarDoPersonagemAsync(Guid sheetId) => await ListarAsync(await AtivasAsync(Origem.Personagem, sheetId));

    public async Task<List<CondicaoAtivaNaFichaResponse>> ListarDoNpcAsync(NpcSheet sheet) =>
        sheet.OwnerId is null ? [] : await ListarAsync(await AtivasAsync(Origem.Npc, sheet.Id));

    public async Task<List<CondicaoAtivaNaFichaResponse>> ListarDaCriaturaAsync(CreatureSheet sheet) =>
        sheet.OwnerId is null ? [] : await ListarAsync(await AtivasAsync(Origem.Criatura, sheet.Id));

    /// <summary>
    /// Uma consulta: as Condições dos participantes daquela origem em encontros em andamento. O prazo é conferido
    /// depois, com a mesma regra da resposta do encontro (<see cref="CondicaoAplicadaMapper.Ativa"/>). Sem Condição
    /// nenhuma — o caso de quase toda leitura de ficha — devolve a lista vazia compartilhada e nada mais é feito.
    /// </summary>
    private async Task<IReadOnlyList<Aplicada>> AtivasAsync(Origem origem, Guid id)
    {
        if (_porOrigem.TryGetValue((origem, id), out var guardadas))
            return guardadas;

        var participantes = origem switch
        {
            Origem.Personagem => db.EncounterParticipants.Where(p => p.SourceCharacterSheetId == id),
            Origem.Npc => db.EncounterParticipants.Where(p => p.SourceNpcSheetId == id),
            Origem.Criatura => db.EncounterParticipants.Where(p => p.SourceCreatureSheetId == id),
            _ => db.EncounterParticipants.Where(p => p.Id == id)
        };

        var linhas = await (
            from condicao in db.EncounterParticipantCondicoes.AsNoTracking()
            join participante in participantes on condicao.EncounterParticipantId equals participante.Id
            join encontro in db.Encounters on participante.EncounterId equals encontro.Id
            where encontro.Status == EncounterStatus.EmAndamento
            orderby condicao.Nome, condicao.Id
            select new { Condicao = condicao, encontro.Status, encontro.CurrentRound }).ToListAsync();

        var ativas = linhas
            .Where(l => CondicaoAplicadaMapper.Ativa(l.Condicao, l.Status, l.CurrentRound))
            .Select(l => new Aplicada(l.Condicao, l.CurrentRound))
            .ToList();
        return _porOrigem[(origem, id)] = ativas.Count == 0 ? Nenhuma : ativas;
    }

    private static EfeitosDeCondicao Efeitos(IReadOnlyList<Aplicada> ativas) => EfeitosDe(ativas.Select(a => a.Condicao).ToList());

    /// <summary>
    /// Os efeitos de um conjunto de Condições dado por quem chama, sem consulta nem cache: CondicoesDoEncontro calcula
    /// assim os totais com as Condições que estarão ativas depois da ação, antes de ela ser salva.
    /// </summary>
    public static EfeitosDeCondicao EfeitosDe(IReadOnlyCollection<EncounterParticipantCondicao> ativas) =>
        ativas.Count == 0 ? EfeitosDeCondicao.Nenhum : new EfeitosDeCondicao(ativas.Select(ComoAtiva).ToList());

    private static CondicaoAtiva ComoAtiva(EncounterParticipantCondicao c) => new(
        c.Id, c.Nome,
        c.Modificadores.Select(m => new ModificadorDeCondicao(m.Alvo, m.AlvoEspecifico, m.TipoDeValor, m.Valor, m.Momento)).ToList(),
        c.Desabilitacoes.ToDictionary(d => d.Key, d => (IReadOnlySet<Guid>)d.Value.ToHashSet()));

    private async Task<List<CondicaoAtivaNaFichaResponse>> ListarAsync(IReadOnlyList<Aplicada> ativas)
    {
        if (ativas.Count == 0)
            return [];

        // O texto cita a Perícia pelo nome; uma chave que saiu do catálogo aparece como está.
        var nomes = (await pericias.TodasAsync()).GroupBy(p => p.Chave, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Nome, StringComparer.OrdinalIgnoreCase);
        var explicacoes = Efeitos(ativas).Explicar(chave => nomes.GetValueOrDefault(chave, chave));

        // Explicar devolve uma explicação por Condição, na mesma ordem.
        return ativas.Select((a, i) => new CondicaoAtivaNaFichaResponse(a.Condicao.Nome, a.Condicao.Descricao, a.Condicao.Icone,
            DuracaoDeCondicao.TurnosRestantes(a.Condicao.AplicadaNoTurno, a.Condicao.DuracaoTurnos, CondicaoAplicadaMapper.TurnoDeContagem(a.CurrentRound)),
            explicacoes[i].Linhas.ToList())).ToList();
    }
}
