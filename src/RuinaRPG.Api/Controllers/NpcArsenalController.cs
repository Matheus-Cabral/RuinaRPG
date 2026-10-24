using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}")]
public sealed class NpcArsenalController(RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades, EquipmentPenaltyService penalidades, NpcSheetStats stats, ITipoDeFicha<NpcSheet> tipo)
    : ArsenalDaFichaController<NpcSheet, NpcWeapon, NpcArmorSlot, NpcShield, WeaponResponse, ArmorSlotResponse, AddWeaponRequest>(
        db, durabilidades, penalidades, tipo)
{
    protected override Task<ActionResult?> PreencherArmaAsync(NpcWeapon arma, AddWeaponRequest request) => PreencherArmaDoCatalogoAsync(request.ItemId, LigarAoCatalogo(arma));

    protected override async Task<ActionResult?> AjustarDurabilidadeDaArmaAsync(NpcWeapon arma, int durabilidadeAtual)
    {
        arma.DurabilidadeAtual = await DurabilidadeDeArmaLimitadaAsync(arma.ItemId, durabilidadeAtual);
        return null;
    }

    protected override Task<WeaponResponse> RespostaDeArmaAsync(NpcSheet ficha, NpcWeapon arma, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela) =>
        RespostaDeArmaDoCatalogoAsync(ficha, arma.ItemId, arma.Id, tabela, (item, d) =>
            new WeaponResponse(arma.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeDano?.ToString(), item.Alcance, item.Dados, item.Dano, item.Critico, item.Rank?.ToString(), item.Peso, arma.IsEquipped, DurabilidadeDeItem.LimitarAtual(arma.DurabilidadeAtual, d.Maxima), d.Maxima ?? 0, d.ImageUrl, item.Descricao, d.Inquebravel,
                d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor));

    protected override ArmorSlotResponse SlotVazio(NpcArmorSlot slot) =>
        new(slot.Slot.ToString(), null, null, null, null, null, null, null, null, null, null, null);

    protected override ArmorSlotResponse RespostaDeSlot(NpcArmorSlot slot, Armadura item, DetalhesDoItemEquipado d) =>
        new(slot.Slot.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.Defesa, item.RF, item.RM, item.Peso, slot.DurabilidadeAtual is null ? null : DurabilidadeDeItem.LimitarAtual(slot.DurabilidadeAtual.Value, d.Maxima), d.Maxima, d.ImageUrl, item.Descricao, d.Inquebravel,
            d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor);

    protected override ShieldResponse RespostaDeEscudo(NpcShield escudo, Escudo item, DetalhesDoItemEquipado d) =>
        new(escudo.Id.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.BonusDefesa, item.Peso, escudo.IsEquipped, DurabilidadeDeItem.LimitarAtual(escudo.DurabilidadeAtual, d.Maxima), d.Maxima ?? 0, d.ImageUrl, item.Descricao, d.Inquebravel,
            d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor);

    protected override string? ItemIdDoSlot(UpdateArmorSlotRequest request) => request.ItemId;

    protected override string ItemIdDoEscudo(AddShieldRequest request) => request.ItemId;

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(NpcSheet ficha) => stats.FichaParaRequisitosAsync(ficha);
}
