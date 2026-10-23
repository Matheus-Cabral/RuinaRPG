using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>O que toda resposta de item do Arsenal ligado ao Catálogo junta além dos campos do próprio item.</summary>
public sealed record DetalhesDoItemEquipado(string? ImageUrl, int? Maxima, bool Inquebravel, string? DesabilitadoPor, AvaliacaoDeEquipamento Avaliacao);

/// <summary>
/// Arsenal da ficha (Armas, slots de Armadura e Escudos), para os três tipos. A rota (<c>api/&lt;tipo&gt;-sheets/{sheetId}</c>)
/// fica na subclasse de cada tipo; os records de resposta são do contrato de cada tipo, montados pela subclasse.
/// </summary>
public abstract class ArsenalDaFichaController<TSheet, TArma, TSlot, TEscudo, TArmaResponse, TSlotResponse, TAddArma>(
    RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades, EquipmentPenaltyService penalidades, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TArma : class, IEquipavelDaFicha, new()
    where TSlot : class, ISlotDeArmaduraDaFicha
    where TEscudo : class, IEquipavelDoCatalogo, new()
{
    protected RuinaRpgDbContext Db { get; } = db;
    protected DurabilidadePorRankProvider Durabilidades { get; } = durabilidades;
    protected EquipmentPenaltyService Penalidades { get; } = penalidades;

    private DbSet<TArma> Armas => Db.Set<TArma>();
    private DbSet<TSlot> Slots => Db.Set<TSlot>();
    private DbSet<TEscudo> Escudos => Db.Set<TEscudo>();

    /// <summary>Preenche a Arma nova com o pedido, ou devolve o 400 dele. Ficha de Criaturas R0006 3.a: a Criatura também aceita ataque natural.</summary>
    protected abstract Task<ActionResult?> PreencherArmaAsync(TArma arma, TAddArma request);

    /// <summary>Grava a Durabilidade Atual pedida, limitada ao Máximo; ou o 400 se a Arma não tem Durabilidade (ataque natural da Criatura).</summary>
    protected abstract Task<ActionResult?> AjustarDurabilidadeDaArmaAsync(TArma arma, int durabilidadeAtual);

    protected abstract Task<TArmaResponse> RespostaDeArmaAsync(TSheet ficha, TArma arma, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela);

    protected abstract TSlotResponse SlotVazio(TSlot slot);

    protected abstract TSlotResponse RespostaDeSlot(TSlot slot, Armadura item, DetalhesDoItemEquipado detalhes);

    protected abstract ShieldResponse RespostaDeEscudo(TEscudo escudo, Escudo item, DetalhesDoItemEquipado detalhes);

    protected abstract string? ItemIdDoSlot(UpdateArmorSlotRequest request);

    protected abstract string ItemIdDoEscudo(AddShieldRequest request);

    /// <summary>O retrato da ficha usado para conferir Requisitos (o Stats do tipo).</summary>
    protected abstract Task<FichaParaRequisitos> FichaParaRequisitosAsync(TSheet ficha);

    [HttpPost("weapons")]
    public async Task<ActionResult<TArmaResponse>> AddWeapon(Guid sheetId, TAddArma request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var weapon = new TArma { Id = Guid.NewGuid(), SheetId = sheetId, IsEquipped = false };
        if (await PreencherArmaAsync(weapon, request) is { } erroDoPedido)
            return erroDoPedido;

        Armas.Add(weapon);
        await Db.SaveChangesAsync();

        return Created(string.Empty, await RespostaDeArmaAsync(ficha, weapon, await Durabilidades.TabelaAsync()));
    }

    [HttpGet("weapons")]
    public async Task<ActionResult<List<TArmaResponse>>> ListWeapons(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var weapons = await Armas.Where(w => w.SheetId == sheetId).ToListAsync();
        var tabela = await Durabilidades.TabelaAsync();
        var responses = new List<TArmaResponse>();
        foreach (var weapon in weapons)
            responses.Add(await RespostaDeArmaAsync(ficha, weapon, tabela));
        return responses;
    }

    [HttpPut("weapons/{id}")]
    public async Task<IActionResult> UpdateWeapon(Guid sheetId, Guid id, [FromBody] bool isEquipped)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var weapon = await Armas.FirstOrDefaultAsync(w => w.Id == id && w.SheetId == sheetId);
        if (weapon is null)
            return NotFound();

        if (isEquipped)
        {
            // Regra padrão (sem a exceção de Ambidestria — fora do escopo do plano): só 1 Arma equipada por vez.
            var currentlyEquipped = await Armas.Where(w => w.SheetId == sheetId && w.IsEquipped).ToListAsync();
            foreach (var other in currentlyEquipped)
                other.IsEquipped = false;
        }
        weapon.IsEquipped = isEquipped;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("weapons/{id}/durabilidade")]
    public async Task<IActionResult> UpdateWeaponDurability(Guid sheetId, Guid id, [FromBody] int durabilidadeAtual)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var weapon = await Armas.FirstOrDefaultAsync(w => w.Id == id && w.SheetId == sheetId);
        if (weapon is null)
            return NotFound();

        if (await AjustarDurabilidadeDaArmaAsync(weapon, durabilidadeAtual) is { } erroDaArma)
            return erroDaArma;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("weapons/{id}")]
    public async Task<IActionResult> DeleteWeapon(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var weapon = await Armas.FirstOrDefaultAsync(w => w.Id == id && w.SheetId == sheetId);
        if (weapon is null)
            return NotFound();

        Armas.Remove(weapon);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("armor-slots")]
    public async Task<ActionResult<List<TSlotResponse>>> ListArmorSlots(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var slots = await Slots.Where(a => a.SheetId == sheetId).ToListAsync();
        var tabela = await Durabilidades.TabelaAsync();
        var responses = new List<TSlotResponse>();
        foreach (var slot in slots)
            responses.Add(slot.ItemId is { } itemId
                ? await RespostaDoCatalogoAsync<Armadura, TSlotResponse>(ficha, itemId, slot.Id, CategoriaDesabilitavel.Armadura, tabela, a => a.Rank, (item, d) => RespostaDeSlot(slot, item, d))
                : SlotVazio(slot));
        return responses;
    }

    [HttpPut("armor-slots/{slot}")]
    public async Task<IActionResult> UpdateArmorSlot(Guid sheetId, ArmorSlotType slot, UpdateArmorSlotRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var armorSlot = await Slots.SingleAsync(a => a.SheetId == sheetId && a.Slot == slot);

        var itemIdPedido = ItemIdDoSlot(request);
        if (itemIdPedido is null)
        {
            armorSlot.ItemId = null;
            armorSlot.DurabilidadeAtual = null;
        }
        else
        {
            if (!Guid.TryParse(itemIdPedido, out var itemId))
                return BadRequest("ItemId inválido.");

            var item = await Db.Set<Armadura>().FirstOrDefaultAsync(a => a.Id == itemId);
            if (item is null)
                return BadRequest("Item de armadura não encontrado.");

            armorSlot.ItemId = itemId;
            armorSlot.DurabilidadeAtual = (await Durabilidades.ResolverAsync(item.Rank)).Maxima ?? 0;
        }

        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("armor-slots/{slot}/durabilidade")]
    public async Task<IActionResult> UpdateArmorSlotDurability(Guid sheetId, ArmorSlotType slot, [FromBody] int durabilidadeAtual)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var armorSlot = await Slots.SingleAsync(a => a.SheetId == sheetId && a.Slot == slot);
        if (armorSlot.ItemId is not { } itemId)
            return BadRequest("Nenhuma armadura equipada nesse slot.");

        // "Atual ... não pode exceder o Máximo" (Ficha de Personagem 3.b; NPC e Criatura seguem a mesma estrutura).
        armorSlot.DurabilidadeAtual = await DurabilidadeLimitadaAsync<Armadura>(itemId, durabilidadeAtual, a => a.Rank);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("shields")]
    public async Task<ActionResult<ShieldResponse>> AddShield(Guid sheetId, AddShieldRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var shield = new TEscudo { Id = Guid.NewGuid(), SheetId = sheetId, IsEquipped = false };
        if (await PreencherDoCatalogoAsync<Escudo>(ItemIdDoEscudo(request), e => e.Rank, "Item de escudo não encontrado.", LigarAoCatalogo(shield)) is { } erroDoPedido)
            return erroDoPedido;

        Escudos.Add(shield);
        await Db.SaveChangesAsync();

        return Created(string.Empty, await RespostaDeEscudoAsync(ficha, shield, await Durabilidades.TabelaAsync()));
    }

    [HttpGet("shields")]
    public async Task<ActionResult<List<ShieldResponse>>> ListShields(Guid sheetId)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha))
            return erro;

        var shields = await Escudos.Where(s => s.SheetId == sheetId).ToListAsync();
        var tabela = await Durabilidades.TabelaAsync();
        var responses = new List<ShieldResponse>();
        foreach (var shield in shields)
            responses.Add(await RespostaDeEscudoAsync(ficha, shield, tabela));
        return responses;
    }

    [HttpPut("shields/{id}")]
    public async Task<IActionResult> UpdateShield(Guid sheetId, Guid id, [FromBody] bool isEquipped)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var shield = await Escudos.FirstOrDefaultAsync(s => s.Id == id && s.SheetId == sheetId);
        if (shield is null)
            return NotFound();

        if (isEquipped)
        {
            // A mesma regra de "1 equipado por vez" das Armas (UpdateWeapon) — nenhuma exceção modelada ainda.
            var currentlyEquipped = await Escudos.Where(s => s.SheetId == sheetId && s.IsEquipped).ToListAsync();
            foreach (var other in currentlyEquipped)
                other.IsEquipped = false;
        }
        shield.IsEquipped = isEquipped;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("shields/{id}/durabilidade")]
    public async Task<IActionResult> UpdateShieldDurability(Guid sheetId, Guid id, [FromBody] int durabilidadeAtual)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var shield = await Escudos.FirstOrDefaultAsync(s => s.Id == id && s.SheetId == sheetId);
        if (shield is null)
            return NotFound();

        // "Atual ... não pode exceder o Máximo" (Ficha de Personagem 3.c; NPC e Criatura seguem a mesma estrutura).
        shield.DurabilidadeAtual = await DurabilidadeLimitadaAsync<Escudo>(shield.ItemId, durabilidadeAtual, e => e.Rank);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("shields/{id}")]
    public async Task<IActionResult> DeleteShield(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out _))
            return erro;

        var shield = await Escudos.FirstOrDefaultAsync(s => s.Id == id && s.SheetId == sheetId);
        if (shield is null)
            return NotFound();

        Escudos.Remove(shield);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Confere a Arma do Catálogo pedida e entrega a <paramref name="ligar"/> o ItemId e a Durabilidade cheia do Rank;
    /// ou devolve o 400 do ItemId.
    /// </summary>
    protected Task<ActionResult?> PreencherArmaDoCatalogoAsync(string? itemIdPedido, Action<Guid, int> ligar) =>
        PreencherDoCatalogoAsync<Arma>(itemIdPedido, a => a.Rank, "Item de arma não encontrado.", ligar);

    /// <summary>A Durabilidade Atual pedida, limitada ao Máximo do Rank da Arma (Ficha de Personagem 3.a, aplicado a NPC pelo preâmbulo da Ficha de NPCs).</summary>
    protected Task<int> DurabilidadeDeArmaLimitadaAsync(Guid itemId, int durabilidadeAtual) =>
        DurabilidadeLimitadaAsync<Arma>(itemId, durabilidadeAtual, a => a.Rank);

    /// <summary>A resposta de uma Arma ligada ao Catálogo, com Durabilidade, Requisitos e Condições.</summary>
    protected Task<TArmaResponse> RespostaDeArmaDoCatalogoAsync(TSheet ficha, Guid itemId, Guid linhaId, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela, Func<Arma, DetalhesDoItemEquipado, TArmaResponse> montar) =>
        RespostaDoCatalogoAsync(ficha, itemId, linhaId, CategoriaDesabilitavel.Arma, tabela, a => a.Rank, montar);

    /// <summary>Condições ativas da ficha que desabilitam esta linha (null se nenhuma). O Id é o da linha da ficha, o mesmo de itens-desabilitaveis.</summary>
    protected async Task<string?> DesabilitadoPorAsync(TSheet ficha, CategoriaDesabilitavel categoria, Guid linhaId) =>
        (await Tipo.CondicoesAsync(ficha)).DesabilitadoPor(categoria, linhaId);

    private Task<ShieldResponse> RespostaDeEscudoAsync(TSheet ficha, TEscudo shield, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela) =>
        RespostaDoCatalogoAsync<Escudo, ShieldResponse>(ficha, shield.ItemId, shield.Id, CategoriaDesabilitavel.Escudo, tabela, e => e.Rank, (item, d) => RespostaDeEscudo(shield, item, d));

    /// <summary>Grava na linha o ItemId e a Durabilidade que <see cref="PreencherArmaDoCatalogoAsync"/> entrega.</summary>
    protected static Action<Guid, int> LigarAoCatalogo(IEquipavelDoCatalogo linha) =>
        (itemId, durabilidade) => (linha.ItemId, linha.DurabilidadeAtual) = (itemId, durabilidade);

    private async Task<ActionResult?> PreencherDoCatalogoAsync<TItem>(string? itemIdPedido, Func<TItem, RankDeItem?> rank, string naoEncontrado, Action<Guid, int> ligar)
        where TItem : Item
    {
        if (!Guid.TryParse(itemIdPedido, out var itemId))
            return BadRequest("ItemId inválido.");

        var item = await Db.Set<TItem>().FirstOrDefaultAsync(i => i.Id == itemId);
        if (item is null)
            return BadRequest(naoEncontrado);

        ligar(itemId, (await Durabilidades.ResolverAsync(rank(item))).Maxima ?? 0);
        return null;
    }

    private async Task<int> DurabilidadeLimitadaAsync<TItem>(Guid itemId, int durabilidadeAtual, Func<TItem, RankDeItem?> rank) where TItem : Item
    {
        var item = await Db.Set<TItem>().SingleAsync(i => i.Id == itemId);
        return DurabilidadeDeItem.LimitarAtual(durabilidadeAtual, (await Durabilidades.ResolverAsync(rank(item))).Maxima);
    }

    private async Task<TResponse> RespostaDoCatalogoAsync<TItem, TResponse>(TSheet ficha, Guid itemId, Guid linhaId, CategoriaDesabilitavel categoria,
        IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela, Func<TItem, RankDeItem?> rank, Func<TItem, DetalhesDoItemEquipado, TResponse> montar)
        where TItem : Item
    {
        var item = await Db.Set<TItem>().SingleAsync(i => i.Id == itemId);
        var imageUrl = await UrlDaImagemAsync(Db, item.ImageId);
        var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(rank(item), tabela);
        var desabilitadoPor = await DesabilitadoPorAsync(ficha, categoria, linhaId);
        var avaliacao = await Penalidades.AvaliarAsync(item, FichaSemPenalidades(ficha));
        return montar(item, new DetalhesDoItemEquipado(imageUrl, maxima, inquebravel, desabilitadoPor, avaliacao));
    }

    // O retrato da ficha SEM penalidades é calculado uma vez por requisição, e só se algum item tiver Requisitos.
    private Task<FichaParaRequisitos>? _fichaSemPenalidades;

    private Func<Task<FichaParaRequisitos>> FichaSemPenalidades(TSheet ficha) =>
        () => _fichaSemPenalidades ??= FichaParaRequisitosAsync(ficha);
}
