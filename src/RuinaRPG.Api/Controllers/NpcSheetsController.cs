using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/npc-sheets")]
[Authorize]
public class NpcSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules, ITabelaDeNiveis tabelaDeNiveis, IHubContext<EncounterHub> hub, ILogger<NpcSheetsController> logger, NpcSheetStats stats, IPericiaCatalogo pericias, EquipmentPenaltyService penalidades, CondicoesAtivasDaFicha condicoes, ITipoDeFicha<NpcSheet> tipo) : ControllerBase
{
    // Creating a fresh (un-granted) NPC is GM roster curation, not something a player who's been
    // granted one already does — same reasoning as List below.
    [HttpPost]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<NpcSheetResponse>> Create()
    {
        var gmId = CurrentUserId();

        var sheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gmId };
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Inicial(sheet.Nivel, await tabelaDeNiveis.ObterAsync());
        db.NpcSheets.Add(sheet);

        foreach (var atributo in Enum.GetValues<Atributo>())
            db.NpcAttributes.Add(new NpcAttribute { Id = Guid.NewGuid(), SheetId = sheet.Id, Atributo = atributo });
        foreach (var pericia in (await pericias.TodasAsync()).Where(p => !p.IsDeleted))
            db.NpcSkills.Add(new NpcSkill { Id = Guid.NewGuid(), SheetId = sheet.Id, PericiaId = pericia.Id, AtributoEscolhido = pericia.AtributoSugerido });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            db.NpcArmorSlots.Add(new NpcArmorSlot { Id = Guid.NewGuid(), SheetId = sheet.Id, Slot = slot });

        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NpcSheetResponse>> Get(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await ToResponseAsync(sheet);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateNpcSheetRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var tabela = await tabelaDeNiveis.ObterAsync();
        if (request.Nivel < 1 || request.Nivel > tabela.UltimoNivel)
            return BadRequest($"O nível deve estar entre 1 e {tabela.UltimoNivel}.");

        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (!TryParseEnum<Linhagem>(request.Linhagem, out var linhagem))
            return BadRequest("Linhagem inválida.");
        if (!TryParseEnum<Variante>(request.Variante, out var variante))
            return BadRequest("Variante inválida.");
        if (linhagem is not null && variante is not null && !LinhagemVarianteValidator.IsValidCombination(linhagem.Value, variante.Value))
            return BadRequest("A Variante escolhida não pertence à Linhagem escolhida.");
        if (await VarianteLiberadaResolver.EscolhaBloqueadaAsync(db, sheet.GmId, variante, sheet.Variante))
            return BadRequest("Essa Variante ainda não foi liberada pelo GM.");
        if (!TryParseEnum<Vocacao>(request.Vocacao, out var vocacao))
            return BadRequest("Vocação inválida.");
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
        if (afinidade is not null && afinidade != sheet.Afinidade && CaminhoSubElementoRules.EhCaminho(afinidade.Value))
            return BadRequest("Alma e Vida são Caminhos, não Afinidades.");
        if (!Enum.TryParse<Cobertura>(request.Cobertura, out var cobertura))
            return BadRequest("Cobertura inválida.");
        if (!TryParseEnum<Estrela>(request.Estrela, out var estrela))
            return BadRequest("Estrela inválida.");
        if (request.SinaAtual is < 0 or > 3)
            return BadRequest("SinaAtual deve estar entre 0 e 3.");
        if (request.AfinidadeAdicional < 0)
            return BadRequest("Afinidade Adicional não pode ser negativa.");

        Guid? historicoId = null;
        if (!string.IsNullOrWhiteSpace(request.HistoricoId))
        {
            if (!Guid.TryParse(request.HistoricoId, out var parsedHistoricoId))
                return BadRequest("HistoricoId inválido.");
            if (!await db.Historicos.AnyAsync(h => h.Id == parsedHistoricoId && !h.IsDeleted))
                return BadRequest("Histórico não encontrado.");
            historicoId = parsedHistoricoId;
        }

        // A Variante change makes any racial characteristics already granted for the old one stale
        // (Ruína RPG - Sistema Básico.md §7 grants are per-Variante) — remove them so 5.d never
        // shows a grant that no longer matches the NPC's race, and PendingRacialTraitChoice
        // correctly reports the new Variante as unresolved again.
        var varianteChanged = sheet.Variante != variante;

        sheet.ImageId = imageId;
        sheet.Nome = request.Nome;
        sheet.Linhagem = linhagem;
        sheet.Variante = variante;
        sheet.Vocacao = vocacao;
        sheet.SubVocacao = request.SubVocacao;
        sheet.Afinidade = afinidade;
        sheet.Propriedade = request.Propriedade;
        sheet.Estrela = estrela;
        sheet.HistoricoId = historicoId;
        var nivelAntigo = sheet.Nivel;
        sheet.Nivel = request.Nivel;
        sheet.PossuiCoracaoDeMana = request.PossuiCoracaoDeMana;
        sheet.AfinidadeAdicional = request.AfinidadeAdicional;
        sheet.ExperienciaAtual = request.ExperienciaAtual;
        // EAPAtual (VIS) é calculado por Nível + Âmbares (NpcSheetStats.EapAtualAsync), como na Ficha de
        // Personagem: request.EAPAtual é aceito mas ignorado e a coluna NpcSheet.EAPAtual não é mais usada.
        sheet.NucleosRankF = request.NucleosRankF;
        sheet.NucleosRankE = request.NucleosRankE;
        sheet.NucleosRankD = request.NucleosRankD;
        sheet.NucleosRankC = request.NucleosRankC;
        sheet.NucleosRankB = request.NucleosRankB;
        sheet.NucleosRankA = request.NucleosRankA;
        sheet.NucleosRankS = request.NucleosRankS;
        // Mesma regra do Personagem (Ficha de Personagem 1.b): o Atual acompanha o Nível. O Total do
        // NPC deixou de ser editável na 1.5.0 — request.PontosDeIgnicaoTotal é aceito mas ignorado.
        // request.PontosDeIgnicaoAtual também é aceito mas ignorado: a diferença de nível entra sobre o
        // saldo guardado, para um formulário desatualizado não apagar um débito, uma devolução ou um
        // ajuste de nível feito pelo servidor. A edição manual tem endpoint próprio (UpdatePontosDeIgnicao).
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Aplicar(sheet.PontosDeIgnicaoAtual,
            PontosDeIgnicaoPorNivel.Delta(nivelAntigo, request.Nivel, tabela));
        // Só o Atual que o jogador editou é gravado, e no máximo atual da ficha (AtualDoFormulario): o servidor também o
        // escreve, e um formulário desatualizado não pode desfazer um ajuste nem devolver o que um corte tirou.
        var (vitalidadeMaximo, focoMaximo, adrenalinaMaximo, estresseMaximo) = await stats.MaximosAsync(sheet, await condicoes.ParaNpcAsync(sheet));
        sheet.VitalidadeAtual = AtualDoFormulario.EscolherNoMaximo(sheet.VitalidadeAtual, request.VitalidadeAtual, request.VitalidadeAtualCarregada, vitalidadeMaximo);
        sheet.FocoAtual = AtualDoFormulario.EscolherNoMaximo(sheet.FocoAtual, request.FocoAtual, request.FocoAtualCarregado, focoMaximo);
        sheet.AdrenalinaAtual = AtualDoFormulario.EscolherNoMaximo(sheet.AdrenalinaAtual, request.AdrenalinaAtual, request.AdrenalinaAtualCarregada, adrenalinaMaximo);
        sheet.EstresseAtual = AtualDoFormulario.EscolherNoMaximo(sheet.EstresseAtual, request.EstresseAtual, request.EstresseAtualCarregado, estresseMaximo);
        // Só rejeita quando o valor MUDA para fora da tabela: se o GM diminuiu o dado depois da rolagem,
        // a ficha continua podendo salvar os outros campos mantendo a mesma ArcaRolada.
        if (request.ArcaRolada != sheet.ArcaRolada)
        {
            var arcaDado = await db.DadoDeArcaDoGmAsync(sheet.GmId);
            if (request.ArcaRolada < 1 || request.ArcaRolada > arcaDado)
                return BadRequest($"ArcaRolada deve estar entre 1 e {arcaDado}.");
        }

        sheet.Cobertura = cobertura;
        sheet.Ciclos = request.Ciclos;
        sheet.ArcaRolada = request.ArcaRolada;
        sheet.SinaAtual = request.SinaAtual;

        if (varianteChanged)
        {
            var staleRacialTraits = await db.NpcTraits.Where(t => t.SheetId == id && t.IsRacial).ToListAsync();
            db.NpcTraits.RemoveRange(staleRacialTraits);
        }

        await db.SaveChangesAsync();
        await NotifyAffectedEncountersAsync(id);

        return NoContent();
    }

