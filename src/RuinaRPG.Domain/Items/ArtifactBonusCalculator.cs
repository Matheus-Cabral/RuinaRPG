namespace RuinaRPG.Domain.Items;

/// <summary>
/// Sums the Valor of a sheet's equipped Artefatos that target a given Atributo/SubAtributo/Perícia,
/// per Requisitos - Ficha de Personagem.md 2.a ("Artefatos refere-se à soma dos Valores de Artefatos
/// equipados cujo Tipo é Atributo e cujo Alvo é este atributo") and the equivalent Sub-Atributo terms
/// in Formulas.md. Simply being on the sheet (Posses 5.b) counts as equipped — Artefatos have no
/// separate equip/unequip toggle, unlike weapons/armor/shields.
/// </summary>
public static class ArtifactBonusCalculator
{
    public static int Sum(IEnumerable<ArtifactBonusInput> artefatos, TipoDeAlvo tipo, string alvo) =>
        artefatos
            .Where(a => a.TipoDeAlvo == tipo && string.Equals(a.Alvo, alvo, StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.Valor);
}

/// <summary>Projection of a sheet's Artefato items, decoupled from any EF entity so the calculator stays pure.</summary>
public readonly record struct ArtifactBonusInput(TipoDeAlvo TipoDeAlvo, string? Alvo, int Valor);
