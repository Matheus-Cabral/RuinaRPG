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
[Authorize(Roles = "GM")]
public class ItemsController(RuinaRpgDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ItemResponse>> Create(CreateItemRequest request)
    {
        if (!Enum.TryParse<ItemTipo>(request.Tipo, out var tipo))
            return BadRequest("Tipo de item desconhecido.");

        var gmId = CurrentGmId();
        Guid? imageId = request.ImageId is not null ? Guid.Parse(request.ImageId) : null;

        Item item = tipo switch
        {
            ItemTipo.ItemGeral => new ItemGeral { Nome = request.Nome, Subcategoria = request.Subcategoria, Descricao = request.Descricao },
            ItemTipo.Arma => new Arma
            {
                Nome = request.Nome,
                Subcategoria = request.Subcategoria,
                Tier = ParseEnum<Tier>(request.Tier),
                Empunhadura = ParseEnum<Empunhadura>(request.Empunhadura),
                Dados = request.Dados,
                Dano = request.Dano,
                Critico = request.Critico,
                Alcance = request.Alcance,
                TipoDeDano = ParseEnum<TipoDeDano>(request.TipoDeDano),
                RequisitoAtributo = request.RequisitoAtributo,
                DurabilidadeMaxima = request.DurabilidadeMaxima
            },
            ItemTipo.Armadura => new Armadura
            {
                Nome = request.Nome,
                Categoria = ParseEnum<CategoriaProtecao>(request.Categoria),
                Defesa = request.Defesa,
                RF = request.RF,
                RM = request.RM,
                Penalidade = request.Penalidade,
                RequisitoVigor = request.RequisitoVigor,
                DurabilidadeMaxima = request.DurabilidadeMaxima
            },
            ItemTipo.Escudo => new Escudo
            {
                Nome = request.Nome,
                Categoria = ParseEnum<CategoriaProtecao>(request.Categoria),
                BonusDefesa = request.BonusDefesa,
                Penalidade = request.Penalidade,
                RequisitoVigor = request.RequisitoVigor,
                DurabilidadeMaxima = request.DurabilidadeMaxima
            },
            ItemTipo.Artefato => new Artefato
            {
                Nome = request.Nome,
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

        db.Items.Add(item);
        await db.SaveChangesAsync();

        return Created(string.Empty, await ToResponseAsync(item));
    }

    [HttpGet]
    public async Task<ActionResult<List<ItemResponse>>> List(
        [FromQuery] string? tipo,
        [FromQuery] string? subcategoria,
        [FromQuery] string? tier,
        [FromQuery] string? categoria,
        [FromQuery] string? tipoDeDano)
    {
        var gmId = CurrentGmId();
        var query = db.Items.Where(i => i.GmId == gmId);

        if (tipo is not null && Enum.TryParse<ItemTipo>(tipo, out var tipoParsed))
            query = query.Where(i => EF.Property<string>(i, "Tipo") == tipoParsed.ToString());

        var items = await query.ToListAsync();

        var responses = new List<ItemResponse>();
        foreach (var item in items)
            responses.Add(await ToResponseAsync(item));

        return responses
            .Where(r => subcategoria is null || r.Subcategoria == subcategoria)
            .Where(r => tier is null || r.Tier == tier)
            .Where(r => categoria is null || r.Categoria == categoria)
            .Where(r => tipoDeDano is null || r.TipoDeDano == tipoDeDano)
            .ToList();
    }

    private static TEnum? ParseEnum<TEnum>(string? value) where TEnum : struct, Enum =>
        value is not null && Enum.TryParse<TEnum>(value, out var parsed) ? parsed : null;

    private async Task<ItemResponse> ToResponseAsync(Item item)
    {
        string? imageUrl = null;
        if (item.ImageId is not null)
        {
            var image = await db.Images.FindAsync(item.ImageId.Value);
            imageUrl = image is not null ? $"/{image.Path}" : null;
        }

        return item switch
        {
            ItemGeral g => new ItemResponse(g.Id.ToString(), "ItemGeral", g.Nome, g.Peso, g.Preco, imageUrl,
                g.Subcategoria, g.Descricao, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null),
            Arma a => new ItemResponse(a.Id.ToString(), "Arma", a.Nome, a.Peso, a.Preco, imageUrl,
                a.Subcategoria, null, a.Tier?.ToString(), a.Empunhadura?.ToString(), a.Dados, a.Dano, a.Critico, a.Alcance, a.TipoDeDano?.ToString(), a.RequisitoAtributo,
                a.DurabilidadeMaxima, null, null, null, null, null, null, null, null, null, null),
            Armadura ar => new ItemResponse(ar.Id.ToString(), "Armadura", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, ar.DurabilidadeMaxima,
                ar.Categoria?.ToString(), ar.Defesa, ar.RF, ar.RM, ar.Penalidade, ar.RequisitoVigor, null, null, null, null),
            Escudo e => new ItemResponse(e.Id.ToString(), "Escudo", e.Nome, e.Peso, e.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, e.DurabilidadeMaxima,
                e.Categoria?.ToString(), null, null, null, e.Penalidade, e.RequisitoVigor, e.BonusDefesa, null, null, null),
            Artefato ar => new ItemResponse(ar.Id.ToString(), "Artefato", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, ar.TipoDeAlvo?.ToString(), ar.Alvo, ar.Valor),
            _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
        };
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UpdateItemRequest request)
    {
        var gmId = CurrentGmId();
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.GmId == gmId);
        if (item is null)
            return NotFound();

        item.Nome = request.Nome;
        item.Peso = request.Peso;
        item.Preco = request.Preco;
        item.ImageId = request.ImageId is not null ? Guid.Parse(request.ImageId) : null;

        switch (item)
        {
            case ItemGeral g:
                g.Subcategoria = request.Subcategoria;
                g.Descricao = request.Descricao;
                break;
            case Arma a:
                a.Subcategoria = request.Subcategoria;
                a.Tier = ParseEnum<Tier>(request.Tier);
                a.Empunhadura = ParseEnum<Empunhadura>(request.Empunhadura);
                a.Dados = request.Dados;
                a.Dano = request.Dano;
                a.Critico = request.Critico;
                a.Alcance = request.Alcance;
                a.TipoDeDano = ParseEnum<TipoDeDano>(request.TipoDeDano);
                a.RequisitoAtributo = request.RequisitoAtributo;
                a.DurabilidadeMaxima = request.DurabilidadeMaxima;
                break;
            case Armadura ar:
                ar.Categoria = ParseEnum<CategoriaProtecao>(request.Categoria);
                ar.Defesa = request.Defesa;
                ar.RF = request.RF;
                ar.RM = request.RM;
                ar.Penalidade = request.Penalidade;
                ar.RequisitoVigor = request.RequisitoVigor;
                ar.DurabilidadeMaxima = request.DurabilidadeMaxima;
                break;
            case Escudo e:
                e.Categoria = ParseEnum<CategoriaProtecao>(request.Categoria);
                e.BonusDefesa = request.BonusDefesa;
                e.Penalidade = request.Penalidade;
                e.RequisitoVigor = request.RequisitoVigor;
                e.DurabilidadeMaxima = request.DurabilidadeMaxima;
                break;
            case Artefato art:
                art.TipoDeAlvo = ParseEnum<TipoDeAlvo>(request.TipoDeAlvo);
                art.Alvo = request.Alvo;
                art.Valor = request.Valor;
                break;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var gmId = CurrentGmId();
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id && i.GmId == gmId);
        if (item is null)
            return NotFound();

        db.Items.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
