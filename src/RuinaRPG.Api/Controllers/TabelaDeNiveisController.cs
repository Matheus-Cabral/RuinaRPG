using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Auditoria da Tabela de Níveis. Leitura aberta a qualquer usuário autenticado; toda escrita é
/// restrita ao Auditor de Regras (ApplicationUser.IsRulesAuditor, checado no banco, não no JWT).
/// </summary>
[ApiController]
[Route("api/tabela-de-niveis")]
[Authorize]
public class TabelaDeNiveisController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TabelaDeNiveisResponse>> Get()
    {
        var colunas = await db.ColunasDeNivel.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Ordem).ToListAsync();
        var ids = colunas.Select(c => c.Id).ToHashSet();
        var niveis = await db.NiveisProgressao.AsNoTracking().OrderBy(n => n.Nivel).ToListAsync();
        var valores = (await db.ValoresDeNivel.AsNoTracking().ToListAsync()).Where(v => ids.Contains(v.ColunaId)).ToLookup(v => v.Nivel);

        return new TabelaDeNiveisResponse(
            colunas.Select(ToResponse).ToList(),
            niveis.Select(n => new LinhaDeNivelResponse(n.Nivel, n.OutrosBonus,
                valores[n.Nivel].ToDictionary(v => v.ColunaId, v => v.Valor))).ToList(),
            await db.TabelaDeNiveisConfigs.AsNoTracking().Where(c => c.Id == TabelaDeNiveisConfig.IdUnico).Select(c => c.MostrarLimitesNoLivro).SingleOrDefaultAsync());
    }

    [HttpPut("config")]
    public async Task<IActionResult> AtualizarConfig(AtualizarConfigDaTabelaDeNiveisRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        var config = await db.TabelaDeNiveisConfigs.SingleOrDefaultAsync(c => c.Id == TabelaDeNiveisConfig.IdUnico);
        if (config is null)
            db.TabelaDeNiveisConfigs.Add(new TabelaDeNiveisConfig { MostrarLimitesNoLivro = request.MostrarLimitesNoLivro });
        else
            config.MostrarLimitesNoLivro = request.MostrarLimitesNoLivro;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{nivel:int}/valores/{colunaId:guid}")]
    public async Task<IActionResult> AtualizarValor(int nivel, Guid colunaId, AtualizarValorDeNivelRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        if (request.Valor < 0) return BadRequest("O valor não pode ser negativo.");
        if (!await db.NiveisProgressao.AnyAsync(n => n.Nivel == nivel)) return NotFound();
        if (!await db.ColunasDeNivel.AnyAsync(c => c.Id == colunaId && !c.IsDeleted)) return NotFound();

        var celula = await db.ValoresDeNivel.SingleOrDefaultAsync(v => v.Nivel == nivel && v.ColunaId == colunaId);
        if (celula is null)
            db.ValoresDeNivel.Add(new ValorDeNivel { Nivel = nivel, ColunaId = colunaId, Valor = request.Valor });
        else
            celula.Valor = request.Valor;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{nivel:int}/outros-bonus")]
    public async Task<IActionResult> AtualizarOutrosBonus(int nivel, AtualizarOutrosBonusRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        var linha = await db.NiveisProgressao.SingleOrDefaultAsync(n => n.Nivel == nivel);
        if (linha is null) return NotFound();

        linha.OutrosBonus = request.OutrosBonus;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("niveis")]
    public async Task<ActionResult<LinhaDeNivelResponse>> AdicionarNivel()
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        var ultimo = await db.NiveisProgressao.MaxAsync(n => (int?)n.Nivel) ?? 0;
        var novo = new NivelProgressao { Nivel = ultimo + 1 };
        db.NiveisProgressao.Add(novo);
        await db.SaveChangesAsync();
        return Created($"api/tabela-de-niveis", new LinhaDeNivelResponse(novo.Nivel, novo.OutrosBonus, new Dictionary<Guid, int?>()));
    }

    [HttpDelete("niveis/ultimo")]
    public async Task<IActionResult> RemoverUltimoNivel()
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        var niveis = await db.NiveisProgressao.CountAsync();
        if (niveis <= 1) return BadRequest("A tabela precisa ter pelo menos um nível.");

        var ultimo = await db.NiveisProgressao.MaxAsync(n => n.Nivel);
        var emUso = await db.CharacterSheets.CountAsync(s => s.Nivel == ultimo)
            + await db.NpcSheets.CountAsync(s => s.Nivel == ultimo)
            + await db.CreatureSheets.CountAsync(s => s.Nivel == ultimo);
        if (emUso > 0)
            return BadRequest($"{emUso} ficha(s) estão no nível {ultimo}.");

        db.ValoresDeNivel.RemoveRange(db.ValoresDeNivel.Where(v => v.Nivel == ultimo));
        db.NiveisProgressao.RemoveRange(db.NiveisProgressao.Where(n => n.Nivel == ultimo));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("colunas")]
    public async Task<ActionResult<ColunaDeNivelResponse>> CriarColuna(CriarColunaDeNivelRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        if (string.IsNullOrWhiteSpace(request.Nome)) return BadRequest("Informe o nome da coluna.");
        // Enum.TryParse alone would accept a numeric string like "1": require the exact enum name.
        if (!Enum.GetNames<TipoDeColunaDeNivel>().Contains(request.Tipo) || !Enum.TryParse<TipoDeColunaDeNivel>(request.Tipo, out var tipo))
            return BadRequest("Tipo de coluna inválido.");

        var ordem = (await db.ColunasDeNivel.MaxAsync(c => (int?)c.Ordem) ?? -1) + 1;
        var coluna = new ColunaDeNivel { Id = Guid.NewGuid(), Nome = request.Nome.Trim(), Tipo = tipo, Ordem = ordem };
        db.ColunasDeNivel.Add(coluna);
        await db.SaveChangesAsync();
        return Created($"api/tabela-de-niveis/colunas/{coluna.Id}", ToResponse(coluna));
    }

    [HttpPut("colunas/{id:guid}")]
    public async Task<IActionResult> RenomearColuna(Guid id, RenomearColunaDeNivelRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        if (string.IsNullOrWhiteSpace(request.Nome)) return BadRequest("Informe o nome da coluna.");
        var coluna = await db.ColunasDeNivel.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (coluna is null) return NotFound();

        coluna.Nome = request.Nome.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("colunas/{id:guid}")]
    public async Task<IActionResult> RemoverColuna(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        var coluna = await db.ColunasDeNivel.SingleOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (coluna is null) return NotFound();
        if (coluna.ChaveDeSistema is not null) return BadRequest("Colunas do sistema não podem ser removidas.");

        db.ValoresDeNivel.RemoveRange(db.ValoresDeNivel.Where(v => v.ColunaId == id));
        db.ColunasDeNivel.Remove(coluna);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("colunas/ordem")]
    public async Task<IActionResult> ReordenarColunas(ReordenarColunasDeNivelRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null) return authError;

        var colunas = await db.ColunasDeNivel.Where(c => !c.IsDeleted).ToListAsync();
        var ids = request.Ids ?? [];
        if (ids.Count != colunas.Count || !ids.ToHashSet().SetEquals(colunas.Select(c => c.Id)))
            return BadRequest("Informe exatamente as colunas da tabela, cada uma uma vez.");

        for (var i = 0; i < ids.Count; i++)
            colunas.Single(c => c.Id == ids[i]).Ordem = i;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static ColunaDeNivelResponse ToResponse(ColunaDeNivel c) =>
        new(c.Id, c.Nome, c.Tipo.ToString(), c.ChaveDeSistema, c.ChaveDeSistema is not null, c.Ordem);

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
