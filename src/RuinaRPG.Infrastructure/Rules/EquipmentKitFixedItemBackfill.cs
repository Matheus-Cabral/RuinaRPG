using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.Rules;

/// <summary>
/// Conversão da 1.4.3: os itens fixos dos kits deixam de ser um par Nome+Tipo e passam a apontar para a
/// base global EquipmentKitFixedItems. Para cada Nome+Tipo ainda sem vínculo, cria (uma vez) o item fixo
/// — com os dados completos do catálogo padrão quando existe um item padrão de mesmo nome e tipo, senão
/// só com o nome e marcado DetalhesIncompletos — e liga as linhas. Idempotente; roda com os outros
/// seeders (Development no startup, Production em --migrate).
/// </summary>
public static class EquipmentKitFixedItemBackfill
{
    public static async Task<int> RunAsync(RuinaRpgDbContext db)
    {
        var antes = await db.EquipmentKitFixedItems.CountAsync();

        var itens = await db.EquipmentKitItems.Where(i => i.FixedItemId == null && i.Nome != null).ToListAsync();
        foreach (var item in itens)
            item.FixedItemId = (await ObterOuCriarAsync(db, item.Nome!, item.Tipo, item.SubcategoriaHint)).Id;

        var slots = await db.EquipmentKitChoiceSlots.Where(s => s.BonusFixedItemId == null && s.BonusNome != null).ToListAsync();
        foreach (var slot in slots)
            slot.BonusFixedItemId = (await ObterOuCriarAsync(db, slot.BonusNome!, ItemTipo.ItemGeral, null)).Id;

        await db.SaveChangesAsync();
        return await db.EquipmentKitFixedItems.CountAsync() - antes;
    }

    /// <summary>O item fixo de Nome+Tipo: o que já existe na base (ou já foi criado nesta unidade de trabalho), ou um novo.</summary>
    public static async Task<EquipmentKitFixedItem> ObterOuCriarAsync(RuinaRpgDbContext db, string nome, ItemTipo tipo, string? subcategoriaHint)
    {
        var existente = db.EquipmentKitFixedItems.Local.FirstOrDefault(f => f.Nome == nome && f.Tipo == tipo)
            ?? await db.EquipmentKitFixedItems.FirstOrDefaultAsync(f => f.Nome == nome && f.Tipo == tipo);
        if (existente is not null)
            return existente;

        var padrao = DefaultCatalogItems.Build(Guid.Empty).FirstOrDefault(i => i.Nome == nome && ItemFactory.TipoDe(i) == tipo);
        var request = padrao is not null
            ? ItemFactory.ParaRequest(padrao)
            : new CreateItemRequest(
                Tipo: tipo.ToString(), Nome: nome, Peso: 0m, Preco: 0, ImageId: null,
                Subcategoria: tipo == ItemTipo.ItemGeral ? subcategoriaHint ?? "Equipamentos de Aventura" : null,
                Descricao: null, Rank: null, Empunhadura: null, Dados: null, Dano: null, Critico: null, Alcance: null,
                TipoDeDano: null, Categoria: null, Defesa: null, RF: null, RM: null, BonusDefesa: null,
                TipoDeAlvo: null, Alvo: null, Valor: null, CapacidadeExtra: null);

        var novo = new EquipmentKitFixedItem
        {
            Id = Guid.NewGuid(), Nome = nome, Tipo = tipo, Dados = ItemFactory.Serializar(request),
            Requisitos = padrao?.Requisitos, PenalidadeDeRequisitos = padrao?.PenalidadeDeRequisitos,
            DetalhesIncompletos = padrao is null,
        };
        db.EquipmentKitFixedItems.Add(novo);
        return novo;
    }
}
