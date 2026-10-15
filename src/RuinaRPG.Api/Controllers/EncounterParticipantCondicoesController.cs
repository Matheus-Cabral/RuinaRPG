using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Condicoes;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Condições do catálogo aplicadas a um participante do encontro. A aplicação copia a entrada (cópia independente);
/// o efeito nos números da ficha vem de CondicoesAtivasDaFicha, e o que muda no momento (corte do Atual, log da
/// ficha, aviso ao dono) de CondicoesDoEncontro.
/// </summary>
[ApiController]
[Authorize(Roles = "GM")]
[Route("api/encounters/{encounterId}/participants/{participantId}")]
public class EncounterParticipantCondicoesController(RuinaRpgDbContext db, EncounterLog log, ItensDesabilitaveisDaFicha itens, CondicoesDoEncontro condicoesDoEncontro) : ControllerBase
{
    [HttpPost("condicoes")]
    [ConflitoDeConcorrenciaDoEncontro]
    public async Task<ActionResult<CondicaoAplicadaResponse>> Aplicar(Guid encounterId, Guid participantId, AplicarCondicaoRequest request)
    {
        var (authError, encounter) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;
        if (RequireEditable(encounter!) is { } wrongPhase)
            return wrongPhase;

        var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == participantId && p.EncounterId == encounterId);
        if (participant is null)
            return NotFound();

        if (!Guid.TryParse(request.CondicaoId, out var condicaoId))
            return BadRequest("Condição não encontrada.");
        var condicao = await db.Condicoes.FirstOrDefaultAsync(c => c.Id == condicaoId && !c.IsDeleted);
        if (condicao is null)
            return BadRequest("Condição não encontrada.");
        if (await db.EncounterParticipantCondicoes.AnyAsync(c => c.EncounterParticipantId == participantId && c.CondicaoId == condicaoId))
            return BadRequest("Esta condição já está neste participante.");
        if (request.DuracaoTurnos is < 1)
            return BadRequest("A duração deve ser de pelo menos 1 Turno.");
        if (request.SemDuracao && request.DuracaoTurnos is not null)
            return BadRequest("Informe a duração ou marque sem duração, não os dois.");

        var modificadores = await db.CondicaoModificadores.Where(m => m.CondicaoId == condicaoId).OrderBy(m => m.Ordem).ToListAsync();
        // Cada valor ajustado diz qual linha o GM viu; se a entrada mudou no catálogo desde então, a escolha é refeita.
        // O valor segue os limites da linha no catálogo (CondicaoValidator).
        var ajustados = new Dictionary<int, int>();
        foreach (var ajuste in request.ValoresAjustados ?? [])
        {
            if (ajuste.Linha < 0 || ajuste.Linha >= modificadores.Count)
                return BadRequest($"Não existe a linha {ajuste.Linha} nesta condição.");
            var linha = modificadores[ajuste.Linha];
            if (ajuste.Alvo != linha.Alvo.ToString() || !string.Equals(ajuste.AlvoEspecifico, linha.AlvoEspecifico, StringComparison.OrdinalIgnoreCase))
                return Conflict("A Condição foi alterada no catálogo. Reabra a escolha.");
            if (!ajustados.TryAdd(ajuste.Linha, ajuste.Valor))
                return BadRequest($"A linha {ajuste.Linha} foi ajustada mais de uma vez.");
            if (CondicaoValidator.ValidarValor(new ModificadorDeCondicao(linha.Alvo, linha.AlvoEspecifico, linha.TipoDeValor, ajuste.Valor, linha.Momento)) is { } erroDoValor)
                return BadRequest(erroDoValor);
        }

        var (erro, desabilitacoes) = await ResolverDesabilitacoesAsync(participant, condicaoId, request.ItensDesabilitados ?? []);
        if (erro is not null)
            return BadRequest(erro);
        var (erroDasMaestrias, maestrias) = await ResolverMaestriasAsync(participant, modificadores, request.MaestriasEscolhidas ?? []);
        if (erroDasMaestrias is not null)
            return BadRequest(erroDasMaestrias);

        var aplicada = new EncounterParticipantCondicao
        {
            Id = Guid.NewGuid(),
            EncounterParticipantId = participantId,
            CondicaoId = condicaoId,
            Nome = condicao.Nome,
            Descricao = condicao.Descricao,
            Icone = condicao.Icone,
            AplicadaNoTurno = encounter!.CurrentRound,
            DuracaoTurnos = request.SemDuracao ? null : request.DuracaoTurnos ?? condicao.DuracaoPadraoTurnos,
            Modificadores = modificadores.Select((m, i) => new LinhaDeCondicaoAplicada(Guid.NewGuid(), m.Alvo, m.AlvoEspecifico, m.TipoDeValor,
                ajustados.TryGetValue(i, out var valor) ? valor : m.Valor, m.Momento, maestrias!.GetValueOrDefault(i))).ToList(),
            Desabilitacoes = desabilitacoes!,
        };
        db.EncounterParticipantCondicoes.Add(aplicada);
        await log.RegistrarAsync(encounter, EncounterLogTipo.CondicaoAdicionada, participant.Nome, texto: aplicada.Nome);
        await condicoesDoEncontro.AoMudarAtivacaoAsync([participantId]);
        await condicoesDoEncontro.AoAplicarAsync(encounter, participant, aplicada);
        await db.SaveChangesAsync();
        await condicoesDoEncontro.NotificarAsync();

