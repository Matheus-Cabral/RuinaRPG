namespace RuinaRPG.Client.Shared.Fields;

/// <summary>
/// One row of an in-progress Magia/Habilidade's Efeitos list — the shared row type behind
/// EfeitosTable, replacing 4 near-identical local classes (BancoDeMagiasForm.EffectFormModel,
/// FichaDePersonagem/FichaDeNpc/FichaDeCriatura's SpellAbilityEffectFormModel).
/// </summary>
public class EfeitoLinha
{
    public string EfeitoNome { get; set; } = "";
    public int? Quantidade { get; set; }
    public int CustoPI { get; set; }

    public EfeitoLinha() { }

    public EfeitoLinha(string efeitoNome, int? quantidade, int custoPI)
    {
        EfeitoNome = efeitoNome;
        Quantidade = quantidade;
        CustoPI = custoPI;
    }
}
