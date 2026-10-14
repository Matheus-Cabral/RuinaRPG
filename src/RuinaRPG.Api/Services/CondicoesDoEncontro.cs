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
/// <item><see cref="AoMudarAVezAsync"/> e <see cref="AoAplicarAsync"/>, também antes do salvamento: o lado do tempo —
/// expiração ao entrar num Turno novo e os ajustes de valor atual "ao aplicar" e "a cada vez" (Gerenciador de
/// Encontros R0012 e R0013).</item>
/// </list>
/// Os totais "depois da ação" saem do ChangeTracker (Condições acrescentadas e removidas, status e Turno do encontro
/// já alterados, participante removido) e não de CondicoesAtivasDaFicha, que lê o banco e guarda o resultado na requisição.
/// </summary>
public class CondicoesDoEncontro(
    RuinaRpgDbContext db, CharacterSheetStats personagens, NpcSheetStats npcs, CreatureSheetStats criaturas, SheetLogDoServidor logDoServidor,
    EncounterLog logDoEncontro, IHubContext<NotificationHub> notificacoes, IHubContext<EncounterHub> encontros, ILogger<CondicoesDoEncontro> logger)
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

            var situacao = await SituacaoAsync(ficha);

            foreach (var a in situacao.Aplicadas.Where(a => participantes.Contains(a.Condicao.EncounterParticipantId) && a.Estado is EntityState.Added or EntityState.Deleted))
                Registrar(ficha, a, expiracao);

            var mudou = participantes.Any(id => !situacao.AtivasAntes(id).SetEquals(situacao.AtivasDepois(id).Select(c => c.Id)));
            if (!mudou)
                continue;

            await CortarAsync(ficha, situacao.EfeitosDepois());
            Avisar(ficha, ownerId.Value, situacao);
        }
    }

    /// <summary>
    /// Logo depois de <see cref="EncounterTurnos.DarAVezAsync"/>, ANTES do salvamento da ação (o mesmo SaveChanges da
    /// mudança de vez: o 409 do encontro impede que duas mudanças simultâneas apliquem duas vezes). Nesta ordem:
    /// <list type="number">
    /// <item>Num Turno novo, as Condições do encontro com o prazo vencido saem (a linha é apagada), com
    /// CondicaoExpirada no log do encontro e "expirada" no log da ficha; os totais voltam por <see cref="AoMudarAtivacaoAsync"/>.</item>
    /// <item>As linhas "ao aplicar" que ainda não rodaram rodam (as aplicadas na preparação, ao Iniciar).</item>
    /// <item>Quem recebeu a vez (todo motivo menos Vez anterior) sofre as linhas "a cada vez" das suas Condições
    /// ativas, uma vez por (linha, Turno): voltar e avançar de novo no mesmo Turno não repete.</item>
    /// </list>
    /// </summary>
    public async Task AoMudarAVezAsync(Encounter encounter, MudancaDeVez mudanca)
    {
        ConferirVersaoDoEncontro(encounter);
        var participantes = await ParticipantesDoEncontroAsync(encounter.Id);
        var ids = participantes.Select(p => p.Id).ToHashSet();
        await db.EncounterParticipantCondicoes.Where(c => ids.Contains(c.EncounterParticipantId)).LoadAsync();
        List<EncounterParticipantCondicao> Aplicadas() => db.ChangeTracker.Entries<EncounterParticipantCondicao>()
            .Where(e => ids.Contains(e.Entity.EncounterParticipantId) && e.State is not (EntityState.Deleted or EntityState.Detached))
            .Select(e => e.Entity).OrderBy(c => c.Nome).ThenBy(c => c.Id).ToList();

        if (mudanca.EntrouEmNovoTurno)
        {
            // A mesma regra de Turno da resposta do encontro (CondicaoAplicadaMapper), para o contador e a expiração concordarem.
            var turno = CondicaoAplicadaMapper.TurnoDeContagem(encounter.CurrentRound);
            var vencidas = Aplicadas().Where(c => DuracaoDeCondicao.Expirou(c.AplicadaNoTurno, c.DuracaoTurnos, turno)).ToList();
            foreach (var condicao in vencidas)
            {
                db.EncounterParticipantCondicoes.Remove(condicao);
                await logDoEncontro.RegistrarAsync(encounter, EncounterLogTipo.CondicaoExpirada,
                    participantes.First(p => p.Id == condicao.EncounterParticipantId).Nome, texto: condicao.Nome);
            }
            if (vencidas.Count > 0)
                await AoMudarAtivacaoAsync(vencidas.Select(c => c.EncounterParticipantId).Distinct(), expiracao: true);
        }

        foreach (var participante in participantes)
            foreach (var condicao in Aplicadas().Where(c => c.EncounterParticipantId == participante.Id))
                await RodarAsync(encounter, participante, condicao, MomentoDeCondicao.AoAplicar);

        if (mudanca.ParticipanteRecebeuAVez)
            foreach (var condicao in Aplicadas().Where(c => c.EncounterParticipantId == mudanca.Participante!.Id))
                await RodarAsync(encounter, mudanca.Participante!, condicao, MomentoDeCondicao.ACadaVez);
    }

    /// <summary>
    /// Para a Condição recém-aplicada, depois de <see cref="AoMudarAtivacaoAsync"/> e antes do salvamento: com o
    /// encontro em andamento, as linhas "ao aplicar" rodam já. Na preparação nada roda (fica para o Iniciar).
    /// </summary>
    public Task AoAplicarAsync(Encounter encounter, EncounterParticipant participante, EncounterParticipantCondicao aplicada)
    {
        if (encounter.Status == EncounterStatus.EmAndamento)
            ConferirVersaoDoEncontro(encounter);
        return RodarAsync(encounter, participante, aplicada, MomentoDeCondicao.AoAplicar);
    }

    /// <summary>
    /// Põe a linha do encontro no UPDATE da ação, mesmo quando nada nela muda (escolher quem já tem a vez, aplicar uma
    /// Condição): o salvamento então confere a versão (xmin) e uma ação simultânea que rodaria os mesmos ajustes
    /// recebe 409 (ConflitoDeConcorrenciaDoEncontro) em vez de aplicá-los de novo.
    /// </summary>
    private void ConferirVersaoDoEncontro(Encounter encounter) =>
        db.Entry(encounter).Property(e => e.Nome).IsModified = true;

    /// <summary>
    /// Roda as linhas de valor atual de <paramref name="momento"/> que ainda não rodaram, se a Condição estiver ativa, e
    /// as anota em AjustesAplicados: "ao aplicar" com Turno 0 (uma vez só), "a cada vez" com o Turno atual.
    /// </summary>
    private async Task RodarAsync(Encounter encounter, EncounterParticipant participante, EncounterParticipantCondicao condicao, MomentoDeCondicao momento)
    {
        if (!CondicaoAplicadaMapper.Ativa(condicao, encounter.Status, encounter.CurrentRound))
            return;
        var turno = momento == MomentoDeCondicao.AoAplicar ? 0 : encounter.CurrentRound;
        foreach (var linha in condicao.Modificadores.Where(l => l.Alvo == AlvoDeCondicao.RecursoAtual && l.Momento == momento))
        {
            if (condicao.AjustesAplicados.Any(a => a.LinhaId == linha.LinhaId && (momento == MomentoDeCondicao.AoAplicar || a.Turno == turno)))
                continue;
            condicao.AjustesAplicados = [.. condicao.AjustesAplicados, new AjusteAplicado(linha.LinhaId, turno)];
            await AjustarAsync(encounter, participante, condicao, linha);
        }
    }

    /// <summary>
    /// Onde o ajuste cai: participante ligado a uma ficha que muda com Condições → o Atual da ficha, entre 0 e o total
    /// com as Condições (escrita automática); instância de NPC/Criatura do GM → PV/PF/PA do participante, piso 0 e sem
    /// teto (a instância não guarda total, e o GM já pode pôr ali qualquer valor). Cada ajuste, feito ou pulado, vira
    /// uma linha AjusteDeCondicao no log do encontro.
    /// </summary>
    private async Task AjustarAsync(Encounter encounter, EncounterParticipant participante, EncounterParticipantCondicao condicao, LinhaDeCondicaoAplicada linha)
    {
        var recurso = Enum.Parse<RecursoDeCondicao>(linha.AlvoEspecifico!);
        var modificador = new ModificadorDeCondicao(linha.Alvo, linha.AlvoEspecifico, linha.TipoDeValor, linha.Valor, linha.Momento);
        Task Registrar(string campo, string? antes, string? depois, string? motivo = null) =>
            logDoEncontro.RegistrarAsync(encounter, EncounterLogTipo.AjusteDeCondicao, participante.Nome, campo, antes, depois,
                motivo is null ? condicao.Nome : $"{condicao.Nome}: {motivo}");

        var ficha = FichaDe(participante);
        var ownerId = ficha is null ? null : await DonoAsync(ficha);
        if (ficha is not null && ownerId is not null)
        {
            var situacao = await SituacaoAsync(ficha);
            var recursos = await RecursosAsync(ficha, situacao.EfeitosDepois());
            if (!recursos.TryGetValue(recurso, out var guardado))
            {
                await Registrar(recurso.ToString(), null, null, "a Criatura não tem Estresse");
                return;
            }
            var antes = guardado.Ler();
            var depois = Math.Clamp(antes + DuracaoDeCondicao.ValorDoAjuste(modificador, guardado.Maximo), 0, Math.Max(guardado.Maximo, 0));
            guardado.Gravar(depois);
            logDoServidor.MarcarAutomatica(guardado.Entidade, guardado.Propriedade);
            await Registrar(guardado.Rotulo, antes.ToString(), depois.ToString());
            if (depois != antes)
                Avisar(ficha, ownerId.Value, situacao);
            return;
        }

        var (campo, ler, gravar) = recurso switch
        {
            RecursoDeCondicao.Vitalidade => ("PV", (Func<int?>)(() => participante.PVAtual), (Action<int>)(v => participante.PVAtual = v)),
            RecursoDeCondicao.Foco => ("PF", () => participante.PFAtual, v => participante.PFAtual = v),
            RecursoDeCondicao.Adrenalina => ("PA", () => participante.PAAtual, v => participante.PAAtual = v),
            _ => ("Estresse", (Func<int?>?)null, (Action<int>?)null),
        };
        if (ler is null || gravar is null)
        {
            await Registrar(campo, null, null, "Estresse não é acompanhado no encontro");
            return;
        }
        if (ler() is not { } atual)
        {
            await Registrar(campo, null, null, $"{campo} sem valor no encontro");
            return;
        }

        var total = 0;
        if (linha.TipoDeValor == TipoDeValorDeCondicao.Percentual)
        {
            // O total é o da ficha de origem com as Condições DESTE participante (como no resumo do encontro).
            var dele = Ativas(participante.Id, encounter);
            if (await TotalDaOrigemAsync(ficha, recurso, CondicoesAtivasDaFicha.EfeitosDe(dele)) is not { } daOrigem)
            {
                await Registrar(campo, null, null, "ficha de origem removida");
                return;
            }
            total = daOrigem;
        }
        var novo = Math.Max(0, atual + DuracaoDeCondicao.ValorDoAjuste(modificador, total));
        gravar(novo);
        await Registrar(campo, atual.ToString(), novo.ToString());
    }

    /// <summary>As Condições ativas de um participante como estarão depois da ação (rastreadas, sem as apagadas).</summary>
    private List<EncounterParticipantCondicao> Ativas(Guid participantId, Encounter encounter) =>
        db.ChangeTracker.Entries<EncounterParticipantCondicao>()
            .Where(e => e.Entity.EncounterParticipantId == participantId && e.State is not (EntityState.Deleted or EntityState.Detached)
                && CondicaoAplicadaMapper.Ativa(e.Entity, encounter.Status, encounter.CurrentRound))
            .Select(e => e.Entity).OrderBy(c => c.Nome).ThenBy(c => c.Id).ToList();

    /// <summary>O total do recurso na ficha de origem da instância; null quando a ficha não existe mais.</summary>
    private async Task<int?> TotalDaOrigemAsync(Ficha? ficha, RecursoDeCondicao recurso, EfeitosDeCondicao efeitos) =>
        ficha is null ? null
            : (await RecursosAsync(ficha, efeitos)).TryGetValue(recurso, out var guardado) ? guardado.Maximo
            : null;

    /// <summary>Os participantes do encontro que continuam nele depois da ação (rastreados; os removidos ficam de fora).</summary>
    private async Task<List<EncounterParticipant>> ParticipantesDoEncontroAsync(Guid encounterId)
    {
        await db.EncounterParticipants.Where(p => p.EncounterId == encounterId).LoadAsync();
        return db.ChangeTracker.Entries<EncounterParticipant>()
            .Where(e => e.Entity.EncounterId == encounterId && e.State is not (EntityState.Deleted or EntityState.Detached))
            .Select(e => e.Entity).OrderByDescending(p => p.Iniciativa).ThenBy(p => p.Id).ToList();
    }

    private void Avisar(Ficha ficha, Guid ownerId, Situacao situacao)
    {
        _donosAAvisar[ficha] = ownerId;
        _encontrosAAvisar.UnionWith(situacao.Ligados.Values.Where(p => !p.Removido).Select(p => p.EncounterId));
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
            if (FichaDe(p) is not { } ficha)
                continue;
            if (!fichas.TryGetValue(ficha, out var daFicha))
                fichas[ficha] = daFicha = [];
            daFicha.Add(p.Id);
        }
        return fichas;
    }

    private static Ficha? FichaDe(EncounterParticipant p) =>
        p.SourceCharacterSheetId is { } personagem ? new(SheetLogKind.Character, personagem)
        : p.SourceNpcSheetId is { } npc ? new(SheetLogKind.Npc, npc)
        : p.SourceCreatureSheetId is { } criatura ? new(SheetLogKind.Creature, criatura)
        : null;

    private async Task<Guid?> DonoAsync(Ficha ficha) => ficha.Kind switch
    {
        SheetLogKind.Character => (await db.CharacterSheets.FindAsync(ficha.Id))?.OwnerId,
        SheetLogKind.Npc => (await db.NpcSheets.FindAsync(ficha.Id))?.OwnerId,
        _ => (await db.CreatureSheets.FindAsync(ficha.Id))?.OwnerId,
    };

    /// <summary>
    /// A ficha vista de todos os participantes ligados a ela, em qualquer encontro, antes e depois da ação: as Condições
    /// de cada um, com o estado em que a ação as deixou, e o status/Turno de cada encontro.
    /// </summary>
    private sealed class Situacao(Dictionary<Guid, Participante> ligados, List<Aplicada> aplicadas, Dictionary<Guid, (EstadoDoEncontro Antes, EstadoDoEncontro Depois)> estados)
    {
        public IReadOnlyDictionary<Guid, Participante> Ligados => ligados;

        public List<Aplicada> Aplicadas => aplicadas;

        public HashSet<Guid> AtivasAntes(Guid participantId)
        {
            var antes = estados[ligados[participantId].EncounterId].Antes;
            return aplicadas.Where(a => a.Condicao.EncounterParticipantId == participantId && a.Estado != EntityState.Added
                    && CondicaoAplicadaMapper.Ativa(a.Condicao, antes.Status, antes.CurrentRound))
                .Select(a => a.Condicao.Id).ToHashSet();
        }

        public IEnumerable<EncounterParticipantCondicao> AtivasDepois(Guid participantId)
        {
            var participante = ligados[participantId];
            if (participante.Removido)
                return [];
            var depois = estados[participante.EncounterId].Depois;
            return aplicadas.Where(a => a.Condicao.EncounterParticipantId == participantId && a.Estado is not (EntityState.Deleted or EntityState.Detached)
                    && CondicaoAplicadaMapper.Ativa(a.Condicao, depois.Status, depois.CurrentRound))
                .Select(a => a.Condicao);
        }

        /// <summary>Os efeitos de todas as Condições ativas da ficha depois da ação, somados entre os encontros.</summary>
        public EfeitosDeCondicao EfeitosDepois() =>
            CondicoesAtivasDaFicha.EfeitosDe(ligados.Keys.SelectMany(AtivasDepois).OrderBy(c => c.Nome).ThenBy(c => c.Id).ToList());
    }

    private async Task<Situacao> SituacaoAsync(Ficha ficha)
    {
        var ligados = await LigadosAsync(ficha);
        var aplicadas = await AplicadasAsync(ligados.Keys.ToList());
        var estados = await EstadosAsync(ligados.Values.Select(p => p.EncounterId).Distinct().ToList());
        return new Situacao(ligados, aplicadas, estados);
    }

    /// <summary>
    /// Todos os participantes ligados à ficha, em qualquer encontro: os totais somam as Condições de todos. Inclui os
    /// que a ação acabou de acrescentar (ainda não salvos) e marca os que ela removeu.
    /// </summary>
    private async Task<Dictionary<Guid, Participante>> LigadosAsync(Ficha ficha)
    {
        bool DaFicha(EncounterParticipant p) => FichaDe(p) == ficha;
        var doBanco = ficha.Kind switch
        {
            SheetLogKind.Character => db.EncounterParticipants.AsNoTracking().Where(p => p.SourceCharacterSheetId == ficha.Id),
            SheetLogKind.Npc => db.EncounterParticipants.AsNoTracking().Where(p => p.SourceNpcSheetId == ficha.Id),
            _ => db.EncounterParticipants.AsNoTracking().Where(p => p.SourceCreatureSheetId == ficha.Id),
        };
        var ligados = (await doBanco.Select(p => new Participante(p.Id, p.EncounterId, false)).ToListAsync()).ToDictionary(p => p.Id);
        foreach (var entry in db.ChangeTracker.Entries<EncounterParticipant>().Where(e => DaFicha(e.Entity)))
        {
            if (entry.State == EntityState.Deleted)
                ligados[entry.Entity.Id] = new Participante(entry.Entity.Id, entry.Entity.EncounterId, true);
            else if (entry.State == EntityState.Added)
                ligados[entry.Entity.Id] = new Participante(entry.Entity.Id, entry.Entity.EncounterId, false);
        }
        return ligados;
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

    private void Registrar(Ficha ficha, Aplicada aplicada, bool expiracao)
    {
        var (acao, automatica) = aplicada.Estado == EntityState.Added ? (SheetLogAcao.Adicionou, false)
            : expiracao ? (SheetLogAcao.Expirou, true)
            : (SheetLogAcao.Removeu, false);
        logDoServidor.Registrar(new SheetLogDoServidor.LinhaExplicita(ficha.Kind, ficha.Id, SecaoDoLog, acao, aplicada.Condicao.Nome, automatica));
    }

    /// <summary>Um recurso guardado na ficha (o Atual), com o total calculado com as Condições dadas.</summary>
    private sealed record RecursoGuardado(object Entidade, string Propriedade, string Rotulo, Func<int> Ler, Action<int> Gravar, int Maximo);

    /// <summary>Os recursos guardados da ficha com os totais calculados com <paramref name="efeitos"/>; vazio se a ficha não existe mais. A Criatura não tem Estresse.</summary>
    private async Task<Dictionary<RecursoDeCondicao, RecursoGuardado>> RecursosAsync(Ficha ficha, EfeitosDeCondicao efeitos)
    {
        RecursoGuardado Recurso<T>(T entidade, string propriedade, Func<int> ler, Action<int> gravar, int maximo) where T : class =>
            new(entidade, propriedade, SheetLogMap.Entidades[typeof(T)].Campos[propriedade], ler, gravar, maximo);

        switch (ficha.Kind)
        {
            case SheetLogKind.Character:
                if (await db.CharacterSheets.FindAsync(ficha.Id) is not { } p)
                    return [];
                var (vp, fp, ap, ep) = await personagens.MaximosAsync(p, efeitos);
                return new()
                {
                    [RecursoDeCondicao.Vitalidade] = Recurso(p, nameof(CharacterSheet.VitalidadeAtual), () => p.VitalidadeAtual, v => p.VitalidadeAtual = v, vp),
                    [RecursoDeCondicao.Foco] = Recurso(p, nameof(CharacterSheet.FocoAtual), () => p.FocoAtual, v => p.FocoAtual = v, fp),
                    [RecursoDeCondicao.Adrenalina] = Recurso(p, nameof(CharacterSheet.AdrenalinaAtual), () => p.AdrenalinaAtual, v => p.AdrenalinaAtual = v, ap),
                    [RecursoDeCondicao.Estresse] = Recurso(p, nameof(CharacterSheet.EstresseAtual), () => p.EstresseAtual, v => p.EstresseAtual = v, ep),
                };
            case SheetLogKind.Npc:
                if (await db.NpcSheets.FindAsync(ficha.Id) is not { } n)
                    return [];
                var (vn, fn, an, en) = await npcs.MaximosAsync(n, efeitos);
                return new()
                {
                    [RecursoDeCondicao.Vitalidade] = Recurso(n, nameof(NpcSheet.VitalidadeAtual), () => n.VitalidadeAtual, v => n.VitalidadeAtual = v, vn),
                    [RecursoDeCondicao.Foco] = Recurso(n, nameof(NpcSheet.FocoAtual), () => n.FocoAtual, v => n.FocoAtual = v, fn),
                    [RecursoDeCondicao.Adrenalina] = Recurso(n, nameof(NpcSheet.AdrenalinaAtual), () => n.AdrenalinaAtual, v => n.AdrenalinaAtual = v, an),
                    [RecursoDeCondicao.Estresse] = Recurso(n, nameof(NpcSheet.EstresseAtual), () => n.EstresseAtual, v => n.EstresseAtual = v, en),
                };
            default:
                if (await db.CreatureSheets.FindAsync(ficha.Id) is not { } c)
                    return [];
                var (vc, fc, ac) = await criaturas.MaximosAsync(c, efeitos);
                return new()
                {
                    [RecursoDeCondicao.Vitalidade] = Recurso(c, nameof(CreatureSheet.VitalidadeAtual), () => c.VitalidadeAtual, v => c.VitalidadeAtual = v, vc),
                    [RecursoDeCondicao.Foco] = Recurso(c, nameof(CreatureSheet.FocoAtual), () => c.FocoAtual, v => c.FocoAtual = v, fc),
                    [RecursoDeCondicao.Adrenalina] = Recurso(c, nameof(CreatureSheet.AdrenalinaAtual), () => c.AdrenalinaAtual, v => c.AdrenalinaAtual = v, ac),
                };
        }
    }

    /// <summary>O Atual acima do novo total desce até ele, de vez: tirar a Condição depois não o devolve.</summary>
    private async Task CortarAsync(Ficha ficha, EfeitosDeCondicao efeitos)
    {
        foreach (var recurso in (await RecursosAsync(ficha, efeitos)).Values.Where(r => r.Ler() > r.Maximo))
        {
            recurso.Gravar(recurso.Maximo);
            logDoServidor.MarcarAutomatica(recurso.Entidade, recurso.Propriedade);
        }
    }
}
