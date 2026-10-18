using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Infrastructure.Fichas;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>Base dos controllers genéricos das fichas (spec D.3): carga e autorização pelo <see cref="ITipoDeFicha{TSheet}"/>.</summary>
public abstract class FichaControllerBase<TSheet>(ITipoDeFicha<TSheet> tipo) : ControllerBase where TSheet : class, IFicha
{
    protected ITipoDeFicha<TSheet> Tipo { get; } = tipo;

    protected Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    /// <summary>Carrega e autoriza na ordem de sempre: ficha inexistente → 404; sem permissão → Negado.</summary>
    protected async Task<(TSheet? Ficha, ActionResult? Erro)> FichaEditavelAsync(Guid sheetId)
    {
        var ficha = await Tipo.CarregarAsync(sheetId);
        if (ficha is null)
            return (null, NotFound());
        if (!await Tipo.PodeEditarAsync(ficha, CurrentUserId()))
            return (null, Tipo.Negado(this));
        return (ficha, null);
    }
}
