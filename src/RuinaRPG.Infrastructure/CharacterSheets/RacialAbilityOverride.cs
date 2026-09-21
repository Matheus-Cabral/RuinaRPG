using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Infrastructure.CharacterSheets;

public class RacialAbilityOverride
{
    public Guid Id { get; set; }
    public Guid GmId { get; set; }
    public Variante Variante { get; set; }
    public required string Nome { get; set; }
    public required string Descricao { get; set; }
    /// <summary>
    /// Só usado por Variante.AloraSolar: o nome que o GM dá à variante e que aparece no dropdown das
    /// fichas. Vazio/null = variante ainda não liberada (Requisitos - Habilidades Raciais R0005).
    /// </summary>
    public string? NomeDaVariante { get; set; }
}
