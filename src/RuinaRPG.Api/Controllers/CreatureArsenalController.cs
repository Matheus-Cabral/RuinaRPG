using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}")]
public sealed class CreatureArsenalController(RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades, EquipmentPenaltyService penalidades, CreatureSheetStats stats, ITipoDeFicha<CreatureSheet> tipo)
    : ArsenalDaFichaController<CreatureSheet, CreatureWeapon, CreatureArmorSlot, CreatureShield, CreatureWeaponResponse, CreatureArmorSlotResponse, CreatureShieldResponse, AddCreatureWeaponRequest, UpdateCreatureArmorSlotRequest, AddCreatureShieldRequest>(
        db, durabilidades, penalidades, tipo)
{
    /// <summary>
    /// R0006 3.a: a Arma da Criatura é OU uma Arma ligada ao Catálogo (ItemId) OU um ataque natural descrito
    /// na própria linha (os 4 campos Manual*) — exatamente um, nunca os dois nem nenhum. Slots de Armadura e
    /// Escudos ficam iguais aos da Ficha de NPCs — a R0006 diz isso explicitamente.
    /// </summary>
    protected override async Task<ActionResult?> PreencherArmaAsync(CreatureWeapon arma, AddCreatureWeaponRequest request)
    {
        var isFromCatalogo = request.ItemId is not null;
        var isManual = request.ManualNome is not null && request.ManualTipoDeDano is not null && request.ManualDados is not null && request.ManualDano is not null;
        if (isFromCatalogo == isManual)
            return BadRequest("Informe exatamente um: ItemId (vincular ao Catálogo) ou os 4 campos manuais (ataque natural).");

        if (isFromCatalogo)
        {
            if (!Guid.TryParse(request.ItemId, out var itemId))
                return BadRequest("ItemId inválido.");

            var item = await Db.Set<Arma>().FirstOrDefaultAsync(a => a.Id == itemId);
            if (item is null)
                return BadRequest("Item de arma não encontrado.");

            arma.ItemId = itemId;
            arma.DurabilidadeAtual = (await Durabilidades.ResolverAsync(item.Rank)).Maxima ?? 0;
            return null;
        }

        if (!Enum.TryParse<TipoDeDano>(request.ManualTipoDeDano, out var tipoDeDano))
            return BadRequest("Tipo de Dano desconhecido.");

        arma.ManualNome = request.ManualNome;
        arma.ManualTipoDeDano = tipoDeDano;
        arma.ManualDados = request.ManualDados;
        arma.ManualDano = request.ManualDano;
        return null;
    }

    protected override async Task<ActionResult?> AjustarDurabilidadeDaArmaAsync(CreatureWeapon arma, int durabilidadeAtual)
    {
        // O ataque natural (manual) não tem ItemId, então não há Durabilidade do Catálogo a controlar.
        if (arma.ItemId is not { } itemId)
            return BadRequest("Ataques naturais (manuais) não têm Durabilidade.");

        arma.DurabilidadeAtual = await DurabilidadeDeArmaLimitadaAsync(itemId, durabilidadeAtual);
        return null;
    }

    /// <summary>
    /// A Arma do Catálogo lê seus campos ao vivo da Arma; o ataque natural (ItemId null) não tem linha do
    /// Catálogo, então lê os próprios campos Manual* e devolve Alcance/Crítico/Rank/Durabilidade null — não tem nenhum deles.
    /// </summary>
    protected override async Task<CreatureWeaponResponse> RespostaDeArmaAsync(CreatureSheet ficha, CreatureWeapon arma, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        if (arma.ItemId is { } itemId)
            return await RespostaDeArmaDoCatalogoAsync(ficha, itemId, arma.Id, tabela, (item, d) =>
                new CreatureWeaponResponse(arma.Id.ToString(), item.Id.ToString(), item.Nome, item.TipoDeDano?.ToString(), item.Dados, item.Dano, item.Alcance, item.Critico, item.Rank?.ToString(), arma.IsEquipped, arma.DurabilidadeAtual is null ? null : DurabilidadeDeItem.LimitarAtual(arma.DurabilidadeAtual.Value, d.Maxima), d.Maxima, d.ImageUrl, item.Descricao, d.Inquebravel,
                    d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor));

        // O ataque natural não tem Item do Catálogo de onde ler ImageUrl/Descricao.
        return new CreatureWeaponResponse(arma.Id.ToString(), null, arma.ManualNome!, arma.ManualTipoDeDano?.ToString(), arma.ManualDados, arma.ManualDano, null, null, null, arma.IsEquipped, null, null, null, null)
            with { DesabilitadoPor = await DesabilitadoPorAsync(ficha, CategoriaDesabilitavel.Arma, arma.Id) };
    }

    protected override CreatureArmorSlotResponse SlotVazio(CreatureArmorSlot slot) =>
        new(slot.Slot.ToString(), null, null, null, null, null, null, null, null, null, null, null);

    protected override CreatureArmorSlotResponse RespostaDeSlot(CreatureArmorSlot slot, Armadura item, DetalhesDoItemEquipado d) =>
        new(slot.Slot.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.Defesa, item.RF, item.RM, item.Peso, slot.DurabilidadeAtual is null ? null : DurabilidadeDeItem.LimitarAtual(slot.DurabilidadeAtual.Value, d.Maxima), d.Maxima, d.ImageUrl, item.Descricao, d.Inquebravel,
            d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor);

    protected override CreatureShieldResponse RespostaDeEscudo(CreatureShield escudo, Escudo item, DetalhesDoItemEquipado d) =>
        new(escudo.Id.ToString(), item.Id.ToString(), item.Nome, item.Categoria?.ToString(), item.BonusDefesa, item.Peso, escudo.IsEquipped, DurabilidadeDeItem.LimitarAtual(escudo.DurabilidadeAtual, d.Maxima), d.Maxima ?? 0, d.ImageUrl, item.Descricao, d.Inquebravel,
            d.Avaliacao.Requisitos, d.Avaliacao.RequisitosPendentes, d.Avaliacao.Penalidade, d.Avaliacao.OutrasPenalidades, d.DesabilitadoPor);

    protected override string? ItemIdDoSlot(UpdateCreatureArmorSlotRequest request) => request.ItemId;

    protected override string ItemIdDoEscudo(AddCreatureShieldRequest request) => request.ItemId;

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(CreatureSheet ficha) => stats.FichaParaRequisitosAsync(ficha);
}
