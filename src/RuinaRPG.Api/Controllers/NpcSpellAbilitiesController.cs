using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize(Roles = "GM")]
[Route("api/npc-sheets/{sheetId}/spell-abilities")]
public class NpcSpellAbilitiesController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<NpcSpellAbilityResponse>> Add(Guid sheetId, AddNpcSpellAbilityRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (sheet.GmId != CurrentGmId())
            return NotFound();

        var fromScratch = request.Nome is not null && request.Tipo is not null && request.Grau is not null && request.Descricao is not null && request.Efeitos is not null;
        var fromBank = request.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // both or neither set
            return BadRequest("Informe exatamente um: os campos para montar do zero, ou SourceBankEntryId.");

        string nome; SpellAbilityTipo tipo; int grau; string descricao; List<SpellAbilityEffectRequest> efeitos;
        Guid? sourceBankEntryId = null;

        if (fromBank)
        {
            if (!Guid.TryParse(request.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            var bankEntry = await db.SpellAbilityBankEntries.Include(e => e.Efeitos).FirstOrDefaultAsync(e => e.Id == bankEntryId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            nome = bankEntry.Nome; tipo = bankEntry.Tipo; grau = bankEntry.Grau; descricao = bankEntry.Descricao;
            efeitos = bankEntry.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();
            sourceBankEntryId = bankEntryId;
        }
        else
        {
            if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out tipo))
                return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");
            nome = request.Nome!; grau = request.Grau!.Value; descricao = request.Descricao!; efeitos = request.Efeitos!;
        }

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(efeitos.Select(e => e.CustoPI));
        var custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

        var sheetCopy = new NpcSpellAbility
        {
            Id = Guid.NewGuid(), NpcSheetId = sheetId, SourceBankEntryId = sourceBankEntryId,
            Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao
        };
        sheetCopy.Efeitos = efeitos.Select(e => new NpcSpellAbilityEffect { Id = Guid.NewGuid(), NpcSpellAbilityId = sheetCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
        db.NpcSpellAbilities.Add(sheetCopy);

        // R0001 (Banco de Magias): every creation — from scratch or from an existing bank entry —
        // also lands an independent copy in the GM's bank, applying "em qualquer ficha" (NPC included).
        var bankCopy = new SpellAbilityBankEntry
        {
            Id = Guid.NewGuid(), GmId = sheet.GmId, Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao
        };
        bankCopy.Efeitos = efeitos.Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = bankCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
        db.SpellAbilityBankEntries.Add(bankCopy);

        await db.SaveChangesAsync();
        return Created(string.Empty, ToResponse(sheetCopy));
    }

    [HttpGet]
    public async Task<ActionResult<List<NpcSpellAbilityResponse>>> List(Guid sheetId)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (sheet.GmId != CurrentGmId())
            return NotFound();

        var entries = await db.NpcSpellAbilities.Include(e => e.Efeitos).Where(e => e.NpcSheetId == sheetId).ToListAsync();
        return entries.Select(ToResponse).ToList();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.NpcSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (sheet.GmId != CurrentGmId())
            return NotFound();

        var entry = await db.NpcSpellAbilities.FirstOrDefaultAsync(e => e.Id == id && e.NpcSheetId == sheetId);
        if (entry is null)
            return NotFound();

        db.NpcSpellAbilities.Remove(entry); // the bank copy this creation also made is untouched — independent copies
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static NpcSpellAbilityResponse ToResponse(NpcSpellAbility e) => new(
        e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao,
        e.Efeitos.Select(ef => new SpellAbilityEffectResponse(ef.EfeitoNome, ef.Quantidade, ef.CustoPI)).ToList());

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
