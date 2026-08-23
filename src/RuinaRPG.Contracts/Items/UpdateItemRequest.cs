using System.ComponentModel.DataAnnotations;

namespace RuinaRPG.Contracts.Items;

public record UpdateItemRequest(
    string Nome,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal Peso,
    [Range(0, int.MaxValue)] int Preco,
    string? ImageId,
    string? Subcategoria,
    string? Descricao,
    string? Tier,
    string? Empunhadura,
    string? Dados,
    int? Dano,
    string? Critico,
    int? Alcance,
    string? TipoDeDano,
    string? RequisitoAtributo,
    [Range(0, int.MaxValue)] int? DurabilidadeMaxima,
    string? Categoria,
    int? Defesa,
    int? RF,
    int? RM,
    string? Penalidade,
    int? RequisitoVigor,
    int? BonusDefesa,
    string? TipoDeAlvo,
    string? Alvo,
    int? Valor);
