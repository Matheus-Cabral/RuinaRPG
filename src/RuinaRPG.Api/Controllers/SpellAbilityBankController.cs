using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/spell-ability-bank")]
[Authorize(Roles = "GM")]
public class SpellAbilityBankController(RuinaRpgDbContext db) : ControllerBase
{
    // The whole Banco is GM-only — curating and browsing. A Jogador never reads the GM's private bank:
    // to pick a Magia/Habilidade for their sheet they use CampaignCatalogController's
    // "available-spell-abilities" (only entries the GM attached as public), and the sheet controllers
    // re-check that on the server. Same model as the Banco de Runas.
    [HttpPost]
    public async Task<ActionResult<SpellAbilityEntryResponse>> Create(CreateSpellAbilityEntryRequest request)
    {
        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo) || !Enum.IsDefined(tipo))
            return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");

        var validationError = await EfeitoValidationHelper.ValidarAsync(db, request.Grau, request.Efeitos);
        if (validationError is not null)
            return BadRequest(validationError);

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

        var entry = new SpellAbilityBankEntry
        {
            Id = Guid.NewGuid(),
            GmId = CurrentUserId(),
            Nome = request.Nome,
            Tipo = tipo,
            Grau = request.Grau,
            GastoEmPI = gastoEmPI,
            Custo = SpellAbilityCostCalculator.Custo(gastoEmPI),
            Descricao = request.Descricao
        };
        entry.Efeitos = request.Efeitos
            .Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = entry.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI })
            .ToList();

        db.SpellAbilityBankEntries.Add(entry);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(entry));
    }

    [HttpGet]
    public async Task<ActionResult<List<SpellAbilityEntryResponse>>> List(
        [FromQuery] string? nome,
        [FromQuery] string? tipo,
        [FromQuery] int? grau)
    {
        var gmId = CurrentUserId();

        var query = db.SpellAbilityBankEntries
            .Include(e => e.Efeitos)
            .Where(e => e.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(e => EF.Functions.ILike(e.Nome, $"%{nome}%"));

        if (tipo is not null && Enum.TryParse<SpellAbilityTipo>(tipo, out var tipoParsed))
            query = query.Where(e => e.Tipo == tipoParsed);

        if (grau is not null)
            query = query.Where(e => e.Grau == grau);

        var entries = await query.ToListAsync();
        return entries.Select(ToResponse).ToList();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateSpellAbilityEntryRequest request)
    {
        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo) || !Enum.IsDefined(tipo))
            return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");

        var gmId = CurrentUserId();
        var entry = await db.SpellAbilityBankEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
        if (entry is null)
            return NotFound();

        var validationError = await EfeitoValidationHelper.ValidarAsync(db, request.Grau, request.Efeitos);
        if (validationError is not null)
            return BadRequest(validationError);

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

        entry.Nome = request.Nome;
        entry.Tipo = tipo;
        entry.Grau = request.Grau;
        entry.Descricao = request.Descricao;
        entry.GastoEmPI = gastoEmPI;
        entry.Custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

        // Delete old effects
        var oldEffects = await db.SpellAbilityBankEffects
            .Where(e => e.SpellAbilityBankEntryId == id)
            .ToListAsync();
        db.SpellAbilityBankEffects.RemoveRange(oldEffects);

        // Add new effects
        var newEffects = request.Efeitos
            .Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = entry.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI })
            .ToList();
        db.SpellAbilityBankEffects.AddRange(newEffects);

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var gmId = CurrentUserId();
        var entry = await db.SpellAbilityBankEntries.FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
        if (entry is null)
            return NotFound();

        db.SpellAbilityBankEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static SpellAbilityEntryResponse ToResponse(SpellAbilityBankEntry entry) => new(
        entry.Id.ToString(), entry.Nome, entry.Tipo.ToString(), entry.Grau, entry.GastoEmPI, entry.Custo, entry.Descricao,
        entry.Efeitos.Select(e => new SpellAbilityEffectResponse(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList());

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
