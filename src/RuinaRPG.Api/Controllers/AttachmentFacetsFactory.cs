using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Runes;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// Monta as <see cref="AttachmentFacets"/> de um anexo de campanha a partir do registro de origem —
/// um lugar só para a lista do GM (CampaignAttachmentsController) e a do jogador
/// (CampaignPlayerViewController), que filtram pelos mesmos critérios.
/// </summary>
public static class AttachmentFacetsFactory
{
    public static AttachmentFacets De(Item item) => item switch
    {
        ItemGeral g => new(ItemTipo: "ItemGeral", Subcategoria: g.Subcategoria),
        Arma a => new(ItemTipo: "Arma", Subcategoria: a.Subcategoria),
        Armadura ar => new(ItemTipo: "Armadura", Subcategoria: ar.Subcategoria),
        Escudo e => new(ItemTipo: "Escudo", Subcategoria: e.Subcategoria),
        Artefato at => new(ItemTipo: "Artefato", Subcategoria: at.Subcategoria),
        _ => new(),
    };

    // Uma Passiva não tem Grau (fica sempre 0 no banco) — mandar 0 faria "Grau 0" aparecer como opção
    // de filtro.
    public static AttachmentFacets De(SpellAbilityBankEntry entry) =>
        new(EntradaTipo: entry.Tipo.ToString(), Grau: entry.Tipo == SpellAbilityTipo.Passiva ? null : entry.Grau);

    public static AttachmentFacets De(RuneBankEntry rune) =>
        new(Grau: rune.Grau, Disciplina: rune.Disciplina?.ToString());
}
