using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/items")]
[Authorize]
public class ItemsController(RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades, IPericiaCatalogo pericias) : ControllerBase
{
    // Curating the Catálogo (create/edit/delete) stays GM-only; browsing it (List, below) doesn't
    // — a player needs to see their own GM's catalog to pick a weapon/item for their own sheet.
    [HttpPost]
    [Authorize(Roles = "GM")]
    public async Task<ActionResult<ItemResponse>> Create(CreateItemRequest request)
    {
        if (!Enum.TryParse<ItemTipo>(request.Tipo, out var tipo))
            return BadRequest("Tipo de item desconhecido.");

        // Rank só vale para Arma/Armadura/Escudo (define a durabilidade pela Tabela de Durabilidade
        // por Rank); nos outros Tipos é ignorado, como qualquer outro campo específico de Tipo.
        RankDeItem? rank = null;
        if (ItemFactory.TemRank(tipo) && !ItemFactory.TryParseRank(request.Rank, out rank))
            return BadRequest($"Rank inválido: \"{request.Rank}\".");

        var gmId = CurrentUserId();

        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (imageId is not null && !await OwnsImageAsync(imageId.Value, gmId))
            return BadRequest("Imagem não encontrada.");

        var porId = await pericias.PorIdAsync();
        if (!EquipamentoRequisitosMapper.TryParse(tipo, request.Requisitos, request.PenalidadeDeRequisitos, porId.Values.ToList(), out var requisitos, out var penalidade, out var erro))
            return BadRequest(erro);

        var item = ItemFactory.Criar(tipo, request, rank, requisitos, penalidade);
        item.Id = Guid.NewGuid();
        item.GmId = gmId;
        item.ImageId = imageId;

        db.Items.Add(item);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(item, await durabilidades.TabelaAsync(), porId));
    }

    [HttpGet]
    public async Task<ActionResult<List<ItemResponse>>> List(
        [FromQuery] string? nome,
        [FromQuery] string? tipo,
        [FromQuery] string? subcategoria,
        [FromQuery] string? rank,
        [FromQuery] string? categoria,
        [FromQuery] string? tipoDeDano)
    {
        var gmId = await ResolveEffectiveGmIdAsync();
        if (gmId is null)
            return Forbid();

        var query = db.Items.Where(i => i.GmId == gmId);

        if (!string.IsNullOrWhiteSpace(nome))
            query = query.Where(i => EF.Functions.ILike(i.Nome, $"%{nome}%"));
        if (tipo is not null && Enum.TryParse<ItemTipo>(tipo, out var tipoParsed))
            query = query.Where(i => EF.Property<string>(i, "Tipo") == tipoParsed.ToString());

        var items = await query.ToListAsync();

        var tabela = await durabilidades.TabelaAsync();
        var porId = await pericias.PorIdAsync();
        var responses = new List<ItemResponse>();
        foreach (var item in items)
            responses.Add(await ToResponseAsync(item, tabela, porId));

        return responses
            .Where(r => subcategoria is null || r.Subcategoria == subcategoria)
            .Where(r => rank is null || r.Rank == rank)
            .Where(r => categoria is null || r.Categoria == categoria)
            .Where(r => tipoDeDano is null || r.TipoDeDano == tipoDeDano)
            .ToList();
    }

    /// <summary>
    /// Treats null, empty, or whitespace-only as "no image" (returns true with imageId null).
    /// A non-empty string that isn't a valid Guid is rejected (returns false) rather than
    /// throwing — the client's "— nenhuma —" option posts ImageId="" rather than null.
    /// </summary>
    private static bool TryParseImageId(string? raw, out Guid? imageId)
    {
        imageId = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (!Guid.TryParse(raw, out var parsed))
            return false;

        imageId = parsed;
        return true;
    }

    /// <summary>
    /// R0010 scopes referencing to images the GM themselves uploaded — attaching another GM's
    /// (or a nonexistent) image Guid must fail with a controlled 400, not an FK-violation 500.
    /// </summary>
    private Task<bool> OwnsImageAsync(Guid imageId, Guid gmId) =>
        db.Images.AnyAsync(i => i.Id == imageId && i.UploadedByUserId == gmId);

    private async Task<ItemResponse> ToResponseAsync(Item item, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela, IReadOnlyDictionary<int, PericiaDefinicao> porId)
    {
        string? imageUrl = null;
        if (item.ImageId is not null)
        {
            var image = await db.Images.FindAsync(item.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        var rank = item switch { Arma a => a.Rank, Armadura ar => ar.Rank, Escudo e => e.Rank, _ => null };
        var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(rank, tabela);

        string? NomeDaPericia(int id) => porId.TryGetValue(id, out var p) && !p.IsDeleted ? p.Nome : null;

        var resposta = item switch
        {
            ItemGeral g => new ItemResponse(g.Id.ToString(), "ItemGeral", g.Nome, g.Peso, g.Preco, imageUrl,
                g.Subcategoria, g.Descricao, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, g.CapacidadeExtra),
            Arma a => new ItemResponse(a.Id.ToString(), "Arma", a.Nome, a.Peso, a.Preco, imageUrl,
                a.Subcategoria, a.Descricao, a.Rank?.ToString(), a.Empunhadura?.ToString(), a.Dados, a.Dano, a.Critico, a.Alcance, a.TipoDeDano?.ToString(), a.RequisitoAtributo,
                maxima, null, null, null, null, null, null, null, null, null, null, null, inquebravel),
            Armadura ar => new ItemResponse(ar.Id.ToString(), "Armadura", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                ar.Subcategoria, ar.Descricao, ar.Rank?.ToString(), null, null, null, null, null, null, null, maxima,
                ar.Categoria?.ToString(), ar.Defesa, ar.RF, ar.RM, ar.Penalidade, ar.RequisitoVigor, null, null, null, null, null, inquebravel),
            Escudo e => new ItemResponse(e.Id.ToString(), "Escudo", e.Nome, e.Peso, e.Preco, imageUrl,
                e.Subcategoria, e.Descricao, e.Rank?.ToString(), null, null, null, null, null, null, null, maxima,
                e.Categoria?.ToString(), null, null, null, e.Penalidade, e.RequisitoVigor, e.BonusDefesa, null, null, null, null, inquebravel),
            Artefato ar => new ItemResponse(ar.Id.ToString(), "Artefato", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                ar.Subcategoria, ar.Descricao, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, ar.TipoDeAlvo?.ToString(), ar.Alvo, ar.Valor, null),
            _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
        };

        return resposta with
        {
            Requisitos = RequisitosDePassivaMapper.ToDto(item.Requisitos, porId),
            PenalidadeDeRequisitos = EquipamentoRequisitosMapper.ToDto(item.PenalidadeDeRequisitos, porId),
            RequisitosPorExtenso = PassivaRequisitosEvaluator.Descrever(item.Requisitos, null, NomeDaPericia).ToList(),
            PenalidadePorExtenso = PenalidadesDeEquipamento.Descrever(item.PenalidadeDeRequisitos, NomeDaPericia).ToList(),
        };
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> Update(Guid id, UpdateItemRequest request)
    {
        var gmId = CurrentUserId();
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.GmId == gmId);
        if (item is null)
            return NotFound();

        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (imageId is not null && !await OwnsImageAsync(imageId.Value, gmId))
            return BadRequest("Imagem não encontrada.");

        RankDeItem? rank = null;
        if (item is Arma or Armadura or Escudo && !ItemFactory.TryParseRank(request.Rank, out rank))
            return BadRequest($"Rank inválido: \"{request.Rank}\".");

        var tipo = ItemFactory.TipoDe(item);
        var porId = await pericias.PorIdAsync();
        if (!EquipamentoRequisitosMapper.TryParse(tipo, request.Requisitos, request.PenalidadeDeRequisitos, porId.Values.ToList(), out var requisitos, out var penalidade, out var erro))
            return BadRequest(erro);

        ItemFactory.Aplicar(item, request, rank, requisitos, penalidade);
        item.ImageId = imageId;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var gmId = CurrentUserId();
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.GmId == gmId);
        if (item is null)
            return NotFound();

        db.Items.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    /// <summary>
    /// Curating the Catálogo is GM-only, but a player needs read access to their own GM's catalog
    /// to pick something for their own sheet — so List resolves "whose catalog" instead of always
    /// meaning "my own": the GM's own id for a GM, or their linked GM's id for a player (a Jogador
    /// always belongs to exactly one GM via InvitedByGmId). Null means the caller has no catalog to see.
    /// </summary>
    private async Task<Guid?> ResolveEffectiveGmIdAsync()
    {
        var callerId = CurrentUserId();
        if (User.IsInRole("GM"))
            return callerId;

        var caller = await db.Users.FindAsync(callerId);
        return caller?.InvitedByGmId;
    }
}
