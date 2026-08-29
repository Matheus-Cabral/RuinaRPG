namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Canonical Alvo strings for an Artefato whose TipoDeAlvo is SubAtributo (ver "Requisitos - Catálogo
/// de Itens e Equipamentos" R0009 e "Requisitos - Modelo de Dados" — Alvo é texto livre, então esta
/// classe fixa a grafia esperada em vez de deixar cada GM inventar a sua). Não há enum SubAtributo no
/// domínio — sub-atributos são termos de fórmula, não uma entidade — então estas strings são a única
/// fonte de verdade para casar um Artefato com o sub-atributo que ele bonifica.
/// </summary>
public static class SubAtributoAlvo
{
    public const string Iniciativa = "Iniciativa";
    public const string Movimentacao = "Movimentação";
    public const string EsquivaNatural = "Esquiva Natural";
    public const string DefesaNatural = "Defesa Natural";
    public const string ReducaoFisica = "Redução Física";
    public const string ReducaoMagica = "Redução Mágica";
    public const string Adrenalina = "Adrenalina";
}
