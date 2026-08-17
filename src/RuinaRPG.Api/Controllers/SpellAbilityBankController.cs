using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    [HttpPost]
    public async Task<ActionResult<SpellAbilityEntryResponse>> Create(CreateSpellAbilityEntryRequest request)
    {
        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo))
            return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

        var entry = new SpellAbilityBankEntry
        {
            Id = Guid.NewGuid(),
            GmId = CurrentGmId(),
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

    private static SpellAbilityEntryResponse ToResponse(SpellAbilityBankEntry entry) => new(
        entry.Id.ToString(), entry.Nome, entry.Tipo.ToString(), entry.Grau, entry.GastoEmPI, entry.Custo, entry.Descricao,
        entry.Efeitos.Select(e => new SpellAbilityEffectResponse(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList());

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
