using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Infrastructure.Afinidades;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// A Auditoria de Afinidades (Auditoria de Regras R0018–R0021), ao lado do catálogo de <see cref="AfinidadesController"/>:
/// totais por Círculo, elementos, cruzamentos e visibilidade por Vocação/Classe. As listas (GET) ficam abertas a
/// qualquer usuário autenticado; toda escrita é só do Auditor de Regras, checado no banco
/// (ApplicationUser.IsRulesAuditor) antes de qualquer 404, como em <see cref="TabelaDeAfinidadesController"/>.
/// As regras do que se pode gravar ficam no Domain (<see cref="AuditoriaDeAfinidades"/>).
/// </summary>
[ApiController]
[Route("api/afinidades")]
[Authorize]
public class AuditoriaDeAfinidadesController(RuinaRpgDbContext db, CatalogoDeAfinidadesService catalogo) : ControllerBase
{
    // ── Totais por Círculo ──

    [HttpGet("totais-por-circulo")]
    public async Task<ActionResult<List<TotalDeAfinidadePorCirculoResponse>>> Totais() =>
        await db.TotaisDeAfinidadePorCirculo.AsNoTracking().OrderBy(t => t.Circulo)
            .Select(t => new TotalDeAfinidadePorCirculoResponse(t.Circulo, t.Total)).ToListAsync();

