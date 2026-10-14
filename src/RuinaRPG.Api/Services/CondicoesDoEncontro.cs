using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Api.Services;

/// <summary>
/// O que acontece quando as Condições ativas de participantes mudam (aplicar, remover, expirar, iniciar, finalizar,
/// reabrir, remover o participante). Só conta o participante ligado a uma ficha que muda com elas: Ficha de
/// Personagem, ou NPC/Criatura concedida a um jogador. NPC/Criatura do GM não tem ficha a cortar nem dono a avisar.
/// <list type="number">
/// <item><see cref="AoMudarAtivacaoAsync"/>, ANTES do salvamento da ação: corta o Atual guardado acima do total
/// recalculado com as Condições que estarão ativas depois da ação (corte permanente, marcado como automático no log
/// da ficha) e registra no log da ficha "Condição X aplicada/removida/expirada". Tudo entra no mesmo SaveChanges da
/// ação, então o token de concorrência do encontro (409) protege também isto.</item>
/// <item><see cref="NotificarAsync"/>, DEPOIS do salvamento: CondicoesAlteradas ao dono de cada ficha e
/// ParticipantsChanged aos encontros dela. Uma falha dos hubs vai para o log da aplicação e nunca derruba a ação.</item>
/// </list>
/// Os totais "depois da ação" saem do ChangeTracker (Condições acrescentadas e removidas, status e Turno do encontro
/// já alterados, participante removido) e não de CondicoesAtivasDaFicha, que lê o banco e guarda o resultado na requisição.
/// </summary>
public class CondicoesDoEncontro(
    RuinaRpgDbContext db, CharacterSheetStats personagens, NpcSheetStats npcs, CreatureSheetStats criaturas, SheetLogDoServidor logDoServidor,
    IHubContext<NotificationHub> notificacoes, IHubContext<EncounterHub> encontros, ILogger<CondicoesDoEncontro> logger)
{
    private const string SecaoDoLog = "Condições";

    private sealed record Ficha(SheetLogKind Kind, Guid Id);

    private sealed record EstadoDoEncontro(EncounterStatus Status, int CurrentRound);

    private sealed record Participante(Guid Id, Guid EncounterId, bool Removido);

    private sealed record Aplicada(EncounterParticipantCondicao Condicao, EntityState Estado);

    private readonly Dictionary<Ficha, Guid> _donosAAvisar = [];
    private readonly HashSet<Guid> _encontrosAAvisar = [];

    /// <param name="participantIds">Os participantes cujas Condições a ação pode ter mudado.</param>
    /// <param name="expiracao">
    /// As Condições removidas expiraram (o servidor as tirou ao vencer o prazo): o log da ficha diz "expirada" e a
    /// linha é automática. Sem isso, aplicar e remover são do GM.
    /// </param>
    public async Task AoMudarAtivacaoAsync(IEnumerable<Guid> participantIds, bool expiracao = false)
    {
        var ids = participantIds.ToHashSet();
        foreach (var (ficha, participantes) in await FichasAsync(ids))
        {
            var ownerId = await DonoAsync(ficha);
            if (ownerId is null)
                continue; // NPC/Criatura do GM

            var ligados = await LigadosAsync(ficha);
            var aplicadas = await AplicadasAsync(ligados.Select(p => p.Id).ToList());
            var estados = await EstadosAsync(ligados.Select(p => p.EncounterId).Distinct().ToList());

            foreach (var a in aplicadas.Where(a => participantes.Contains(a.Condicao.EncounterParticipantId) && a.Estado is EntityState.Added or EntityState.Deleted))
                Registrar(ficha, a, expiracao);

            var mudou = participantes.Any(id =>
                !AtivasAntes(id, aplicadas, ligados, estados).SetEquals(AtivasDepois(id, aplicadas, ligados, estados).Select(c => c.Id)));
            if (!mudou)
                continue;

            var depois = ligados.SelectMany(p => AtivasDepois(p.Id, aplicadas, ligados, estados)).OrderBy(c => c.Nome).ThenBy(c => c.Id).ToList();
            await CortarAsync(ficha, CondicoesAtivasDaFicha.EfeitosDe(depois));
            _donosAAvisar[ficha] = ownerId.Value;
            _encontrosAAvisar.UnionWith(ligados.Where(p => !p.Removido).Select(p => p.EncounterId));
        }
    }

    /// <summary>Depois do salvamento que deu certo. Um jogador só é avisado da ficha de que é dono.</summary>
    public async Task NotificarAsync()
    {
        foreach (var (ficha, ownerId) in _donosAAvisar)
        {
            try
            {
                await notificacoes.Clients.User(ownerId.ToString()).SendAsync(NotificationEvents.CondicoesAlteradas,
                    new CondicoesAlteradasNotification(KindDaRota(ficha.Kind), ficha.Id.ToString()));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to push CondicoesAlteradas for sheet {SheetId}", ficha.Id);
            }
        }
        foreach (var encounterId in _encontrosAAvisar)
        {
            try
            {
                await encontros.Clients.Group($"encounter-{encounterId}").SendAsync("ParticipantsChanged");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify encounter {EncounterId} of a ParticipantsChanged update.", encounterId);
            }
        }
        _donosAAvisar.Clear();
        _encontrosAAvisar.Clear();
    }

    private static string KindDaRota(SheetLogKind kind) => kind switch
    {
        SheetLogKind.Character => "character",
        SheetLogKind.Npc => "npc",
        _ => "creature",
    };

    /// <summary>A ficha de cada participante dado (rastreado pela ação, até removido, ou lido do banco), com os participantes dela.</summary>
    private async Task<Dictionary<Ficha, HashSet<Guid>>> FichasAsync(HashSet<Guid> ids)
    {
        var rastreados = db.ChangeTracker.Entries<EncounterParticipant>().Where(e => ids.Contains(e.Entity.Id)).Select(e => e.Entity).ToList();
        var faltam = ids.Except(rastreados.Select(p => p.Id)).ToList();
        var lidos = faltam.Count == 0 ? [] : await db.EncounterParticipants.AsNoTracking().Where(p => faltam.Contains(p.Id)).ToListAsync();

        var fichas = new Dictionary<Ficha, HashSet<Guid>>();
        foreach (var p in rastreados.Concat(lidos))
        {
            Ficha? ficha = p.SourceCharacterSheetId is { } personagem ? new(SheetLogKind.Character, personagem)
                : p.SourceNpcSheetId is { } npc ? new(SheetLogKind.Npc, npc)
                : p.SourceCreatureSheetId is { } criatura ? new(SheetLogKind.Creature, criatura)
                : null;
            if (ficha is null)
                continue;
            if (!fichas.TryGetValue(ficha, out var daFicha))
                fichas[ficha] = daFicha = [];
            daFicha.Add(p.Id);
        }
        return fichas;
    }

    private async Task<Guid?> DonoAsync(Ficha ficha) => ficha.Kind switch
    {
        SheetLogKind.Character => (await db.CharacterSheets.FindAsync(ficha.Id))?.OwnerId,
        SheetLogKind.Npc => (await db.NpcSheets.FindAsync(ficha.Id))?.OwnerId,
        _ => (await db.CreatureSheets.FindAsync(ficha.Id))?.OwnerId,
    };

    /// <summary>Todos os participantes ligados à ficha, em qualquer encontro: os totais somam as Condições de todos.</summary>
    private async Task<List<Participante>> LigadosAsync(Ficha ficha)
    {
        var doBanco = ficha.Kind switch
        {
            SheetLogKind.Character => db.EncounterParticipants.AsNoTracking().Where(p => p.SourceCharacterSheetId == ficha.Id),
            SheetLogKind.Npc => db.EncounterParticipants.AsNoTracking().Where(p => p.SourceNpcSheetId == ficha.Id),
            _ => db.EncounterParticipants.AsNoTracking().Where(p => p.SourceCreatureSheetId == ficha.Id),
        };
        var lidos = await doBanco.Select(p => new Participante(p.Id, p.EncounterId, false)).ToListAsync();
        var removidos = db.ChangeTracker.Entries<EncounterParticipant>().Where(e => e.State == EntityState.Deleted).Select(e => e.Entity.Id).ToHashSet();
        return lidos.Select(p => p with { Removido = removidos.Contains(p.Id) }).ToList();
    }

    /// <summary>As Condições desses participantes, cada uma com o estado em que a ação a deixou (Added, Deleted ou como está no banco).</summary>
    private async Task<List<Aplicada>> AplicadasAsync(List<Guid> participantIds)
    {
        var rastreadas = db.ChangeTracker.Entries<EncounterParticipantCondicao>()
            .Where(e => participantIds.Contains(e.Entity.EncounterParticipantId))
            .ToDictionary(e => e.Entity.Id, e => new Aplicada(e.Entity, e.State));
        var doBanco = await db.EncounterParticipantCondicoes.AsNoTracking().Where(c => participantIds.Contains(c.EncounterParticipantId)).ToListAsync();
        return doBanco.Where(c => !rastreadas.ContainsKey(c.Id)).Select(c => new Aplicada(c, EntityState.Unchanged)).Concat(rastreadas.Values).ToList();
    }

    /// <summary>Status e Turno de cada encontro antes e depois da ação (o encontro que a ação alterou está rastreado).</summary>
    private async Task<Dictionary<Guid, (EstadoDoEncontro Antes, EstadoDoEncontro Depois)>> EstadosAsync(List<Guid> encounterIds)
    {
        var estados = new Dictionary<Guid, (EstadoDoEncontro, EstadoDoEncontro)>();
        foreach (var entry in db.ChangeTracker.Entries<Encounter>().Where(e => encounterIds.Contains(e.Entity.Id)))
            estados[entry.Entity.Id] = (
                new EstadoDoEncontro(entry.Property(e => e.Status).OriginalValue, entry.Property(e => e.CurrentRound).OriginalValue),
                new EstadoDoEncontro(entry.Entity.Status, entry.Entity.CurrentRound));
        var faltam = encounterIds.Where(id => !estados.ContainsKey(id)).ToList();
        foreach (var e in await db.Encounters.AsNoTracking().Where(e => faltam.Contains(e.Id)).Select(e => new { e.Id, e.Status, e.CurrentRound }).ToListAsync())
            estados[e.Id] = (new EstadoDoEncontro(e.Status, e.CurrentRound), new EstadoDoEncontro(e.Status, e.CurrentRound));
        return estados;
    }

    private static HashSet<Guid> AtivasAntes(Guid participantId, List<Aplicada> aplicadas, List<Participante> ligados, Dictionary<Guid, (EstadoDoEncontro Antes, EstadoDoEncontro Depois)> estados)
    {
        var antes = estados[ligados.Single(p => p.Id == participantId).EncounterId].Antes;
        return aplicadas.Where(a => a.Condicao.EncounterParticipantId == participantId && a.Estado != EntityState.Added
                && CondicaoAplicadaMapper.Ativa(a.Condicao, antes.Status, antes.CurrentRound))
            .Select(a => a.Condicao.Id).ToHashSet();
    }

    private static IEnumerable<EncounterParticipantCondicao> AtivasDepois(Guid participantId, List<Aplicada> aplicadas, List<Participante> ligados, Dictionary<Guid, (EstadoDoEncontro Antes, EstadoDoEncontro Depois)> estados)
    {
        var participante = ligados.Single(p => p.Id == participantId);
        if (participante.Removido)
            return [];
        var depois = estados[participante.EncounterId].Depois;
        return aplicadas.Where(a => a.Condicao.EncounterParticipantId == participantId && a.Estado != EntityState.Deleted
                && CondicaoAplicadaMapper.Ativa(a.Condicao, depois.Status, depois.CurrentRound))
            .Select(a => a.Condicao);
    }

    private void Registrar(Ficha ficha, Aplicada aplicada, bool expiracao)
    {
        var (acao, automatica) = aplicada.Estado == EntityState.Added ? (SheetLogAcao.Adicionou, false)
            : expiracao ? (SheetLogAcao.Expirou, true)
            : (SheetLogAcao.Removeu, false);
        logDoServidor.Registrar(new SheetLogDoServidor.LinhaExplicita(ficha.Kind, ficha.Id, SecaoDoLog, acao, aplicada.Condicao.Nome, automatica));
    }

    /// <summary>O Atual acima do novo total desce até ele, de vez: tirar a Condição depois não o devolve.</summary>
    private async Task CortarAsync(Ficha ficha, EfeitosDeCondicao efeitos)
    {
        switch (ficha.Kind)
        {
            case SheetLogKind.Character:
                var personagem = (await db.CharacterSheets.FindAsync(ficha.Id))!;
                var (vp, fp, ap, ep) = await personagens.MaximosAsync(personagem, efeitos);
                Cortar(personagem, nameof(CharacterSheet.VitalidadeAtual), personagem.VitalidadeAtual, vp, v => personagem.VitalidadeAtual = v);
                Cortar(personagem, nameof(CharacterSheet.FocoAtual), personagem.FocoAtual, fp, v => personagem.FocoAtual = v);
                Cortar(personagem, nameof(CharacterSheet.AdrenalinaAtual), personagem.AdrenalinaAtual, ap, v => personagem.AdrenalinaAtual = v);
                Cortar(personagem, nameof(CharacterSheet.EstresseAtual), personagem.EstresseAtual, ep, v => personagem.EstresseAtual = v);
                break;
            case SheetLogKind.Npc:
                var npc = (await db.NpcSheets.FindAsync(ficha.Id))!;
                var (vn, fn, an, en) = await npcs.MaximosAsync(npc, efeitos);
                Cortar(npc, nameof(NpcSheet.VitalidadeAtual), npc.VitalidadeAtual, vn, v => npc.VitalidadeAtual = v);
                Cortar(npc, nameof(NpcSheet.FocoAtual), npc.FocoAtual, fn, v => npc.FocoAtual = v);
                Cortar(npc, nameof(NpcSheet.AdrenalinaAtual), npc.AdrenalinaAtual, an, v => npc.AdrenalinaAtual = v);
                Cortar(npc, nameof(NpcSheet.EstresseAtual), npc.EstresseAtual, en, v => npc.EstresseAtual = v);
                break;
            default:
                var criatura = (await db.CreatureSheets.FindAsync(ficha.Id))!;
                var (vc, fc, ac) = await criaturas.MaximosAsync(criatura, efeitos);
                Cortar(criatura, nameof(CreatureSheet.VitalidadeAtual), criatura.VitalidadeAtual, vc, v => criatura.VitalidadeAtual = v);
                Cortar(criatura, nameof(CreatureSheet.FocoAtual), criatura.FocoAtual, fc, v => criatura.FocoAtual = v);
                Cortar(criatura, nameof(CreatureSheet.AdrenalinaAtual), criatura.AdrenalinaAtual, ac, v => criatura.AdrenalinaAtual = v);
                break;
        }
    }

    private void Cortar(object ficha, string propriedade, int atual, int maximo, Action<int> gravar)
    {
        if (atual <= maximo)
            return;
        gravar(maximo);
        logDoServidor.MarcarAutomatica(ficha, propriedade);
    }
}
