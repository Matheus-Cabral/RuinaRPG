using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// O catálogo de elementos de Afinidade (Auditoria de Regras R0018), aberto a qualquer usuário autenticado: os
/// pickers de Afinidade da ficha e da Criatura, os alvos das Condições e os requisitos das Passivas o leem.
/// </summary>
[ApiController]
[Route("api/afinidades")]
[Authorize]
public class AfinidadesController(CatalogoDeAfinidadesService catalogo) : ControllerBase
{
    [HttpGet("catalogo")]
    public async Task<ActionResult<List<ElementoDeAfinidadeResponse>>> Catalogo() =>
        (await catalogo.CarregarAsync()).Elementos
            .Select(e => new ElementoDeAfinidadeResponse(e.Id, e.Nome, e.Tipo.ToString(), e.Teto)).ToList();
}
