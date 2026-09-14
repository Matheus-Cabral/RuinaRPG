using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// The "[[GRAUS & CÍRCULOS]]" effect catalog — global, Auditor-editable, same access model as
/// TraitsController/CreatureExclusiveTraitsController: List open to any authenticated caller
/// (every Magia/Habilidade-editing form uses it), Create/Update/Delete gated to the Rules
/// Auditor.
/// </summary>
[ApiController]
[Route("api/efeitos")]
[Authorize]
public class EfeitosController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<EfeitoResponse>>> List()
    {
        var efeitos = await db.Efeitos.Where(e => !e.IsDeleted).ToListAsync();
        return efeitos.Select(ToResponse).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<EfeitoResponse>> Create(CreateEfeitoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (!Enum.TryParse<TipoDeCusto>(request.TipoDeCusto, out var tipoDeCusto))
            return BadRequest("TipoDeCusto inválido.");
        if (await db.Efeitos.AnyAsync(e => e.Nome == request.Nome && !e.IsDeleted))
            return BadRequest("Já existe um Efeito com esse Nome.");

        var efeito = new Efeito
        {
            Id = Guid.NewGuid(), Nome = request.Nome, Grau = request.Grau, Descricao = request.Descricao,
            TipoDeCusto = tipoDeCusto, CustoFixo = request.CustoFixo, CustoPorUnidade = request.CustoPorUnidade,
            UnidadeLabel = request.UnidadeLabel, QuantidadeDerivadaDeEfeito = request.QuantidadeDerivadaDeEfeito,
            MaxUnidades = request.MaxUnidades, MaxEscalaPorGrau = request.MaxEscalaPorGrau,
            MaxContandoAPartirDoGrau = request.MaxContandoAPartirDoGrau,
            CustoAlternativo = request.CustoAlternativo, CustoAlternativoAPartirDoGrau = request.CustoAlternativoAPartirDoGrau,
            PreRequisitosJson = JsonSerializer.Serialize(request.PreRequisitos),
            IsCustomized = true, UpdatedByUserId = CurrentUserId(), UpdatedAt = DateTime.UtcNow,
        };
        db.Efeitos.Add(efeito);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(efeito));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateEfeitoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var efeito = await db.Efeitos.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        if (efeito is null)
            return NotFound();

        if (!Enum.TryParse<TipoDeCusto>(request.TipoDeCusto, out var tipoDeCusto))
            return BadRequest("TipoDeCusto inválido.");
        if (await db.Efeitos.AnyAsync(e => e.Id != id && e.Nome == request.Nome && !e.IsDeleted))
            return BadRequest("Já existe um Efeito com esse Nome.");

        efeito.Nome = request.Nome; efeito.Grau = request.Grau; efeito.Descricao = request.Descricao;
        efeito.TipoDeCusto = tipoDeCusto; efeito.CustoFixo = request.CustoFixo; efeito.CustoPorUnidade = request.CustoPorUnidade;
        efeito.UnidadeLabel = request.UnidadeLabel; efeito.QuantidadeDerivadaDeEfeito = request.QuantidadeDerivadaDeEfeito;
        efeito.MaxUnidades = request.MaxUnidades; efeito.MaxEscalaPorGrau = request.MaxEscalaPorGrau;
        efeito.MaxContandoAPartirDoGrau = request.MaxContandoAPartirDoGrau;
        efeito.CustoAlternativo = request.CustoAlternativo; efeito.CustoAlternativoAPartirDoGrau = request.CustoAlternativoAPartirDoGrau;
        efeito.PreRequisitosJson = JsonSerializer.Serialize(request.PreRequisitos);
        efeito.IsCustomized = true; efeito.UpdatedByUserId = CurrentUserId(); efeito.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var efeito = await db.Efeitos.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        if (efeito is null)
            return NotFound();

        efeito.IsDeleted = true;
        efeito.UpdatedByUserId = CurrentUserId(); efeito.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static EfeitoResponse ToResponse(Efeito e) => new(
        e.Id.ToString(), e.Nome, e.Grau, e.Descricao, e.TipoDeCusto.ToString(),
        e.CustoFixo, e.CustoPorUnidade, e.UnidadeLabel, e.QuantidadeDerivadaDeEfeito,
        e.MaxUnidades, e.MaxEscalaPorGrau, e.MaxContandoAPartirDoGrau,
        e.CustoAlternativo, e.CustoAlternativoAPartirDoGrau,
        string.IsNullOrEmpty(e.PreRequisitosJson) ? [] : JsonSerializer.Deserialize<List<List<string>>>(e.PreRequisitosJson)!);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
