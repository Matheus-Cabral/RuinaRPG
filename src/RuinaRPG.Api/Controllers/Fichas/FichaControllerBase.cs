using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Base dos controllers genéricos das fichas (spec D.3): carga e autorização pelo <see cref="ITipoDeFicha{TSheet}"/>.</summary>
public abstract class FichaControllerBase<TSheet>(ITipoDeFicha<TSheet> tipo) : ControllerBase where TSheet : class, IFicha
{
    protected ITipoDeFicha<TSheet> Tipo { get; } = tipo;

    protected Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    /// <summary>
    /// Carrega e autoriza na ordem de sempre: ficha inexistente → 404; sem permissão → Negado. Uso:
    /// <c>if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var ficha)) return erro;</c>
    /// </summary>
    protected async Task<AcessoAFicha> FichaEditavelAsync(Guid sheetId)
    {
        var ficha = await Tipo.CarregarAsync(sheetId);
        if (ficha is null)
            return new AcessoAFicha(NotFound());
        if (!await Tipo.PodeEditarAsync(ficha, CurrentUserId()))
            return new AcessoAFicha(Tipo.Negado(this));
        return new AcessoAFicha(ficha);
    }

    /// <summary>A URL pública da imagem (<c>/images/&lt;Path&gt;</c>), ou null se não há imagem.</summary>
    protected static async Task<string?> UrlDaImagemAsync(RuinaRpgDbContext db, Guid? imageId)
    {
        if (imageId is null)
            return null;

        var image = await db.Images.FindAsync(imageId.Value);
        return image is not null ? $"/images/{image.Path}" : null;
    }

    /// <summary>Resultado de <see cref="FichaEditavelAsync"/>: a ficha editável, ou o erro a devolver no lugar dela.</summary>
    protected sealed class AcessoAFicha
    {
        private readonly TSheet? ficha;
        private readonly ActionResult? erro;

        public AcessoAFicha(TSheet ficha) => this.ficha = ficha;

        public AcessoAFicha(ActionResult erro) => this.erro = erro;

        /// <summary>true com o erro a devolver; false com a ficha, nunca nula.</summary>
        public bool Negado([NotNullWhen(true)] out ActionResult? erro, [NotNullWhen(false)] out TSheet? ficha)
        {
            if (this.ficha is { } carregada)
            {
                erro = null;
                ficha = carregada;
                return false;
            }
            erro = this.erro ?? throw new InvalidOperationException("AcessoAFicha sem ficha e sem erro.");
            ficha = null;
            return true;
        }
    }
}
