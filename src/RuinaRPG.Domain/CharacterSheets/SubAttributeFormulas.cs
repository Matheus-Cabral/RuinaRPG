namespace RuinaRPG.Domain.CharacterSheets;

public static class SubAttributeFormulas
{
    public static int Iniciativa(int agilidade, int brutoProntidao, int artefatoOuItem) =>
        agilidade + brutoProntidao + artefatoOuItem;

    public static int Movimentacao(int agilidade, int artefato, int pesoTotalCarregado, int forca, int vigor)
    {
        var limiteDeCarga = (forca + vigor) / 2;
        var sobrepeso = Math.Max(0, pesoTotalCarregado - limiteDeCarga);
        var raw = agilidade * 2 + artefato - sobrepeso;
        return Math.Max(1, raw);
    }

    public static int EsquivaNatural(int agilidade, int brutoReflexos, int artefatos, int penalidadeArmadura) =>
        agilidade + brutoReflexos + artefatos - penalidadeArmadura;

    public static int DefesaNatural(int vigor, int brutoFortitude, int escudo, int artefatos, int cobertura) =>
        vigor + brutoFortitude + escudo + artefatos + cobertura;

    public static int ReducaoFisica(int artefato, int armadura) => artefato + armadura;

    public static int ReducaoMagica(int artefato, int armaduraMagica) => artefato + armaduraMagica;
}
