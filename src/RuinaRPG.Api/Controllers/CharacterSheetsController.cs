using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

// A rota da classe é a da ficha (api/character-sheets/{id}/…); criar e listar ficam sob a campanha
// e "mine" sob o jogador, com rotas absolutas (começam com "/", ignoram a da classe).
[ApiController]
[Authorize]
[Route("api/character-sheets")]
public sealed class CharacterSheetsController(RuinaRpgDbContext db, IRulesDataProvider rules, ITabelaDeNiveis tabelaDeNiveis, IHubContext<EncounterHub> hub, ILogger<CharacterSheetsController> logger, CharacterSheetStats stats, IPericiaCatalogo pericias, EquipmentPenaltyService penalidades, ITipoDeFicha<CharacterSheet> tipo, CemiterioDaFicha cemiterio)
    : FichaComLinhagemController<CharacterSheet, CharacterSheetStats, CharacterSheetHistoriaImage>(db, tabelaDeNiveis, tipo, stats, penalidades, hub, logger)
{
    [HttpPost("/api/campaigns/{campaignId}/character-sheets")]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<CharacterSheetResponse>> Create(Guid campaignId, CreateCharacterSheetRequest request)
    {
        var gmId = CurrentUserId();
        var campaignExists = await Db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        if (!Guid.TryParse(request.OwnerId, out var ownerId))
            return BadRequest("O jogador informado não é membro desta campanha.");

        var isMember = await Db.CampaignMembers.AnyAsync(m => m.CampaignId == campaignId && m.UserId == ownerId);
        if (!isMember)
            return BadRequest("O jogador informado não é membro desta campanha.");

        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaignId, OwnerId = ownerId };
        // Ficha nova já nasce com o que a Tabela de Níveis concede até o nível inicial (1.b).
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Inicial(sheet.Nivel, await TabelaDeNiveis.ObterAsync());
        Db.CharacterSheets.Add(sheet);

        foreach (var atributo in Enum.GetValues<Atributo>())
            Db.CharacterAttributes.Add(new CharacterAttribute { Id = Guid.NewGuid(), SheetId = sheet.Id, Atributo = atributo });
        foreach (var pericia in (await pericias.TodasAsync()).Where(p => !p.IsDeleted))
            Db.CharacterSkills.Add(new CharacterSkill { Id = Guid.NewGuid(), SheetId = sheet.Id, PericiaId = pericia.Id, AtributoEscolhido = pericia.AtributoSugerido });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            Db.CharacterArmorSlots.Add(new CharacterArmorSlot { Id = Guid.NewGuid(), SheetId = sheet.Id, Slot = slot });

        await Db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var gmId = CurrentUserId();
        var sheet = await Db.CharacterSheets
            .Join(Db.Campaigns, s => s.CampaignId, c => c.Id, (s, c) => new { Sheet = s, c.GmId })
            .Where(x => x.Sheet.Id == id && x.GmId == gmId)
            .Select(x => x.Sheet)
            .FirstOrDefaultAsync();
        if (sheet is null)
            return NotFound();

        Db.CharacterSheets.Remove(sheet);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("/api/campaigns/{campaignId}/character-sheets")]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<List<CharacterSheetResponse>>> ListForCampaign(Guid campaignId)
    {
        var gmId = CurrentUserId();
        var campaignExists = await Db.Campaigns.AnyAsync(c => c.Id == campaignId && c.GmId == gmId);
        if (!campaignExists)
            return NotFound();

        var sheets = await Db.CharacterSheets.Where(s => s.CampaignId == campaignId).ToListAsync();
        var motivos = await cemiterio.MotivosAsync(sheets);
        var responses = new List<CharacterSheetResponse>();
        foreach (var sheet in sheets)
            responses.Add(await ToResponseAsync(sheet, motivos[sheet.Id]));
        return responses;
    }

    /// <summary>
    /// Painel do Jogador (canvas "01 - Visão Geral" — the login/signup/panel flow diagram):
    /// a player lands on a list of their own character sheets across every campaign, not scoped
    /// to one — unlike ListForCampaign above, which is the GM's per-campaign roster view.
    /// </summary>
    [HttpGet("/api/character-sheets/mine")]
    [Authorize(Roles = "Jogador")]
    public async Task<ActionResult<List<MyCharacterSheetSummaryResponse>>> ListMine()
    {
        var playerId = CurrentUserId();

        var sheets = await Db.CharacterSheets
            .Where(s => s.OwnerId == playerId)
            .GroupJoin(Db.Campaigns, s => s.CampaignId, c => c.Id, (s, campaigns) => new { Sheet = s, Campaign = campaigns.FirstOrDefault() })
            .ToListAsync();

        var motivos = await cemiterio.MotivosAsync(sheets.Select(x => x.Sheet).ToList());
        var responses = new List<MyCharacterSheetSummaryResponse>();
        foreach (var x in sheets)
        {
            responses.Add(new MyCharacterSheetSummaryResponse(
                x.Sheet.Id.ToString(), await UrlDaImagemAsync(Db, x.Sheet.ImageId), x.Sheet.Nome, x.Sheet.Nivel,
                x.Sheet.CampaignId.ToString(), x.Campaign?.Nome ?? "",
                motivos[x.Sheet.Id].Count > 0, MotivosComoTexto(motivos[x.Sheet.Id])));
        }

        return responses;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CharacterSheetResponse>> Get(Guid id)
    {
        // NotFound e não Forbid — não confirma a um estranho que a ficha existe.
        if ((await FichaParaLeituraAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        return await ToResponseAsync(sheet);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateCharacterSheetRequest request)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        var campaignGmId = await Tipo.MestreAsync(sheet);
        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (!TryParseEnum<Linhagem>(request.Linhagem, out var linhagem))
            return BadRequest("Linhagem inválida.");
        if (!TryParseEnum<Variante>(request.Variante, out var variante))
            return BadRequest("Variante inválida.");
        if (linhagem is not null && variante is not null && !LinhagemVarianteValidator.IsValidCombination(linhagem.Value, variante.Value))
            return BadRequest("A Variante escolhida não pertence à Linhagem escolhida.");
        if (await VarianteLiberadaResolver.EscolhaBloqueadaAsync(Db, campaignGmId, variante, sheet.Variante))
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
            if (!await Db.Historicos.AnyAsync(h => h.Id == parsedHistoricoId && !h.IsDeleted))
                return BadRequest("Histórico não encontrado.");
            historicoId = parsedHistoricoId;
        }

        // A Variante change makes any racial characteristics already granted for the old one stale
        // (Ruína RPG - Sistema Básico.md §7 grants are per-Variante) — remove them so 5.d never
        // shows a grant that no longer matches the character's race, and PendingRacialTraitChoice
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
        // Nível is a pure function of Experiência Atual now (1.b, "Para o próximo") — no more
        // GM-editable override for Ficha de Personagem, unlike NPC/Criatura sheets.
        var tabela = await TabelaDeNiveis.ObterAsync();
        var nivel = NivelCalculator.Compute(request.ExperienciaAtual, tabela.ComoXpPorNivel());
        var nivelAntigo = sheet.Nivel;
        sheet.Nivel = nivel;
        sheet.PossuiCoracaoDeMana = request.PossuiCoracaoDeMana;
        sheet.AfinidadeAdicional = request.AfinidadeAdicional;
        sheet.ExperienciaAtual = request.ExperienciaAtual;
        // sheet.EAPAtual is intentionally never written from here on — EAPAtual is now a pure
        // function of Nivel + NucleosRank* (EapCalculator, used in ToResponseAsync), same
        // treatment already given to Círculo/Grau. request.EAPAtual is accepted-but-ignored to
        // avoid a wider positional-record rewrite across every existing call site.
        sheet.NucleosRankF = request.NucleosRankF;
        sheet.NucleosRankE = request.NucleosRankE;
        sheet.NucleosRankD = request.NucleosRankD;
        sheet.NucleosRankC = request.NucleosRankC;
        sheet.NucleosRankB = request.NucleosRankB;
        sheet.NucleosRankA = request.NucleosRankA;
        sheet.NucleosRankS = request.NucleosRankS;
        // PontosDeIgnicaoTotal is a pure function of Nível + PontosDeIgnicaoBonusManual now (1.b,
        // "os bônus de PI por nível constam na Tabela de Níveis") — same treatment already given
        // to EAPAtual. Only the manual bonus on top is ever written from here on.
        // 1.b: o Atual acompanha o Nível (sobe e desce, piso 0) e também o bônus manual do GM, que
        // continua somando ao total calculado em ToResponseAsync.
        // request.PontosDeIgnicaoAtual é aceito mas ignorado, como EAPAtual: as diferenças entram sobre
        // o saldo guardado. O formulário é salvo por inteiro e sem controle de concorrência — se o saldo
        // viesse dele, um formulário desatualizado apagaria um débito, uma devolução ou um ajuste de
        // nível feito pelo servidor. A edição manual do campo tem endpoint próprio (UpdatePontosDeIgnicao).
        var deltaDePi = PontosDeIgnicaoPorNivel.Delta(nivelAntigo, nivel, tabela)
            + (request.PontosDeIgnicaoBonusManual - sheet.PontosDeIgnicaoBonusManual);
        sheet.PontosDeIgnicaoAtual = PontosDeIgnicaoPorNivel.Aplicar(sheet.PontosDeIgnicaoAtual, deltaDePi);
        sheet.PontosDeIgnicaoBonusManual = request.PontosDeIgnicaoBonusManual;

        // "Atual não pode exceder o máximo" (1.c, all 4 resources) — clamped rather than
        // rejected, since a Máximo can legitimately shrink (e.g. unequipping an Artefato) out
        // from under an Atual that was valid a moment ago. O máximo aqui já conta as Condições ativas:
        // não se salva acima do total reduzido, e esse corte é uma mudança real, que fica.
        // Um Atual que o jogador não editou fica com o valor guardado (AtualDoFormulario): o servidor também o escreve.
        var maximos = await ComputeResourceMaximumsAsync(sheet, vocacao, nivel);
        sheet.VitalidadeAtual = Math.Min(AtualDoFormulario.Escolher(sheet.VitalidadeAtual, request.VitalidadeAtual, request.VitalidadeAtualCarregada), maximos.Vitalidade);
        sheet.FocoAtual = Math.Min(AtualDoFormulario.Escolher(sheet.FocoAtual, request.FocoAtual, request.FocoAtualCarregado), maximos.Foco);
        sheet.AdrenalinaAtual = Math.Min(AtualDoFormulario.Escolher(sheet.AdrenalinaAtual, request.AdrenalinaAtual, request.AdrenalinaAtualCarregada), maximos.Adrenalina);
        sheet.EstresseAtual = Math.Min(AtualDoFormulario.Escolher(sheet.EstresseAtual, request.EstresseAtual, request.EstresseAtualCarregado), maximos.Estresse);
        // Só rejeita quando o valor MUDA para fora da tabela: se o GM diminuiu o dado depois da rolagem,
        // a ficha continua podendo salvar os outros campos mantendo a mesma ArcaRolada.
        if (request.ArcaRolada != sheet.ArcaRolada)
        {
            var arcaDado = await Db.DadoDeArcaDoGmAsync(campaignGmId);
            if (request.ArcaRolada < 1 || request.ArcaRolada > arcaDado)
                return BadRequest($"ArcaRolada deve estar entre 1 e {arcaDado}.");
        }

        sheet.Cobertura = cobertura;
        sheet.Ciclos = request.Ciclos;
        sheet.PontosDePericiaBonusCritico = request.PontosDePericiaBonusCritico;
        sheet.ArcaRolada = request.ArcaRolada;
        sheet.SinaAtual = request.SinaAtual;

        if (varianteChanged)
        {
            var staleRacialTraits = await Db.CharacterTraits.Where(t => t.SheetId == id && t.IsRacial).ToListAsync();
            Db.CharacterTraits.RemoveRange(staleRacialTraits);
        }

        await Db.SaveChangesAsync();

        await NotificarEncontrosAsync(id);

        return NoContent();
    }

    private static List<string> MotivosComoTexto(IReadOnlyList<MotivoDoCemiterio> motivos) => motivos.Select(m => m.ToString()).ToList();

    private async Task<CharacterSheetResponse> ToResponseAsync(CharacterSheet s)
        => await ToResponseAsync(s, await cemiterio.MotivosAsync(s));

    private async Task<CharacterSheetResponse> ToResponseAsync(CharacterSheet s, IReadOnlyList<MotivoDoCemiterio> motivosDoCemiterio)
    {
        var imageUrl = await UrlDaImagemAsync(Db, s.ImageId);

        // EAPAtual is no longer a stored/editable value — same treatment already given to
        // Círculo/Grau, which became a pure function instead of a directly-set column. "Segue a
        // tabela [XP/EAP por Nível] e é somado pelo resultado de Âmbares Absorvidos" (1.b).
        var tabela = await TabelaDeNiveis.ObterAsync();
        var eapAtual = EapCalculator.Compute(s.Nivel, s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS, tabela.ComoEapPorNivel());

        var vocacao = s.Vocacao ?? RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao; // no vocação chosen yet → Graduacao is meaningless but must not throw
        var graduacao = s.Vocacao is null ? 0 : GraduacaoCalculator.Compute(vocacao, eapAtual, s.PossuiCoracaoDeMana, rules.CirculoGrauPorEap);
        var graduacaoLabel = vocacao is RuinaRPG.Domain.CharacterSheets.Vocacao.Campeao or RuinaRPG.Domain.CharacterSheets.Vocacao.Cacador ? "Grau" : "Círculo";

        var linhasDeAfinidade = await Db.CharacterAffinities.Where(a => a.SheetId == s.Id)
            .Select(a => new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor))
            .ToListAsync();
        var vocacaoArcanaGasta = VocacaoArcanaCalculator.Gasto(linhasDeAfinidade);

        var maximos = await ComputeResourceMaximumsAsync(s, s.Vocacao, s.Nivel);
        var xpParaProximoNivel = NivelCalculator.XpParaProximoNivel(s.ExperienciaAtual, tabela.ComoXpPorNivel());
        var pontosDeIgnicaoTotal = PontosDeIgnicaoCalculator.ComputeTotal(s.Nivel, s.PontosDeIgnicaoBonusManual, tabela);

        return new CharacterSheetResponse(
            s.Id.ToString(), s.CampaignId.ToString(), s.OwnerId.ToString(), imageUrl,
            s.Nome, s.Linhagem?.ToString(), s.Variante?.ToString(), s.Vocacao?.ToString(), s.SubVocacao, s.Afinidade?.ToString(), s.Propriedade,
            s.Nivel, s.Circulo, s.Grau, s.PossuiCoracaoDeMana, s.ExperienciaAtual, eapAtual,
            s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
            s.PontosDeIgnicaoAtual, pontosDeIgnicaoTotal,
            s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
            s.Cobertura.ToString(), s.Ciclos, graduacao, graduacaoLabel,
            maximos.Vitalidade, maximos.Foco, maximos.Adrenalina, maximos.Estresse, xpParaProximoNivel,
            s.PontosDeIgnicaoBonusManual, s.PontosDePericiaBonusCritico, s.ImageId?.ToString(), s.ArcaRolada,
            s.Estrela?.ToString(), s.SinaAtual, s.HistoricoId?.ToString(), s.EquipmentKitId?.ToString(), s.Historia,
            s.AfinidadeAdicional, vocacaoArcanaGasta, VocacaoArcanaCalculator.Maxima(s.Vocacao, graduacao, s.AfinidadeAdicional, rules.CirculoGrauPorEap),
            motivosDoCemiterio.Count > 0, MotivosComoTexto(motivosDoCemiterio));
    }

    /// <summary>
    /// Shared by ToResponseAsync (display) and Update (clamping "Atual não pode exceder o
    /// máximo", 1.c): os totais com as Condições ativas da ficha (CharacterSheetStats.MaximosAsync).
    /// </summary>
    private async Task<(int Vitalidade, int Foco, int Adrenalina, int Estresse)> ComputeResourceMaximumsAsync(CharacterSheet sheet, RuinaRPG.Domain.CharacterSheets.Vocacao? vocacao, int nivel) =>
        await Stats.MaximosAsync(sheet, await Tipo.CondicoesAsync(sheet), vocacao, nivel);
}
