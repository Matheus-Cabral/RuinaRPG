using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Auditoria de Perícias (Requisitos - Auditoria de Regras R0012). A lista ativa é aberta a qualquer
/// usuário autenticado (fichas e dropdowns); todo o resto exige o Auditor de Regras, conferido no banco.
/// Remover é lógico e devolve os pontos gastos: zera o Gasto da perícia em todas as fichas.
/// </summary>
[ApiController]
[Route("api/pericias")]
[Authorize]
public class PericiasController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PericiaResponse>>> List()
    {
        var ativas = await db.Pericias.Where(p => !p.IsDeleted).ToListAsync();
        return ativas.OrderBy(p => p.Nome, StringComparer.CurrentCulture)
            .Select(p => new PericiaResponse(p.Id, p.Chave, p.Nome, p.Descricao, p.AtributoSugerido?.ToString(), p.DisponivelParaCriaturas, PericiasDeSistema.IsProtegida(p.Id)))
            .ToList();
    }

    [HttpGet("auditoria")]
    public async Task<ActionResult<List<PericiaAuditoriaResponse>>> ListAuditoria()
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var todas = await db.Pericias.ToListAsync();
        return todas.OrderBy(p => p.Nome, StringComparer.CurrentCulture).Select(ToAuditoria).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<PericiaAuditoriaResponse>> Create(SalvarPericiaRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        if (await ValidateAsync(request, idAtual: null) is { } invalid)
            return invalid;

        var chaves = await db.Pericias.Select(p => p.Chave).ToListAsync();
        var proximoId = await db.Pericias.MaxAsync(p => p.Id) + 1;
        var pericia = new PericiaDefinicao
        {
            Id = proximoId,
            Chave = PericiaChave.Gerar(request.Nome, chaves),
            Nome = request.Nome.Trim(),
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            AtributoSugerido = ParseAtributo(request.AtributoSugerido),
            DisponivelParaCriaturas = request.DisponivelParaCriaturas,
        };
        db.Pericias.Add(pericia);
        await db.SaveChangesAsync();
        return Created($"/api/pericias/{pericia.Id}", ToAuditoria(pericia));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PericiaAuditoriaResponse>> Update(int id, SalvarPericiaRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        var pericia = await db.Pericias.FindAsync(id);
        if (pericia is null)
            return NotFound();
        if (await ValidateAsync(request, idAtual: id) is { } invalid)
            return invalid;

        pericia.Nome = request.Nome.Trim();
        pericia.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        pericia.AtributoSugerido = ParseAtributo(request.AtributoSugerido);
        // As protegidas entram nas fórmulas de Criatura também — não podem sair da lista de Criaturas.
        pericia.DisponivelParaCriaturas = PericiasDeSistema.IsProtegida(id) || request.DisponivelParaCriaturas;
        await db.SaveChangesAsync();
        return ToAuditoria(pericia);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        var pericia = await db.Pericias.FindAsync(id);
        if (pericia is null)
            return NotFound();
        if (PericiasDeSistema.IsProtegida(id))
            return BadRequest("Esta perícia entra em fórmulas e não pode ser removida.");

        await using var tx = await db.Database.BeginTransactionAsync();
        pericia.IsDeleted = true;
        await db.SaveChangesAsync();
        await ZerarGastoAsync(id);
        await tx.CommitAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/restaurar")]
    public async Task<ActionResult<PericiaAuditoriaResponse>> Restore(int id)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;
        var pericia = await db.Pericias.FindAsync(id);
        if (pericia is null)
            return NotFound();
        if (await NomeEmUsoAsync(pericia.Nome, id))
            return BadRequest("Já existe uma perícia ativa com esse nome.");

        pericia.IsDeleted = false;
        await db.SaveChangesAsync();
        return ToAuditoria(pericia);
    }

    private async Task<ActionResult?> ValidateAsync(SalvarPericiaRequest request, int? idAtual)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            return BadRequest("Informe o nome da perícia.");
        if (request.AtributoSugerido is not null && ParseAtributo(request.AtributoSugerido) is null)
            return BadRequest("Atributo sugerido desconhecido.");
        if (await NomeEmUsoAsync(request.Nome.Trim(), idAtual))
            return BadRequest("Já existe uma perícia ativa com esse nome.");
        return null;
    }

    private Task<bool> NomeEmUsoAsync(string nome, int? excetoId) =>
        db.Pericias.AnyAsync(p => !p.IsDeleted && p.Id != excetoId && p.Nome.ToLower() == nome.ToLower());

    private static Atributo? ParseAtributo(string? valor) =>
        Enum.GetNames<Atributo>().Contains(valor) && Enum.TryParse<Atributo>(valor, out var a) ? a : null;

    private async Task ZerarGastoAsync(int id)
    {
        await db.CharacterSkills.Where(s => s.PericiaId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Gasto, 0));
        await db.NpcSkills.Where(s => s.PericiaId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Gasto, 0));
        await db.CreatureSkills.Where(s => s.PericiaId == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Gasto, 0));
    }

    private static PericiaAuditoriaResponse ToAuditoria(PericiaDefinicao p) =>
        new(p.Id, p.Chave, p.Nome, p.Descricao, p.AtributoSugerido?.ToString(), p.DisponivelParaCriaturas, PericiasDeSistema.IsProtegida(p.Id), p.IsDeleted);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