        return Created(string.Empty, CondicaoAplicadaMapper.ToResponse(aplicada, encounter));
    }

    [HttpDelete("condicoes/{condicaoAplicadaId}")]
    public async Task<IActionResult> Remover(Guid encounterId, Guid participantId, Guid condicaoAplicadaId)
    {
        var (authError, encounter) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;
        if (RequireEditable(encounter!) is { } wrongPhase)
            return wrongPhase;

        var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == participantId && p.EncounterId == encounterId);
        var condicao = participant is null ? null
            : await db.EncounterParticipantCondicoes.FirstOrDefaultAsync(c => c.Id == condicaoAplicadaId && c.EncounterParticipantId == participantId);
        if (participant is null || condicao is null)
            return NotFound();

        db.EncounterParticipantCondicoes.Remove(condicao);
        await log.RegistrarAsync(encounter!, EncounterLogTipo.CondicaoRemovida, participant.Nome, texto: condicao.Nome);
        await condicoesDoEncontro.AoMudarAtivacaoAsync([participantId]);
        await db.SaveChangesAsync();
        await condicoesDoEncontro.NotificarAsync();
        return NoContent();
    }

    [HttpGet("itens-desabilitaveis")]
    public async Task<ActionResult<ItensDesabilitaveisResponse>> ItensDesabilitaveis(Guid encounterId, Guid participantId)
    {
        var (authError, _) = await CheckEncounterOwnershipAsync(encounterId);
        if (authError is not null)
            return authError;

        var participant = await db.EncounterParticipants.FirstOrDefaultAsync(p => p.Id == participantId && p.EncounterId == encounterId);
        if (participant is null)
            return NotFound();

        var porCategoria = await itens.ListarAsync(participant);
        var maestrias = await itens.MaestriasAsync(participant);
        return new ItensDesabilitaveisResponse(porCategoria.ToDictionary(
            c => c.Key.ToString(),
            c => c.Value.Select(i => new ItemDesabilitavelDto(i.Id.ToString(), i.Nome)).ToList()),
            maestrias.Select(m => new MaestriaDaFichaDto(m.Id.ToString(), m.Nome, m.Pericia)).ToList());
    }

    /// <summary>
    /// Uma categoria da entrada do catálogo sem ids escolhidos desabilita a categoria inteira (lista vazia). Os ids
    /// escolhidos têm de ser linhas da ficha do participante, naquela categoria, e a categoria tem de ser da entrada.
    /// </summary>
    private async Task<(string? Erro, Dictionary<CategoriaDesabilitavel, List<Guid>>? Desabilitacoes)> ResolverDesabilitacoesAsync(
        EncounterParticipant participant, Guid condicaoId, Dictionary<string, List<string>> escolhidos)
    {
        var doCatalogo = (await db.CondicaoDesabilitacoes.Where(d => d.CondicaoId == condicaoId).Select(d => d.Categoria).ToListAsync()).ToHashSet();
        var resultado = doCatalogo.ToDictionary(c => c, _ => new List<Guid>());
        if (escolhidos.Count == 0)
            return (null, resultado);

        var daFicha = await itens.ListarAsync(participant);
        foreach (var (chave, ids) in escolhidos)
        {
            if (!Enum.TryParse<CategoriaDesabilitavel>(chave, out var categoria) || !Enum.IsDefined(categoria))
                return ($"Categoria inválida: {chave}.", null);
            if (!doCatalogo.Contains(categoria))
                return ($"Esta condição não desabilita a categoria {chave}.", null);

            var daCategoria = daFicha[categoria].Select(i => i.Id).ToHashSet();
            foreach (var texto in ids)
            {
                if (!Guid.TryParse(texto, out var id) || !daCategoria.Contains(id))
                    return ($"O item {texto} não é da ficha do participante na categoria {chave}.", null);
                if (!resultado[categoria].Contains(id))
                    resultado[categoria].Add(id);
            }
        }
        return (null, resultado);
    }

    /// <summary>
    /// Por linha de Maestria (índice), as linhas de Maestria da ficha do participante a que o GM a restringiu. Lista vazia =
    /// sem restrição. Cada id tem de ser de uma Maestria da ficha e, se a linha é de uma Perícia, de uma Maestria dela.
    /// </summary>
    private async Task<(string? Erro, Dictionary<int, List<Guid>>? Maestrias)> ResolverMaestriasAsync(
        EncounterParticipant participant, List<CondicaoModificador> modificadores, Dictionary<int, List<string>> escolhidas)
    {
        var resultado = new Dictionary<int, List<Guid>>();
        if (escolhidas.Count == 0)
            return (null, resultado);

        var daFicha = (await itens.MaestriasAsync(participant)).ToDictionary(m => m.Id);
        foreach (var (indice, ids) in escolhidas)
        {
            if (indice < 0 || indice >= modificadores.Count)
                return ($"Não existe a linha {indice} nesta condição.", null);
            var linha = modificadores[indice];
            if (linha.Alvo != AlvoDeCondicao.Maestria)
                return ($"A linha {indice} desta condição não é de Maestria.", null);
            if (ids.Count == 0)
                continue;

            var escolhidasDaLinha = new List<Guid>();
            foreach (var texto in ids)
            {
                if (!Guid.TryParse(texto, out var id) || !daFicha.TryGetValue(id, out var maestria))
                    return ($"A Maestria {texto} não é da ficha do participante.", null);
                if (linha.AlvoEspecifico is not null && !string.Equals(linha.AlvoEspecifico, maestria.Pericia, StringComparison.OrdinalIgnoreCase))
                    return ($"A Maestria {maestria.Nome} não é da Perícia da linha {indice}.", null);
                if (!escolhidasDaLinha.Contains(id))
                    escolhidasDaLinha.Add(id);
            }
            resultado[indice] = escolhidasDaLinha;
        }
        return (null, resultado);
    }

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
