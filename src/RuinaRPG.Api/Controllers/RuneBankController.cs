using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Runes;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Runes;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Banco de Runas do GM (Requisitos - Banco de Runas). Diferente do Banco de Magias — cujo GET
/// resolve o GM efetivo e deixa um jogador ler o banco inteiro —, tudo aqui é GM-only: o jogador só
/// enxerga as Runas que o GM anexou como públicas à campanha (CampaignCatalogController,
/// "available-runes").
/// </summary>
[ApiController]
[Route("api/rune-bank")]
[Authorize(Roles = "GM")]
public class RuneBankController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RuneBankEntryResponse>> Create(CreateRuneBankEntryRequest request)
    {
        var gmId = CurrentUserId();

        if (!RuneImageAccess.TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (imageId is not null && !await RuneImageAccess.CanUseAsync(db, imageId.Value, gmId, gmId, null))
            return BadRequest("Imagem não encontrada.");

        if (!RuneTipo.TryParse(request.Tipo, out var tipo))
            return BadRequest(RuneTipo.UnknownMessage);

        if (!RuneDisciplina.TryParse(request.Disciplina, out var disciplina))
            return BadRequest(RuneDisciplina.UnknownMessage);
        if (disciplina is null)
            return BadRequest(RuneDisciplina.RequiredMessage);

        var entry = new RuneBankEntry
        {
            Id = Guid.NewGuid(),
            GmId = gmId,
            Nome = request.Nome,
            Descricao = request.Descricao,
            Grau = request.Grau,
            Tipo = tipo,
            Disciplina = disciplina,
            ImageId = imageId
        };
        db.RuneBankEntries.Add(entry);
        await db.SaveChangesAsync();

        var urls = await RuneImageAccess.UrlsAsync(db, [entry.ImageId]);
        return Created(string.Empty, ToResponse(entry, urls));
    }

    [HttpGet]
    public async Task<ActionResult<List<RuneBankEntryResponse>>> List([FromQuery] string? nome, [FromQuery] int? grau, [FromQuery] string? tipo, [FromQuery] string? disciplina)
    {
        var gmId = CurrentUserId();
        var query = db.RuneBankEntries.Where(e => e.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(e => EF.Functions.ILike(e.Nome, $"%{nome}%"));

        if (grau is not null)
            query = query.Where(e => e.Grau == grau);

        // "Nenhum" filtra as Runas sem tipo; "Arcana"/"Negra" filtram pelo tipo; vazio não filtra.
        if (tipo == "Nenhum")
            query = query.Where(e => e.Tipo == null);
        else if (!string.IsNullOrEmpty(tipo))
        {
            if (!RuneTipo.TryParse(tipo, out var tipoFiltro))
                return BadRequest(RuneTipo.UnknownMessage);
            query = query.Where(e => e.Tipo == tipoFiltro);
        }

        // "Nenhuma" filtra as Runas anteriores à 1.4.3, sem Disciplina; vazio não filtra.
        if (disciplina == "Nenhuma")
            query = query.Where(e => e.Disciplina == null);
        else if (!string.IsNullOrEmpty(disciplina))
        {
            if (!RuneDisciplina.TryParse(disciplina, out var disciplinaFiltro))
                return BadRequest(RuneDisciplina.UnknownMessage);
            query = query.Where(e => e.Disciplina == disciplinaFiltro);
        }

        var entries = await query.OrderBy(e => e.Nome).ToListAsync();
        var urls = await RuneImageAccess.UrlsAsync(db, entries.Select(e => e.ImageId));
        return entries.Select(e => ToResponse(e, urls)).ToList();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateRuneBankEntryRequest request)
    {
        var gmId = CurrentUserId();
        var entry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
        if (entry is null)
            return NotFound();

        if (!RuneImageAccess.TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        // Reenviar a imagem que a entrada já tem é sempre válido (a cópia automática de uma Runa criada por
        // um jogador guarda a imagem dele, que o GM não subiu); trocar exige uma imagem que o GM possa usar.
        if (imageId is not null && imageId != entry.ImageId && !await RuneImageAccess.CanUseAsync(db, imageId.Value, gmId, gmId, null))
            return BadRequest("Imagem não encontrada.");

        if (!RuneTipo.TryParse(request.Tipo, out var tipo))
            return BadRequest(RuneTipo.UnknownMessage);

        if (!RuneDisciplina.TryParse(request.Disciplina, out var disciplina))
            return BadRequest(RuneDisciplina.UnknownMessage);
        if (disciplina is null)
            return BadRequest(RuneDisciplina.RequiredMessage);

        entry.Nome = request.Nome;
        entry.Descricao = request.Descricao;
        entry.Grau = request.Grau;
        entry.Tipo = tipo;
        entry.Disciplina = disciplina;
        entry.ImageId = imageId;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var gmId = CurrentUserId();
        var entry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
        if (entry is null)
            return NotFound();

        db.RuneBankEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static RuneBankEntryResponse ToResponse(RuneBankEntry entry, Dictionary<Guid, string> imageUrls) =>
        new(entry.Id.ToString(), entry.Nome, entry.Descricao, entry.Grau, entry.ImageId?.ToString(), RuneImageAccess.UrlOf(imageUrls, entry.ImageId), RuneTipo.Format(entry.Tipo), RuneDisciplina.Format(entry.Disciplina));

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
