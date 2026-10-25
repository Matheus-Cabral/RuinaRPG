namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Marca a ação que continua valendo numa ficha no cemitério (caixa "Morto"); o filtro de escrita a lê.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PermitidoNoCemiterioAttribute : Attribute;
