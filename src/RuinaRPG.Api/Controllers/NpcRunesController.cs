using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/runes")]
public sealed class NpcRunesController(RuinaRpgDbContext db, ITipoDeFicha<NpcSheet> tipo)
    : RunasDaFichaController<NpcSheet, NpcRune, NpcRuneResponse, AddNpcRuneRequest>(db, tipo)
{
    protected override (string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId, string? ImageId, string? Tipo, string? Disciplina) Ler(AddNpcRuneRequest r) =>
        (r.Nome, r.Descricao, r.Grau, r.SourceBankEntryId, r.ImageId, r.Tipo, r.Disciplina);

    protected override NpcRune Nova(string nome, string descricao) => new() { Nome = nome, Descricao = descricao };

    protected override NpcRuneResponse Resposta(NpcRune r, string? imageUrl, string? desabilitadoPor) =>
        new(r.Id.ToString(), r.Nome, r.Descricao, r.Grau, imageUrl, RuneTipo.Format(r.Tipo), RuneDisciplina.Format(r.Disciplina), DesabilitadoPor: desabilitadoPor);

    protected override Task<Guid> MestreDaFichaAsync(NpcSheet ficha) => Task.FromResult(ficha.GmId);

    // A ficha de NPC não tem coluna CampaignId: a campanha de uma ficha concedida sai do CampaignAttachment de
    // vínculo (a mesma regra do CampanhaDaFichaAsync de NpcSpellAbilitiesController).
    protected override async Task<Guid?> CampanhaDaFichaAsync(NpcSheet ficha) =>
        await Db.CampaignAttachments
            .Where(a => a.NpcSheetId == ficha.Id && Db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == ficha.OwnerId))
            .Select(a => (Guid?)a.CampaignId)
            .FirstOrDefaultAsync();
}
