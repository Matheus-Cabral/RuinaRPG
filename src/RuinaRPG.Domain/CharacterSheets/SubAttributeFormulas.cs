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

    // 1:1 por ora — cada uma é sua própria função porque a proporção pode divergir no futuro
    // (ver docs/superpowers/specs/2026-09-15-automatizar-afinidades-design.md).
    public static int EficienciaElemental(int valorDaAfinidadeCorrespondente) => valorDaAfinidadeCorrespondente;

    public static int DanoElemental(int valorDaAfinidadeCorrespondente) => valorDaAfinidadeCorrespondente;

    /// <summary>
    /// Acha, entre as linhas de Afinidade (2.c), o Valor da que bate com a Afinidade escolhida em
    /// 1.a — entrada de EficienciaElemental/DanoElemental acima. AfinidadeElemental compartilha os
    /// mesmos nomes de membro que Elemento/SubElemento, então resolve pra qual dos dois enums o
    /// valor pertence antes de procurar a linha.
    /// </summary>
    public static int ValorDaAfinidadeCorrespondente(AfinidadeElemental? afinidade, IReadOnlyList<LinhaDeAfinidade> linhas)
    {
        if (afinidade is not { } valor)
            return 0;

        if (Enum.TryParse<Elemento>(valor.ToString(), out var elemento))
            return linhas.FirstOrDefault(l => l.Elemento == elemento).ElementoValor ?? 0;

        var subElemento = Enum.Parse<SubElemento>(valor.ToString());
        return linhas.FirstOrDefault(l => l.SubElemento == subElemento).SubElementoValor ?? 0;
    }
}
