using System.Globalization;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Runes;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>Como um valor de coluna aparece numa linha do log de ficha.</summary>
public static class SheetLogValues
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Nome exibido dos valores de enum cuja tela não mostra o nome do membro (acento, espaço, sufixo). É a cópia,
    /// para o log, dos rótulos que vivem no Client (AtributoDisplay, RuneTipoRotulo, os selects de Shared/Fields) —
    /// a Infrastructure não referencia o Client. Um membro sem entrada aparece com o próprio nome.
    /// </summary>
    private static readonly Dictionary<Enum, string> RotulosDeEnum = new()
    {
        [Atributo.Influencia] = "Influência", [Atributo.Astucia] = "Astúcia", [Atributo.Forca] = "Força",
        [Vocacao.Campeao] = "Campeão", [Vocacao.Cacador] = "Caçador",
        [Variante.Sinir] = "Sinir (Sol)", [Variante.Laonir] = "Laonir (Lua)",
        [Variante.PhylacTai] = "Phylac'tai (Sol)", [Variante.EsPhylauc] = "Es'Phylauc (Lua)",
        [Variante.Koroanos] = "Koroanos (Sol)", [Variante.Yavos] = "Yavos (Lua)",
        [Variante.Alora] = "Alóra (Lua)", [Variante.AloraSolar] = "Alóra (Sol)",
        [AfinidadeElemental.Agua] = "Água", [AfinidadeElemental.Invocacao] = "Invocação",
        [Elemento.Agua] = "Água", [EssenciaBasica.Agua] = "Água", [SubElemento.Invocacao] = "Invocação",
        [Arquetipo.Fisico] = "Físico",
        [TipoDeRuna.Arcana] = "Runa Arcana", [TipoDeRuna.Negra] = "Runa Negra",
        [CategoriaDePassiva.Livre] = "Passiva Livre", [CategoriaDePassiva.Vocacional] = "Passiva Vocacional",
        [CategoriaDePassiva.DeClasse] = "Passiva de Classe",
    };

    /// <summary>
    /// bool → "Sim"/"Não"; null → "—"; decimal sem zeros à direita (vírgula); Disciplina da Runa → seu rótulo;
    /// demais enums → o nome exibido na tela (RotulosDeEnum) ou, sem entrada, o nome do valor; outros → ToString().
    /// </summary>
    public static string Render(object? valor) => valor switch
    {
        null => "—",
        bool b => b ? "Sim" : "Não",
        DisciplinaDeRuna d => DisciplinaDeRunaInfo.Rotulo(d),
        Enum e => RotulosDeEnum.GetValueOrDefault(e, e.ToString()),
        decimal m => m.ToString("0.############################", PtBr),
        double d => d.ToString("0.################", PtBr),
        float f => f.ToString("0.#######", PtBr),
        IFormattable f => f.ToString(null, PtBr),
        _ => valor.ToString() ?? "—"
    };
}
