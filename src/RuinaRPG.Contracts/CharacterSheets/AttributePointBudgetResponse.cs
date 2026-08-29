namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>Atributos 2.a: "A soma de Gasto ... deve ser exibida e não pode ultrapassar" o total recebido.</summary>
public record AttributePointBudgetResponse(int GastoTotal, int PontosDisponiveis);
