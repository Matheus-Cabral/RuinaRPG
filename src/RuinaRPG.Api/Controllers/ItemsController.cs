using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Route("api/items")]
[Authorize]
public class ItemsController(RuinaRpgDbContext db, DurabilidadePorRankProvider durabilidades) : ControllerBase
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
        if (TemRank(tipo) && !TryParseRank(request.Rank, out rank))
            return BadRequest($"Rank inválido: \"{request.Rank}\".");

        var gmId = CurrentUserId();

        if (!TryParseImageId(request.ImageId, out var imageId))
            return BadRequest("ImageId inválido.");

        if (imageId is not null && !await OwnsImageAsync(imageId.Value, gmId))
            return BadRequest("Imagem não encontrada.");

        Item item = tipo switch
        {
            ItemTipo.ItemGeral => new ItemGeral { Nome = request.Nome, Subcategoria = request.Subcategoria, CapacidadeExtra = request.CapacidadeExtra },
            ItemTipo.Arma => new Arma
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                Rank = rank,
                Empunhadura = ParseEnum<Empunhadura>(request.Empunhadura),
                Dados = request.Dados,
                Dano = request.Dano,
                Critico = request.Critico,
                Alcance = request.Alcance,
                TipoDeDano = ParseEnum<TipoDeDano>(request.TipoDeDano),
                RequisitoAtributo = request.RequisitoAtributo
            },
            ItemTipo.Armadura => new Armadura
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                Rank = rank,
                Categoria = ParseEnum<CategoriaProtecao>(request.Categoria),
                Defesa = request.Defesa,
                RF = request.RF,
                RM = request.RM,
                Penalidade = request.Penalidade,
                RequisitoVigor = request.RequisitoVigor
            },
            ItemTipo.Escudo => new Escudo
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                Rank = rank,
                Categoria = ParseEnum<CategoriaProtecao>(request.Categoria),
                BonusDefesa = request.BonusDefesa,
                Penalidade = request.Penalidade,
                RequisitoVigor = request.RequisitoVigor
            },
            ItemTipo.Artefato => new Artefato
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                TipoDeAlvo = ParseEnum<TipoDeAlvo>(request.TipoDeAlvo),
                Alvo = request.Alvo,
                Valor = request.Valor
            },
            _ => throw new InvalidOperationException("Unreachable — Tipo already validated above.")
        };

        item.Id = Guid.NewGuid();
        item.GmId = gmId;
        item.Peso = request.Peso;
        item.Preco = request.Preco;
        item.ImageId = imageId;
        item.Descricao = request.Descricao;

        db.Items.Add(item);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(item, await durabilidades.TabelaAsync()));
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
        var responses = new List<ItemResponse>();
        foreach (var item in items)
            responses.Add(await ToResponseAsync(item, tabela));

        return responses
            .Where(r => subcategoria is null || r.Subcategoria == subcategoria)
            .Where(r => rank is null || r.Rank == rank)
            .Where(r => categoria is null || r.Categoria == categoria)
            .Where(r => tipoDeDano is null || r.TipoDeDano == tipoDeDano)
            .ToList();
    }

    private static bool TemRank(ItemTipo tipo) => tipo is ItemTipo.Arma or ItemTipo.Armadura or ItemTipo.Escudo;

    /// <summary>
    /// Vazio = sem Rank (sem durabilidade). Qualquer outro valor tem de ser exatamente um nome de
    /// <see cref="RankDeItem"/> (F..SS) — um Rank desconhecido é rejeitado em vez de virar NULL em
    /// silêncio, já que apagaria a durabilidade do item.
    /// </summary>
    private static bool TryParseRank(string? raw, out RankDeItem? rank)
    {
        rank = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;
        if (!Enum.GetNames<RankDeItem>().Contains(raw) || !Enum.TryParse<RankDeItem>(raw, out var parsed))
            return false;
        rank = parsed;
        return true;
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        value is not null && Enum.TryParse<TEnum>(value, out var parsed) ? parsed : null;

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

    private async Task<ItemResponse> ToResponseAsync(Item item, IReadOnlyDictionary<RankDeItem, DurabilidadeDeRank> tabela)
    {
        string? imageUrl = null;
        if (item.ImageId is not null)
        {
            var image = await db.Images.FindAsync(item.ImageId.Value);
            imageUrl = image is not null ? $"/images/{image.Path}" : null;
        }

        var rank = item switch { Arma a => a.Rank, Armadura ar => ar.Rank, Escudo e => e.Rank, _ => null };
        var (maxima, inquebravel) = DurabilidadeDeItem.Resolver(rank, tabela);

        return item switch
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
        if (item is Arma or Armadura or Escudo && !TryParseRank(request.Rank, out rank))
            return BadRequest($"Rank inválido: \"{request.Rank}\".");

        item.Nome = request.Nome;
        item.Peso = request.Peso;
        item.Preco = request.Preco;
        item.ImageId = imageId;
        item.Descricao = request.Descricao;

        switch (item)
        {
            case ItemGeral g:
                g.Subcategoria = request.Subcategoria;
                g.CapacidadeExtra = request.CapacidadeExtra;
                break;
            case Arma a:
                a.Subcategoria = request.Subcategoria;
                a.Rank = rank;
                a.Empunhadura = ParseEnum<Empunhadura>(request.Empunhadura);
                a.Dados = request.Dados;
                a.Dano = request.Dano;
                a.Critico = request.Critico;
                a.Alcance = request.Alcance;
                a.TipoDeDano = ParseEnum<TipoDeDano>(request.TipoDeDano);
                a.RequisitoAtributo = request.RequisitoAtributo;
                break;
            case Armadura ar:
                ar.Subcategoria = request.Subcategoria;
                ar.Rank = rank;
                ar.Categoria = ParseEnum<CategoriaProtecao>(request.Categoria);
                ar.Defesa = request.Defesa;
                ar.RF = request.RF;
                ar.RM = request.RM;
                ar.Penalidade = request.Penalidade;
                ar.RequisitoVigor = request.RequisitoVigor;
                break;
            case Escudo e:
                e.Subcategoria = request.Subcategoria;
                e.Rank = rank;
                e.Categoria = ParseEnum<CategoriaProtecao>(request.Categoria);
                e.BonusDefesa = request.BonusDefesa;
                e.Penalidade = request.Penalidade;
                e.RequisitoVigor = request.RequisitoVigor;
                break;
            case Artefato art:
                art.Subcategoria = request.Subcategoria;
                art.TipoDeAlvo = ParseEnum<TipoDeAlvo>(request.TipoDeAlvo);
                art.Alvo = request.Alvo;
                art.Valor = request.Valor;
                break;
        }

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
