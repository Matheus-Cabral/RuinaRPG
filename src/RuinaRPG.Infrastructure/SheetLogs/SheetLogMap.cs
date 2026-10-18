using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.NpcSheets;

namespace RuinaRPG.Infrastructure.SheetLogs;

public enum SheetLogKind { Character, Npc, Creature }

/// <summary>How one entity type takes part in the sheet log.</summary>
public sealed record SheetLogEntityMap(
    SheetLogKind Kind,
    bool IsSheet,                       // the sheet row itself
    string Secao,                       // screen section, e.g. "Atributos"
    string? SheetIdProperty,            // FK to the sheet; null when IsSheet or when ParentType is set
    Type? ParentType,                   // for a child of a child (spell effect → spell ability)
    string? ParentIdProperty,
    IReadOnlyDictionary<string, string> Campos,   // property → Portuguese label
    IReadOnlySet<string> Ignorados,               // properties never logged
    IReadOnlySet<string> TextosLongos,            // logged as "alterado", no values
    IReadOnlyDictionary<string, SheetLogCategoria> Categorias, // property → categoria (default Geral)
    string? ItemNomeProperty,           // property whose value names the row (Nome, Atributo, Slot…)
    string? ItemRefProperty);           // or a FK whose target names the row: "ItemId", "PericiaId", "TraitId", "ArtifactItemId", "CreatureExclusiveTraitId"

/// <summary>
/// Mapa estático de cada tabela de ficha para o log de ficha: a qual ficha pertence, em que seção da tela aparece,
/// o rótulo (o mesmo da tela) de cada coluna e o que fica de fora. Os testes de completude falham quando uma tabela
/// ou coluna de ficha nova não é classificada aqui.
/// </summary>
public static class SheetLogMap
{
    public static IReadOnlyDictionary<Type, SheetLogEntityMap> Entidades { get; } = Montar();

    /// <summary>Entity types that look like sheet tables but are deliberately not logged.</summary>
    public static IReadOnlySet<Type> ForaDoLog { get; } = new HashSet<Type>
    {
        typeof(CharacterSheetHistoriaImage), // imagens do texto livre da História: o texto já é "alterado"
        typeof(NpcSheetHistoriaImage),
    };

    /// <summary>Rótulo dos textos longos (TextosLongos), que entram no log sem valores; vale para qualquer tabela.</summary>
    public static IReadOnlyDictionary<string, string> RotulosDeTextoLongo { get; } = new Dictionary<string, string>
    {
        ["Historia"] = "História", ["Descricao"] = "Descrição", ["Requisitos"] = "Requisitos",
    };

