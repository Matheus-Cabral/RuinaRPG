namespace RuinaRPG.Domain.CharacterSheets;

public static class SubAttributeFormulas
{
    public static int Iniciativa(int agilidade, int brutoProntidao, int artefatoOuItem) =>
        agilidade + brutoProntidao + artefatoOuItem;

    public static int Movimentacao(int agilidade, int artefato, decimal pesoAtual, decimal pesoMaximo)
    {
        var sobrepeso = (int)Math.Ceiling(Math.Max(0m, pesoAtual - pesoMaximo));
        var raw = agilidade * 2 + artefato - sobrepeso;
        return Math.Max(1, raw);
    }

    public static int EsquivaNatural(int agilidade, int brutoReflexos, int artefatos, int penalidadeArmadura) =>
        agilidade + brutoReflexos + artefatos - penalidadeArmadura;

    public static int DefesaNatural(int vigor, int brutoFortitude, int escudo, int artefatos, int cobertura) =>
        vigor + brutoFortitude + escudo + artefatos + cobertura;

    public static int ReducaoFisica(int artefato, int armadura) => artefato + armadura;

    public static int ReducaoMagica(int artefato, int armaduraMagica) => artefato + armaduraMagica;

    public const int PontosPorEficienciaElemental = 2;
    public const int PontosPorDanoElemental = 3;

    // 2.b: cada 2 pontos no elemento da Afinidade valem 1 de Eficiência Elemental, e cada 3 valem 1
    // de Dano Elemental — o que sobra não conta (a divisão inteira arredonda para baixo).
    public static int EficienciaElemental(int valorDaAfinidadeCorrespondente) => valorDaAfinidadeCorrespondente / PontosPorEficienciaElemental;

    public static int DanoElemental(int valorDaAfinidadeCorrespondente) => valorDaAfinidadeCorrespondente / PontosPorDanoElemental;

    /// <summary>
    /// Acha, entre as linhas de Afinidade (2.c), o Valor que bate com a Afinidade escolhida em
    /// 1.a — entrada de EficienciaElemental/DanoElemental acima. AfinidadeElemental compartilha os
    /// mesmos nomes de membro que Elemento/SubElemento, então resolve pra qual dos dois enums o
    /// valor pertence antes de procurar. Se for um Elemento, vale o maior Valor entre as linhas
    /// cuja Essência 1 é esse Elemento (mais de uma linha pode tê-lo); se for um Sub-Elemento,
    /// vale o Valor da única linha com ele.
    /// </summary>
    public static int ValorDaAfinidadeCorrespondente(AfinidadeElemental? afinidade, IReadOnlyList<LinhaDeAfinidade> linhas)
    {
        if (afinidade is not { } valor)
            return 0;

        if (Enum.TryParse<Elemento>(valor.ToString(), out var elemento))
            return linhas.Where(l => l.Elemento == elemento).Select(l => l.ElementoValor ?? 0).DefaultIfEmpty(0).Max();

        var subElemento = Enum.Parse<SubElemento>(valor.ToString());
        return linhas.FirstOrDefault(l => l.SubElemento == subElemento).SubElementoValor ?? 0;
    }
}
