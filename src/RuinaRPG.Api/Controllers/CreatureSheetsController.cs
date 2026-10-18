using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/creature-sheets")]
[Authorize]
public sealed class CreatureSheetsController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, IHubContext<EncounterHub> hub, ILogger<CreatureSheetsController> logger, CreatureSheetStats stats, IPericiaCatalogo pericias, EquipmentPenaltyService penalidades, ITipoDeFicha<CreatureSheet> tipo)
    : FichasController<CreatureSheet, CreatureSheetStats>(db, tabelaDeNiveis, tipo, stats, penalidades, hub, logger)
{
    // Creating a fresh (un-granted) Creature is GM roster curation, not something a player who's
    // been granted one already does — same reasoning as List below.
    [HttpPost]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<CreatureSheetResponse>> Create()
    {
        var gmId = CurrentUserId();

        var sheet = new CreatureSheet { Id = Guid.NewGuid(), GmId = gmId };
        Db.CreatureSheets.Add(sheet);

        foreach (var atributo in Enum.GetValues<AtributoCriatura>())
            Db.CreatureAttributes.Add(new CreatureAttribute { Id = Guid.NewGuid(), SheetId = sheet.Id, Atributo = atributo });
        // R0005's "lista fixa mais curta" — only the Perícias flagged DisponivelParaCriaturas, not
        // every active one (unlike Ficha de NPCs). The suggested Atributo only applies when the
        // Criatura has it (AtributoCriatura is a shorter enum).
        foreach (var pericia in (await pericias.TodasAsync()).Where(p => !p.IsDeleted && p.DisponivelParaCriaturas))
            Db.CreatureSkills.Add(new CreatureSkill { Id = Guid.NewGuid(), SheetId = sheet.Id, PericiaId = pericia.Id, AtributoEscolhido = Enum.TryParse<AtributoCriatura>(pericia.AtributoSugerido?.ToString(), out var sugerido) ? sugerido : null });
        foreach (var slot in Enum.GetValues<ArmorSlotType>())
            Db.CreatureArmorSlots.Add(new CreatureArmorSlot { Id = Guid.NewGuid(), SheetId = sheet.Id, Slot = slot });

        await Db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(sheet));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CreatureSheetResponse>> Get(Guid id)
    {
        // NotFound e não Forbid — não confirma a um estranho que a ficha existe.
        if ((await FichaParaLeituraAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        return await ToResponseAsync(sheet);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateCreatureSheetRequest request)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        var tabela = await TabelaDeNiveis.ObterAsync();
        if (request.Nivel < 1 || request.Nivel > tabela.UltimoNivel)
            return BadRequest($"O nível deve estar entre 1 e {tabela.UltimoNivel}.");

        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (!TryParseEnum<Arquetipo>(request.Arquetipo, out var arquetipo))
            return BadRequest("Arquetipo inválido.");
        if (!TryParseEnum<AfinidadeElemental>(request.Afinidade, out var afinidade))
            return BadRequest("Afinidade inválida.");
        if (afinidade is not null && afinidade != sheet.Afinidade && CaminhoSubElementoRules.EhCaminho(afinidade.Value))
            return BadRequest("Alma e Vida são Caminhos, não Afinidades.");
        if (!TryParseEnum<Rank>(request.Rank, out var rank))
            return BadRequest("Rank inválido.");
        if (!Enum.TryParse<Cobertura>(request.Cobertura, out var cobertura))
            return BadRequest("Cobertura inválida.");

        sheet.ImageId = imageId;
        sheet.Nome = request.Nome;
        sheet.Raca = request.Raca;
        sheet.Arquetipo = arquetipo;
        sheet.SubArquetipo = request.SubArquetipo;
        sheet.Afinidade = afinidade;
        sheet.Rank = rank;
        sheet.Nivel = request.Nivel;
        sheet.ExperienciaAtual = request.ExperienciaAtual;
        // request.PontosDeIgnicao é aceito mas ignorado: o saldo só muda pelo endpoint próprio
        // (UpdatePontosDeIgnicao) e pelas Magias/Habilidades, para um formulário desatualizado não
        // apagar um débito ou uma devolução feita pelo servidor.
        sheet.Cobertura = cobertura;
        // Só o Atual que o jogador editou é gravado, e no máximo atual da ficha (AtualDoFormulario), como no NPC.
        var (vitalidadeMaximo, focoMaximo, adrenalinaMaximo) = await Stats.MaximosAsync(sheet, await Tipo.CondicoesAsync(sheet));
        sheet.VitalidadeAtual = AtualDoFormulario.EscolherNoMaximo(sheet.VitalidadeAtual, request.VitalidadeAtual, request.VitalidadeAtualCarregada, vitalidadeMaximo);
        sheet.FocoAtual = AtualDoFormulario.EscolherNoMaximo(sheet.FocoAtual, request.FocoAtual, request.FocoAtualCarregado, focoMaximo);
        sheet.AdrenalinaAtual = AtualDoFormulario.EscolherNoMaximo(sheet.AdrenalinaAtual, request.AdrenalinaAtual, request.AdrenalinaAtualCarregada, adrenalinaMaximo);

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

    // The GM's whole Creature roster/library, not scoped to any one player — GM-only, same reasoning as Create.
    [HttpGet]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<List<CreatureSheetSummaryResponse>>> List(
        [FromQuery] string? nome, [FromQuery] string? raca, [FromQuery] string? arquetipo, [FromQuery] string? rank,
        /// <summary>No-op placeholder until CampaignAttachments lands in the Campanha — Anexos plan.</summary>
        [FromQuery] string? campaignId)
    {
        var gmId = CurrentUserId();
        var query = Db.CreatureSheets.Where(s => s.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(s => s.Nome != null && EF.Functions.ILike(s.Nome, $"%{nome}%"));
        if (!string.IsNullOrWhiteSpace(raca))
            query = query.Where(s => s.Raca != null && EF.Functions.ILike(s.Raca, $"%{raca}%"));
        if (arquetipo is not null && Enum.TryParse<Arquetipo>(arquetipo, out var arquetipoParsed))
            query = query.Where(s => s.Arquetipo == arquetipoParsed);
        if (rank is not null && Enum.TryParse<Rank>(rank, out var rankParsed))
            query = query.Where(s => s.Rank == rankParsed);

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
            .Select(x => new CreatureSheetSummaryResponse(x.Sheet.Id.ToString(), x.Sheet.Nome ?? "", x.Sheet.Raca, x.Sheet.Arquetipo == null ? null : x.Sheet.Arquetipo.ToString(), x.Sheet.Rank == null ? null : x.Sheet.Rank.ToString(), x.Sheet.Nivel, x.Owner != null ? x.Owner.Nickname : null, x.Image != null ? "/images/" + x.Image.Path : null))
            .ToListAsync();
    }

    /// <summary>
    /// Abate/Assistencia are computed live via XpAwardCalculator, never persisted — same pattern
    /// as CharacterSheetResponse.Graduacao/NpcSheetResponse.Graduacao.
    ///
    /// VitalidadeMaximo/FocoMaximo/AdrenalinaMaximo come from CreatureSheetStats.MaximosAsync (Tabela de
    /// Arquetipos by Arquetipo/Nivel), shared with the Condições activation clamp.
    /// </summary>
    private async Task<CreatureSheetResponse> ToResponseAsync(CreatureSheet s)
    {
        var imageUrl = await UrlDaImagemAsync(Db, s.ImageId);

        var abate = XpAwardCalculator.Abate(s.ExperienciaAtual);
        var assistencia = XpAwardCalculator.Assistencia(s.ExperienciaAtual);

        // Totais exibidos com as Condições ativas (Nenhum numa ficha do GM, sem dono). A Criatura não tem Estresse.
        var (vitalidadeMaximo, focoMaximo, adrenalinaMaximo) = await Stats.MaximosAsync(s, await Tipo.CondicoesAsync(s));

        var campaignId = await Tipo.CampanhaAsync(s);

        return new CreatureSheetResponse(
            s.Id.ToString(), s.OwnerId?.ToString(), imageUrl,
            s.Nome, s.Raca, s.Arquetipo?.ToString(), s.SubArquetipo, s.Afinidade?.ToString(),
            s.Rank?.ToString(), s.Nivel, s.ExperienciaAtual, abate, assistencia,
            s.PontosDeIgnicao, s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.Cobertura.ToString(),
            vitalidadeMaximo, focoMaximo, adrenalinaMaximo,
            campaignId?.ToString(), s.ImageId?.ToString());
    }
}