    private static Dictionary<Type, SheetLogEntityMap> Montar()
    {
        var mapa = new Dictionary<Type, SheetLogEntityMap>();

        // ---- Rótulos compartilhados entre Personagem e NPC (mesmos campos nas duas telas) ----
        var identidade = new Dictionary<string, string>
        {
            ["ImageId"] = "Imagem", ["Nome"] = "Nome", ["Linhagem"] = "Linhagem", ["Variante"] = "Variante",
            ["Vocacao"] = "Vocação", ["SubVocacao"] = "Classe", ["Afinidade"] = "Afinidade", ["Propriedade"] = "Propriedade",
            ["Estrela"] = "Estrela", ["HistoricoId"] = "Histórico", ["EquipmentKitId"] = "Equipagem",
            ["Nivel"] = "Nível", ["PossuiCoracaoDeMana"] = "Possui Coração de Mana?", ["AfinidadeAdicional"] = "Afinidade Adicional",
            ["ExperienciaAtual"] = "Experiência Atual", ["PontosDeIgnicaoAtual"] = "Pontos de Ignição Atual",
            ["NucleosRankF"] = "Âmbares Absorvidos — Rank F", ["NucleosRankE"] = "Âmbares Absorvidos — Rank E",
            ["NucleosRankD"] = "Âmbares Absorvidos — Rank D", ["NucleosRankC"] = "Âmbares Absorvidos — Rank C",
            ["NucleosRankB"] = "Âmbares Absorvidos — Rank B", ["NucleosRankA"] = "Âmbares Absorvidos — Rank A",
            ["NucleosRankS"] = "Âmbares Absorvidos — Rank S",
            ["VitalidadeAtual"] = "Vitalidade", ["FocoAtual"] = "Foco (PF)", ["AdrenalinaAtual"] = "Adrenalina (PA)",
            ["EstresseAtual"] = "Estresse", ["SinaAtual"] = "Pontos de Sina", ["Cobertura"] = "Cobertura", ["Ciclos"] = "Ciclos",
            ["ArcaRolada"] = "Número rolado da Arca",
        };
        // Circulo/Grau/EAPAtual/PontosDeIgnicaoTotal ainda existem na tabela, mas hoje são calculados; nenhuma edição de ficha os grava (só a cópia da concessão de NPC os repassa, numa ficha nova).
        var vestigiais = new[] { "EAPAtual", "Circulo", "Grau", "PontosDeIgnicaoTotal" };
        var recursos = new Dictionary<string, SheetLogCategoria>
        {
            ["VitalidadeAtual"] = SheetLogCategoria.RecursoVFA, ["FocoAtual"] = SheetLogCategoria.RecursoVFA,
            ["AdrenalinaAtual"] = SheetLogCategoria.RecursoVFA, ["EstresseAtual"] = SheetLogCategoria.Estresse,
        };

        var personagem = new Dictionary<string, string>(identidade)
        {
            ["PontosDeIgnicaoBonusManual"] = "Pontos de Ignição (bônus manual)",
            ["PontosDePericiaBonusCritico"] = "Pontos de Acerto Crítico",
        };
        mapa[typeof(CharacterSheet)] = new(SheetLogKind.Character, true, "Informações Básicas", null, null, null,
            personagem, Set(["Id", "CampaignId", "OwnerId", "LastDismissedLevelUpLevel", .. vestigiais]), Set(["Historia"]),
            recursos, null, null);
        mapa[typeof(NpcSheet)] = new(SheetLogKind.Npc, true, "Informações Básicas", null, null, null,
            identidade, Set(["Id", "GmId", "OwnerId", "LastDismissedLevelUpLevel", .. vestigiais]), Set(["Historia"]),
            recursos, null, null);
        mapa[typeof(CreatureSheet)] = new(SheetLogKind.Creature, true, "Informações Básicas", null, null, null,
            new Dictionary<string, string>
            {
                ["ImageId"] = "Imagem", ["Nome"] = "Nome", ["Raca"] = "Raça", ["Arquetipo"] = "Arquétipo",
                ["SubArquetipo"] = "Sub Arquétipo", ["Afinidade"] = "Afinidade", ["Rank"] = "Rank", ["Nivel"] = "Nível",
                ["ExperienciaAtual"] = "Experiência Atual", ["PontosDeIgnicao"] = "Pontos de Ignição",
                ["VitalidadeAtual"] = "Vitalidade", ["FocoAtual"] = "Arcana (PF)", ["AdrenalinaAtual"] = "Adrenalina (PA)",
                ["Cobertura"] = "Cobertura",
            },
            Set(["Id", "GmId", "OwnerId", "LastDismissedLevelUpLevel"]), Set([]),
            new Dictionary<string, SheetLogCategoria>
            {
                ["VitalidadeAtual"] = SheetLogCategoria.RecursoVFA, ["FocoAtual"] = SheetLogCategoria.RecursoVFA,
                ["AdrenalinaAtual"] = SheetLogCategoria.RecursoVFA,
            }, null, null);

        // ---- Filhas: Personagem, NPC e Criatura (as telas usam os mesmos rótulos) ----
        Filhas(mapa, "Atributos", Campos(("Atributo", "Atributo"), ("Gasto", "Gasto"), ("Bonus", "Bônus"), ("TemMaestria", "Proeficiência")),
            [typeof(CharacterAttribute), typeof(NpcAttribute), typeof(CreatureAttribute)], nome: "Atributo");
        Filhas(mapa, "Perícias", Campos(("PericiaId", "Perícia"), ("Gasto", "Gasto"), ("AtributoEscolhido", "Atributo escolhido")),
            [typeof(CharacterSkill), typeof(NpcSkill), typeof(CreatureSkill)], itemRef: "PericiaId");
        Filhas(mapa, "Maestrias", Campos(("Nome", "Nome"), ("PericiaId", "Perícia"), ("Atributo", "Atributo"), ("GastoMaestria", "Gasto Maestria")),
            [typeof(CharacterMastery), typeof(NpcMastery), typeof(CreatureMastery)], nome: "Nome");
        Filhas(mapa, "Escudos", Campos(("ItemId", "Escudo"), ("IsEquipped", "Equipado"), ("DurabilidadeAtual", "Durabilidade")),
            [typeof(CharacterShield), typeof(NpcShield), typeof(CreatureShield)], itemRef: "ItemId");
        Filhas(mapa, "Armaduras", Campos(("Slot", "Slot de Armadura"), ("ItemId", "Armadura"), ("DurabilidadeAtual", "Durabilidade")),
            [typeof(CharacterArmorSlot), typeof(NpcArmorSlot), typeof(CreatureArmorSlot)], nome: "Slot", itemRef: "ItemId");
        Filhas(mapa, "Artefatos", Campos(("ArtifactItemId", "Artefato")),
            [typeof(CharacterArtifact), typeof(NpcArtifact), typeof(CreatureArtifact)], itemRef: "ArtifactItemId");
        Filhas(mapa, "Afeições", Campos(("Nome", "Nome"), ("Favorabilidade", "Favorabilidade")),
            [typeof(CharacterAffection), typeof(NpcAffection), typeof(CreatureAffection)], nome: "Nome");

        Filhas(mapa, "Armas", Campos(("ItemId", "Arma"), ("IsEquipped", "Equipada"), ("DurabilidadeAtual", "Durabilidade")),
            [typeof(CharacterWeapon), typeof(NpcWeapon)], itemRef: "ItemId");
        // A Criatura também aceita arma manual (sem item do Catálogo): ItemId fica nulo e o nome vem de ManualNome.
        Filhas(mapa, "Armas", Campos(("ItemId", "Arma"), ("IsEquipped", "Equipada"), ("DurabilidadeAtual", "Durabilidade"),
                ("ManualNome", "Nome"), ("ManualTipoDeDano", "Tipo de Dano"), ("ManualDados", "Dados"), ("ManualDano", "Dano")),
            [typeof(CreatureWeapon)], nome: "ManualNome", itemRef: "ItemId");

        // Afinidades, Runas e Inventário só existem em Personagem e NPC (a Criatura tem Espólios no lugar do Inventário).
        Filhas(mapa, "Afinidades", Campos(("Elemento", "Essência Básica 1"), ("ElementoValor", "Essência Básica 1 — valor"),
                ("SegundaEssencia", "Essência Básica 2"), ("SegundaEssenciaValor", "Essência Básica 2 — valor"),
                ("SubElemento", "Sub-Elemento"), ("SubElementoValor", "Sub-Elemento — valor"), ("Experiencia", "Experiência")),
            [typeof(CharacterAffinity), typeof(NpcAffinity)], nome: "Elemento");
        Filhas(mapa, "Runas", Campos(("Nome", "Nome"), ("Grau", "Grau"), ("Tipo", "Tipo"), ("Disciplina", "Disciplina"), ("ImageId", "Imagem")),
            [typeof(CharacterRune), typeof(NpcRune)], nome: "Nome", longos: ["Descricao"], ignorados: ["SourceBankEntryId"]);
        Filhas(mapa, "Inventário", Campos(("ItemId", "Item"), ("Qtd", "Quantidade")),
            [typeof(CharacterInventoryItem), typeof(NpcInventoryItem)], itemRef: "ItemId");
        Filhas(mapa, "Espólios", Campos(("ItemId", "Item"), ("Qtd", "Quantidade"), ("DT", "DT")),
            [typeof(CreatureSpoil)], itemRef: "ItemId");

        // Características: a Criatura também aceita uma Característica exclusiva e não tem origem racial.
        Filhas(mapa, "Características", Campos(("TraitId", "Característica"), ("Polaridade", "Polaridade"), ("Especificacao", "Especificação")),
            [typeof(CharacterTrait), typeof(NpcTrait)], itemRef: "TraitId", ignorados: ["IsRacial", "RacialVariante"]);
        Filhas(mapa, "Características", Campos(("TraitId", "Característica"), ("CreatureExclusiveTraitId", "Característica exclusiva"),
                ("Polaridade", "Polaridade"), ("Especificacao", "Especificação")),
            [typeof(CreatureTrait)], itemRef: "TraitId");

        // Magias e Habilidades; os Efeitos são filhos da Magia, não da ficha.
        Filhas(mapa, "Magias e Habilidades", Campos(("Nome", "Nome"), ("Tipo", "Tipo"), ("Grau", "Grau"), ("GastoEmPI", "Gasto em PI"),
                ("Custo", "Custo"), ("Categoria", "Categoria")),
            [typeof(CharacterSpellAbility), typeof(NpcSpellAbility), typeof(CreatureSpellAbility)],
            nome: "Nome", longos: ["Descricao", "Requisitos"], ignorados: ["SourceBankEntryId", "CriadaNaFicha"]);
        mapa[typeof(CharacterSpellAbilityEffect)] = Efeito(SheetLogKind.Character, typeof(CharacterSpellAbility), "CharacterSpellAbilityId");
        mapa[typeof(NpcSpellAbilityEffect)] = Efeito(SheetLogKind.Npc, typeof(NpcSpellAbility), "NpcSpellAbilityId");
        mapa[typeof(CreatureSpellAbilityEffect)] = Efeito(SheetLogKind.Creature, typeof(CreatureSpellAbility), "CreatureSpellAbilityId");

        return mapa;
    }