    /// <summary>
    /// Aba História — separate from UpdateNpcSheetRequest for the same reason as
    /// CharacterSheetsController.UpdateHistoria. Same authorization as Update (NotFound for strangers).
    /// </summary>
    [HttpPut("{id}/historia")]
    public async Task<IActionResult> UpdateHistoria(Guid id, UpdateHistoriaRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        // The limit applies to what's stored (sanitizing can grow CSS values); the raw cap only bounds the work.
        if (request.Historia is { Length: > HistoriaSanitizer.MaxRawLength })
            return BadRequest(HistoriaSanitizer.MaxLengthMessage);

        // Imagens dentro do texto: só as do app que quem salva pode usar (ou que a História já trazia) —
        // qualquer outro <img> é removido, sem erro (HistoriaSanitizer / HistoriaImageAccess).
        var imagens = await HistoriaImageAccess.AllowedInlineFilesAsync(db, request.Historia, sheet.Historia, CurrentUserId(), sheet.GmId, await CampaignIdForImagesAsync(sheet));
        var clean = HistoriaSanitizer.Sanitize(request.Historia, imagens);
        if (clean is { Length: > HistoriaSanitizer.MaxLength })
            return BadRequest(HistoriaSanitizer.MaxLengthMessage);

        sheet.Historia = clean;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Aba História — a galeria de imagens anexadas, na ordem em que aparecem. Quem pode abrir a ficha pode ver.</summary>
    [HttpGet("{id}/historia/imagens")]
    public async Task<ActionResult<List<HistoriaImagemResponse>>> HistoriaImagens(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var ids = await db.NpcSheetHistoriaImages.Where(i => i.SheetId == id).OrderBy(i => i.Ordem).Select(i => i.ImageId).ToListAsync();
        return await HistoriaImageAccess.ToResponseAsync(db, ids);
    }

    /// <summary>
    /// Aba História — substitui a galeria pela lista recebida (anexar, remover e reordenar são o mesmo PUT).
    /// Mesma autorização de UpdateHistoria; uma imagem nova na galeria precisa ser de quem salva
    /// (HistoriaImageAccess), senão 400 e nada muda.
    /// </summary>
    [HttpPut("{id}/historia/imagens")]
    public async Task<ActionResult<List<HistoriaImagemResponse>>> UpdateHistoriaImagens(Guid id, UpdateHistoriaImagensRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        if (!HistoriaImageAccess.TryParseIds(request.ImageIds, out var ids))
            return BadRequest(HistoriaImageAccess.InvalidIdMessage);
        if (ids.Count > HistoriaImageAccess.MaxImagens)
            return BadRequest(HistoriaImageAccess.MaxImagensMessage);

        var existing = await db.NpcSheetHistoriaImages.Where(i => i.SheetId == id).ToListAsync();
        var novas = ids.Where(imageId => existing.All(e => e.ImageId != imageId)).ToList();
        if ((await HistoriaImageAccess.UsableAsync(db, novas, CurrentUserId(), sheet.GmId, await CampaignIdForImagesAsync(sheet))).Count != novas.Count)
            return BadRequest(HistoriaImageAccess.NotFoundMessage);

        db.NpcSheetHistoriaImages.RemoveRange(existing.Where(e => !ids.Contains(e.ImageId)));
        for (var ordem = 0; ordem < ids.Count; ordem++)
        {
            var imageId = ids[ordem];
            if (existing.FirstOrDefault(e => e.ImageId == imageId) is { } row)
                row.Ordem = ordem;
            else
                db.NpcSheetHistoriaImages.Add(new NpcSheetHistoriaImage { SheetId = id, ImageId = imageId, Ordem = ordem });
        }
        await db.SaveChangesAsync();

        return await HistoriaImageAccess.ToResponseAsync(db, ids);
    }

    /// <summary>
    /// A campanha em que o NPC foi concedido ao jogador — só interessa quando quem chama é o jogador (é o que
    /// libera as imagens públicas daquela campanha para ele, como em NpcRunesController); null para o GM.
    /// </summary>
    private async Task<Guid?> CampaignIdForImagesAsync(NpcSheet sheet)
    {
        if (CurrentUserId() == sheet.GmId)
            return null;

        return await tipo.CampanhaAsync(sheet);
    }

    // NPCs (unlike Ficha de Personagem, where Nível is calculated-only from Experiência Atual)
    // keep Nível directly GM-editable — but the two fields still need to land on the same pair the
    // XP-threshold table would derive from either alone (Requisitos - Ficha de NPCs). The generic
    // Update above can't apply that rule itself: it always receives both fields in one full-form
    // submission, with no reliable way to tell "the GM just edited Nível" apart from "Nível simply
    // wasn't touched and still reads its old value" (a resend that happens to equal the old value
    // either way). These two endpoints exist so the client can say which field it actually means,
    // by calling one or the other — see FichaDeNpc.razor's Nível/Experiência Atual fields.
    [HttpPut("{id}/nivel")]
    public async Task<IActionResult> UpdateNivel(Guid id, [FromBody] int nivel)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var tabela = await tabelaDeNiveis.ObterAsync();
        if (nivel < 1 || nivel > tabela.UltimoNivel)
            return BadRequest($"O nível deve estar entre 1 e {tabela.UltimoNivel}.");

        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Aplicar(sheet.PontosDeIgnicaoAtual,
            PontosDeIgnicaoPorNivel.Delta(sheet.Nivel, nivel, tabela));
        sheet.Nivel = nivel;
        sheet.ExperienciaAtual = NivelCalculator.MinXpParaNivel(nivel, tabela.ComoXpPorNivel());
        await db.SaveChangesAsync();
        await NotifyAffectedEncountersAsync(id);

        return NoContent();
    }

