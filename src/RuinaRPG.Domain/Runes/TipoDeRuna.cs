namespace RuinaRPG.Domain.Runes;

/// <summary>
/// Classificação opcional de uma Runa (Requisitos - Banco de Runas). Só é exibida — não entra em cálculo algum.
/// Uma Runa sem tipo guarda null na coluna; no fio o tipo trafega como o nome exato do enum.
/// </summary>
public enum TipoDeRuna
{
    Arcana,
    Negra
}
