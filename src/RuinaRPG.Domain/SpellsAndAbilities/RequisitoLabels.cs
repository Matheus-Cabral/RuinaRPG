using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.SpellsAndAbilities;

/// <summary>Rótulos em português usados nas pendências de requisito e na tela da Passiva.</summary>
public static class RequisitoLabels
{
    public static string Categoria(CategoriaDePassiva categoria) => categoria switch
    {
        CategoriaDePassiva.Livre => "Passiva Livre",
        CategoriaDePassiva.Vocacional => "Passiva Vocacional",
        CategoriaDePassiva.DeClasse => "Passiva de Classe",
        _ => categoria.ToString()
    };

    public static string Vocacao(Vocacao vocacao) => vocacao switch
    {
        CharacterSheets.Vocacao.Campeao => "Campeão",
        CharacterSheets.Vocacao.Cacador => "Caçador",
        _ => vocacao.ToString()
    };

    public static string Linhagem(Linhagem linhagem) => linhagem == CharacterSheets.Linhagem.Econos ? "Ecônos" : linhagem.ToString();

    public static string Variante(Variante variante) => variante switch
    {
        CharacterSheets.Variante.PhylacTai => "Phylac'tai",
        CharacterSheets.Variante.EsPhylauc => "Es'Phylauc",
        CharacterSheets.Variante.Alora => "Alóra",
        CharacterSheets.Variante.AloraSolar => "Alóra (Sol)",
        _ => variante.ToString()
    };

    public static string Afinidade(AfinidadeElemental afinidade) => afinidade switch
    {
        AfinidadeElemental.Agua => "Água",
        AfinidadeElemental.Invocacao => "Invocação",
        _ => afinidade.ToString()
    };

    public static string Atributo(Atributo atributo) => atributo switch
    {
        CharacterSheets.Atributo.Influencia => "Influência",
        CharacterSheets.Atributo.Astucia => "Astúcia",
        CharacterSheets.Atributo.Forca => "Força",
        _ => atributo.ToString()
    };

    public static string SubAtributo(SubAtributo subAtributo) => subAtributo switch
    {
        SpellsAndAbilities.SubAtributo.Movimentacao => "Movimentação",
        SpellsAndAbilities.SubAtributo.EsquivaNatural => "Esquiva Natural",
        SpellsAndAbilities.SubAtributo.DefesaNatural => "Defesa Natural",
        SpellsAndAbilities.SubAtributo.ReducaoFisica => "Redução Física",
        SpellsAndAbilities.SubAtributo.ReducaoMagica => "Redução Mágica",
        _ => subAtributo.ToString()
    };
}
