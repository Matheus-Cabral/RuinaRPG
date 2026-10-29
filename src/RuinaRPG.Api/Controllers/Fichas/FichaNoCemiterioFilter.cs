using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Cemitério de fichas, "Leitura × escrita": toda escrita numa Ficha de Personagem no cemitério é 409, salvo as ações
/// com <see cref="PermitidoNoCemiterioAttribute"/>. Ficha inexistente ou chamador sem acesso seguem para a ação, que
/// responde 404/403 como sempre. É um filtro de recurso, e não de ação, para rodar antes do model binding: um corpo
/// inválido numa ficha no cemitério também é 409, e não o 400 da validação do [ApiController].
/// </summary>
public sealed class FichaNoCemiterioFilter(RuinaRpgDbContext db, CemiterioDaFicha cemiterio) : IAsyncResourceFilter
{
    public const string Mensagem = RuinaRPG.Contracts.CharacterSheets.TextosDoCemiterio.RecusaDeEscrita;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        if (await NoCemiterioAsync(context))
            context.Result = new ConflictObjectResult(Mensagem);
        else
            await next();
    }

    private async Task<bool> NoCemiterioAsync(ResourceExecutingContext context)
    {
        if (HttpMethods.IsGet(context.HttpContext.Request.Method)
            || context.ActionDescriptor is not ControllerActionDescriptor acao
            || ParametroDaFicha(acao.AttributeRouteInfo?.Template) is not { } parametro
            || acao.MethodInfo.IsDefined(typeof(PermitidoNoCemiterioAttribute), true)
            || !Guid.TryParse(context.RouteData.Values[parametro]?.ToString(), out var sheetId)
            || !Guid.TryParse(context.HttpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var callerId))
            return false;

        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return false;
        var gmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(callerId, sheet.OwnerId, gmId))
            return false;
        return (await cemiterio.MotivosAsync(sheet)).Count > 0;
    }

    /// <summary>"id" ou "sheetId" quando o template é <c>api/character-sheets/{id|sheetId}…</c>; senão null.</summary>
    private static string? ParametroDaFicha(string? template)
    {
        var segmentos = (template ?? "").TrimStart('~').TrimStart('/').Split('/');
        if (segmentos.Length < 3 || segmentos[0] != "api" || segmentos[1] != "character-sheets"
            || !segmentos[2].StartsWith('{') || !segmentos[2].EndsWith('}'))
            return null;
        var nome = segmentos[2][1..^1].Split(':')[0];
        return nome is "id" or "sheetId" ? nome : null;
    }
}
