namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>
/// Authoritative, server-side validation for a submitted Efeitos list against the Efeito catalog
/// — Grau availability (cumulative), prerequisite groups (AND of ORs), teto, and CustoPI. Pure, no
/// I/O — the API layer resolves the catalog rows and Dano/Alcance's own teto table before calling
/// this. Returns the first error found, or null when everything checks out.
/// </summary>
public static class EfeitoValidator
{
    public static string? Validar(IReadOnlyList<EfeitoRegra> catalogo, int grauDaMagia, IReadOnlyList<EfeitoSubmetido> submetidos)
    {
        var nomesPresentes = submetidos.Select(s => s.EfeitoNome).ToHashSet();

        foreach (var submetido in submetidos)
        {
            var regra = catalogo.FirstOrDefault(r => r.Nome == submetido.EfeitoNome);
            if (regra is null)
                return $"Efeito \"{submetido.EfeitoNome}\" não encontrado no catálogo.";

            if (regra.Grau > grauDaMagia)
                return $"Efeito \"{submetido.EfeitoNome}\" exige Grau/Círculo {regra.Grau} ou superior.";

            foreach (var grupo in regra.PreRequisitos)
            {
                if (!grupo.Any(nomesPresentes.Contains))
                    return $"Efeito \"{submetido.EfeitoNome}\" exige {string.Join(" ou ", grupo)} já presente na mesma Magia/Habilidade.";
            }

            int? quantidadeDerivada = null;
            if (regra.QuantidadeDerivadaDeEfeito is { } nomeOrigem)
            {
                var origem = submetidos.FirstOrDefault(s => s.EfeitoNome == nomeOrigem);
                if (origem is null)
                    return $"Efeito \"{submetido.EfeitoNome}\" exige que \"{nomeOrigem}\" já tenha uma Quantidade definida na mesma Magia/Habilidade.";
                quantidadeDerivada = origem.Quantidade ?? 0;
            }

            var maxPermitido = EfeitoCustoCalculator.MaxPermitido(regra.MaxUnidades, regra.MaxEscalaPorGrau, regra.MaxContandoAPartirDoGrau, grauDaMagia);
            if (maxPermitido is { } max && (submetido.Quantidade ?? 1) > max)
                return $"Efeito \"{submetido.EfeitoNome}\" excede o teto de {max} para Grau/Círculo {grauDaMagia}.";

            // Manual/ManualPorUnidade: the GM's own typed value IS the input, not a value to
            // recompute — accept whatever CustoPI was submitted for those two types without
            // recalculating (there is nothing to check it against).
            if (regra.TipoDeCusto is Enums.TipoDeCusto.Manual or Enums.TipoDeCusto.ManualPorUnidade)
                continue;

            var custoEsperado = EfeitoCustoCalculator.Calcular(
                regra.TipoDeCusto, regra.CustoFixo, regra.CustoPorUnidade,
                regra.CustoAlternativo, regra.CustoAlternativoAPartirDoGrau,
                grauDaMagia, submetido.Quantidade, null, quantidadeDerivada);
            if (submetido.CustoPI != custoEsperado)
                return $"Efeito \"{submetido.EfeitoNome}\" tem Custo em PI incorreto (esperado {custoEsperado}).";
        }

        return null;
    }
}
