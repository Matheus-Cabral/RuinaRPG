using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>
/// Leva ao log do encontro (Gerenciador de Encontros R0010) as mudanças de Vitalidade, Foco e Adrenalina de uma
/// ficha que participa de um encontro em andamento: uma linha RecursoDaFicha por participante ligado à ficha.
/// Parte das linhas que o SheetLogInterceptor já montou, então vale o mesmo recorte do log de ficha (Ficha de
/// Personagem, ou NPC/Criatura concedida a um jogador) — as fichas cujo participante lê os recursos ao vivo.
/// </summary>
internal static class EncounterLogDaFicha
{
    /// <summary>
    /// Roda em todo salvamento que mexe num recurso de ficha, o mais frequente de uma sessão de jogo: custa uma
    /// consulta, e nada além dela quando a ficha não está em nenhum encontro em andamento. Só monta as linhas; quem chama as acrescenta ao salvamento.
    /// </summary>
    public static async Task<List<EncounterLogEntry>> MontarAsync(RuinaRpgDbContext db, IReadOnlyList<SheetLogEntry> linhas, CancellationToken ct)
    {
        var recursos = linhas.Where(l => l is { Categoria: SheetLogCategoria.RecursoVFA, Acao: SheetLogAcao.Alterou }).ToList();
        if (recursos.Count == 0)
            return [];
        var personagens = recursos.Where(l => l.CharacterSheetId is not null).Select(l => l.CharacterSheetId!.Value).Distinct().ToList();
        var npcs = recursos.Where(l => l.NpcSheetId is not null).Select(l => l.NpcSheetId!.Value).Distinct().ToList();
        var criaturas = recursos.Where(l => l.CreatureSheetId is not null).Select(l => l.CreatureSheetId!.Value).Distinct().ToList();

        // A fotografia do encontro (Turno e de quem é a vez) vem na mesma consulta. A vez pode estar vazia, ou
        // apontar para outro participante que não o da ficha: daí a junção à esquerda.
        var participantes = await (
            from p in db.EncounterParticipants.AsNoTracking()
            join e in db.Encounters on p.EncounterId equals e.Id
            join v in db.EncounterParticipants on e.CurrentParticipantId equals (Guid?)v.Id into daVez
            from vez in daVez.DefaultIfEmpty()
            where e.Status == EncounterStatus.EmAndamento
                && ((p.SourceCharacterSheetId != null && personagens.Contains(p.SourceCharacterSheetId.Value))
                    || (p.SourceNpcSheetId != null && npcs.Contains(p.SourceNpcSheetId.Value))
                    || (p.SourceCreatureSheetId != null && criaturas.Contains(p.SourceCreatureSheetId.Value)))
            select new
            {
                p.Nome, p.EncounterId, p.SourceCharacterSheetId, p.SourceNpcSheetId, p.SourceCreatureSheetId,
                e.CurrentRound, e.CurrentParticipantId, VezNome = (string?)vez.Nome,
            }).ToListAsync(ct);
        if (participantes.Count == 0)
            return [];

        var doEncontro = new List<EncounterLogEntry>();
        foreach (var recurso in recursos)
            foreach (var p in participantes)
            {
                var daFicha = recurso.CharacterSheetId is { } personagem ? p.SourceCharacterSheetId == personagem
                    : recurso.NpcSheetId is { } npc ? p.SourceNpcSheetId == npc
                    : p.SourceCreatureSheetId == recurso.CreatureSheetId;
                if (!daFicha)
                    continue;
                doEncontro.Add(new EncounterLogEntry
                {
                    Id = Guid.NewGuid(),
                    Ordem = EncounterLogOrdem.Proxima(),
                    EncounterId = p.EncounterId,
                    OccurredAt = recurso.OccurredAt,
                    Turno = p.CurrentRound,
                    VezParticipantId = p.CurrentParticipantId,
                    VezNome = p.VezNome,
                    Tipo = EncounterLogTipo.RecursoDaFicha,
                    ParticipanteNome = p.Nome,
                    Campo = recurso.Campo,
                    ValorAntes = recurso.ValorAntes,
                    ValorDepois = recurso.ValorDepois,
                });
            }
        return doEncontro;
    }
}
