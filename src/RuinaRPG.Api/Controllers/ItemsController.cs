using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
                null, null, null, null, null, null, null, null, null, null, null,
                ar.Categoria?.ToString(), ar.Defesa, ar.RF, ar.RM, ar.Penalidade, ar.RequisitoVigor, null, null, null, null),
            Escudo e => new ItemResponse(e.Id.ToString(), "Escudo", e.Nome, e.Peso, e.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, null,
                e.Categoria?.ToString(), null, null, null, e.Penalidade, e.RequisitoVigor, e.BonusDefesa, null, null, null),
            Artefato ar => new ItemResponse(ar.Id.ToString(), "Artefato", ar.Nome, ar.Peso, ar.Preco, imageUrl,
                null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, ar.TipoDeAlvo?.ToString(), ar.Alvo, ar.Valor),
            _ => throw new InvalidOperationException($"Unhandled item type {item.GetType()}")
        };
    }

    private Guid CurrentGmId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
