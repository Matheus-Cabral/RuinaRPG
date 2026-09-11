namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Hardcoded defaults for each Variante's racial characteristic choices, per Ruína RPG - Sistema
/// Básico.md §7 — mirrors RacialAbilityLookup. Two naming corrections against the raw §7 prose,
/// resolved against "Características" (the actual catalog, source of Custo/Polaridade):
/// "Imunidade a Venenos" -> "Imunidade de Venenos" (§7's wording vs. the catalog's own spelling),
/// and Alóra's "Fraqueza Elemental (fogo)" -> the catalog's generic "Desvantagem Elemental",
/// Especificação "Fogo" (§7 never meant a distinct trait, just this one pre-picked element).
/// </summary>
public static class RacialTraitLookup
{
    private static readonly Dictionary<Variante, RacialTraitSlots> Slots = new()
    {
        [Variante.Sinir] = new(
            Gratuita: [new("Alfabetizado"), new("Sedutor"), new("Aparência Inofensiva")],
            Obrigatoria: []),
        [Variante.Laonir] = new(
            Gratuita: [new("Alfabetizado"), new("Sedutor"), new("Aparência Inofensiva")],
            Obrigatoria: []),
        [Variante.PhylacTai] = new(
            Gratuita: [new("Coragem"), new("Imunidade de Venenos"), new("Sentidos Aguçados")],
            Obrigatoria: [new("Código de Honra"), new("Crédulo")]),
        [Variante.EsPhylauc] = new(
            Gratuita: [new("Coragem"), new("Imunidade de Venenos"), new("Sentidos Aguçados")],
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
