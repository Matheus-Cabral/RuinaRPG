using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// Seeds the Efeito catalog from a hand-authored table (not a markdown parser — see the design
/// spec's Contexto section for why "[[GRAUS & CÍRCULOS]]"'s prose is too irregular to parse
/// generically). Mirrors TraitSeeder: upsert by Nome, skip any row the Auditor has already
/// customized, called once at API startup (see Program.cs).
/// </summary>
public static class EfeitoSeeder
{
    private sealed record Seed(
        string Nome, int Grau, string Descricao, TipoDeCusto Tipo,
        int? CustoFixo = null, int? CustoPorUnidade = null, string? Unidade = null,
        string? DerivadoDe = null, int? MaxUnidades = null, bool MaxEscalaPorGrau = false,
        int? MaxContandoAPartirDoGrau = null, int? CustoAlternativo = null, int? CustoAlternativoAPartirDoGrau = null,
        string[][]? PreRequisitos = null);

    private static readonly Seed[] Seeds =
    [
        // --- Efeitos básicos (Grau 1, sempre disponíveis) ---
        new("Dano", 1, "Efeito básico: causa dano em dados. Teto de dados por Grau/Círculo em EfeitoCustoCalculator.DanoAlcanceMaxPorGrau.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Dado"),
        new("Alcance", 1, "Efeito básico: alcance em pés. Teto de pés por Grau/Círculo em EfeitoCustoCalculator.DanoAlcanceMaxPorGrau.", TipoDeCusto.PorUnidade, CustoPorUnidade: 3, Unidade: "Pé"),
        new("Duração", 1, "Efeito básico: duração em dados (o tipo de dado, não a contagem, escala por Grau/Círculo — sem teto de Quantidade).", TipoDeCusto.PorUnidade, CustoPorUnidade: 4, Unidade: "Dado"),

        // --- Grau/Círculo 1 ---
        new("Aumentar Armadura", 1, "Concede Redução Física temporária.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Ponto de Redução", MaxUnidades: 5, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Aumentar Atributo", 1, "Aumenta um atributo do personagem temporariamente.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Ponto de Atributo", MaxUnidades: 2, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Contrato Mágico", 1, "Cria um vínculo arcano com uma criatura, essencial para Invocação.", TipoDeCusto.Fixo, CustoFixo: 3),
        new("Cura", 1, "Os dados de dano recuperam Vitalidade em vez de retirá-la.", TipoDeCusto.Fixo, CustoFixo: 2, PreRequisitos: [["Dano"]]),
        new("Deslocamento", 1, "Força o alvo a se deslocar pelo grid de batalha.", TipoDeCusto.Fixo, CustoFixo: 1, PreRequisitos: [["Alcance"]]),
        new("Miragem", 1, "Cria uma ilusão que engana visão, olfato ou audição do alvo.", TipoDeCusto.PorUnidade, CustoPorUnidade: 1, Unidade: "Sentido", MaxUnidades: 3, MaxEscalaPorGrau: false, PreRequisitos: [["Duração"]]),
        new("Regeneração", 1, "Cura 1d por turno e remove efeitos negativos de Grau/Círculo igual ou inferior.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 2 ---
        new("Aceleração", 2, "O alvo recebe metade de sua movimentação como movimentação adicional.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Amedrontar", 2, "O alvo não pode se mover na direção do causador do efeito.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Diminuir Atributo", 2, "Diminui um atributo do alvo temporariamente.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Ponto de Atributo", MaxUnidades: 2, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Encantamento Elemental", 2, "Encanta armas com dano mágico adicional. A partir do 4º Grau/Círculo, pode aplicar Condições em vez de dano, ao custo de 4 PI.", TipoDeCusto.Fixo, CustoFixo: 2, CustoAlternativo: 4, CustoAlternativoAPartirDoGrau: 4, PreRequisitos: [["Duração"], ["Dano"]]),
        new("Enraizar", 2, "O alvo não pode se deslocar, mas pode atacar, defender e esquivar.", TipoDeCusto.Fixo, CustoFixo: 2, PreRequisitos: [["Duração"]]),
        new("Envenenar", 2, "Corta pela metade as curas recebidas pelo alvo; causa 1d de dano por turno.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"]]),
        new("Libra (Vitalidade)", 2, "Permite ver a Vitalidade atual do alvo. Combinável com Libra (Arcana), pago separadamente.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Alcance"], ["Duração"]]),
        new("Libra (Arcana)", 2, "Permite ver a Arcana atual do alvo. Combinável com Libra (Vitalidade), pago separadamente.", TipoDeCusto.Fixo, CustoFixo: 6, PreRequisitos: [["Alcance"], ["Duração"]]),

        // --- Grau/Círculo 3 ---
        new("Área", 3, "A habilidade age em uma área, calculada em anéis a partir de um epicentro.", TipoDeCusto.PorUnidade, CustoPorUnidade: 4, Unidade: "Anel", MaxUnidades: 1, MaxEscalaPorGrau: true),
        new("Armadura Arcana", 3, "Concede Redução Mágica temporária.", TipoDeCusto.PorUnidade, CustoPorUnidade: 3, Unidade: "Ponto de Redução", MaxUnidades: 5, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Ato Múltiplo", 3, "Permite múltiplos ataques rápidos numa única ação. O Mestre define o custo por Dado.", TipoDeCusto.ManualPorUnidade, Unidade: "Dado", MaxUnidades: 1, MaxEscalaPorGrau: true, MaxContandoAPartirDoGrau: 3),
        new("Aumentar Max. Vitalidade", 3, "Aumenta temporariamente a Vitalidade máxima.", TipoDeCusto.PorUnidade, CustoPorUnidade: 2, Unidade: "Dado", MaxUnidades: 2, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Congelar", 3, "O alvo não pode se deslocar e sofre 1d de dano por turno de Duração.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"]]),
        new("Dreno de Vitalidade", 3, "Absorve parte do dano causado para recuperar Vitalidade — não pode ter mais dados de recuperação do que de dano, então o custo é sempre derivado da Quantidade de Dano já comprada na mesma Magia/Habilidade.", TipoDeCusto.DerivadoDeOutroEfeito, CustoPorUnidade: 2, Unidade: "Dado", DerivadoDe: "Dano", PreRequisitos: [["Dano"]]),
        new("Reflexão", 3, "Causa dano toda vez que recebe um ataque corpo-a-corpo.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Dano"], ["Duração"]]),
        new("Proteção", 3, "Cria uma \"vida extra\" que toma dano no lugar do protegido.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Dano"], ["Duração"]]),
        new("Provocar", 3, "O alvo é obrigado a atacar o causador do efeito pela duração dele.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Marionete Arcana", 3, "Conjura uma entidade de arcana elemental.", TipoDeCusto.Fixo, CustoFixo: 5, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 4 ---
        new("Abençoar", 4, "Aumenta em 50% a precisão dos ataques do alvo (exclusivo do Caminho da Bênção).", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Aumentar Max. Arcana", 4, "Aumenta temporariamente a Arcana máxima.", TipoDeCusto.PorUnidade, CustoPorUnidade: 3, Unidade: "Dado", MaxUnidades: 2, MaxEscalaPorGrau: true, PreRequisitos: [["Duração"]]),
        new("Confusão", 4, "Ataques falhos contra o alvo confuso podem acertar outro alvo aleatório na área.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Dreno de Arcana", 4, "Absorve parte do dano causado para recuperar Arcana — mesmo tratamento de Dreno de Vitalidade.", TipoDeCusto.DerivadoDeOutroEfeito, CustoPorUnidade: 4, Unidade: "Dado", DerivadoDe: "Dano", PreRequisitos: [["Dano"]]),
        new("Decair", 4, "Os pontos fracos do alvo ficam expostos; ataques contra ele causam dano adicional.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"], ["Dano"]]),
        new("Detrito", 4, "Condição aplicada a uma área em vez de um alvo.", TipoDeCusto.Fixo, CustoFixo: 2, PreRequisitos: [["Duração"], ["Área"], ["Atordoamento", "Congelar", "Enraizar"]]),
        new("Fadiga", 4, "O alvo não pode defender e seus golpes físicos têm o dano cortado pela metade.", TipoDeCusto.Fixo, CustoFixo: 3, PreRequisitos: [["Duração"]]),
        new("Imagem Ilusória", 4, "Cria uma imagem ilusória. O Mestre define o custo total.", TipoDeCusto.Manual, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 5 ---
        new("Feixe", 5, "Afeta todos os hexes entre o alcance final e o originador do ataque.", TipoDeCusto.Fixo, CustoFixo: 10, PreRequisitos: [["Alcance"]]),
        new("Ações por Turno", 5, "Permite realizar múltiplas ações num único turno de batalha.", TipoDeCusto.PorUnidade, CustoPorUnidade: 20, Unidade: "Ação", MaxUnidades: 2, MaxEscalaPorGrau: false, PreRequisitos: [["Duração"]]),
        new("Cegueira", 5, "O alvo só pode realizar ataques mágicos.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"]]),
        new("Defesa Verdadeira", 5, "Ao defender, o alvo aplica o número das rolagens de dados.", TipoDeCusto.Fixo, CustoFixo: 6, PreRequisitos: [["Duração"]]),
        new("Deflexão", 5, "Reage a golpes à distância redirecionando-os ao originador do ataque.", TipoDeCusto.Fixo, CustoFixo: 8, PreRequisitos: [["Dano"]]),
        new("Deflexão Mágica", 5, "Reage a ataques mágicos direcionando-os ao originador do ataque.", TipoDeCusto.Fixo, CustoFixo: 8, PreRequisitos: [["Dano"]]),
        new("Encantamento Pessoal", 5, "Como Encantamento Elemental, mas encanta uma pessoa/criatura em vez de um equipamento.", TipoDeCusto.Fixo, CustoFixo: 5, PreRequisitos: [["Duração"]]),
        new("Encantar", 5, "O alvo encantado é obrigado a se aproximar e defender o causador do efeito.", TipoDeCusto.Fixo, CustoFixo: 10, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 6 ---
        new("Amplificação Arcana", 6, "A próxima magia conjurada terá o dobro do dano e do custo.", TipoDeCusto.Fixo, CustoFixo: 4),
        new("Fúria", 6, "O alvo tem o dano dobrado, mas ataca sempre a entidade mais próxima.", TipoDeCusto.Fixo, CustoFixo: 4, PreRequisitos: [["Duração"]]),
        new("Julgamento", 6, "O alvo recebe 1d de dano por turno e perde efeitos positivos imediatamente.", TipoDeCusto.Fixo, CustoFixo: 5, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 7 ---
        new("Absorção", 7, "Absorve dados de dano.", TipoDeCusto.PorUnidade, CustoPorUnidade: 3, Unidade: "Dado", MaxUnidades: 3, MaxEscalaPorGrau: true, MaxContandoAPartirDoGrau: 7, PreRequisitos: [["Duração"]]),
        new("Crítico Aprimorado", 7, "Reduz o número necessário no d20 para um acerto crítico.", TipoDeCusto.PorUnidade, CustoPorUnidade: 4, Unidade: "5% de Taxa Crítica", MaxUnidades: 4, MaxEscalaPorGrau: false, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 8 ---
        new("Atordoamento", 8, "O alvo perde completamente sua ação — incapaz de agir, mover, defender ou esquivar.", TipoDeCusto.Fixo, CustoFixo: 10, PreRequisitos: [["Duração"]]),

        // --- Grau/Círculo 9 ---
        new("Imunidade", 9, "O alvo se torna completamente imune ao dano de um elemento específico. O Mestre define o custo total.", TipoDeCusto.Manual),
    ];

    // Non-ASCII (á, ã, ç, ...) must survive un-escaped so string-containment checks against
    // PreRequisitosJson (e.g. "Duração", "Atordoamento") work on the raw column value.
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static async Task SeedAsync(RuinaRpgDbContext db)
    {
        var existentes = await db.Efeitos.ToDictionaryAsync(e => e.Nome);

        foreach (var seed in Seeds)
        {
            var preRequisitosJson = seed.PreRequisitos is null ? null : JsonSerializer.Serialize(seed.PreRequisitos, JsonOptions);

            if (existentes.TryGetValue(seed.Nome, out var existente))
            {
                // IsDeleted is checked here (not just IsCustomized) so a deliberately deleted seeded
                // row is never resurrected on reseed, even if some future Delete caller forgets to
                // also set IsCustomized — mirrors TraitSeeder's reasoning for the same guarantee.
                if (existente.IsCustomized || existente.IsDeleted)
                    continue;

                existente.Grau = seed.Grau;
                existente.Descricao = seed.Descricao;
                existente.TipoDeCusto = seed.Tipo;
                existente.CustoFixo = seed.CustoFixo;
                existente.CustoPorUnidade = seed.CustoPorUnidade;
                existente.UnidadeLabel = seed.Unidade;
                existente.QuantidadeDerivadaDeEfeito = seed.DerivadoDe;
                existente.MaxUnidades = seed.MaxUnidades;
                existente.MaxEscalaPorGrau = seed.MaxEscalaPorGrau;
                existente.MaxContandoAPartirDoGrau = seed.MaxContandoAPartirDoGrau;
                existente.CustoAlternativo = seed.CustoAlternativo;
                existente.CustoAlternativoAPartirDoGrau = seed.CustoAlternativoAPartirDoGrau;
                existente.PreRequisitosJson = preRequisitosJson;
                existente.IsDeleted = false;
            }
            else
            {
                db.Efeitos.Add(new Efeito
                {
                    Id = Guid.NewGuid(), Nome = seed.Nome, Grau = seed.Grau, Descricao = seed.Descricao,
                    TipoDeCusto = seed.Tipo, CustoFixo = seed.CustoFixo, CustoPorUnidade = seed.CustoPorUnidade,
                    UnidadeLabel = seed.Unidade, QuantidadeDerivadaDeEfeito = seed.DerivadoDe,
                    MaxUnidades = seed.MaxUnidades, MaxEscalaPorGrau = seed.MaxEscalaPorGrau,
                    MaxContandoAPartirDoGrau = seed.MaxContandoAPartirDoGrau,
                    CustoAlternativo = seed.CustoAlternativo, CustoAlternativoAPartirDoGrau = seed.CustoAlternativoAPartirDoGrau,
                    PreRequisitosJson = preRequisitosJson,
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
