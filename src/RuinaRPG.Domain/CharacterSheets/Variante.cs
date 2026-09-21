namespace RuinaRPG.Domain.CharacterSheets;

public enum Variante
{
    Sinir,       // Humano
    Laonir,      // Humano
    PhylacTai,   // Phylauc
    EsPhylauc,   // Phylauc
    Yavos,       // Nephrytes
    Koroanos,    // Nephrytes
    Alora,       // Econos (Lua)
    // Econos (Sol). O livro não a define: só fica disponível nas fichas quando o GM dá um nome a ela
    // (RacialAbilityOverride.NomeDaVariante) — ver Requisitos - Habilidades Raciais R0005.
    AloraSolar
}
