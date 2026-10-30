namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Marca a ação que continua valendo numa ficha no cemitério (caixa "Morto", exclusão pelo GM, dispensa do aviso de nível); <see cref="FichaNoCemiterioFilter"/> a lê.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PermitidoNoCemiterioAttribute : Attribute;
