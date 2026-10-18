using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}")]
public sealed class CharacterArsenalController(RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades, EquipmentPenaltyService penalidades, CharacterSheetStats stats, ITipoDeFicha<CharacterSheet> tipo)
    : ArsenalDaFichaController<CharacterSheet, CharacterWeapon, CharacterArmorSlot, CharacterShield, CharacterWeaponResponse, CharacterArmorSlotResponse, CharacterShieldResponse, AddCharacterWeaponRequest, UpdateCharacterArmorSlotRequest, AddCharacterShieldRequest>(
        db, durabilidades, penalidades, tipo)
{
    protected override Task<ActionResult?> PreencherArmaAsync(CharacterWeapon arma, AddCharacterWeaponRequest request) => PreencherArmaDoCatalogoAsync(arma, request.ItemId);

    protected override async Task<ActionResult?> AjustarDurabilidadeDaArmaAsync(CharacterWeapon arma, int durabilidadeAtual)
    {
        arma.DurabilidadeAtual = await DurabilidadeDeArmaLimitadaAsync(arma.ItemId, durabilidadeAtual);
        return null;
    }

    protected override Task<CharacterWeaponResponse> RespostaDeArmaAsync(CharacterSheet ficha, CharacterWeapon arma, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela) =>
        RespostaDeArmaDoCatalogoAsync(ficha, arma.ItemId, arma.Id, tabela, (item, d) =>
            new CharacterWeaponResponse(arma.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeDano?.ToString(), item.Alcance, item.Dados, item.Dano, item.Critico, item.Rank?.ToString(), item.Peso, arma.IsEquipped, DurabilidadeDeItem.LimitarAtual(arma.DurabilidadeAtual, d.Maxima), d.Maxima ?? 0, d.ImageUrl, item.Descricao, d.Inquebravel,
                d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor));

    protected override CharacterArmorSlotResponse SlotVazio(CharacterArmorSlot slot) =>
        new(slot.Slot.ToString(), null, null, null, null, null, null, null, null, null, null, null);

    protected override CharacterArmorSlotResponse RespostaDeSlot(CharacterArmorSlot slot, Armadura item, DetalhesDoItemEquipado d) =>
        new(slot.Slot.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.Defesa, item.RF, item.RM, item.Peso, slot.DurabilidadeAtual is null ? null : DurabilidadeDeItem.LimitarAtual(slot.DurabilidadeAtual.Value, d.Maxima), d.Maxima, d.ImageUrl, item.Descricao, d.Inquebravel,
            d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor);

    protected override CharacterShieldResponse RespostaDeEscudo(CharacterShield escudo, Escudo item, DetalhesDoItemEquipado d) =>
        new(escudo.Id.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.BonusDefesa, item.Peso, escudo.IsEquipped, DurabilidadeDeItem.LimitarAtual(escudo.DurabilidadeAtual, d.Maxima), d.Maxima ?? 0, d.ImageUrl, item.Descricao, d.Inquebravel,
            d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor);

    protected override string? ItemIdDoSlot(UpdateCharacterArmorSlotRequest request) => request.ItemId;

    protected override string ItemIdDoEscudo(AddCharacterShieldRequest request) => request.ItemId;

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(CharacterSheet ficha) => stats.FichaParaRequisitosAsync(ficha);
}
