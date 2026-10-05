using RuinaRPG.Contracts.Items;

namespace RuinaRPG.Contracts.Rules;

/// <summary>Item da base global de itens fixos dos kits, completo (Dados traz Requisitos e PenalidadeDeRequisitos), com os kits que o usam.</summary>
public record EquipmentKitFixedItemResponse(string Id, string Nome, string Tipo, bool DetalhesIncompletos, CreateItemRequest Dados, List<string> Kits);
