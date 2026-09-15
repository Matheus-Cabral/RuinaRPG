namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>Qual Escola de Magia cada Elemento/Sub-Elemento pertence — ver EscolaDeMagia.</summary>
public static class EscolaDeMagiaCatalog
{
    // Todo Elemento-base pertence a Dobra.
    public static EscolaDeMagia DoElemento(Elemento elemento) => EscolaDeMagia.Dobra;

    // Alma e Vida não aparecem na imagem "Escolas de Magia" (que só mostra 12 dos 14
    // Sub-Elementos) — confirmado com o usuário: os dois pertencem a Consagração.
    public static EscolaDeMagia DoSubElemento(SubElemento subElemento) => subElemento switch
    {
        SubElemento.Flora or SubElemento.Ferro or SubElemento.Raio or SubElemento.Gelo => EscolaDeMagia.Transmutacao,
        SubElemento.Necromancia or SubElemento.Invocacao or SubElemento.Ecomancia or SubElemento.Hemomancia => EscolaDeMagia.Maculacao,
        SubElemento.Curar or SubElemento.Aprimorar or SubElemento.Prever or SubElemento.Purificar
            or SubElemento.Alma or SubElemento.Vida => EscolaDeMagia.Consagracao,
        _ => throw new ArgumentOutOfRangeException(nameof(subElemento)),
    };
}
