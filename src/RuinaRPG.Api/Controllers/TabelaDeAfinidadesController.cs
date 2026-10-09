using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Auditoria over the Tabela de Afinidades (see "Tabela de Afinidades.md") — the table that gives
/// Eficiência Elemental and Dano Elemental from the points in the Afinidade. Unlike the fixed
/// DurabilidadesPorRank rows, the Auditor creates, edits and deletes rows here; Afinidade is unique.
/// List (GET) stays open to any authenticated caller; Create/Update/Delete are gated to the Rules
/// Auditor, checked directly against the DB (ApplicationUser.IsRulesAuditor), not a JWT claim.
/// </summary>
[ApiController]
[Route("api/tabela-de-afinidades")]
[Authorize]
public class TabelaDeAfinidadesController(RuinaRpgDbContext db) : ControllerBase
{
    private const string MensagemDeValoresInvalidos = "Afinidade, Eficiência e Dano devem ser números inteiros maiores ou iguais a zero.";

    [HttpGet]
    public async Task<ActionResult<List<LinhaDaTabelaDeAfinidadesResponse>>> List()
    {
        var linhas = await db.TabelaDeAfinidades.OrderBy(l => l.Afinidade).ToListAsync();
        return linhas.Select(ToResponse).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<LinhaDaTabelaDeAfinidadesResponse>> Create(SalvarLinhaDaTabelaDeAfinidadesRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        if (ValoresInvalidos(request))
            return BadRequest(MensagemDeValoresInvalidos);

        if (await db.TabelaDeAfinidades.AnyAsync(l => l.Afinidade == request.Afinidade))
            return Conflict(MensagemDeDuplicidade(request.Afinidade));

        var linha = new AfinidadeElementalLinha
        {
            Id = Guid.NewGuid(),
            Afinidade = request.Afinidade,
            Eficiencia = request.Eficiencia,
            Dano = request.Dano,
        };
        db.TabelaDeAfinidades.Add(linha);
        await db.SaveChangesAsync();

        return Created($"api/tabela-de-afinidades/{linha.Id}", ToResponse(linha));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SalvarLinhaDaTabelaDeAfinidadesRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var linha = await db.TabelaDeAfinidades.SingleOrDefaultAsync(l => l.Id == id);
        if (linha is null)
            return NotFound();

        if (ValoresInvalidos(request))
            return BadRequest(MensagemDeValoresInvalidos);

        if (await db.TabelaDeAfinidades.AnyAsync(l => l.Afinidade == request.Afinidade && l.Id != id))
            return Conflict(MensagemDeDuplicidade(request.Afinidade));

        linha.Afinidade = request.Afinidade;
        linha.Eficiencia = request.Eficiencia;
        linha.Dano = request.Dano;
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var linha = await db.TabelaDeAfinidades.SingleOrDefaultAsync(l => l.Id == id);
        if (linha is null)
            return NotFound();

        db.TabelaDeAfinidades.Remove(linha);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private static bool ValoresInvalidos(SalvarLinhaDaTabelaDeAfinidadesRequest request) =>
        request.Afinidade < 0 || request.Eficiencia < 0 || request.Dano < 0;

    private static string MensagemDeDuplicidade(int afinidade) => $"Já existe uma linha para a Afinidade {afinidade}.";

    private static LinhaDaTabelaDeAfinidadesResponse ToResponse(AfinidadeElementalLinha l) =>
        new(l.Id, l.Afinidade, l.Eficiencia, l.Dano);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
