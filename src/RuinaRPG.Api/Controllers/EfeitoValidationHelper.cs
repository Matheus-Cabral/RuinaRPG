using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Shared by SpellAbilityBankController and the 3 sheet SpellAbilities controllers — maps the
/// Efeito catalog into the plain EfeitoRegra shape EfeitoValidator needs (Domain has no EF
/// dependency, so this mapping has to happen at the API boundary) and runs it against a
/// client-submitted Efeitos list. Only ever called for "from scratch" submissions — "from bank"
/// copies reuse an already-validated bank entry's effects verbatim, see this plan's Global
/// Constraints.
/// </summary>
public static class EfeitoValidationHelper
{
    public static async Task<string?> ValidarAsync(RuinaRpgDbContext db, int grau, List<SpellAbilityEffectRequest> efeitos)
    {
        var catalogo = await db.Efeitos.Where(e => !e.IsDeleted).ToListAsync();
        var regras = catalogo.Select(ToRegra).ToList();
        var submetidos = efeitos.Select(e => new EfeitoSubmetido(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();

        var generalError = EfeitoValidator.Validar(regras, grau, submetidos);
        if (generalError is not null)
            return generalError;

        if (!EfeitoCustoCalculator.DanoAlcanceMaxPorGrau.TryGetValue(grau, out var teto))
            return null; // Grau outside 1-9 shouldn't happen (validated elsewhere), skip defensively

        var dano = submetidos.FirstOrDefault(s => s.EfeitoNome == "Dano");
        if (dano is not null && (dano.Quantidade ?? 1) > teto.MaxDano)
            return $"Efeito \"Dano\" excede o teto de {teto.MaxDano} dados para Grau/Círculo {grau}.";

        var alcance = submetidos.FirstOrDefault(s => s.EfeitoNome == "Alcance");
        if (alcance is not null && (alcance.Quantidade ?? 1) > teto.MaxAlcance)
            return $"Efeito \"Alcance\" excede o teto de {teto.MaxAlcance} pés para Grau/Círculo {grau}.";

        return null;
    }

    private static EfeitoRegra ToRegra(Efeito e) => new(
        e.Nome, e.Grau, e.TipoDeCusto, e.CustoFixo, e.CustoPorUnidade, e.QuantidadeDerivadaDeEfeito,
        e.MaxUnidades, e.MaxEscalaPorGrau, e.MaxContandoAPartirDoGrau, e.CustoAlternativo, e.CustoAlternativoAPartirDoGrau,
        string.IsNullOrEmpty(e.PreRequisitosJson) ? [] : JsonSerializer.Deserialize<List<List<string>>>(e.PreRequisitosJson)!);
}