    [HttpPut("{id}/experiencia-atual")]
    public async Task<IActionResult> UpdateExperienciaAtual(Guid id, [FromBody] int experienciaAtual)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        sheet.ExperienciaAtual = experienciaAtual;
        var tabela = await tabelaDeNiveis.ObterAsync();
        var nivel = NivelCalculator.Compute(experienciaAtual, tabela.ComoXpPorNivel());
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Aplicar(sheet.PontosDeIgnicaoAtual,
            PontosDeIgnicaoPorNivel.Delta(sheet.Nivel, nivel, tabela));
        sheet.Nivel = nivel;
        await db.SaveChangesAsync();
        await NotifyAffectedEncountersAsync(id);

        return NoContent();
    }

    // Edição manual do saldo de Pontos de Ignição — fora do Update genérico pelo mesmo motivo de Nível e
    // Experiência Atual terem os seus: o formulário inteiro não diz qual campo o usuário de fato mexeu, e
    // um saldo desatualizado nele apagaria o que o servidor debitou ou devolveu. Não avisa os Encontros:
    // eles não exibem Pontos de Ignição.
    [HttpPut("{id}/pontos-de-ignicao")]
    public async Task<IActionResult> UpdatePontosDeIgnicao(Guid id, [FromBody] int pontos)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        if (pontos < 0)
            return BadRequest("Os Pontos de Ignição não podem ser negativos.");

        sheet.PontosDeIgnicaoAtual = pontos;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id}/level-up-notice")]
    public async Task<ActionResult<LevelUpNoticeResponse>> LevelUpNotice(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var tabela = await tabelaDeNiveis.ObterAsync();
        return new LevelUpNoticeResponse(tabela.LinhasDeBonusAcumuladas(sheet.LastDismissedLevelUpLevel ?? 0, sheet.Nivel).ToList());
    }

    [HttpPost("{id}/dismiss-level-up-notice")]
    public async Task<IActionResult> DismissLevelUpNotice(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        sheet.LastDismissedLevelUpLevel = sheet.Nivel;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task NotifyAffectedEncountersAsync(Guid npcSheetId)
    {
        var affectedEncounterIds = await db.EncounterParticipants
            .Where(p => p.SourceNpcSheetId == npcSheetId)
            .Select(p => p.EncounterId)
            .Distinct()
            .ToListAsync();
        foreach (var encounterId in affectedEncounterIds)
        {
            // Item 3 of the gap audit: the sheet is already saved at this point — a hub failure
            // must not surface as a 500 to a caller whose save genuinely succeeded.
            try
            {
                await hub.Clients.Group($"encounter-{encounterId}").SendAsync("ParticipantsChanged");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify encounter {EncounterId} of a ParticipantsChanged update.", encounterId);
            }
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        db.NpcSheets.Remove(sheet);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/variantes-liberadas")]
    public async Task<ActionResult<List<VarianteLiberadaResponse>>> VariantesLiberadas(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await VarianteLiberadaResolver.ListAsync(db, sheet.GmId);
    }

    [HttpGet("{id}/racial-ability")]
    public async Task<ActionResult<RacialAbilityResponse>> RacialAbility(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var arcaDado = await db.DadoDeArcaDoGmAsync(sheet.GmId);
        if (sheet.Variante is null)
            return new RacialAbilityResponse(null, null, null, null, null, new(), arcaDado);

        var over = await db.RacialAbilityOverrides.FirstOrDefaultAsync(o => o.GmId == sheet.GmId && o.Variante == sheet.Variante.Value);
        var (nome, descricao) = over is not null
            ? (over.Nome, over.Descricao)
            : (RacialAbilityLookup.For(sheet.Variante.Value, arcaDado).Nome, RacialAbilityLookup.For(sheet.Variante.Value, arcaDado).Descricao);

        string? arcaNome = null;
        string? arcaDescricao = null;
        var arcaEvolucoes = new List<ArcaEvolucaoResponse>();
        var arcaForaDaTabela = sheet.Linhagem == Linhagem.Humano && sheet.ArcaRolada > arcaDado;
        if (sheet.Linhagem == Linhagem.Humano && sheet.ArcaRolada is not null && !arcaForaDaTabela)
        {
            var arca = await db.ArcaEntries.Include(a => a.Evolucoes)
                .FirstOrDefaultAsync(a => a.GmId == sheet.GmId && a.Roll == sheet.ArcaRolada.Value);
            // An Arca row created only to hold evoluções has an empty Nome — the sheet treats it as not registered.
            arcaNome = string.IsNullOrEmpty(arca?.Nome) ? null : arca.Nome;
            arcaDescricao = string.IsNullOrEmpty(arca?.Descricao) ? null : arca.Descricao;
            if (arca is not null)
                arcaEvolucoes = ArcaEvolucaoRules.Desbloqueadas(arca.Evolucoes, e => e.Nivel, e => e.CriadaEm, sheet.Nivel)
                    .Select(e => new ArcaEvolucaoResponse(e.Id, e.Nivel, e.Descricao)).ToList();
        }

        return new RacialAbilityResponse(nome, descricao, sheet.ArcaRolada, arcaNome, arcaDescricao, arcaEvolucoes, arcaDado, arcaForaDaTabela);
    }

    /// <summary>
    /// Read-only, everything derived live — nothing here is persisted. The computation itself
    /// lives in NpcSheetStats.SubAtributosAsync, shared with the FichaParaRequisitos snapshot
    /// used by Passiva requisitos.
    /// </summary>
    [HttpGet("{id}/sub-attributes")]
    public async Task<ActionResult<SubAttributesResponse>> SubAttributes(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await stats.SubAtributosAsync(sheet, await condicoes.ParaNpcAsync(sheet));
    }

    /// <summary>
    /// Used by the máximo computation (vigorTotal/astuciaTotal) in ToResponseAsync — a single-row
    /// lookup + AttributeTotalCalculator.Total. SubAttributes no longer calls this: that formula was
    /// extracted into NpcSheetStats, shared with the Passiva requisitos snapshot.
    /// Mirrors CharacterSheetsController.GetAttributeTotalAsync, scoped to NpcAttributes.
    /// </summary>
    private async Task<int> GetAttributeTotalAsync(Guid sheetId, Atributo atributo, IReadOnlyList<ArtifactBonusInput> artefatos, EfeitosDeCondicao efeitos)
    {
        var attribute = await db.NpcAttributes.SingleAsync(a => a.SheetId == sheetId && a.Atributo == atributo);
        return efeitos.NoAtributo(atributo.ToString(), AttributeTotalCalculator.Total(attribute.Gasto, attribute.Bonus, attribute.TemMaestria,
            artefatos: ArtifactBonusCalculator.Sum(artefatos, TipoDeAlvo.Atributo, atributo.ToString())));
    }

    /// <summary>
    /// As Condições ativas na ficha, com o que cada uma muda e os Turnos que faltam. Sempre vazia numa ficha do GM
    /// (sem dono): as Condições de um participante dela só valem no resumo do encontro.
    /// </summary>
    [HttpGet("{id}/condicoes-ativas")]
    public async Task<ActionResult<List<CondicaoAtivaNaFichaResponse>>> CondicoesAtivas(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await condicoes.ListarDoNpcAsync(sheet);
    }

    /// <summary>Equipamentos em uso cujos Requisitos a ficha não cumpre, com o que falta e a penalidade aplicada.</summary>
    [HttpGet("{id}/equipment-penalties")]
    public async Task<ActionResult<List<PenalidadeAtivaResponse>>> EquipmentPenalties(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        return await penalidades.ComoRespostaAsync(await stats.PenalidadesAtivasAsync(sheet, await condicoes.ParaNpcAsync(sheet)));
    }

    /// <summary>
    /// Ficha de Personagem 3.f (inherited by NPC): one read-only value per Tipo de Dano, each the
    /// sum of equipped Artefatos whose Tipo de alvo is Dano and whose Alvo is that Tipo de Dano.
    /// Mirrors CharacterSheetsController.ModificadorDeDano.
    /// </summary>
    [HttpGet("{id}/modificador-de-dano")]
    public async Task<ActionResult<ModificadorDeDanoResponse>> ModificadorDeDano(Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var efeitos = await condicoes.ParaNpcAsync(sheet);
        return efeitos.NoModificadorDeDano(await stats.ModificadoresAsync(sheet, efeitos));
    }

    // The GM's whole NPC roster/library, not scoped to any one player — GM-only, same reasoning as Create.
    [HttpGet]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<List<NpcSheetSummaryResponse>>> List(
        [FromQuery] string? nome, [FromQuery] string? linhagem, [FromQuery] string? variante, [FromQuery] string? vocacao, [FromQuery] string? subVocacao, [FromQuery] int? nivel,
        /// <summary>No-op placeholder until CampaignAttachments lands in the Campanha — Anexos plan.</summary>
        [FromQuery] string? campaignId)
    {
        var gmId = CurrentUserId();
        var query = db.NpcSheets.Where(s => s.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(s => s.Nome != null && EF.Functions.ILike(s.Nome, $"%{nome}%"));
        if (linhagem is not null && Enum.TryParse<Linhagem>(linhagem, out var linhagemParsed))
            query = query.Where(s => s.Linhagem == linhagemParsed);
        if (variante is not null && Enum.TryParse<Variante>(variante, out var variantesParsed))
            query = query.Where(s => s.Variante == variantesParsed);
        if (vocacao is not null && Enum.TryParse<Vocacao>(vocacao, out var vocacaoParsed))
            query = query.Where(s => s.Vocacao == vocacaoParsed);
        if (!string.IsNullOrWhiteSpace(subVocacao))
            query = query.Where(s => s.SubVocacao == subVocacao);
        if (nivel is not null)
            query = query.Where(s => s.Nivel == nivel);

        // Two chained GroupJoins over an already-anonymous-typed source don't translate on this
        // EF Core/Npgsql version ("could not be translated") — a correlated FirstOrDefault
        // subquery per join is the reliable equivalent and translates to a plain LEFT JOIN each.
        return await query
            .Select(s => new
            {
                Sheet = s,
                Owner = db.Users.FirstOrDefault(u => u.Id == s.OwnerId),
                Image = db.Images.FirstOrDefault(i => i.Id == s.ImageId)
            })
            .Select(x => new NpcSheetSummaryResponse(x.Sheet.Id.ToString(), x.Sheet.Nome ?? "", x.Sheet.Linhagem.ToString(), x.Sheet.Vocacao.ToString(), x.Sheet.SubVocacao, x.Sheet.Nivel, x.Owner != null ? x.Owner.Nickname : null, x.Image != null ? "/images/" + x.Image.Path : null))
            .ToListAsync();
    }

    /// <summary>
    /// A null request value means "not set" and maps to null on the entity. A non-null value
    /// that fails to parse is malformed input, not an absent one — the caller must 400 rather
    /// than silently persisting null (e.g. a garbage Variante silently skipping
    /// LinhagemVarianteValidator).
    /// </summary>
    private static bool TryParseEnum<TEnum>(string? value, out TEnum? parsed) where TEnum : struct, Enum
    {
        if (value is null)
        {
            parsed = null;
            return true;
        }

        if (Enum.TryParse<TEnum>(value, out var result))
        {
            parsed = result;
            return true;
        }

        parsed = null;
        return false;
    }

    /// <summary>
    /// Treats null, empty, or whitespace-only as "no image" (returns true with imageId null).
    /// A non-empty string that isn't a valid Guid is rejected (returns false) rather than
    /// throwing. Mirrors CharacterSheetsController's TryParseImageId.
    /// </summary>
    private static bool TryParseImageId(string? raw, out Guid? imageId)
    {
        imageId = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (!Guid.TryParse(raw, out var parsed))
            return false;

        imageId = parsed;
        return true;
    }

    /// <summary>
    /// Same shape as CharacterSheetsController.ToResponseAsync's Graduação/máximo computation,
    /// now that NpcAttribute exists — vigorTotal/astuciaTotal are queried via GetAttributeTotalAsync,
    /// same as SubAttributes uses.
    /// </summary>
    private async Task<NpcSheetResponse> ToResponseAsync(NpcSheet s)
    {
        string? imageUrl = null;
        if (s.ImageId is not null)
        {
            var image = await db.Images.FindAsync(s.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        // XP que o NPC concede ao ser derrotado: calculado ao vivo, nunca persistido, e só o GM da
        // ficha vê (o jogador dono de um NPC concedido recebe null).
        var ehGm = s.GmId == CurrentUserId();
        int? abate = ehGm ? XpAwardCalculator.Abate(s.ExperienciaAtual) : null;
        int? assistencia = ehGm ? XpAwardCalculator.Assistencia(s.ExperienciaAtual) : null;

        var vocacao = s.Vocacao ?? Vocacao.Campeao; // no vocação chosen yet → Graduacao is meaningless but must not throw
        var eapAtual = await stats.EapAtualAsync(s);
        var graduacao = s.Vocacao is null ? 0 : GraduacaoCalculator.Compute(vocacao, eapAtual, s.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);
        var graduacaoLabel = vocacao is Vocacao.Campeao or Vocacao.Cacador ? "Grau" : "Círculo";

        var linhasDeAfinidade = await db.NpcAffinities.Where(a => a.SheetId == s.Id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor))
            .ToListAsync();
        var vocacaoArcanaGasta = VocacaoArcanaCalculator.Gasto(linhasDeAfinidade);

        // Totais exibidos: Artefatos + penalidades de equipamento ativas e as Condições ativas (Nenhum numa ficha do GM, sem dono).
        var (vitalidadeMaximo, focoMaximo, adrenalinaMaximo, estresseMaximo) = await stats.MaximosAsync(s, await condicoes.ParaNpcAsync(s));

        var campaignId = await tipo.CampanhaAsync(s);

        return new NpcSheetResponse(
            s.Id.ToString(), s.OwnerId?.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade?.ToString(), s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, eapAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, s.PontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos, graduacao, graduacaoLabel,
            vitalidadeMaximo, focoMaximo, adrenalinaMaximo, estresseMaximo,
            campaignId?.ToString(), s.ImageId?.ToString(), s.ArcaRolada,
            s.Estrela?.ToString(), s.SinaAtual, s.HistoricoId?.ToString(), s.EquipmentKitId?.ToString(), s.Historia,
            s.AfinidadeAdicional, vocacaoArcanaGasta, VocacaoArcanaCalculator.Maxima(s.Vocacao, graduacao, s.AfinidadeAdicional, rules.CirculoGrauPorEap),
            abate, assistencia);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
