using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/runes")]
public sealed class CharacterRunesController(RuinaRpgDbContext db, ITipoDeFicha<CharacterSheet> tipo)
    : RunasDaFichaController<CharacterSheet, CharacterRune, CharacterRuneResponse, AddCharacterRuneRequest>(db, tipo)
{
    protected override (string? Nome, string? Descricao, int? Grau, string? SourceBankEntryId, string? ImageId, string? Tipo, string? Disciplina) Ler(AddCharacterRuneRequest r) =>
        (r.Nome, r.Descricao, r.Grau, r.SourceBankEntryId, r.ImageId, r.Tipo, r.Disciplina);

    protected override CharacterRune Nova(string nome, string descricao) => new() { Nome = nome, Descricao = descricao };

    protected override CharacterRuneResponse Resposta(CharacterRune r, string? imageUrl, string? desabilitadoPor) =>
        new(r.Id.ToString(), r.Nome, r.Descricao, r.Grau, imageUrl, RuneTipo.Format(r.Tipo), RuneDisciplina.Format(r.Disciplina), DesabilitadoPor: desabilitadoPor);

    protected override async Task<Guid> MestreDaFichaAsync(CharacterSheet ficha) =>
        await Db.Campaigns.Where(c => c.Id == ficha.CampaignId).Select(c => c.GmId).SingleAsync();

    protected override Task<Guid?> CampanhaDaFichaAsync(CharacterSheet ficha) => Task.FromResult<Guid?>(ficha.CampaignId);
}
