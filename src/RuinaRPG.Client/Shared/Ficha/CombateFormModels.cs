namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// O formulário "Adicionar Arma". A Origem e os campos manuais só aparecem onde o tipo tem arma manual
/// (<see cref="TipoDeFichaCliente.TemArmaManual"/>, no <c>ArmaManualForm</c>); nos outros a Origem fica "Catalogo".
/// </summary>
public sealed class WeaponFormModel
{
    public string Origem { get; set; } = "Catalogo";
    public string ItemId { get; set; } = "";
    public string ManualNome { get; set; } = "";
    public string ManualTipoDeDano { get; set; } = "";
    public string ManualDados { get; set; } = "";
    public int ManualDano { get; set; }

    /// <summary>Depois de adicionar: o item e os campos manuais esvaziam; a Origem fica a que estava.</summary>
    public void Limpar()
    {
        ItemId = "";
        ManualNome = "";
        ManualTipoDeDano = "";
        ManualDados = "";
        ManualDano = 0;
    }
}

/// <summary>O formulário "Adicionar Escudo".</summary>
public sealed class ShieldFormModel
{
    public string ItemId { get; set; } = "";
}

/// <summary>O formulário "Adicionar Maestria".</summary>
public sealed class MasteryFormModel
{
    public string Nome { get; set; } = "";
    public string Pericia { get; set; } = "";
    public string Atributo { get; set; } = "";
    public int GastoMaestria { get; set; }
}