    private static SheetLogEntityMap Efeito(SheetLogKind kind, Type pai, string paiId) =>
        new(kind, false, "Magias e Habilidades", null, pai, paiId,
            Campos(("EfeitoNome", "Efeito"), ("Quantidade", "Quantidade"), ("CustoPI", "Custo em PI")),
            Set(["Id", paiId]), Set([]), new Dictionary<string, SheetLogCategoria>(), "EfeitoNome", null);

    /// <summary>Registra tabelas filhas de ficha; o tipo de ficha e a FK saem do prefixo do nome (Character/Npc/Creature).</summary>
    private static void Filhas(Dictionary<Type, SheetLogEntityMap> mapa, string secao, Dictionary<string, string> campos, Type[] tipos,
        string? nome = null, string? itemRef = null, string[]? longos = null, string[]? ignorados = null)
    {
        foreach (var tipo in tipos)
        {
            var kind = tipo.Name switch
            {
                var n when n.StartsWith("Character") => SheetLogKind.Character,
                var n when n.StartsWith("Npc") => SheetLogKind.Npc,
                _ => SheetLogKind.Creature,
            };
            const string fk = "SheetId"; // a FK é a mesma nos três tipos de ficha
            var fora = new List<string> { "Id", fk };
            if (ignorados is not null) fora.AddRange(ignorados);
            mapa[tipo] = new(kind, false, secao, fk, null, null, campos, Set(fora), Set(longos ?? []),
                new Dictionary<string, SheetLogCategoria>(), nome, itemRef);
        }
    }

    private static Dictionary<string, string> Campos(params (string Propriedade, string Rotulo)[] itens) =>
        itens.ToDictionary(i => i.Propriedade, i => i.Rotulo);

    private static HashSet<string> Set(IEnumerable<string> itens) => new(itens);
}
