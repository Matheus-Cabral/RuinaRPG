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
    /// O valor da Afinidade escolhida (o id do elemento do catálogo) — entrada de EficienciaElemental/DanoElemental
    /// acima: os pontos que a ficha tem nesse elemento; 0 sem Afinidade ou sem pontos nele.
    /// </summary>
    public static int ValorDaAfinidadeCorrespondente(int? afinidade, IReadOnlyDictionary<int, int> pontos) =>
        afinidade is { } id ? pontos.GetValueOrDefault(id) : 0;
}
