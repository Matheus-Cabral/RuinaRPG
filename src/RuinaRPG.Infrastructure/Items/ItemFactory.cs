using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Infrastructure.Items;

/// <summary>
/// O único lugar que monta ou atualiza um Item a partir de um pedido — usado pelo Catálogo
/// (ItemsController) e pela aplicação de um kit de Equipagem, que copia um item fixo para o catálogo do
/// GM. Não define Id, GmId nem ImageId: isso é de quem chama.
/// </summary>
public static class ItemFactory
{
    public static bool TemRank(ItemTipo tipo) => tipo is ItemTipo.Arma or ItemTipo.Armadura or ItemTipo.Escudo;
    public static bool AceitaRequisitos(ItemTipo tipo) => tipo is not ItemTipo.ItemGeral;

    public static ItemTipo TipoDe(Item item) => item switch
    {
        ItemGeral => ItemTipo.ItemGeral, Arma => ItemTipo.Arma, Armadura => ItemTipo.Armadura, Escudo => ItemTipo.Escudo, Artefato => ItemTipo.Artefato,
        _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
    };

    /// <summary>
    /// Vazio = sem Rank (sem durabilidade). Qualquer outro valor tem de ser exatamente um nome de
    /// <see cref="RankDeItem"/> (F..SS) — um Rank desconhecido é rejeitado em vez de virar NULL em
    /// silêncio, já que apagaria a durabilidade do item.
    /// </summary>
    public static bool TryParseRank(string? raw, out RankDeItem? rank)
    {
        rank = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        if (!Enum.GetNames<RankDeItem>().Contains(raw) || !Enum.TryParse<RankDeItem>(raw, out var parsed))
            return false;
        rank = parsed;
        return true;
    }

    public static Item Criar(ItemTipo tipo, CreateItemRequest request, RankDeItem? rank, RequisitosDePassiva? requisitos, PenalidadeDeEquipamento? penalidade)
    {
        Item item = tipo switch
        {
            ItemTipo.ItemGeral => new ItemGeral { Nome = request.Nome, Subcategoria = request.Subcategoria, CapacidadeExtra = request.CapacidadeExtra },
            ItemTipo.Arma => new Arma
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                Rank = rank,
                Empunhadura = ParseEnum<Empunhadura>(request.Empunhadura),
                Dados = request.Dados,
                Dano = request.Dano,
                Critico = request.Critico,
                Alcance = request.Alcance,
                TipoDeDano = ParseEnum<TipoDeDano>(request.TipoDeDano)
            },
            ItemTipo.Armadura => new Armadura
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                Rank = rank,
                Categoria = ParseEnum<CategoriaProtecao>(request.Categoria),
                Defesa = request.Defesa,
                RF = request.RF,
                RM = request.RM
            },
            ItemTipo.Escudo => new Escudo
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                Rank = rank,
                Categoria = ParseEnum<CategoriaProtecao>(request.Categoria),
                BonusDefesa = request.BonusDefesa
            },
            ItemTipo.Artefato => new Artefato
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                TipoDeAlvo = ParseEnum<TipoDeAlvo>(request.TipoDeAlvo),
                Alvo = request.Alvo,
                Valor = request.Valor
            },
            _ => throw new InvalidOperationException("Unreachable — Tipo already validated above.")
        };

        item.Peso = request.Peso;
        item.Preco = request.Preco;
        item.Descricao = request.Descricao;
        item.Requisitos = AceitaRequisitos(tipo) ? requisitos : null;
        item.PenalidadeDeRequisitos = AceitaRequisitos(tipo) ? penalidade : null;
        return item;
    }

    public static void Aplicar(Item item, UpdateItemRequest request, RankDeItem? rank, RequisitosDePassiva? requisitos, PenalidadeDeEquipamento? penalidade)
    {
        item.Nome = request.Nome;
        item.Peso = request.Peso;
        item.Preco = request.Preco;
        item.Descricao = request.Descricao;

        switch (item)
        {
            case ItemGeral g:
                g.Subcategoria = request.Subcategoria;
                g.CapacidadeExtra = request.CapacidadeExtra;
                break;
            case Arma a:
                a.Subcategoria = request.Subcategoria;
                a.Rank = rank;
                a.Empunhadura = ParseEnum<Empunhadura>(request.Empunhadura);
                a.Dados = request.Dados;
                a.Dano = request.Dano;
                a.Critico = request.Critico;
                a.Alcance = request.Alcance;
                a.TipoDeDano = ParseEnum<TipoDeDano>(request.TipoDeDano);
                break;
            case Armadura ar:
                ar.Subcategoria = request.Subcategoria;
                ar.Rank = rank;
                ar.Categoria = ParseEnum<CategoriaProtecao>(request.Categoria);
                ar.Defesa = request.Defesa;
                ar.RF = request.RF;
                ar.RM = request.RM;
                break;
            case Escudo e:
                e.Subcategoria = request.Subcategoria;
                e.Rank = rank;
                e.Categoria = ParseEnum<CategoriaProtecao>(request.Categoria);
                e.BonusDefesa = request.BonusDefesa;
                break;
            case Artefato art:
                art.Subcategoria = request.Subcategoria;
                art.TipoDeAlvo = ParseEnum<TipoDeAlvo>(request.TipoDeAlvo);
                art.Alvo = request.Alvo;
                art.Valor = request.Valor;
                break;
        }


        var aceita = AceitaRequisitos(TipoDe(item));
        item.Requisitos = aceita ? requisitos : null;
        item.PenalidadeDeRequisitos = aceita ? penalidade : null;
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        value is not null && Enum.TryParse<TEnum>(value, out var parsed) ? parsed : null;
}
