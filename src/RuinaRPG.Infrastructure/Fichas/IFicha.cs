using RuinaRPG.Domain.CharacterSheets;

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

/// <summary>Uma linha de Atributo da ficha (CharacterAttribute, NpcAttribute, CreatureAttribute).</summary>
public interface IAtributoDaFicha<TAtributo> : ILinhaDaFicha where TAtributo : struct, Enum
{
    TAtributo Atributo { get; set; }
    int Gasto { get; set; }
    int Bonus { get; set; }
    bool TemMaestria { get; set; }
}

/// <summary>Uma Maestria da ficha (CharacterMastery, NpcMastery, CreatureMastery).</summary>
public interface IMaestriaDaFicha<TAtributo> : ILinhaDaFicha where TAtributo : struct, Enum
{
    string Nome { get; set; }
    int PericiaId { get; set; }
    TAtributo Atributo { get; set; }
    int GastoMaestria { get; set; }
}

/// <summary>Uma linha do Arsenal que se equipa (Arma ou Escudo), uma de cada vez por ficha.</summary>
public interface IEquipavelDaFicha : ILinhaDaFicha
{
    bool IsEquipped { get; set; }
}

/// <summary>
/// Um equipável sempre ligado ao Catálogo, com Durabilidade (Escudos; Armas de Personagem e NPC).
/// A Arma da Criatura fica de fora: pode ser ataque natural, sem ItemId nem Durabilidade (Ficha de Criaturas R0006 3.a).
/// </summary>
public interface IEquipavelDoCatalogo : IEquipavelDaFicha
{
    Guid ItemId { get; set; }
    int DurabilidadeAtual { get; set; }
}

/// <summary>Um slot de Armadura da ficha (CharacterArmorSlot, NpcArmorSlot, CreatureArmorSlot); vazio quando ItemId é null.</summary>
public interface ISlotDeArmaduraDaFicha : ILinhaDaFicha
{
    ArmorSlotType Slot { get; set; }
    Guid? ItemId { get; set; }
    int? DurabilidadeAtual { get; set; }
}
