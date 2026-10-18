namespace RuinaRPG.Infrastructure.Fichas;

/// <summary>Uma ficha (Personagem, NPC ou Criatura) vista pelo código genérico do D.</summary>
public interface IFicha
{
    Guid Id { get; }
    int Nivel { get; set; }
    Guid? ImageId { get; set; }
}

/// <summary>Uma linha de uma tabela por tipo de ficha (CharacterSkill, NpcSkill, …).</summary>
public interface ILinhaDaFicha
{
    Guid Id { get; set; }
    Guid SheetId { get; set; }
}

public interface IPericiaDaFicha<TAtributo> : ILinhaDaFicha where TAtributo : struct, Enum
{
    int PericiaId { get; set; }
    int Gasto { get; set; }
    TAtributo? AtributoEscolhido { get; set; }
}
