using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.Condicoes;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Catálogo global de Condições (Auditoria → Condições). Leitura aberta a qualquer usuário
/// autenticado (o GM precisa da lista para aplicar); criar, editar e excluir só o Auditor de Regras.
/// A exclusão é lógica e sempre permitida: uma Condição aplicada a um participante é cópia independente.
/// </summary>
[ApiController]
[Route("api/condicoes")]
[Authorize]
public class CondicoesController(RuinaRpgDbContext db, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CondicaoResponse>>> List([FromQuery] string? nome)
    {
        var query = db.Condicoes.Where(c => !c.IsDeleted);
        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(c => EF.Functions.ILike(c.Nome, $"%{nome}%"));

        var condicoes = await query.OrderBy(c => c.Nome).ToListAsync();
        var ids = condicoes.Select(c => c.Id).ToList();
        var modificadores = (await db.CondicaoModificadores.Where(m => ids.Contains(m.CondicaoId)).ToListAsync()).ToLookup(m => m.CondicaoId);
        var desabilitacoes = (await db.CondicaoDesabilitacoes.Where(d => ids.Contains(d.CondicaoId)).ToListAsync()).ToLookup(d => d.CondicaoId);
        return condicoes.Select(c => ToResponse(c, modificadores[c.Id], desabilitacoes[c.Id])).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<CondicaoResponse>> Create(SalvarCondicaoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var (erro, modificadores, desabilitacoes) = await ValidarAsync(request);
        if (erro is not null)
            return BadRequest(erro);
        var nome = request.Nome.Trim();
        if (await db.Condicoes.AnyAsync(c => !c.IsDeleted && c.Nome == nome))
            return BadRequest("Já existe uma condição com esse nome.");

        var condicao = new Condicao
        {
            Id = Guid.NewGuid(),
            Nome = nome,
            Descricao = request.Descricao ?? string.Empty,
            Icone = request.Icone,
            DuracaoPadraoTurnos = request.DuracaoPadraoTurnos,
            UpdatedByUserId = CurrentUserId(),
            UpdatedAt = DateTime.UtcNow,
        };
        db.Condicoes.Add(condicao);
        AddFilhos(condicao.Id, modificadores, desabilitacoes);
        await db.SaveChangesAsync();

        return Created(string.Empty, ToResponse(condicao, db.CondicaoModificadores.Local.Where(m => m.CondicaoId == condicao.Id), db.CondicaoDesabilitacoes.Local.Where(d => d.CondicaoId == condicao.Id)));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, SalvarCondicaoRequest request)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var condicao = await db.Condicoes.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (condicao is null)
            return NotFound();

        var (erro, modificadores, desabilitacoes) = await ValidarAsync(request);
        if (erro is not null)
            return BadRequest(erro);
        var nome = request.Nome.Trim();
        if (await db.Condicoes.AnyAsync(c => c.Id != id && !c.IsDeleted && c.Nome == nome))
            return BadRequest("Já existe uma condição com esse nome.");

        condicao.Nome = nome;
        condicao.Descricao = request.Descricao ?? string.Empty;
        condicao.Icone = request.Icone;
        condicao.DuracaoPadraoTurnos = request.DuracaoPadraoTurnos;
        condicao.UpdatedByUserId = CurrentUserId();
        condicao.UpdatedAt = DateTime.UtcNow;

        // Substitui as linhas: remove as antigas e adiciona as novas pelo DbSet.
        db.CondicaoModificadores.RemoveRange(await db.CondicaoModificadores.Where(m => m.CondicaoId == id).ToListAsync());
        db.CondicaoDesabilitacoes.RemoveRange(await db.CondicaoDesabilitacoes.Where(d => d.CondicaoId == id).ToListAsync());
        AddFilhos(id, modificadores, desabilitacoes);
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authError = await RequireRulesAuditorAsync();
        if (authError is not null)
            return authError;

        var condicao = await db.Condicoes.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (condicao is null)
            return NotFound();

        condicao.IsDeleted = true;
        condicao.UpdatedByUserId = CurrentUserId();
        condicao.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<(string? Erro, List<ModificadorDeCondicao> Modificadores, List<CategoriaDesabilitavel> Desabilitacoes)> ValidarAsync(SalvarCondicaoRequest request)
    {
        var modificadores = new List<ModificadorDeCondicao>();
        foreach (var m in request.Modificadores ?? [])
        {
            if (m is null)
                return ("Modificador inválido.", modificadores, []);
            if (!Enum.TryParse<AlvoDeCondicao>(m.Alvo, out var alvo) || !Enum.GetNames<AlvoDeCondicao>().Contains(m.Alvo))
                return ("Alvo de modificador inválido.", modificadores, []);
            if (!Enum.TryParse<TipoDeValorDeCondicao>(m.TipoDeValor, out var tipo) || !Enum.GetNames<TipoDeValorDeCondicao>().Contains(m.TipoDeValor))
                return ("Tipo de valor de modificador inválido.", modificadores, []);
            MomentoDeCondicao? momento = null;
            if (m.Momento is not null)
            {
                if (!Enum.TryParse<MomentoDeCondicao>(m.Momento, out var mo) || !Enum.GetNames<MomentoDeCondicao>().Contains(m.Momento))
                    return ("Momento de modificador inválido.", modificadores, []);
                momento = mo;
            }
            modificadores.Add(new ModificadorDeCondicao(alvo, string.IsNullOrWhiteSpace(m.AlvoEspecifico) ? null : m.AlvoEspecifico, tipo, m.Valor, momento));
        }

        var desabilitacoes = new List<CategoriaDesabilitavel>();
        foreach (var d in request.Desabilitacoes ?? [])
        {
            if (d is null)
                return ("Categoria desabilitável inválida.", modificadores, desabilitacoes);
            if (!Enum.TryParse<CategoriaDesabilitavel>(d, out var categoria) || !Enum.GetNames<CategoriaDesabilitavel>().Contains(d))
                return ("Categoria desabilitável inválida.", modificadores, desabilitacoes);
            desabilitacoes.Add(categoria);
        }

        if (!CondicaoIcones.Chaves.Contains(request.Icone))
            return ("Ícone inválido.", modificadores, desabilitacoes);

        var chaves = (await pericias.TodasAsync()).Where(p => !p.IsDeleted).Select(p => p.Chave).ToHashSet();
        return (CondicaoValidator.Validar(request.Nome, modificadores, desabilitacoes, request.DuracaoPadraoTurnos, chaves.Contains), modificadores, desabilitacoes);
    }

    private void AddFilhos(Guid condicaoId, List<ModificadorDeCondicao> modificadores, List<CategoriaDesabilitavel> desabilitacoes)
    {
        db.CondicaoModificadores.AddRange(modificadores.Select((m, i) => new CondicaoModificador
        {
            Id = Guid.NewGuid(), CondicaoId = condicaoId, Ordem = i,
            Alvo = m.Alvo, AlvoEspecifico = m.AlvoEspecifico, TipoDeValor = m.TipoDeValor, Valor = m.Valor, Momento = m.Momento,
        }));
        db.CondicaoDesabilitacoes.AddRange(desabilitacoes.Select(d => new CondicaoDesabilitacao { Id = Guid.NewGuid(), CondicaoId = condicaoId, Categoria = d }));
    }

    private static CondicaoResponse ToResponse(Condicao c, IEnumerable<CondicaoModificador> modificadores, IEnumerable<CondicaoDesabilitacao> desabilitacoes) =>
        new(c.Id.ToString(), c.Nome, c.Descricao, c.Icone, c.DuracaoPadraoTurnos,
            modificadores.OrderBy(m => m.Ordem).Select(m => new CondicaoModificadorDto(m.Alvo.ToString(), m.AlvoEspecifico, m.TipoDeValor.ToString(), m.Valor, m.Momento?.ToString())).ToList(),
            desabilitacoes.Select(d => d.Categoria.ToString()).ToList());

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
