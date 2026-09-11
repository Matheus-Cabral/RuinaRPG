namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Hardcoded defaults for each Variante's racial characteristic choices, per Ruína RPG - Sistema
/// Básico.md §7 — mirrors RacialAbilityLookup. Naming corrections against the raw §7 prose,
/// resolved against "Características" (the actual catalog, source of Custo/Polaridade):
/// "Imunidade a Venenos" -> "Imunidade de Venenos" (§7's wording vs. the catalog's own spelling),
/// Alóra's "Fraqueza Elemental (fogo)" -> the catalog's generic "Desvantagem Elemental",
/// Especificação "Fogo" (§7 never meant a distinct trait, just this one pre-picked element) — and
/// both "Aparência Inofensiva" and "Imunidade de Venenos" are multi-tier in the catalog
/// (TraitSeedParser suffixes each tier's Nome with "(N pontos)"), so the racial grant pins the
/// cheaper tier explicitly rather than an ambiguous bare name that no seeded Trait row actually has.
/// </summary>
public static class RacialTraitLookup
{
    private static readonly Dictionary<Variante, RacialTraitSlots> Slots = new()
    {
        [Variante.Sinir] = new(
            Gratuita: [new("Alfabetizado"), new("Sedutor"), new("Aparência Inofensiva (2 pontos)")],
            Obrigatoria: []),
        [Variante.Laonir] = new(
            Gratuita: [new("Alfabetizado"), new("Sedutor"), new("Aparência Inofensiva (2 pontos)")],
            Obrigatoria: []),
        [Variante.PhylacTai] = new(
            Gratuita: [new("Coragem"), new("Imunidade de Venenos (2 pontos)"), new("Sentidos Aguçados")],
            Obrigatoria: [new("Código de Honra"), new("Crédulo")]),
        [Variante.EsPhylauc] = new(
            Gratuita: [new("Coragem"), new("Imunidade de Venenos (2 pontos)"), new("Sentidos Aguçados")],
            Obrigatoria: [new("Código de Honra"), new("Crédulo")]),
        [Variante.Yavos] = new(
            Gratuita: [new("Saque Rápido"), new("Visão Noturna")],
            Obrigatoria: [new("Curioso"), new("Covarde")]),
        [Variante.Koroanos] = new(
            Gratuita: [new("Saque Rápido"), new("Visão Noturna")],
            Obrigatoria: [new("Distração"), new("Covarde")]),
        [Variante.Alora] = new(
            Gratuita: [new("Detectar Magia"), new("Amado por feras")],
            Obrigatoria: [new("Desvantagem Elemental", Especificacao: "Fogo")]),
    };

    public static RacialTraitSlots For(Variante variante) => Slots[variante];
}