    [HttpPut("totais-por-circulo/{circulo:int}")]
    public async Task<IActionResult> SalvarTotal(int circulo, SalvarTotalDeAfinidadePorCirculoRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var linha = await db.TotaisDeAfinidadePorCirculo.SingleOrDefaultAsync(t => t.Circulo == circulo);
        if (linha is null)
            return NotFound();
        if (AuditoriaDeAfinidades.MotivoParaRecusarTotal(request.Total) is { } motivo)
            return BadRequest(motivo);

        linha.Total = request.Total;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Elementos ──

    [HttpPost("elementos")]
    public async Task<ActionResult<ElementoDeAfinidadeResponse>> CriarElemento(SalvarElementoDeAfinidadeRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        if (!TryParseExato<TipoDeElemento>(request.Tipo, out var tipo))
            return BadRequest($"Tipo de elemento inválido: {request.Tipo}.");
        if (AuditoriaDeAfinidades.MotivoParaRecusarElemento(await catalogo.CarregarAsync(), null, request.Nome, tipo, request.Teto) is { } motivo)
            return BadRequest(motivo);

        var elemento = new ElementoDeAfinidade { Nome = request.Nome.Trim(), Tipo = tipo, Teto = request.Teto };
        db.ElementosDeAfinidade.Add(elemento);
        await db.SaveChangesAsync();

        return Created($"api/afinidades/elementos/{elemento.Id}", new ElementoDeAfinidadeResponse(elemento.Id, elemento.Nome, elemento.Tipo.ToString(), elemento.Teto));
    }

    [HttpPut("elementos/{id:int}")]
    public async Task<IActionResult> EditarElemento(int id, SalvarElementoDeAfinidadeRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var elemento = await db.ElementosDeAfinidade.SingleOrDefaultAsync(e => e.Id == id);
        if (elemento is null)
            return NotFound();
        if (!TryParseExato<TipoDeElemento>(request.Tipo, out var tipo))
            return BadRequest($"Tipo de elemento inválido: {request.Tipo}.");
        if (AuditoriaDeAfinidades.MotivoParaRecusarElemento(await catalogo.CarregarAsync(), id, request.Nome, tipo, request.Teto) is { } motivo)
            return BadRequest(motivo);

        elemento.Nome = request.Nome.Trim();
        elemento.Tipo = tipo;
        elemento.Teto = request.Teto;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Recusado (409) enquanto alguma ficha, Condição ou Passiva usa o elemento; leva os cruzamentos e as ligações dele.</summary>
    [HttpDelete("elementos/{id:int}")]
    public async Task<IActionResult> RemoverElemento(int id)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var elemento = await db.ElementosDeAfinidade.SingleOrDefaultAsync(e => e.Id == id);
        if (elemento is null)
            return NotFound();

        var (fichas, condicoes, passivas) = await UsosDoElemento.ContarAsync(db, id);
        if (AuditoriaDeAfinidades.MotivoParaRecusarRemocao(fichas, condicoes, passivas) is { } motivo)
            return Conflict(motivo);

        // As ligações de visibilidade saem em cascata pela FK; os cruzamentos (FK Restrict) saem aqui.
        db.CruzamentosDeAfinidade.RemoveRange(await db.CruzamentosDeAfinidade
            .Where(c => c.ElementoAId == id || c.ElementoBId == id || c.SubElementoId == id).ToListAsync());
        db.ElementosDeAfinidade.Remove(elemento);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Cruzamentos ──

    [HttpGet("cruzamentos")]
    public async Task<ActionResult<List<CruzamentoDeAfinidadeResponse>>> Cruzamentos() =>
        await db.CruzamentosDeAfinidade.AsNoTracking()
            .OrderBy(c => c.SubElementoId).ThenBy(c => c.ElementoAId).ThenBy(c => c.ElementoBId)
            .Select(c => new CruzamentoDeAfinidadeResponse(c.Id, c.ElementoAId, c.MinimoA, c.ElementoBId, c.MinimoB, c.SubElementoId))
            .ToListAsync();

    [HttpPost("cruzamentos")]
    public async Task<ActionResult<CruzamentoDeAfinidadeResponse>> CriarCruzamento(SalvarCruzamentoDeAfinidadeRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var novo = DoPedido(request);
        if (AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(await catalogo.CarregarAsync(), novo, null) is { } motivo)
            return BadRequest(motivo);

        var cruzamento = new CruzamentoDeAfinidade { Id = Guid.NewGuid() };
        Aplicar(cruzamento, novo);
        db.CruzamentosDeAfinidade.Add(cruzamento);
        await db.SaveChangesAsync();

        return Created($"api/afinidades/cruzamentos/{cruzamento.Id}", ToResponse(cruzamento));
    }

    [HttpPut("cruzamentos/{id:guid}")]
    public async Task<IActionResult> EditarCruzamento(Guid id, SalvarCruzamentoDeAfinidadeRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var cruzamento = await db.CruzamentosDeAfinidade.SingleOrDefaultAsync(c => c.Id == id);
        if (cruzamento is null)
            return NotFound();

        var novo = DoPedido(request);
        var atual = new Cruzamento(cruzamento.ElementoAId, cruzamento.MinimoA, cruzamento.ElementoBId, cruzamento.MinimoB, cruzamento.SubElementoId);
        if (AuditoriaDeAfinidades.MotivoParaRecusarCruzamento(await catalogo.CarregarAsync(), novo, atual) is { } motivo)
            return BadRequest(motivo);

        Aplicar(cruzamento, novo);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("cruzamentos/{id:guid}")]
    public async Task<IActionResult> RemoverCruzamento(Guid id)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        var cruzamento = await db.CruzamentosDeAfinidade.SingleOrDefaultAsync(c => c.Id == id);
        if (cruzamento is null)
            return NotFound();

        db.CruzamentosDeAfinidade.Remove(cruzamento);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── Visibilidade por Vocação e por Classe ──

    [HttpGet("visibilidade")]
    public async Task<ActionResult<List<VisibilidadeDeElementoResponse>>> Visibilidade() =>
        (await db.VisibilidadesDeElemento.AsNoTracking().ToListAsync())
            .OrderBy(v => v.Origem).ThenBy(v => v.Chave, StringComparer.Ordinal).ThenBy(v => v.ElementoId)
            .Select(v => new VisibilidadeDeElementoResponse(v.Origem.ToString(), v.Chave, v.ElementoId)).ToList();

    /// <summary>Liga ou desliga um Caminho/Sub-Elemento; repetir o estado atual não muda nada.</summary>
    [HttpPut("visibilidade")]
    public async Task<IActionResult> AlterarVisibilidade(AlterarVisibilidadeDeElementoRequest request)
    {
        if (await RequireRulesAuditorAsync() is { } authError)
            return authError;

        if (!TryParseExato<OrigemDaVisibilidade>(request.Origem, out var origem))
            return BadRequest($"Origem inválida: {request.Origem}.");
        if (AuditoriaDeAfinidades.MotivoParaRecusarVisibilidade(await catalogo.CarregarAsync(), origem, request.Chave, request.ElementoId) is { } motivo)
            return BadRequest(motivo);

        var ligacao = await db.VisibilidadesDeElemento.SingleOrDefaultAsync(v =>
            v.Origem == origem && v.Chave == request.Chave && v.ElementoId == request.ElementoId);
        if (request.Visivel && ligacao is null)
            db.VisibilidadesDeElemento.Add(new VisibilidadeDeElemento { Origem = origem, Chave = request.Chave, ElementoId = request.ElementoId });
        else if (!request.Visivel && ligacao is not null)
            db.VisibilidadesDeElemento.Remove(ligacao);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static Cruzamento DoPedido(SalvarCruzamentoDeAfinidadeRequest r) =>
        new Cruzamento(r.ElementoAId, r.MinimoA, r.ElementoBId, r.MinimoB, r.SubElementoId).Ordenado();

    private static void Aplicar(CruzamentoDeAfinidade entidade, Cruzamento c)
    {
        entidade.ElementoAId = c.ElementoA;
        entidade.MinimoA = c.MinimoA;
        entidade.ElementoBId = c.ElementoB;
        entidade.MinimoB = c.MinimoB;
        entidade.SubElementoId = c.SubElemento;
    }

    private static CruzamentoDeAfinidadeResponse ToResponse(CruzamentoDeAfinidade c) =>
        new(c.Id, c.ElementoAId, c.MinimoA, c.ElementoBId, c.MinimoB, c.SubElementoId);

    // Só o nome exato de um membro (nada de números nem caixa diferente).
    private static bool TryParseExato<T>(string? texto, out T valor) where T : struct, Enum =>
        Enum.TryParse(texto, ignoreCase: false, out valor) && Enum.IsDefined(valor) && valor.ToString() == texto;

    private async Task<ActionResult?> RequireRulesAuditorAsync()
    {
        var isAuditor = await db.Users.Where(u => u.Id == CurrentUserId()).Select(u => u.IsRulesAuditor).SingleOrDefaultAsync();
        return isAuditor ? null : Forbid();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
