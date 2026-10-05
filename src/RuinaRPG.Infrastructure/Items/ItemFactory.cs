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

    private static readonly System.Text.Json.JsonSerializerOptions DadosJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <summary>Os campos de um Item como pedido de criação, sem imagem nem Requisitos/Penalidade (guardados à parte no item fixo).</summary>
    public static CreateItemRequest ParaRequest(Item item) => item switch
    {
        ItemGeral g => Base(g, "ItemGeral") with { Subcategoria = g.Subcategoria, CapacidadeExtra = g.CapacidadeExtra },
        Arma a => Base(a, "Arma") with { Subcategoria = a.Subcategoria, Rank = a.Rank?.ToString(), Empunhadura = a.Empunhadura?.ToString(), Dados = a.Dados, Dano = a.Dano, Critico = a.Critico, Alcance = a.Alcance, TipoDeDano = a.TipoDeDano?.ToString() },
        Armadura ar => Base(ar, "Armadura") with { Subcategoria = ar.Subcategoria, Rank = ar.Rank?.ToString(), Categoria = ar.Categoria?.ToString(), Defesa = ar.Defesa, RF = ar.RF, RM = ar.RM },
        Escudo e => Base(e, "Escudo") with { Subcategoria = e.Subcategoria, Rank = e.Rank?.ToString(), Categoria = e.Categoria?.ToString(), BonusDefesa = e.BonusDefesa },
        Artefato art => Base(art, "Artefato") with { Subcategoria = art.Subcategoria, TipoDeAlvo = art.TipoDeAlvo?.ToString(), Alvo = art.Alvo, Valor = art.Valor },
        _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
    };

    public static string Serializar(CreateItemRequest request) =>
        System.Text.Json.JsonSerializer.Serialize(request with { ImageId = null, Requisitos = null, PenalidadeDeRequisitos = null }, DadosJson);

    public static CreateItemRequest Desserializar(string dados) => System.Text.Json.JsonSerializer.Deserialize<CreateItemRequest>(dados, DadosJson)!;

    private static CreateItemRequest Base(Item item, string tipo) => new(
        Tipo: tipo, Nome: item.Nome, Peso: item.Peso, Preco: item.Preco, ImageId: null, Subcategoria: null, Descricao: item.Descricao,
        Rank: null, Empunhadura: null, Dados: null, Dano: null, Critico: null, Alcance: null, TipoDeDano: null, Categoria: null,
        Defesa: null, RF: null, RM: null, BonusDefesa: null, TipoDeAlvo: null, Alvo: null, Valor: null, CapacidadeExtra: null);

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        value is not null && Enum.TryParse<TEnum>(value, out var parsed) ? parsed : null;
}
