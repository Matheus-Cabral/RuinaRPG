namespace RuinaRPG.Domain.Condicoes;

/// <summary>O que uma linha de uma Condição altera (ver "Requisitos - Auditoria de Regras").</summary>
public enum AlvoDeCondicao
{
    Atributo,
    Pericia,
    SubAtributo,
    RecursoTotal,
    RecursoAtual,
    AfinidadeElemental,
    DanoElemental,
    Dano,
    Maestria
}

public enum TipoDeValorDeCondicao
{
    Fixo,
    Percentual
}

/// <summary>Só para <see cref="AlvoDeCondicao.RecursoAtual"/>: uma vez ao aplicar, ou a cada vez do participante.</summary>
public enum MomentoDeCondicao
{
    AoAplicar,
    ACadaVez
}

public enum CategoriaDesabilitavel
{
    Arma,
    Armadura,
    Escudo,
    Artefato,
    Magia,
    Habilidade,
    Runa,
    Passiva
}

/// <summary>Valores válidos de AlvoEspecifico para RecursoTotal e RecursoAtual.</summary>
public enum RecursoDeCondicao
{
    Vitalidade,
    Foco,
    Adrenalina,
    Estresse
}
