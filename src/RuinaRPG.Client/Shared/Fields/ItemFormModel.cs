using System.ComponentModel.DataAnnotations;
using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Client.Shared.Fields;

/// <summary>
/// Estado editável de um item do catálogo (e dos itens fixos dos kits de Equipagem). Convertido de e para
/// os contratos de criação/atualização; sem requisitos marcados, Requisitos e Penalidade vão como nulos.
/// </summary>
public class ItemFormModel
{
    public string Tipo { get; set; } = "ItemGeral";
    public string Nome { get; set; } = "";
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Peso { get; set; }
    [Range(0, int.MaxValue)]
    public int Preco { get; set; }
    public string? ImageId { get; set; }
    public string? Subcategoria { get; set; }
    public string? Descricao { get; set; }
    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? CapacidadeExtra { get; set; }
    public string? Rank { get; set; }
    public string? Empunhadura { get; set; }
    public string? Dados { get; set; }
    public int? Dano { get; set; }
    public string? Critico { get; set; }
    public int? Alcance { get; set; }
    public string? TipoDeDano { get; set; }
    public string? Categoria { get; set; }
    public int? Defesa { get; set; }
    public int? RF { get; set; }
    public int? RM { get; set; }
    public int? BonusDefesa { get; set; }
    public string? TipoDeAlvo { get; set; }
    public string? Alvo { get; set; }
    public int? Valor { get; set; }

    public bool PossuiRequisitos { get; set; }
    public RequisitosFormModel Requisitos { get; set; } = new();
    public PenalidadeFormModel Penalidade { get; set; } = new();

    public static ItemFormModel FromRequest(CreateItemRequest r) => new()
    {
        Tipo = r.Tipo, Nome = r.Nome, Peso = r.Peso, Preco = r.Preco, ImageId = r.ImageId,
        Subcategoria = r.Subcategoria, Descricao = r.Descricao, CapacidadeExtra = r.CapacidadeExtra,
        Rank = r.Rank, Empunhadura = r.Empunhadura, Dados = r.Dados, Dano = r.Dano, Critico = r.Critico,
        Alcance = r.Alcance, TipoDeDano = r.TipoDeDano, Categoria = r.Categoria, Defesa = r.Defesa,
        RF = r.RF, RM = r.RM, BonusDefesa = r.BonusDefesa, TipoDeAlvo = r.TipoDeAlvo, Alvo = r.Alvo, Valor = r.Valor,
        PossuiRequisitos = r.Requisitos is not null || r.PenalidadeDeRequisitos is not null,
        Requisitos = RequisitosFormModel.FromDto(r.Requisitos),
        Penalidade = PenalidadeFormModel.FromDto(r.PenalidadeDeRequisitos),
    };

    /// <summary>Copia os campos de um item vindo do servidor (a imagem é resolvida por quem chama).</summary>
    public void Apply(ItemResponse r)
    {
        Tipo = r.Tipo; Nome = r.Nome; Peso = r.Peso; Preco = r.Preco;
        Subcategoria = r.Subcategoria; Descricao = r.Descricao; CapacidadeExtra = r.CapacidadeExtra;
        Rank = r.Rank; Empunhadura = r.Empunhadura; Dados = r.Dados; Dano = r.Dano; Critico = r.Critico;
        Alcance = r.Alcance; TipoDeDano = r.TipoDeDano; Categoria = r.Categoria; Defesa = r.Defesa;
        RF = r.RF; RM = r.RM; BonusDefesa = r.BonusDefesa; TipoDeAlvo = r.TipoDeAlvo; Alvo = r.Alvo; Valor = r.Valor;
        Requisitos = RequisitosFormModel.FromDto(r.Requisitos);
        Penalidade = PenalidadeFormModel.FromDto(r.PenalidadeDeRequisitos);
        PossuiRequisitos = r.Requisitos is not null || r.PenalidadeDeRequisitos is not null;
    }

    public CreateItemRequest ToCreateRequest() => new(Tipo, Nome, Peso, Preco, ImageId,
        Subcategoria, Descricao, Rank, Empunhadura, Dados, Dano,
        Critico, Alcance, TipoDeDano,
        Categoria, Defesa, RF, RM,
        BonusDefesa, TipoDeAlvo, Alvo, Valor, CapacidadeExtra,
        Requisitos: PossuiRequisitos ? Requisitos.ToDto() : null,
        PenalidadeDeRequisitos: PossuiRequisitos ? Penalidade.ToDto() : null);

    public UpdateItemRequest ToUpdateRequest() => new(Nome, Peso, Preco, ImageId,
        Subcategoria, Descricao, Rank, Empunhadura, Dados, Dano,
        Critico, Alcance, TipoDeDano,
        Categoria, Defesa, RF, RM,
        BonusDefesa, TipoDeAlvo, Alvo, Valor, CapacidadeExtra,
        Requisitos: PossuiRequisitos ? Requisitos.ToDto() : null,
        PenalidadeDeRequisitos: PossuiRequisitos ? Penalidade.ToDto() : null);
}
