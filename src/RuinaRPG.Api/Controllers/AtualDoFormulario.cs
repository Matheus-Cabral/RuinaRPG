namespace RuinaRPG.Api.Controllers;

/// <summary>
/// O Atual de um recurso no salvamento do formulário inteiro da ficha (Personagem, NPC, Criatura). O servidor também
/// escreve esses Atuais (ajustes "ao aplicar"/"a cada vez" das Condições, corte ao mudar as Condições ativas), e o
/// formulário é salvo por inteiro, sem controle de concorrência: um Atual só é gravado quando o jogador o editou, isto é,
/// quando o enviado difere do valor com que o formulário foi carregado. Mesma ideia dos Pontos de Ignição.
/// </summary>
internal static class AtualDoFormulario
{
    /// <summary>O jogador editou o campo. Sem o valor carregado (cliente antigo) conta como editado, como antes.</summary>
    public static bool Editado(int enviado, int? carregado) => carregado != enviado;

    /// <summary>O enviado, se editado; senão o que já está guardado.</summary>
    public static int Escolher(int guardado, int enviado, int? carregado) => Editado(enviado, carregado) ? enviado : guardado;

    /// <summary>
    /// NPC/Criatura: o editado de fato fica no máximo atual da ficha (com as Condições ativas, o mesmo que a ficha mostra);
    /// o não editado fica como está; o de um cliente antigo é gravado como veio (o comportamento de antes).
    /// </summary>
    public static int EscolherNoMaximo(int guardado, int enviado, int? carregado, int maximo) =>
        carregado is null ? enviado
        : Editado(enviado, carregado) ? Math.Min(enviado, maximo)
        : guardado;
}
