using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace RuinaRPG.Api.Services;

/// <summary>Ação que carrega, altera e salva o encontro: se outra ação o alterou no meio, responde 409 em vez de 500.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ConflitoDeConcorrenciaDoEncontroAttribute : ExceptionFilterAttribute
{
    public const string Mensagem = "O encontro foi alterado por outra ação. Recarregue e tente de novo.";

    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not DbUpdateConcurrencyException)
            return;
        context.Result = new ConflictObjectResult(Mensagem);
        context.ExceptionHandled = true;
    }
}
