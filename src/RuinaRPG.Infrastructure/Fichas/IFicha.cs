using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Runes;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.Fichas;

/// <summary>Uma ficha (Personagem, NPC ou Criatura) vista pelo código genérico do D.</summary>
public interface IFicha
{
    Guid Id { get; }
    int Nivel { get; set; }
    Guid? ImageId { get; set; }

    /// <summary>Cobertura atual (2.b), que soma na Defesa Natural.</summary>
    Cobertura Cobertura { get; set; }

    int ExperienciaAtual { get; set; }

    /// <summary>Último Nível cujo aviso de subida de nível o usuário dispensou (null: nenhum).</summary>
    int? LastDismissedLevelUpLevel { get; set; }
}

/// <summary>Uma ficha com Variante de Linhagem, que dá os traços raciais (Personagem e NPC; a Criatura não tem).</summary>
public interface IFichaComVariante : IFicha
{
    Variante? Variante { get; set; }
}

/// <summary>Uma ficha com Linhagem, Arca e aba História (Personagem e NPC; a Criatura não tem).</summary>
public interface IFichaComLinhagem : IFichaComVariante
{
    Linhagem? Linhagem { get; set; }
    int? ArcaRolada { get; set; }

    /// <summary>Aba História: HTML sanitizado (HistoriaSanitizer); null = editor vazio.</summary>
    string? Historia { get; set; }
}

/// <summary>Uma imagem da galeria da aba História (CharacterSheetHistoriaImage, NpcSheetHistoriaImage).</summary>
public interface IImagemDaHistoria
{
    Guid SheetId { get; set; }
    Guid ImageId { get; set; }
    int Ordem { get; set; }
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

/// <summary>Um Item do Catálogo no inventário da ficha (CharacterInventoryItem, NpcInventoryItem).</summary>
public interface IItemDoInventarioDaFicha : ILinhaDaFicha
{
    Guid ItemId { get; set; }
    int Qtd { get; set; }
}

/// <summary>Um Artefato da ficha (CharacterArtifact, NpcArtifact, CreatureArtifact).</summary>
public interface IArtefatoDaFicha : ILinhaDaFicha
{
    Guid ArtifactItemId { get; set; }
}

/// <summary>
/// Uma Característica da ficha tirada do catálogo comum (CharacterTrait, NpcTrait), que pode ser um traço racial.
/// A CreatureTrait fica de fora: aponta para um de dois catálogos (TraitId ou CreatureExclusiveTraitId).
/// </summary>
public interface ICaracteristicaDaFicha : ILinhaDaFicha
{
    Guid TraitId { get; set; }
    Polaridade Polaridade { get; set; }
    string? Especificacao { get; set; }
    bool IsRacial { get; set; }
    Variante? RacialVariante { get; set; }
}

/// <summary>
/// Uma Magia, Habilidade, Passiva ou Racial da ficha, cópia independente com seus Efeitos
/// (CharacterSpellAbility, NpcSpellAbility, CreatureSpellAbility).
/// </summary>
public interface IMagiaDaFicha<TEfeito> : ILinhaDaFicha where TEfeito : IEfeitoDaMagiaDaFicha
{
    Guid? SourceBankEntryId { get; set; }
    string Nome { get; set; }
    SpellAbilityTipo Tipo { get; set; }
    int Grau { get; set; }
    int GastoEmPI { get; set; }
    int Custo { get; set; }
    string Descricao { get; set; }
    List<TEfeito> Efeitos { get; set; }
    CategoriaDePassiva? Categoria { get; set; }
    RequisitosDePassiva? Requisitos { get; set; }
    bool CriadaNaFicha { get; set; }
}

/// <summary>Um Efeito de uma Magia/Habilidade da ficha (CharacterSpellAbilityEffect, NpcSpellAbilityEffect, CreatureSpellAbilityEffect).</summary>
public interface IEfeitoDaMagiaDaFicha
{
    string EfeitoNome { get; set; }
    int? Quantidade { get; set; }
    int CustoPI { get; set; }
}

/// <summary>Uma ficha que escolhe a Equipagem inicial (Personagem e NPC; a Criatura não tem).</summary>
public interface IFichaComEquipagem : IFicha
{
    Guid? EquipmentKitId { get; set; }
    int Ciclos { get; set; }
}

/// <summary>Uma Runa da ficha, cópia independente com imagem opcional (CharacterRune, NpcRune).</summary>
public interface IRunaDaFicha : ILinhaDaFicha
{
    string Nome { get; set; }
    string Descricao { get; set; }
    int Grau { get; set; }
    TipoDeRuna? Tipo { get; set; }
    DisciplinaDeRuna? Disciplina { get; set; }
    Guid? ImageId { get; set; }
    Guid? SourceBankEntryId { get; set; }
}

/// <summary>Uma linha de Afinidade da ficha (CharacterAffinity, NpcAffinity).</summary>
public interface IAfinidadeDaFicha : ILinhaDaFicha
{
    Elemento? Elemento { get; set; }
    int? ElementoValor { get; set; }
    SubElemento? SubElemento { get; set; }
    int? SubElementoValor { get; set; }
    EssenciaBasica? SegundaEssencia { get; set; }
    int? SegundaEssenciaValor { get; set; }
    int? Experiencia { get; set; }
}
