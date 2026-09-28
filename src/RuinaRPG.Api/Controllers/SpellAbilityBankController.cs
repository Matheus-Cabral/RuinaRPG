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
            return BadRequest("Tipo desconhecido. Use Magia, Habilidade, Racial ou Passiva.");

        CategoriaDePassiva? categoria = null;
        RequisitosDePassiva? requisitos = null;
        var passivaError = await ValidarPassivaAsync(tipo, request.Grau, request.Efeitos, request.Categoria, request.Requisitos, (c, r) => { categoria = c; requisitos = r; });
        if (passivaError is not null)
            return BadRequest(passivaError);

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
            Descricao = request.Descricao,
            DeCriatura = request.DeCriatura,
            Categoria = categoria,
            Requisitos = requisitos
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
        [FromQuery] int? grau,
        [FromQuery] bool? deCriatura)
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

        if (deCriatura is not null)
            query = query.Where(e => e.DeCriatura == deCriatura);

        var entries = await query.ToListAsync();
        return entries.Select(ToResponse).ToList();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateSpellAbilityEntryRequest request)
    {
        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo) || !Enum.IsDefined(tipo))
            return BadRequest("Tipo desconhecido. Use Magia, Habilidade, Racial ou Passiva.");

        var gmId = CurrentUserId();
        var entry = await db.SpellAbilityBankEntries
            .FirstOrDefaultAsync(e => e.Id == id && e.GmId == gmId);
        if (entry is null)
            return NotFound();

        CategoriaDePassiva? categoria = null;
        RequisitosDePassiva? requisitos = null;
        var passivaError = await ValidarPassivaAsync(tipo, request.Grau, request.Efeitos, request.Categoria, request.Requisitos, (c, r) => { categoria = c; requisitos = r; });
        if (passivaError is not null)
            return BadRequest(passivaError);

        var validationError = await EfeitoValidationHelper.ValidarAsync(db, request.Grau, request.Efeitos);
        if (validationError is not null)
            return BadRequest(validationError);

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

        entry.Nome = request.Nome;
        entry.Tipo = tipo;
        entry.Grau = request.Grau;
        entry.Descricao = request.Descricao;
        entry.DeCriatura = request.DeCriatura;
        entry.GastoEmPI = gastoEmPI;
        entry.Custo = SpellAbilityCostCalculator.Custo(gastoEmPI);
        entry.Categoria = categoria;
        entry.Requisitos = requisitos;

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

    /// <summary>
    /// Passiva (R0009): exige Categoria, aceita Requisitos, não tem Grau nem Efeitos. Os demais tipos não
    /// têm Categoria nem Requisitos. Devolve a mensagem de erro, ou nulo quando o pedido é válido.
    /// </summary>
    private async Task<string?> ValidarPassivaAsync(SpellAbilityTipo tipo, int grau, List<SpellAbilityEffectRequest> efeitos,
        string? categoriaRaw, RequisitosDePassivaDto? requisitosDto, Action<CategoriaDePassiva?, RequisitosDePassiva?> aplicar)
    {
        if (tipo != SpellAbilityTipo.Passiva)
        {
            if (categoriaRaw is not null || requisitosDto is not null)
                return "Categoria e Requisitos só existem em Passivas.";
            aplicar(null, null);
            return null;
        }

        if (grau != 0 || efeitos.Count > 0)
            return "Uma Passiva não tem Grau nem Efeitos.";
        if (!Enum.TryParse<CategoriaDePassiva>(categoriaRaw, out var categoria) || !Enum.IsDefined(categoria))
            return "Categoria desconhecida. Use Livre, Vocacional ou DeClasse.";
        if (!RequisitosDePassivaMapper.TryParse(requisitosDto, out var requisitos, out var erro))
            return erro;
        if (requisitos?.HistoricoId is { } historicoId && !await db.Historicos.AnyAsync(h => h.Id == historicoId))
            return "Histórico não encontrado.";

        aplicar(categoria, requisitos);
        return null;
    }

    private static SpellAbilityEntryResponse ToResponse(SpellAbilityBankEntry entry) => new(
        entry.Id.ToString(), entry.Nome, entry.Tipo.ToString(), entry.Grau, entry.GastoEmPI, entry.Custo, entry.Descricao,
        entry.Efeitos.Select(e => new SpellAbilityEffectResponse(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList(), entry.DeCriatura,
        entry.Categoria?.ToString(), RequisitosDePassivaMapper.ToDto(entry.Requisitos));

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
