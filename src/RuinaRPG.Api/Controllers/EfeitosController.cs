using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// The "[[GRAUS & CÍRCULOS]]" effect catalog — global, Auditor-editable, same access model as
/// TraitsController/CreatureExclusiveTraitsController: List open to any authenticated caller
/// (every Magia/Habilidade-editing form uses it), Create/Update/Delete gated to the Rules
/// Auditor. Create/Update/Delete also keep the Efeito's block in the Livro de Regras' Graus &amp;
/// Círculos document in sync (LivroDeRegrasEfeitosSync), saved in the same SaveChangesAsync.
/// </summary>
[ApiController]
[Route("api/efeitos")]
[Authorize]
public class EfeitosController(RuinaRpgDbContext db, LivroDeRegrasEfeitosSync livroDeRegras) : ControllerBase
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

        if (!Enum.TryParse<TipoDeCusto>(request.TipoDeCusto, out var tipoDeCusto) || !Enum.IsDefined(tipoDeCusto))
            return BadRequest("TipoDeCusto inválido.");
        if (await NomeJaExisteAsync(request.Nome, idIgnorado: null))
            return BadRequest("Já existe um Efeito com esse Nome.");
        var camposError = ValidarCamposDoTipoDeCusto(
            tipoDeCusto, request.CustoFixo, request.CustoPorUnidade, request.QuantidadeDerivadaDeEfeito,
            request.CustoAlternativo, request.CustoAlternativoAPartirDoGrau);
        if (camposError is not null)
            return BadRequest(camposError);
        // Graus & Círculos only has Graus 1-9, and every catalog Efeito must have a block in the
        // Livro (Requisitos - Auditoria de Regras R0006).
        if (request.Grau is < 1 or > 9)
            return BadRequest("Grau deve estar entre 1 e 9.");

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
        await livroDeRegras.AoCriarAsync(efeito, CurrentUserId());
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

        if (!Enum.TryParse<TipoDeCusto>(request.TipoDeCusto, out var tipoDeCusto) || !Enum.IsDefined(tipoDeCusto))
            return BadRequest("TipoDeCusto inválido.");
        if (await NomeJaExisteAsync(request.Nome, idIgnorado: id))
            return BadRequest("Já existe um Efeito com esse Nome.");
        var camposError = ValidarCamposDoTipoDeCusto(
            tipoDeCusto, request.CustoFixo, request.CustoPorUnidade, request.QuantidadeDerivadaDeEfeito,
            request.CustoAlternativo, request.CustoAlternativoAPartirDoGrau);
        if (camposError is not null)
            return BadRequest(camposError);
        // Graus & Círculos only has Graus 1-9, and every catalog Efeito must have a block in the
        // Livro (Requisitos - Auditoria de Regras R0006).
        if (request.Grau is < 1 or > 9)
            return BadRequest("Grau deve estar entre 1 e 9.");

        var nomeAntigo = efeito.Nome;
        efeito.Nome = request.Nome; efeito.Grau = request.Grau; efeito.Descricao = request.Descricao;
        efeito.TipoDeCusto = tipoDeCusto; efeito.CustoFixo = request.CustoFixo; efeito.CustoPorUnidade = request.CustoPorUnidade;
        efeito.UnidadeLabel = request.UnidadeLabel; efeito.QuantidadeDerivadaDeEfeito = request.QuantidadeDerivadaDeEfeito;
        efeito.MaxUnidades = request.MaxUnidades; efeito.MaxEscalaPorGrau = request.MaxEscalaPorGrau;
        efeito.MaxContandoAPartirDoGrau = request.MaxContandoAPartirDoGrau;
        efeito.CustoAlternativo = request.CustoAlternativo; efeito.CustoAlternativoAPartirDoGrau = request.CustoAlternativoAPartirDoGrau;
        efeito.PreRequisitosJson = JsonSerializer.Serialize(request.PreRequisitos);
        efeito.IsCustomized = true; efeito.UpdatedByUserId = CurrentUserId(); efeito.UpdatedAt = DateTime.UtcNow;
        await livroDeRegras.AoEditarAsync(nomeAntigo, efeito, CurrentUserId());
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
        await livroDeRegras.AoExcluirAsync(efeito, CurrentUserId());
        await db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Rejects a catalog row missing the cost field its own TipoDeCusto requires — a gap here lets
    /// EfeitoCustoCalculator.Calcular's <c>!.Value</c> throw at Magia/Habilidade-validation time
    /// instead of at catalog-write time (Manual/ManualPorUnidade require neither field, since the
    /// GM types the value later — see EfeitoValidator's own skip-recompute handling of those two
    /// types). Also rejects an inconsistent CustoAlternativo/CustoAlternativoAPartirDoGrau pair,
    /// the same class of poison row (see Calcular's own use of that pair).
    /// </summary>
    // Two Nomes equal ignoring case/accents would map to the same "## Nome" block in the Livro
    // (GrausECirculosMarkdown), so they count as duplicates. The catalog is small (~50 rows), so
    // the comparison runs in memory.
    private async Task<bool> NomeJaExisteAsync(string nome, Guid? idIgnorado)
    {
        var nomes = await db.Efeitos
            .Where(e => !e.IsDeleted && e.Id != idIgnorado)
            .Select(e => e.Nome)
            .ToListAsync();
        return nomes.Any(n => GrausECirculosMarkdown.NomesEquivalentes(n, nome));
    }

    private static string? ValidarCamposDoTipoDeCusto(
        TipoDeCusto tipoDeCusto, int? custoFixo, int? custoPorUnidade, string? quantidadeDerivadaDeEfeito,
        int? custoAlternativo, int? custoAlternativoAPartirDoGrau)
    {
        switch (tipoDeCusto)
        {
            case TipoDeCusto.Fixo when custoFixo is null:
                return "CustoFixo é obrigatório para TipoDeCusto Fixo.";
            case TipoDeCusto.PorUnidade when custoPorUnidade is null:
                return "CustoPorUnidade é obrigatório para TipoDeCusto PorUnidade.";
            case TipoDeCusto.DerivadoDeOutroEfeito when custoPorUnidade is null:
                return "CustoPorUnidade é obrigatório para TipoDeCusto DerivadoDeOutroEfeito.";
            case TipoDeCusto.DerivadoDeOutroEfeito when string.IsNullOrWhiteSpace(quantidadeDerivadaDeEfeito):
                return "QuantidadeDerivadaDeEfeito é obrigatório para TipoDeCusto DerivadoDeOutroEfeito.";
        }

        if ((custoAlternativo is null) != (custoAlternativoAPartirDoGrau is null))
            return "CustoAlternativo e CustoAlternativoAPartirDoGrau devem ser preenchidos juntos, ou ambos deixados em branco.";

        return null;
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
