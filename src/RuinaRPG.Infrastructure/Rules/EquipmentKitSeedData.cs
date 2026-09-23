using RuinaRPG.Domain.Items;

namespace RuinaRPG.Infrastructure.Rules;

public record EquipmentKitSeed(string Nome, string Descricao, int Ciclos, List<EquipmentKitItemSeed> Items, List<EquipmentKitChoiceSlotSeed> ChoiceSlots);
public record EquipmentKitItemSeed(string Nome, ItemTipo Tipo, int Qtd, string? SubcategoriaHint = null);
public record EquipmentKitChoiceSlotSeed(string Label, ItemTipo Tipo, List<string>? Subcategorias, Tier? Tier, int Qtd,
    string? BonusSubcategoria = null, string? BonusNome = null, int? BonusQtd = null);

/// <summary>
/// Hand-authored from Docs/Sistema RPG/Equipagem.md — not parsed live from that file, unlike
/// Historico.md, because Equipagem.md's prose format (bare item lines, "de sua escolha"/"ou"
/// choices, a conditional-bonus footnote) can't be parsed reliably without inventing a bespoke
/// mini-language. Mirrors the existing DefaultCatalogItems.cs precedent. Item Nomes here are the
/// exact catalog Nomes (not always Equipagem.md's prose wording — see the aliases noted per kit
/// below), resolved per-GM at apply time by EquipmentKitGrantService.
/// </summary>
public static class EquipmentKitSeedData
{
    public static readonly IReadOnlyList<EquipmentKitSeed> All =
    [
        new("Viajante",
            "Preparado para longas jornadas, o Viajante aprendeu a carregar consigo aquilo que precisa para permanecer dias longe de casa.",
            25,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Saco de Dormir", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Tocha", ItemTipo.ItemGeral, 1),
                new("Ração de Viagem", ItemTipo.ItemGeral, 2),
            ], []),

        new("Explorador",
            "Equipado para atravessar lugares abandonados e superar obstáculos encontrados pelo caminho.",
            15,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Tocha", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Pé de Cabra", ItemTipo.ItemGeral, 1),
                new("Gazúa", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
            ], []),

        new("Patrulheiro",
            "Preparado para enfrentar perigos de perto, carregando uma arma adequada ao seu estilo de combate.",
            10,
            [
                new("Tampa de Madeira", ItemTipo.Escudo, 1), // alias: Equipagem.md's "Escudo de Madeira"
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
            ],
            [
                new("Arma", ItemTipo.Arma, null, RuinaRPG.Domain.Items.Tier.F, 1),
            ]),

        new("Caçador",
            "Preparado para perseguir criaturas e enfrentar ameaças à distância.",
            10,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Ração de Viagem", ItemTipo.ItemGeral, 2),
            ],
            [
                new("Arma à distância", ItemTipo.Arma, ["Arcos", "Fundas e Baladeiras"], RuinaRPG.Domain.Items.Tier.F, 1,
                    BonusSubcategoria: "Arcos", BonusNome: "Flecha de Madeira", BonusQtd: 10),
            ]),

        new("Arcano",
            "Um conjunto de materiais básicos para aqueles que estudam e canalizam forças arcanas.",
            0,
            [
                new("Manuscrito Arcano Vol.1", ItemTipo.ItemGeral, 1), // alias: "Manuscrito Arcano (Volume 1)"
                new("Diário", ItemTipo.ItemGeral, 1),
                new("Tinta", ItemTipo.ItemGeral, 1),
                new("Pena", ItemTipo.ItemGeral, 1),
                new("Tônico de Foco simples", ItemTipo.ItemGeral, 1),
            ],
            [
                new("Condutor", ItemTipo.Arma, ["Varinhas Mágicas", "Cajados Mágicos"], RuinaRPG.Domain.Items.Tier.F, 1),
            ]),

        new("Ocultista",
            "Materiais reunidos por aqueles que decidiram estudar conhecimentos que muitos preferem deixar intocados.",
            0,
            [
                new("Cera-viz (Material Ritualístico)", ItemTipo.ItemGeral, 1), // alias: "Cera-viz"
                new("Manuscrito Arcano Vol.1", ItemTipo.ItemGeral, 1),
                new("Diário", ItemTipo.ItemGeral, 1),
                new("Tônico de Foco simples", ItemTipo.ItemGeral, 1),
            ],
            [
                new("Condutor", ItemTipo.Arma, ["Varinhas Mágicas", "Cajados Mágicos"], RuinaRPG.Domain.Items.Tier.F, 1),
            ]),

        new("Devoto",
            "Pertences de alguém que mantém sua fé consigo mesmo quando está distante de templos e lugares sagrados.",
            15,
            [
                new("Símbolo Sagrado", ItemTipo.ItemGeral, 1),
                new("Diário", ItemTipo.ItemGeral, 1),
                new("Papel", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
            ], []),

        new("Artesão",
            "Ferramentas e materiais para aqueles acostumados a construir, reparar e trabalhar com as próprias mãos.",
            15,
            [
                new("Pé de Cabra", ItemTipo.ItemGeral, 1),
                new("Gazúa", ItemTipo.ItemGeral, 1),
                new("Tinta", ItemTipo.ItemGeral, 1),
                new("Pena", ItemTipo.ItemGeral, 1),
                new("Papel", ItemTipo.ItemGeral, 1),
                new("Mochila", ItemTipo.ItemGeral, 1),
            ], []),

        new("Sobrevivente",
            "Recursos básicos de quem aprendeu a se virar mesmo quando não há ninguém por perto para ajudar.",
            0,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Saco de Dormir", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Ração de Viagem", ItemTipo.ItemGeral, 3),
                new("Vara de Madeira", ItemTipo.ItemGeral, 1),
                new("Isca de Pesca", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
            ], []),

        new("Investigador",
            "Materiais de alguém acostumado a observar, registrar e buscar respostas para aquilo que não compreende.",
            15,
            [
                new("Luneta", ItemTipo.ItemGeral, 1),
                new("Diário", ItemTipo.ItemGeral, 1),
                new("Tinta", ItemTipo.ItemGeral, 1),
                new("Pena", ItemTipo.ItemGeral, 1),
                new("Papel", ItemTipo.ItemGeral, 1),
                new("Espelho", ItemTipo.ItemGeral, 1),
            ], []),

        new("Negociante",
            "Uma reserva financeira acompanhada de materiais simples para registrar acordos, valores e informações importantes.",
            80,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Pena", ItemTipo.ItemGeral, 1),
                new("Papel", ItemTipo.ItemGeral, 1),
            ], []),

        new("Aprendiz",
            "Um conjunto simples e versátil para quem ainda está começando a construir seu próprio caminho.",
            10,
            [
                new("Mochila", ItemTipo.ItemGeral, 1),
                new("Tocha", ItemTipo.ItemGeral, 1),
                new("Corda", ItemTipo.ItemGeral, 1),
                new("Tônico de Vida simples", ItemTipo.ItemGeral, 1),
                new("Tônico de Foco simples", ItemTipo.ItemGeral, 1),
            ], []),
    ];
}
