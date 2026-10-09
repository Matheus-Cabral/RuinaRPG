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

    // 2.b: Eficiência e Dano Elemental vêm da Tabela de Afinidades — vale a linha de maior Afinidade que
    // não ultrapassa o valor; abaixo da menor linha (ou sem linhas) os dois valem 0.
    public static int EficienciaElemental(int valorDaAfinidadeCorrespondente, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela) =>
        LinhaDaTabela(valorDaAfinidadeCorrespondente, tabela).Eficiencia;

    public static int DanoElemental(int valorDaAfinidadeCorrespondente, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela) =>
        LinhaDaTabela(valorDaAfinidadeCorrespondente, tabela).Dano;

    private static LinhaDaTabelaDeAfinidades LinhaDaTabela(int valor, IReadOnlyList<LinhaDaTabelaDeAfinidades> tabela) =>
        tabela.Where(l => l.Afinidade <= valor).OrderByDescending(l => l.Afinidade).FirstOrDefault();

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
