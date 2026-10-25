using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/encounters/{encounterId}/participants")]
public class EncounterParticipantsController(RuinaRpgDbContext db, EncounterLog log, EncounterTurnos turnos, CondicoesDoEncontro condicoesDoEncontro,
    CharacterSheetStats personagens, NpcSheetStats npcs, CreatureSheetStats criaturas, CondicoesAtivasDaFicha condicoes, CemiterioDaFicha cemiterio,
    IHubContext<NotificationHub> notificacoes, IHubContext<EncounterHub> encontros, ILogger<EncounterParticipantsController> logger) : ControllerBase
{
    /// <summary>Gerenciador de Encontros R0002: uma Ficha de Personagem no cemitério não entra num encontro.</summary>
    public const string RecusaDoCemiterio = "Esta ficha está no cemitério e não pode entrar em encontros.";

    [HttpPost]
    public async Task<ActionResult<EncounterParticipantResponse>> Add(Guid encounterId, AddParticipantRequest request)
    {
        var (authError, encounter) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;
        if (RequireEditable(encounter!) is { } wrongPhase)
            return wrongPhase;

        var sourceCount = new[] { request.SourceCharacterSheetId, request.SourceNpcSheetId, request.SourceCreatureSheetId }.Count(id => id is not null);
        if (sourceCount != 1)
            return BadRequest("Informe exatamente uma origem: Ficha de Personagem, de NPC ou de Criatura.");

        var gmId = CurrentGmId();
        var participant = new EncounterParticipant { Id = Guid.NewGuid(), EncounterId = encounterId, Iniciativa = request.Iniciativa, AcoesRestantes = 3, Nome = "" };

        if (request.SourceCharacterSheetId is not null)
        {
            if (!Guid.TryParse(request.SourceCharacterSheetId, out var characterSheetId))
                return BadRequest("SourceCharacterSheetId inválido.");

            var sheet = await db.CharacterSheets.FindAsync(characterSheetId);
            // R0002: the CharacterSheet must belong to the encounter's own campaign. Wrong campaign is
            // treated identically to "doesn't exist" — no distinct message, same convention used elsewhere.
            if (sheet is null || sheet.CampaignId != encounter!.CampaignId) return BadRequest("Ficha de Personagem não encontrada.");
            if ((await cemiterio.MotivosAsync(sheet)).Count > 0) return Conflict(RecusaDoCemiterio);
            participant.SourceCharacterSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            // PV/PF/PA stay null — always live-sourced for a CharacterSheet (owner's own, or a granted pet).
        }
        else if (request.SourceNpcSheetId is not null)
        {
            if (!Guid.TryParse(request.SourceNpcSheetId, out var npcSheetId))
                return BadRequest("SourceNpcSheetId inválido.");

            var sheet = await db.NpcSheets.FindAsync(npcSheetId);
            // R0002: the NpcSheet must belong to the calling GM.
            if (sheet is null || sheet.GmId != gmId) return BadRequest("Ficha de NPC não encontrada.");
            if (sheet.OwnerId is not null)
            {
                // Granted sheets are campaign-scoped via their grant-link CampaignAttachment row
                // (the same one CampaignGrantsController.Grant inserts) — a GM who owns several
                // campaigns must not be able to pull a companion granted in one of them into an
                // encounter in a different one, matching the CharacterSheet branch's own check above.
                var grantedToThisCampaign = await db.CampaignAttachments.AnyAsync(a => a.NpcSheetId == npcSheetId && a.CampaignId == encounter!.CampaignId);
                if (!grantedToThisCampaign) return BadRequest("Ficha de NPC não encontrada.");
            }
            participant.SourceNpcSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            if (sheet.OwnerId is null) // GM's own bestiary entry (R0002's 2nd case) — copy once, then independent.
            {
                participant.PVAtual = sheet.VitalidadeAtual;
                participant.PFAtual = sheet.FocoAtual;
                participant.PAAtual = sheet.AdrenalinaAtual;
            }
            // else: granted to a player — stays live-sourced, same as a CharacterSheet.
        }
        else
        {
            if (!Guid.TryParse(request.SourceCreatureSheetId, out var creatureSheetId))
                return BadRequest("SourceCreatureSheetId inválido.");

            var sheet = await db.CreatureSheets.FindAsync(creatureSheetId);
            // R0002: the CreatureSheet must belong to the calling GM.
            if (sheet is null || sheet.GmId != gmId) return BadRequest("Ficha de Criatura não encontrada.");
            if (sheet.OwnerId is not null)
            {
                // Same campaign-scoping rationale as the NpcSheet branch above.
                var grantedToThisCampaign = await db.CampaignAttachments.AnyAsync(a => a.CreatureSheetId == creatureSheetId && a.CampaignId == encounter!.CampaignId);
                if (!grantedToThisCampaign) return BadRequest("Ficha de Criatura não encontrada.");
            }
            participant.SourceCreatureSheetId = sheet.Id;
            participant.Nome = sheet.Nome ?? "";
            if (sheet.OwnerId is null)
            {
                participant.PVAtual = sheet.VitalidadeAtual;
                participant.PFAtual = sheet.FocoAtual;
                participant.PAAtual = sheet.AdrenalinaAtual;
            }
        }

        participant.Nome = await NomeSemRepeticaoAsync(encounterId, participant.Nome);
        db.EncounterParticipants.Add(participant);
        await log.RegistrarAsync(encounter!, EncounterLogTipo.ParticipanteAdicionado, participant.Nome);
        await db.SaveChangesAsync();

        // Uma ficha no cemitério foi recusada acima: o participante novo nunca está nele.
        return Created(string.Empty, await ToResponseAsync(participant, new List<string>(), new List<CondicaoAplicadaResponse>(), noCemiterio: false));
    }

    /// <summary>
    /// The same ficha can enter more than once: the first keeps its name, the others become "Nome 2", "Nome 3"…
    /// (smallest number ≥ 2 not in use). An empty name is left alone.
    /// </summary>
    private async Task<string> NomeSemRepeticaoAsync(Guid encounterId, string nome)
    {
        if (nome.Length == 0)
            return nome;

        var existentes = await db.EncounterParticipants.Where(p => p.EncounterId == encounterId).Select(p => p.Nome).ToListAsync();
        var prefixo = nome + " ";
        var numerosEmUso = existentes
            .Where(n => n.StartsWith(prefixo, StringComparison.Ordinal) && n.Length > prefixo.Length && n.Skip(prefixo.Length).All(char.IsAsciiDigit))
            .Select(n => int.TryParse(n.AsSpan(prefixo.Length), out var numero) ? numero : 0)
            .ToHashSet();
        if (!existentes.Contains(nome) && numerosEmUso.Count == 0)
            return nome;

        var proximo = 2;
        while (numerosEmUso.Contains(proximo))
            proximo++;
        return $"{nome} {proximo}";
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid encounterId, Guid id, UpdateParticipantRequest request)
    {
        var (authError, encounter) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;
        if (RequireEditable(encounter!) is { } wrongPhase)
            return wrongPhase;

        var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == id && p.EncounterId == encounterId);
        if (participant is null)
            return NotFound();

        if (request.AcoesRestantes is < 0 or > 3)
            return BadRequest("AcoesRestantes deve estar entre 0 e 3.");

        var isLive = participant.SourceCharacterSheetId is not null
            || (participant.SourceNpcSheetId is not null && (await db.NpcSheets.FindAsync(participant.SourceNpcSheetId.Value))?.OwnerId is not null)
            || (participant.SourceCreatureSheetId is not null && (await db.CreatureSheets.FindAsync(participant.SourceCreatureSheetId.Value))?.OwnerId is not null);
        var existingConditions = await db.EncounterParticipantConditions.Where(c => c.EncounterParticipantId == id).ToListAsync();
        // As condições de texto livre só podem ser reduzidas; as novas vêm do catálogo.
        if (request.Condicoes.Any(t => !existingConditions.Any(c => c.Texto == t)))
            return BadRequest("Novas condições vêm do catálogo.");

        // R0015: numa ficha no cemitério o GM não edita PV/PF/PA nem remove Condições; Iniciativa e Ações seguem.
        if (participant.SourceCharacterSheetId is { } fichaId && await cemiterio.NoCemiterioAsync(fichaId)
            && (EditaRecurso(request) || existingConditions.Any(c => !request.Condicoes.Contains(c.Texto))))
            return Conflict(FichaNoCemiterioFilter.Mensagem);

        var antes = (participant.Iniciativa, participant.AcoesRestantes, participant.PVAtual, participant.PFAtual, participant.PAAtual);

        participant.Iniciativa = request.Iniciativa;
        participant.AcoesRestantes = request.AcoesRestantes;
        FichaAlterada? fichaAlterada = null;
        if (isLive)
            fichaAlterada = await GravarNaFichaAsync(participant, request);
        else
        {
            // Instância do GM: os tiques de Condição também escrevem aqui. Com os carregados, só o valor que o GM mudou
            // é gravado; sem eles (cliente antigo, ou carregado vazio) o valor vai como veio, como antes.
            participant.PVAtual = DaInstancia(participant.PVAtual, request.PV, request.PVCarregado);
            participant.PFAtual = DaInstancia(participant.PFAtual, request.PF, request.PFCarregado);
            participant.PAAtual = DaInstancia(participant.PAAtual, request.PA, request.PACarregado);
        }

        db.EncounterParticipantConditions.RemoveRange(existingConditions);
        foreach (var texto in request.Condicoes)
            db.EncounterParticipantConditions.Add(new EncounterParticipantCondition { Id = Guid.NewGuid(), EncounterParticipantId = id, Texto = texto });

        // A ficha ligada não muda o PV/PF/PA do participante (continua nulo): a linha do encontro de cada recurso
        // alterado é a RecursoDaFicha que o SheetLogInterceptor monta, assinada pelo GM no log da ficha.
        await LogChangesAsync(encounter!, participant, antes, existingConditions.Select(c => c.Texto).ToList(), request.Condicoes);

        await db.SaveChangesAsync();
        if (fichaAlterada is not null)
            await AvisarFichaAlteradaAsync(fichaAlterada);
        return NoContent();
    }

    /// <summary>O GM mudou algum recurso: enviado diferente do carregado (a mesma regra de <see cref="GravarNaFichaAsync"/>).</summary>
    private static bool EditaRecurso(UpdateParticipantRequest r) =>
        (r.PV is { } pv && pv != r.PVCarregado) || (r.PF is { } pf && pf != r.PFCarregado) || (r.PA is { } pa && pa != r.PACarregado);

    private static int? DaInstancia(int? guardado, int? enviado, int? carregado) =>
        carregado is null || enviado != carregado ? enviado : guardado;

    private sealed record FichaAlterada(string Kind, Guid Id, Guid OwnerId);

    /// <summary>
    /// Participante ligado a uma ficha (Personagem, NPC/Criatura concedida): o PV/PF/PA editado no encontro vai para o
    /// Atual da ficha, no total que ela mostra com as Condições ativas (a regra do salvamento da ficha; sem piso, como
    /// lá). Só o recurso que o GM mudou (enviado diferente do carregado no formulário) é gravado: o que ele não tocou
    /// fica como está, e uma mudança simultânea do jogador não se perde. Null quando nada na ficha mudou.
    /// </summary>
    private async Task<FichaAlterada?> GravarNaFichaAsync(EncounterParticipant participant, UpdateParticipantRequest request)
    {
        static int? Editado(int? enviado, int? carregado) => enviado is { } valor && valor != carregado ? valor : null;
        var (pv, pf, pa) = (Editado(request.PV, request.PVCarregado), Editado(request.PF, request.PFCarregado), Editado(request.PA, request.PACarregado));
        if (pv is null && pf is null && pa is null)
            return null;

        var mudou = false;
        int Novo(int guardado, int? editado, int maximo)
        {
            if (editado is not { } valor)
                return guardado;
            var novo = Math.Min(valor, maximo);
            mudou |= novo != guardado;
            return novo;
        }

        if (participant.SourceCharacterSheetId is { } personagemId && await db.CharacterSheets.FindAsync(personagemId) is { } personagem)
        {
            var (vitalidade, foco, adrenalina, _) = await personagens.MaximosAsync(personagem, await condicoes.ParaPersonagemAsync(personagem.Id));
            personagem.VitalidadeAtual = Novo(personagem.VitalidadeAtual, pv, vitalidade);
            personagem.FocoAtual = Novo(personagem.FocoAtual, pf, foco);
            personagem.AdrenalinaAtual = Novo(personagem.AdrenalinaAtual, pa, adrenalina);
            return mudou ? new FichaAlterada("character", personagem.Id, personagem.OwnerId) : null;
        }
        if (participant.SourceNpcSheetId is { } npcId && await db.NpcSheets.FindAsync(npcId) is { OwnerId: { } donoDoNpc } npc)
        {
            var (vitalidade, foco, adrenalina, _) = await npcs.MaximosAsync(npc, await condicoes.ParaNpcAsync(npc));
            npc.VitalidadeAtual = Novo(npc.VitalidadeAtual, pv, vitalidade);
            npc.FocoAtual = Novo(npc.FocoAtual, pf, foco);
            npc.AdrenalinaAtual = Novo(npc.AdrenalinaAtual, pa, adrenalina);
            return mudou ? new FichaAlterada("npc", npc.Id, donoDoNpc) : null;
        }
        if (participant.SourceCreatureSheetId is { } criaturaId && await db.CreatureSheets.FindAsync(criaturaId) is { OwnerId: { } donoDaCriatura } criatura)
        {
            var (vitalidade, foco, adrenalina) = await criaturas.MaximosAsync(criatura, await condicoes.ParaCriaturaAsync(criatura));
            criatura.VitalidadeAtual = Novo(criatura.VitalidadeAtual, pv, vitalidade);
            criatura.FocoAtual = Novo(criatura.FocoAtual, pf, foco);
            criatura.AdrenalinaAtual = Novo(criatura.AdrenalinaAtual, pa, adrenalina);
            return mudou ? new FichaAlterada("creature", criatura.Id, donoDaCriatura) : null;
        }
        return null;
    }

    /// <summary>
    /// Depois do salvamento: CondicoesAlteradas ao dono e ao GM (a ficha aberta recarrega os valores atuais, pelo mesmo
    /// caminho adiado das Condições) e ParticipantsChanged a todo encontro com a ficha. Uma falha dos hubs vai para o
    /// log da aplicação e nunca derruba a ação, já salva.
    /// </summary>
    private async Task AvisarFichaAlteradaAsync(FichaAlterada ficha)
    {
        foreach (var userId in new[] { ficha.OwnerId, CurrentGmId() }.Distinct())
        {
            try
            {
                await notificacoes.Clients.User(userId.ToString()).SendAsync(NotificationEvents.CondicoesAlteradas,
                    new CondicoesAlteradasNotification(ficha.Kind, ficha.Id.ToString()));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to push CondicoesAlteradas for sheet {SheetId}", ficha.Id);
            }
        }

        var encounterIds = await db.EncounterParticipants
            .Where(p => p.SourceCharacterSheetId == ficha.Id || p.SourceNpcSheetId == ficha.Id || p.SourceCreatureSheetId == ficha.Id)
            .Select(p => p.EncounterId).Distinct().ToListAsync();
        foreach (var encounterId in encounterIds)
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
    }

    [HttpDelete("{id}")]
    [ConflitoDeConcorrenciaDoEncontro]
    public async Task<IActionResult> Delete(Guid encounterId, Guid id)
    {
        var (authError, encounter) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;
        if (RequireEditable(encounter!) is { } wrongPhase)
            return wrongPhase;

        var participants = await db.EncounterParticipants
            .Where(p => p.EncounterId == encounterId)
            .OrderByDescending(p => p.Iniciativa).ThenBy(p => p.Id)
            .ToListAsync();
        var participant = participants.FirstOrDefault(p => p.Id == id);
        if (participant is null)
            return NotFound();

        // Registered first, while the removed participant still has the turn: "X saiu" belongs under X's own vez and Turno.
        await log.RegistrarAsync(encounter!, EncounterLogTipo.ParticipanteRemovido, participant.Nome);

        MudancaDeVez? mudanca = null;
        if (encounter!.CurrentParticipantId == id)
        {
            // Order passed is the one BEFORE the removal.
            var next = TurnAdvanceCalculator.AfterRemoval(encounter.CurrentRound, id, participants.Select(p => p.Id).ToList());
            var receiver = next is { } handover ? participants.Single(p => p.Id == handover.ParticipantId) : null;
            mudanca = await turnos.DarAVezAsync(encounter, receiver, next?.Round ?? encounter.CurrentRound, MotivoDaVez.Remocao);
        }

        var conditions = await db.EncounterParticipantConditions.Where(c => c.EncounterParticipantId == id).ToListAsync();
        db.EncounterParticipantConditions.RemoveRange(conditions);
        // As Condições do catálogo saem com ele. Apagadas aqui (e não só pela cascata) para o log da ficha registrar cada uma.
        db.EncounterParticipantCondicoes.RemoveRange(await db.EncounterParticipantCondicoes.Where(c => c.EncounterParticipantId == id).ToListAsync());
        db.EncounterParticipants.Remove(participant);
        // Quem recebeu a vez: expiração (se entrou num Turno novo) e linhas "a cada vez" dele. O removido já não conta.
        if (mudanca is not null)
            await condicoesDoEncontro.AoMudarAVezAsync(encounter, mudanca);
        // A ficha do removido volta aos totais sem as Condições dele.
        await condicoesDoEncontro.AoMudarAtivacaoAsync([id]);

        await db.SaveChangesAsync();
        await condicoesDoEncontro.NotificarAsync();
        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<EncounterParticipantResponse>>> List(Guid encounterId)
    {
        var (authError, encounter) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;

        var participants = await db.EncounterParticipants
            .Where(p => p.EncounterId == encounterId)
            .OrderByDescending(p => p.Iniciativa).ThenBy(p => p.Id)
            .ToListAsync();

        var participantIds = participants.Select(p => p.Id).ToList();
        var conditionsByParticipantId = (await db.EncounterParticipantConditions
                .Where(c => participantIds.Contains(c.EncounterParticipantId))
                .ToListAsync())
            .GroupBy(c => c.EncounterParticipantId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Texto).ToList());

        var aplicadasByParticipantId = (await db.EncounterParticipantCondicoes
                .Where(c => participantIds.Contains(c.EncounterParticipantId))
                .ToListAsync())
            .GroupBy(c => c.EncounterParticipantId)
            .ToDictionary(g => g.Key, g => g.Select(c => CondicaoAplicadaMapper.ToResponse(c, encounter!)).ToList());

        var fichaIds = participants.Where(p => p.SourceCharacterSheetId is not null).Select(p => p.SourceCharacterSheetId!.Value).Distinct().ToList();
        var fichas = await db.CharacterSheets.Where(s => fichaIds.Contains(s.Id)).ToListAsync();
        var motivos = await cemiterio.MotivosAsync(fichas);

        var responses = new List<EncounterParticipantResponse>();
        foreach (var participant in participants)
            responses.Add(await ToResponseAsync(participant, conditionsByParticipantId.GetValueOrDefault(participant.Id, new List<string>()),
                aplicadasByParticipantId.GetValueOrDefault(participant.Id, new List<CondicaoAplicadaResponse>()),
                participant.SourceCharacterSheetId is { } fichaId && motivos.TryGetValue(fichaId, out var daFicha) && daFicha.Count > 0));
        return responses;
    }

    /// <param name="noCemiterio">Só uma Ficha de Personagem pode estar no cemitério; NPC/Criatura respondem sempre false.</param>
    private async Task<EncounterParticipantResponse> ToResponseAsync(EncounterParticipant p, List<string> condicoes, List<CondicaoAplicadaResponse> condicoesAplicadas, bool noCemiterio)
    {
        if (p.SourceCharacterSheetId is not null)
        {
            var sheet = await db.CharacterSheets.FindAsync(p.SourceCharacterSheetId.Value);
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa, sheet?.VitalidadeAtual, sheet?.FocoAtual, sheet?.AdrenalinaAtual, p.AcoesRestantes, IsLiveSourced: true, condicoes, condicoesAplicadas, "Personagem", noCemiterio);
        }
        if (p.SourceNpcSheetId is not null)
        {
            var sheet = await db.NpcSheets.FindAsync(p.SourceNpcSheetId.Value);
            var isLive = sheet?.OwnerId is not null;
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa,
                isLive ? sheet?.VitalidadeAtual : p.PVAtual, isLive ? sheet?.FocoAtual : p.PFAtual, isLive ? sheet?.AdrenalinaAtual : p.PAAtual,
                p.AcoesRestantes, isLive, condicoes, condicoesAplicadas, "Npc");
        }
        if (p.SourceCreatureSheetId is not null)
        {
            var sheet = await db.CreatureSheets.FindAsync(p.SourceCreatureSheetId.Value);
            var isLive = sheet?.OwnerId is not null;
            return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa,
                isLive ? sheet?.VitalidadeAtual : p.PVAtual, isLive ? sheet?.FocoAtual : p.PFAtual, isLive ? sheet?.AdrenalinaAtual : p.PAAtual,
                p.AcoesRestantes, isLive, condicoes, condicoesAplicadas, "Criatura");
        }
        // Source sheet was deleted (SetNull cascaded) — Nome snapshot still displays, PV/PF/PA frozen at whatever the columns last held.
        return new EncounterParticipantResponse(p.Id.ToString(), p.Nome, p.Iniciativa, p.PVAtual, p.PFAtual, p.PAAtual, p.AcoesRestantes, IsLiveSourced: false, condicoes, condicoesAplicadas);
    }

    private async Task LogChangesAsync(Encounter encounter, EncounterParticipant participant,
        (int Iniciativa, int AcoesRestantes, int? PV, int? PF, int? PA) antes, List<string> condicoesAntes, List<string> condicoesDepois)
    {
        async Task CampoAsync<T>(string campo, T antigo, T novo)
        {
            if (!EqualityComparer<T>.Default.Equals(antigo, novo))
                await log.RegistrarAsync(encounter, EncounterLogTipo.CampoAlterado, participant.Nome, campo, Texto(antigo), Texto(novo));
        }
        await CampoAsync("Iniciativa", antes.Iniciativa, participant.Iniciativa);
        await CampoAsync("Ações", antes.AcoesRestantes, participant.AcoesRestantes);
        await CampoAsync("PV", antes.PV, participant.PVAtual);
        await CampoAsync("PF", antes.PF, participant.PFAtual);
        await CampoAsync("PA", antes.PA, participant.PAAtual);

        foreach (var texto in condicoesDepois.Distinct().Where(t => !condicoesAntes.Contains(t)))
            await log.RegistrarAsync(encounter, EncounterLogTipo.CondicaoAdicionada, participant.Nome, texto: texto);
        foreach (var texto in condicoesAntes.Distinct().Where(t => !condicoesDepois.Contains(t)))
            await log.RegistrarAsync(encounter, EncounterLogTipo.CondicaoRemovida, participant.Nome, texto: texto);
    }

    private static string Texto<T>(T valor) => valor?.ToString() ?? "—";

    private ActionResult? RequireEditable(Encounter encounter) =>
        encounter.Status == EncounterStatus.Finalizado ? Conflict("Encontro finalizado.") : null;

    private async Task<(ActionResult? Error, Encounter? Encounter)> CheckEncounterOwnershipAsync(Guid encounterId)
    {
        var encounter = await db.Encounters.FindAsync(encounterId);
        if (encounter is null)
            return (NotFound(), null);

        var isOwner = await db.Campaigns.AnyAsync(c => c.Id == encounter.CampaignId && c.GmId == CurrentGmId());
        return isOwner ? (null, encounter) : (NotFound(), null);
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
