namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// A aba Antecedentes (onde o tipo tem Antecedentes). Estrela, Histórico e o texto da História moram no formulário
/// auto-salvo da página (<see cref="FormularioDaFicha"/>), que ela passa à aba; a História tem endpoint e autosave próprios
/// (<c>HistoriaEditor</c>), então esta parte não faz requisições.
/// </summary>
public sealed partial class FichaEstado
{
    /// <summary>
    /// A História já veio da ficha. Ela só é lida na primeira carga: as recargas seguintes (depois do autosave de
    /// qualquer outro campo) não podem passar por cima do que o editor está segurando, e o editor só lê o HTML ao ser
    /// criado, então a aba espera esta marca para criá-lo.
    /// </summary>
    public bool HistoriaCarregada { get; set; }

    /// <summary>O endpoint da História (<c>PUT …/historia</c>; a galeria fica em <c>…/historia/imagens</c>).</summary>
    public string UrlDaHistoria => Tipo.Url(SheetId, "historia");
}
