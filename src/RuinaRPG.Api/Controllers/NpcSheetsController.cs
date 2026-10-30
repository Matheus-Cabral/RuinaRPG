using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
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
public sealed class NpcSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules, ITabelaDeNiveis tabelaDeNiveis, IHubContext<EncounterHub> hub, ILogger<NpcSheetsController> logger, NpcSheetStats stats, IPericiaCatalogo pericias, EquipmentPenaltyService penalidades, ITipoDeFicha<NpcSheet> tipo, CatalogoDeAfinidadesService afinidades)
    : FichaComLinhagemController<NpcSheet, NpcSheetStats, NpcSheetHistoriaImage>(db, tabelaDeNiveis, tipo, stats, penalidades, hub, logger)
{
    // Creating a fresh (un-granted) NPC is GM roster curation, not something a player who's been
    // granted one already does — same reasoning as List below.
    [HttpPost]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<NpcSheetResponse>> Create()
    {
        var gmId = CurrentUserId();

        var sheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gmId };
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Inicial(sheet.Nivel, await TabelaDeNiveis.ObterAsync());
        Db.NpcSheets.Add(sheet);

        foreach (var atributo in Enum.GetValues<Atributo>())
            Db.NpcAttributes.Add(new NpcAttribute { Id = Guid.NewGuid(), SheetId = sheet.Id, Atributo = atributo });
        foreach (var pericia in (await pericias.TodasAsync()).Where(p => !p.IsDeleted))
            Db.NpcSkills.Add(new NpcSkill { Id = Guid.NewGuid(), SheetId = sheet.Id, PericiaId = pericia.Id, AtributoEscolhido = pericia.AtributoSugerido });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            Db.NpcArmorSlots.Add(new NpcArmorSlot { Id = Guid.NewGuid(), SheetId = sheet.Id, Slot = slot });

        await Db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NpcSheetResponse>> Get(Guid id)
    {
        // NotFound e não Forbid — não confirma a um estranho que a ficha existe.
        if ((await FichaParaLeituraAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        return await ToResponseAsync(sheet);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateNpcSheetRequest request)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        var tabela = await TabelaDeNiveis.ObterAsync();
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
        if (await VarianteLiberadaResolver.EscolhaBloqueadaAsync(Db, sheet.GmId, variante, sheet.Variante))
            return BadRequest("Essa Variante ainda não foi liberada pelo GM.");
        if (!TryParseEnum<Vocacao>(request.Vocacao, out var vocacao))
            return BadRequest("Vocação inválida.");
        var afinidade = request.Afinidade;
        if ((await afinidades.CarregarAsync()).MotivoParaRecusarAfinidade(afinidade, sheet.Afinidade) is { } afinidadeRecusada)
            return BadRequest(afinidadeRecusada);
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
            if (!await Db.Historicos.AnyAsync(h => h.Id == parsedHistoricoId && !h.IsDeleted))
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
        var (vitalidadeMaximo, focoMaximo, adrenalinaMaximo, estresseMaximo) = await Stats.MaximosAsync(sheet, await Tipo.CondicoesAsync(sheet));
        sheet.VitalidadeAtual = AtualDoFormulario.EscolherNoMaximo(sheet.VitalidadeAtual, request.VitalidadeAtual, request.VitalidadeAtualCarregada, vitalidadeMaximo);
        sheet.FocoAtual = AtualDoFormulario.EscolherNoMaximo(sheet.FocoAtual, request.FocoAtual, request.FocoAtualCarregado, focoMaximo);
        sheet.AdrenalinaAtual = AtualDoFormulario.EscolherNoMaximo(sheet.AdrenalinaAtual, request.AdrenalinaAtual, request.AdrenalinaAtualCarregada, adrenalinaMaximo);
        sheet.EstresseAtual = AtualDoFormulario.EscolherNoMaximo(sheet.EstresseAtual, request.EstresseAtual, request.EstresseAtualCarregado, estresseMaximo);
        // Só rejeita quando o valor MUDA para fora da tabela: se o GM diminuiu o dado depois da rolagem,
        // a ficha continua podendo salvar os outros campos mantendo a mesma ArcaRolada.
        if (request.ArcaRolada != sheet.ArcaRolada)
        {
            var arcaDado = await Db.DadoDeArcaDoGmAsync(sheet.GmId);
            if (request.ArcaRolada < 1 || request.ArcaRolada > arcaDado)
                return BadRequest($"ArcaRolada deve estar entre 1 e {arcaDado}.");
        }

        sheet.Cobertura = cobertura;
        sheet.Ciclos = request.Ciclos;
        sheet.ArcaRolada = request.ArcaRolada;
        sheet.SinaAtual = request.SinaAtual;

        if (varianteChanged)
        {
            var staleRacialTraits = await Db.NpcTraits.Where(t => t.SheetId == id && t.IsRacial).ToListAsync();
            Db.NpcTraits.RemoveRange(staleRacialTraits);
        }

        await Db.SaveChangesAsync();
        await NotificarEncontrosAsync(id);

        return NoContent();
    }

    // Nível e Experiência Atual editáveis pelo GM, cada um com o seu endpoint: ver FichasController.DefinirNivelAsync.
    [HttpPut("{id}/nivel")]
    public Task<IActionResult> UpdateNivel(Guid id, [FromBody] int nivel) => DefinirNivelAsync(id, nivel);

    [HttpPut("{id}/experiencia-atual")]
    public Task<IActionResult> UpdateExperienciaAtual(Guid id, [FromBody] int experienciaAtual) => DefinirExperienciaAtualAsync(id, experienciaAtual);

    [HttpDelete("{id}")]
    public Task<IActionResult> Delete(Guid id) => ApagarAsync(id);

    // The GM's whole NPC roster/library, not scoped to any one player — GM-only, same reasoning as Create.
    [HttpGet]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<List<NpcSheetSummaryResponse>>> List(
        [FromQuery] string? nome, [FromQuery] string? linhagem, [FromQuery] string? variante, [FromQuery] string? vocacao, [FromQuery] string? subVocacao, [FromQuery] int? nivel,
        /// <summary>No-op placeholder until CampaignAttachments lands in the Campanha — Anexos plan.</summary>
        [FromQuery] string? campaignId)
    {
        var gmId = CurrentUserId();
        var query = Db.NpcSheets.Where(s => s.GmId == gmId);

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
                Owner = Db.Users.FirstOrDefault(u => u.Id == s.OwnerId),
                Image = Db.Images.FirstOrDefault(i => i.Id == s.ImageId)
            })
            .Select(x => new NpcSheetSummaryResponse(x.Sheet.Id.ToString(), x.Sheet.Nome ?? "", x.Sheet.Linhagem.ToString(), x.Sheet.Vocacao.ToString(), x.Sheet.SubVocacao, x.Sheet.Nivel, x.Owner != null ? x.Owner.Nickname : null, x.Image != null ? "/images/" + x.Image.Path : null))
            .ToListAsync();
    }

    /// <summary>Mesma forma do cálculo de Graduação/máximos de CharacterSheetsController.ToResponseAsync.</summary>
    private async Task<NpcSheetResponse> ToResponseAsync(NpcSheet s)
    {
        var imageUrl = await UrlDaImagemAsync(Db, s.ImageId);

        // XP que o NPC concede ao ser derrotado: calculado ao vivo, nunca persistido, e só o GM da
        // ficha vê (o jogador dono de um NPC concedido recebe null).
        var ehGm = s.GmId == CurrentUserId();
        int? abate = ehGm ? XpAwardCalculator.Abate(s.ExperienciaAtual) : null;
        int? assistencia = ehGm ? XpAwardCalculator.Assistencia(s.ExperienciaAtual) : null;

        var vocacao = s.Vocacao ?? Vocacao.Campeao; // no vocação chosen yet → Graduacao is meaningless but must not throw
        var eapAtual = await Stats.EapAtualAsync(s);
        var graduacao = s.Vocacao is null ? 0 : GraduacaoCalculator.Compute(vocacao, eapAtual, s.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);
        var graduacaoLabel = vocacao is Vocacao.Campeao or Vocacao.Cacador ? "Grau" : "Círculo";

        var linhasDeAfinidade = await Db.NpcAffinities.Where(a => a.SheetId == s.Id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor))
            .ToListAsync();
        var vocacaoArcanaGasta = VocacaoArcanaCalculator.Gasto(linhasDeAfinidade);

        // Totais exibidos: Artefatos + penalidades de equipamento ativas e as Condições ativas (Nenhum numa ficha do GM, sem dono).
        var (vitalidadeMaximo, focoMaximo, adrenalinaMaximo, estresseMaximo) = await Stats.MaximosAsync(s, await Tipo.CondicoesAsync(s));

        var campaignId = await Tipo.CampanhaAsync(s);

        return new NpcSheetResponse(
            s.Id.ToString(), s.OwnerId?.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade, s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, eapAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, s.PontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos, graduacao, graduacaoLabel,
            vitalidadeMaximo, focoMaximo, adrenalinaMaximo, estresseMaximo,
            campaignId?.ToString(), await NomeDaCampanhaAsync(Db, campaignId), s.ImageId?.ToString(), s.ArcaRolada,
            s.Estrela?.ToString(), s.SinaAtual, s.HistoricoId?.ToString(), s.EquipmentKitId?.ToString(), s.Historia,
            s.AfinidadeAdicional, vocacaoArcanaGasta, VocacaoArcanaCalculator.Maxima(s.Vocacao, graduacao, s.AfinidadeAdicional, rules.CirculoGrauPorEap),
            abate, assistencia);
    }
}
