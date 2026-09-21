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
        var entry = new RuneBankEntry
        {
            Id = Guid.NewGuid(),
            GmId = CurrentUserId(),
            Nome = request.Nome,
            Descricao = request.Descricao,
            Grau = request.Grau
        };
        db.RuneBankEntries.Add(entry);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(entry));
    }

    [HttpGet]
    public async Task<ActionResult<List<RuneBankEntryResponse>>> List([FromQuery] string? nome, [FromQuery] int? grau)
    {
        var gmId = CurrentUserId();
        var query = db.RuneBankEntries.Where(e => e.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(e => EF.Functions.ILike(e.Nome, $"%{nome}%"));

        if (grau is not null)
            query = query.Where(e => e.Grau == grau);

        var entries = await query.OrderBy(e => e.Nome).ToListAsync();
        return entries.Select(ToResponse).ToList();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateRuneBankEntryRequest request)
    {
        var gmId = CurrentUserId();
        var entry = await db.RuneBankEntries.FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
        if (entry is null)
            return NotFound();

        entry.Nome = request.Nome;
        entry.Descricao = request.Descricao;
        entry.Grau = request.Grau;
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

    private static RuneBankEntryResponse ToResponse(RuneBankEntry entry) =>
        new(entry.Id.ToString(), entry.Nome, entry.Descricao, entry.Grau);

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
