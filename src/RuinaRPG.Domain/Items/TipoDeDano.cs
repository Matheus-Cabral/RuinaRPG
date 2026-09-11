namespace RuinaRPG.Domain.Items;

public enum TipoDeDano
{
    Cortante,
    Perfurante,
    Contundente,
    // Named to match "Formulas.md"'s own term ("Modificador de dano arcano"), not the more
    // colloquial "Mágico" the app used before Artefatos needed a canonical Alvo list per type.
    Arcano
}
